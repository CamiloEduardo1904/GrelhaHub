using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Cardapio;

/// <summary>
/// <c>RN-VEN-046</c> — o teto de produtos ativos por categoria, e o <b>único</b> lugar onde ele é
/// conferido.
/// <para>
/// <b>Existem dois caminhos para um produto entrar numa categoria</b>, e a regra tem de valer nos
/// dois: criar (ou mover) o produto, em <c>SalvarProduto</c>, e <b>reativar</b> um que estava fora
/// do cardápio, em <c>AlternarSituacaoDoProduto</c>. Conferir só no primeiro deixaria o teto
/// contornável pelo caminho mais óbvio que existe — inativar 50, criar outros 50, reativar os
/// primeiros —, e a operação nem pareceria trapaça: ela é dois cliques de gente resolvendo o dia.
/// </para>
/// <para>
/// É o mesmo desenho de <c>FechoDeDependencia</c> para <c>RN-GER-020</c>, pelo mesmo motivo
/// registrado lá: <b>quem habilitar por um caminho novo chama esta função.</b> Regra imposta em um
/// caminho e esquecida no outro é o modo de falha que a duplicação garante.
/// </para>
/// </summary>
public static class TetoDeProdutos
{
    /// <summary>O código do parâmetro. Ver <c>docs/04-parametros-do-sistema.md</c>.</summary>
    public const string Parametro = "PAR-VEN-004";

    /// <summary>
    /// Recusa a entrada quando a categoria já está no teto.
    /// </summary>
    /// <param name="produtoIgnorado">
    /// O próprio produto, quando ele já está na contagem — reativar precisa disso, senão o 50º
    /// produto reativado seria comparado contra uma contagem que já o inclui.
    /// </param>
    public static async Task ConferirOuFalharAsync(
        IAppDbContext db,
        IParametros parametros,
        Guid categoriaId,
        Guid? produtoIgnorado,
        CancellationToken cancellationToken)
    {
        var teto = await parametros.ObterAsync<int>(Parametro, cancellationToken: cancellationToken);

        // Conta só `p.Ativo`. RN-VEN-045 proíbe exclusão: produto retirado do cardápio continua na
        // tabela para o item de pedido histórico não perder a referência. Contar o histórico faria
        // a categoria entupir PERMANENTEMENTE — depois de 50 produtos retirados ao longo de dois
        // anos, "Lanches" não aceitaria mais nada, e o usuário não teria como resolver.
        //
        // O filtro global por unidade (RN-GER-009) já se aplica: a contagem nunca soma produto de
        // outro restaurante.
        var consulta = db.Produtos
            .AsNoTracking()
            .Where(p => p.CategoriaId == categoriaId && p.Ativo);

        // ⚠ O `Where` do produto ignorado é CONDICIONAL, e não `p.Id != produtoIgnorado` com um
        // `Guid?` dentro da árvore de expressão.
        //
        // A segunda forma custou duas horas: `p.Id != @p` com `@p` nulo é `NULL` em SQL, o que
        // filtra TODAS as linhas e devolve contagem zero. O EF Core compensa isso gerando
        // `(p.id <> @p OR @p IS NULL)` — mas a consulta compilada é reaproveitada, e o resultado
        // passou a depender de qual chamada compilou primeiro: criar (id nulo) ou reativar (id
        // preenchido). O sintoma foi o pior possível: a suíte passava com a classe sozinha e
        // falhava com a suíte inteira, em dois testes que nada tinham a ver com ordem.
        //
        // Comparar `Guid` com `Guid?` dentro de expressão é a armadilha; a forma condicional não
        // tem como ser ambígua.
        if (produtoIgnorado is Guid ignorado)
        {
            consulta = consulta.Where(p => p.Id != ignorado);
        }

        var ativos = await consulta.CountAsync(cancellationToken);

        if (ativos < teto)
        {
            return;
        }

        // O número entra na mensagem porque o teto é por unidade e não há tela que o exiba: sem
        // ele, o usuário lê "chegou ao limite" e não sabe qual é o limite nem se ele mudou.
        throw new RegraDeNegocioException(
            $"Esta categoria já tem {teto} produtos no cardápio, que é o limite. " +
            "Tire um do cardápio ou crie outra categoria.");
    }
}
