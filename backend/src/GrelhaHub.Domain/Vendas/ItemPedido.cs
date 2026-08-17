using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Vendas;

/// <summary>
/// Item de um pedido — <c>RN-VEN-003</c>.
/// </summary>
public sealed class ItemPedido : Entidade, IPertenceAUnidade
{
    private ItemPedido() { }

    internal ItemPedido(
        Guid unidadeId,
        Guid pedidoId,
        Guid produtoId,
        string nomeProduto,
        decimal precoPraticado,
        int quantidade,
        string? observacao)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantidade), quantidade,
                "RN-VEN-011 — a quantidade deve ser maior que zero.");
        }

        if (precoPraticado < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precoPraticado), precoPraticado,
                "O preço praticado não pode ser negativo.");
        }

        UnidadeId = unidadeId;
        PedidoId = pedidoId;
        ProdutoId = produtoId;
        NomeProduto = nomeProduto;
        PrecoPraticado = Math.Round(precoPraticado, 2, MidpointRounding.AwayFromZero);
        Quantidade = quantidade;
        Observacao = observacao?.Trim();
    }

    public Guid UnidadeId { get; private set; }
    public Guid PedidoId { get; private set; }
    public Guid ProdutoId { get; private set; }

    /// <summary>
    /// <c>RN-VEN-008</c> ⚠ — <b>cópia</b> do preço no momento da venda. Não muda se o preço
    /// de tabela do produto for alterado depois.
    /// </summary>
    public decimal PrecoPraticado { get; private set; }

    /// <summary>
    /// Nome do produto no momento da venda, também congelado.
    /// <para>
    /// Referenciar o cadastro atual faria um comprovante antigo mudar de conteúdo quando o
    /// produto fosse renomeado — mesmo princípio de <c>RN-VEN-008</c>, aplicado ao texto.
    /// </para>
    /// </summary>
    public string NomeProduto { get; private set; } = null!;

    public int Quantidade { get; private set; }
    public string? Observacao { get; private set; }

    /// <summary><c>RN-VEN-009</c> — quantidade × preço praticado. Coluna gerada no banco.</summary>
    public decimal Total => PrecoPraticado * Quantidade;

    public Pedido? Pedido { get; private set; }
}
