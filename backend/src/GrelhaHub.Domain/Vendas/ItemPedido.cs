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
        Dinheiro precoPraticado,
        int quantidade,
        string? observacao)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantidade), quantidade,
                "RN-VEN-011 — a quantidade deve ser maior que zero.");
        }

        if (precoPraticado.Valor < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precoPraticado), precoPraticado,
                "O preço praticado não pode ser negativo.");
        }

        UnidadeId = unidadeId;
        PedidoId = pedidoId;
        ProdutoId = produtoId;
        NomeProduto = nomeProduto;
        PrecoPraticado = precoPraticado;
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
    public Dinheiro PrecoPraticado { get; private set; }

    /// <summary>
    /// Nome do produto no momento da venda, também congelado.
    /// <para>
    /// Referenciar o cadastro atual faria um comprovante antigo mudar de conteúdo quando o
    /// produto fosse renomeado — mesmo princípio de <c>RN-VEN-008</c>, aplicado ao texto.
    /// </para>
    /// </summary>
    public string NomeProduto { get; private set; } = null!;

    /// <summary>
    /// <c>RN-VEN-011</c> — inteiro, e não <see cref="Common.Quantidade"/>: são dois lanches,
    /// não 2,0000 lanches. Insumo se mede em quatro casas; produto vendido se conta.
    /// </summary>
    public int Quantidade { get; private set; }
    public string? Observacao { get; private set; }

    /// <summary>
    /// <c>RN-VEN-009</c> — quantidade × preço praticado.
    /// <para>
    /// Calculado no domínio, não coluna gerada — dívida declarada na §8 de
    /// <c>SPEC-VEN-001</c>, que resolve na onda 4, quando ajustes e desconto mudam a fórmula
    /// (<c>RN-VEN-028</c>).
    /// </para>
    /// </summary>
    public Dinheiro Total => PrecoPraticado * Quantidade;

    public Pedido? Pedido { get; private set; }
}
