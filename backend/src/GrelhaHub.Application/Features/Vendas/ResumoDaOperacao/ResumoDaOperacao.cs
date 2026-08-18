using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Leitura;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.ResumoDaOperacao;

/// <summary>
/// O painel de operação — <c>RN-VEN-049</c> ⚠⚠, <c>RN-VEN-050</c>, <c>RN-VEN-051</c>,
/// <c>RN-GER-038</c>.
/// <para>
/// <b>Permissão própria</b>, e não <c>pedido.visualizar</c>: quem atende o telefone precisa ver o
/// pedido da mesa 4; o faturamento da semana é outra pergunta. Agregado de venda é o passo anterior
/// a margem, e conceder junto abriria por uma porta lateral o que <c>RN-GER-012</c> fecha na porta
/// da frente.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("operacao.visualizar")]
public sealed record ResumoDaOperacaoQuery(
    /// <summary>
    /// <c>RN-VEN-050</c> / <c>RN-GER-006</c> ⚠⚠ — o período é de <b>dia operacional</b>, e o padrão
    /// é o dia corrente. Nunca data de criação: o pedido da 01h30 de domingo é faturamento de
    /// sábado, e é assim que a casa conta a noite dela.
    /// </summary>
    DateOnly? De,
    DateOnly? Ate,

    /// <summary><c>RN-GER-014</c> / <c>RN-GER-016</c> — só a plataforma informa; para o restaurante é ignorado.</summary>
    Guid? UnidadeId = null) : IRequest<ResumoDaOperacaoDto>;

/// <summary>Um dia da série. <c>RN-VEN-050</c> — dia sem pedido vem com zero, não some.</summary>
public sealed record DiaDaOperacaoDto(DateOnly Dia, decimal Faturamento, int Pedidos);

public sealed record ProdutoVendidoDto(string Produto, int Quantidade, decimal Total);

public sealed record ResumoDaOperacaoDto(
    DateOnly De,
    DateOnly Ate,

    /// <summary><c>RN-VEN-049</c> — só pedidos FINALIZADOS.</summary>
    decimal Faturamento,
    int PedidosFinalizados,

    /// <summary>
    /// <c>RN-VEN-049</c> ⚠⚠ — o que ainda está no salão: confirmado, em produção, pronto ou
    /// entregue. <b>Fora</b> do faturamento e <b>dentro</b> da tela.
    /// </summary>
    int PedidosEmAndamento,
    decimal ValorEmAndamento,

    int PedidosCancelados,

    /// <summary><c>RN-VEN-051</c> — <c>null</c>, e não zero, quando não houve venda.</summary>
    decimal? TicketMedio,

    IReadOnlyList<DiaDaOperacaoDto> Serie,
    IReadOnlyList<ProdutoVendidoDto> MaisVendidos);

