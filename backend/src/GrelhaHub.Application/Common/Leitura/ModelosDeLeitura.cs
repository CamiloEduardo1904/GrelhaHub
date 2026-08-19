namespace GrelhaHub.Application.Common.Leitura;

/// <summary>
/// Os modelos de <b>leitura</b> — ADR-0019.
/// <para>
/// <b>Nenhum destes tipos é entidade de domínio.</b> Eles não têm comportamento, não têm invariante
/// e não têm chave: são o formato que a tela precisa, mapeado a uma visão do banco. Um agregado
/// existe para <b>garantir regra na escrita</b>; um modelo de leitura existe para <b>responder uma
/// pergunta</b>, e confundir os dois é o que faz consulta carregar objeto que ela não vai usar.
/// </para>
/// <para>
/// ⚠ Por que <c>decimal</c> e não <c>Dinheiro</c>: o value object existe para impedir que alguém
/// some R$ 12,00 com 12 gramas <b>ao calcular</b>. Aqui não se calcula nada — o valor já veio somado
/// do banco, e envolvê-lo num value object só criaria a conversão que impede o EF Core de traduzir a
/// consulta, que é exatamente o problema que estas visões resolvem.
/// </para>
/// </summary>
/// <param name="Id">Identidade do pedido. <c>numero</c> é identificador HUMANO e se repete entre dias e unidades.</param>
public sealed record PedidoResumoLeitura(
    Guid Id,
    Guid UnidadeId,
    int Numero,
    int Canal,
    int Status,
    string? NomeCliente,
    string? TelefoneCliente,
    decimal Total,
    int QuantidadeDeItens,
    DateTimeOffset CriadoEm,
    DateOnly DataOperacional,
    string? Observacao,

    /// <summary><c>RN-GER-002</c> — por que o pedido foi cancelado.</summary>
    string? MotivoCancelamento,

    /// <summary><c>RN-VEN-058</c> ⚠⚠ — quando. Campo próprio, nunca <c>dh_finalizacao</c>.</summary>
    DateTimeOffset? CanceladoEm,

    /// <summary>
    /// <c>RN-VEN-058</c> — quem, com nome e sobrenome, resolvido pela <b>visão</b>.
    /// <para>
    /// ⚠ O nome vem do <c>JOIN</c> e não de uma segunda consulta: a lista de pedidos é rota de
    /// operação e já faz duas idas ao banco (pedidos e itens). Uma terceira, para desenhar uma
    /// linha que aparece em poucos cartões, seria paga por todos os outros.
    /// </para>
    /// <para>
    /// ⚠ <b>Nulo no pedido cancelado antes de 19/08/2026</b> — a tela omite a linha em vez de
    /// inventar "sistema".
    /// </para>
    /// </summary>
    string? CanceladoPor) : IRecortadoPorUnidade;

/// <param name="Total">Já multiplicado: <c>preco_praticado × quantidade</c>, calculado pela visão.</param>
public sealed record ItemDePedidoLeitura(
    Guid PedidoId,
    Guid UnidadeId,
    string NomeProduto,
    int Quantidade,
    decimal PrecoPraticado,
    decimal Total,
    string? Observacao) : IRecortadoPorUnidade;

/// <summary>
/// Um dia operacional de uma unidade, com os números já separados por <c>RN-VEN-049</c>.
/// </summary>
/// <param name="Faturamento">⚠ Só o que está <b>Finalizado</b>. O que ainda pode ser cancelado fica fora.</param>
/// <param name="ValorEmAndamento">Confirmado, em produção, pronto ou entregue — <b>fora</b> do faturamento e <b>dentro</b> da tela.</param>
public sealed record OperacaoDoDiaLeitura(
    Guid UnidadeId,
    DateOnly DataOperacional,
    decimal Faturamento,
    int PedidosFinalizados,
    int PedidosEmAndamento,
    decimal ValorEmAndamento,
    int PedidosCancelados) : IRecortadoPorUnidade;

public sealed record ProdutoVendidoLeitura(
    Guid UnidadeId,
    DateOnly DataOperacional,
    string NomeProduto,
    int Quantidade,
    decimal Total) : IRecortadoPorUnidade;

/// <summary>
/// Só o necessário para dizer QUEM fez — ADR-0017.
/// <para>
/// ⚠⚠ <b>Não implementa <see cref="IRecortadoPorUnidade"/>, e o nome estreito é a proteção.</b>
/// A trilha registra também o que a PLATAFORMA fez, e o usuário de plataforma não tem unidade
/// (<c>RN-GER-014</c>) — recortar aqui exibiria "—" no lugar do nome de quem mexeu, justamente no
/// caso que mais importa auditar.
/// </para>
/// <para>
/// Como consequência este modelo <b>enxerga todos os usuários</b>, e por isso ele se chama
/// "para autoria" e carrega só id e nome: quem precisar listar usuário usa outro caminho, com
/// recorte. Modelo largo com nome genérico é convite a vazamento entre casas.
/// </para>
/// </summary>
public sealed record AutorLeitura(Guid Id, string Nome);

/// <summary>
/// O marcador de <b>recorte por unidade</b> no lado da leitura — <c>RN-GER-009</c>.
/// <para>
/// ⚠⚠ <b>Existe porque a primeira versão do contexto de leitura filtrava TUDO</b>, exigindo
/// <c>UnidadeId</c> de todo modelo. Parecia mais seguro e quebrava as rotas anônimas: o cardápio
/// público resolve a casa <b>pelo slug</b>, e nesse instante ainda não há unidade no contexto — o
/// filtro devolveria zero linhas e o cliente veria "restaurante não encontrado" para uma casa que
/// existe.
/// </para>
/// <para>
/// É o mesmo desenho do lado da escrita, onde só entidade que implementa <c>IPertenceAUnidade</c>
/// recebe o filtro global. Modelo que descreve a PRÓPRIA unidade, ou catálogo compartilhado entre
/// todas (módulo, parâmetro), não é recortado — e a ausência do marcador é a declaração disso.
/// </para>
/// </summary>
public interface IRecortadoPorUnidade
{
    Guid UnidadeId { get; }
}
