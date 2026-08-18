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
    string? Observacao);

/// <param name="Total">Já multiplicado: <c>preco_praticado × quantidade</c>, calculado pela visão.</param>
public sealed record ItemDePedidoLeitura(
    Guid PedidoId,
    Guid UnidadeId,
    string NomeProduto,
    int Quantidade,
    decimal PrecoPraticado,
    decimal Total,
    string? Observacao);

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
    int PedidosCancelados);

public sealed record ProdutoVendidoLeitura(
    Guid UnidadeId,
    DateOnly DataOperacional,
    string NomeProduto,
    int Quantidade,
    decimal Total);