/// <summary>
/// ADR-0019 — este handler é a demonstração do lado da <b>leitura</b>.
/// <para>
/// Ele recebe <see cref="ILeituraDb"/> e <b>não tem como alcançar o contexto de escrita</b>: a
/// interface não é registrada para ele. Não carrega agregado, não materializa entidade e não soma
/// nada — quem soma é a visão <c>vw_operacao_dia</c>, onde <c>RN-VEN-049</c> mora.
/// </para>
/// <para>
/// ⚠ <b>A versão anterior carregava <c>Pedido</c> com <c>Itens</c> para somar dois números.</b> Era
/// o sintoma de CQS sem CQRS: a consulta usava o modelo que existe para garantir regra na escrita.
/// E somava em memória porque não tinha escolha — <c>Sum(i =&gt; i.PrecoPraticado.Valor * i.Quantidade)</c>
/// não vira SQL, já que o EF Core não traduz acesso a membro de propriedade com conversor de valor.
/// A visão resolve os dois de uma vez.
/// </para>
/// </summary>
public sealed class ResumoDaOperacaoHandler(ILeituraDb leitura, IRelogio relogio)
    : IRequestHandler<ResumoDaOperacaoQuery, ResumoDaOperacaoDto>
{
    /// <summary>
    /// O teto da série. Não é limite de negócio — é o que impede uma consulta de dez anos de montar
    /// uma série que nenhuma tela desenha e nenhum olho lê.
    /// </summary>
    private const int MaximoDeDias = 366;

    /// <summary>Quantos produtos entram no "mais vendidos". Lista longa vira relatório, e relatório é outra tela.</summary>
    private const int TetoDeProdutos = 8;

    public async Task<ResumoDaOperacaoDto> Handle(
        ResumoDaOperacaoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var de = request.De ?? relogio.DiaOperacionalDeHoje();
        var ate = request.Ate ?? de;

        if (ate < de)
        {
            throw new RegraDeNegocioException(
                "A data final do período não pode ser anterior à inicial.", "RN-GER-006");
        }

        if (ate.DayNumber - de.DayNumber + 1 > MaximoDeDias)
        {
            throw new RegraDeNegocioException(
                $"O período do painel não pode passar de {MaximoDeDias} dias.", "RN-VEN-050");
        }

        // ⚠ O recorte por unidade NÃO é feito aqui: o filtro global do contexto de leitura já o
        // aplica, como no de escrita (RN-GER-009). O que sobra para o handler é o recorte
        // OPCIONAL que só a plataforma informa (RN-GER-014 / RN-GER-016).
        var unidade = request.UnidadeId;

        var porDia = await leitura.OperacaoPorDia
            .Where(d => unidade == null || d.UnidadeId == unidade)
            .Where(d => d.DataOperacional >= de && d.DataOperacional <= ate)
            .ToListAsync(cancellationToken);

        // ⚠ Projeta em tipo ANÔNIMO, e o DTO é montado depois, em memória. Não é preferência:
        // `GroupBy(...).Select(g => new ProdutoVendidoDto(g.Key, g.Sum(...), ...))` **não traduz** —
        // o EF Core recusa a consulta inteira quando a projeção do agrupamento tem construtor, e o
        // erro só aparece em tempo de execução. Tipo anônimo traduz; a montagem do DTO acontece
        // sobre as oito linhas que já vieram do banco.
        var agrupado = await leitura.ProdutosVendidos
            .Where(v => unidade == null || v.UnidadeId == unidade)
            .Where(v => v.DataOperacional >= de && v.DataOperacional <= ate)
            .GroupBy(v => v.NomeProduto)
            .Select(g => new
            {
                Produto = g.Key,
                Quantidade = g.Sum(v => v.Quantidade),
                Total = g.Sum(v => v.Total),
            })
            .OrderByDescending(p => p.Quantidade)
            .ThenBy(p => p.Produto)
            .Take(TetoDeProdutos)
            .ToListAsync(cancellationToken);

        var maisVendidos = agrupado
            .Select(p => new ProdutoVendidoDto(p.Produto, p.Quantidade, p.Total))
            .ToList();

        var faturamento = porDia.Sum(d => d.Faturamento);
        var finalizados = porDia.Sum(d => d.PedidosFinalizados);

        return new ResumoDaOperacaoDto(
            de,
            ate,
            faturamento,
            finalizados,
            porDia.Sum(d => d.PedidosEmAndamento),
            porDia.Sum(d => d.ValorEmAndamento),
            porDia.Sum(d => d.PedidosCancelados),

            // RN-VEN-051 — `null` afirma "não houve venda"; zero afirmaria "o ticket foi zero", e
            // são leituras diferentes para quem compara semanas.
            finalizados == 0
                ? null
                : decimal.Round(faturamento / finalizados, 2, MidpointRounding.AwayFromZero),

            MontarSerie(de, ate, porDia),
            maisVendidos);
    }

    /// <summary>
    /// <c>RN-VEN-050</c> — a faixa inteira, dia a dia, <b>com os zeros</b>.
    /// <para>
    /// ⚠ A visão só devolve dia que teve pedido — ela agrupa o que existe. Completar a faixa é
    /// trabalho da aplicação, e é legítimo: o banco não tem como saber qual período foi pedido.
    /// Devolver só os dias com movimento faria a tela desenhar uma série com buracos, e o olho lê
    /// buraco como continuidade: segunda com R$ 0 desaparecida entre domingo e terça vira uma linha
    /// que sobe, quando a casa não vendeu nada.
    /// </para>
    /// </summary>
    private static List<DiaDaOperacaoDto> MontarSerie(
        DateOnly de, DateOnly ate, List<OperacaoDoDiaLeitura> porDia)
    {
        // Agrupado porque a plataforma pode consultar várias unidades de uma vez: a visão tem uma
        // linha por (unidade, dia), e a série da tela é por dia.
        var somaDoDia = porDia
            .GroupBy(d => d.DataOperacional)
            .ToDictionary(
                g => g.Key,
                g => (Total: g.Sum(d => d.Faturamento), Pedidos: g.Sum(d => d.PedidosFinalizados)));

        var serie = new List<DiaDaOperacaoDto>();

        for (var dia = de; dia <= ate; dia = dia.AddDays(1))
        {
            var achou = somaDoDia.TryGetValue(dia, out var valores);

            serie.Add(new DiaDaOperacaoDto(
                dia,
                achou ? valores.Total : 0m,
                achou ? valores.Pedidos : 0));
        }

        return serie;
    }
}
