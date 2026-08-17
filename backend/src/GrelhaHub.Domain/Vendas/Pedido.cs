using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Domain.Vendas;

/// <summary>
/// Unidade central de venda — <c>RN-VEN-001</c>, <c>RN-VEN-018</c>.
/// </summary>
public sealed class Pedido : Entidade, IPertenceAUnidade, IPossuiDiaOperacional
{
    /// <summary>
    /// <c>RN-VEN-018</c> ⚠ — as únicas transições admitidas.
    /// <para>
    /// A tabela existe para que a regra seja legível de uma vez, em vez de espalhada em
    /// <c>if</c>s. Cancelamento é tratado à parte porque parte de qualquer estado anterior a
    /// Finalizado.
    /// </para>
    /// </summary>
    private static readonly Dictionary<StatusPedido, StatusPedido> Proximo = new()
    {
        [StatusPedido.Aberto] = StatusPedido.Confirmado,
        [StatusPedido.Confirmado] = StatusPedido.EmProducao,
        [StatusPedido.EmProducao] = StatusPedido.Pronto,
        [StatusPedido.Pronto] = StatusPedido.Entregue,
        [StatusPedido.Entregue] = StatusPedido.Finalizado,
    };

    private readonly List<ItemPedido> _itens = [];

    private Pedido() { }

    public Pedido(Guid unidadeId, int numero, CanalVenda canal, Guid? clienteId, string? observacao)
    {
        UnidadeId = unidadeId;
        Numero = numero;
        Canal = canal;
        ClienteId = clienteId;
        Observacao = observacao?.Trim();
        Status = StatusPedido.Aberto;
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public Guid UnidadeId { get; private set; }

    /// <summary>Sequencial por dia operacional — é o número que o cliente acompanha.</summary>
    public int Numero { get; private set; }

    public CanalVenda Canal { get; private set; }
    public StatusPedido Status { get; private set; }
    public Guid? ClienteId { get; private set; }
    public string? Observacao { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? ConfirmadoEm { get; private set; }
    public DateTimeOffset? FinalizadoEm { get; private set; }

    /// <summary>
    /// <c>RN-GER-006</c> ⚠⚠ — coluna <b>gerada pelo banco</b>. A aplicação lê, nunca escreve.
    /// Nenhuma apuração diária usa <see cref="CriadoEm"/>.
    /// </summary>
    public DateOnly DataOperacional { get; private set; }

    public IReadOnlyCollection<ItemPedido> Itens => _itens;

    /// <summary><c>RN-VEN-012</c> — total é a soma dos itens. Sem taxas nesta fatia.</summary>
    public decimal Total => _itens.Sum(i => i.Total);

    public bool AceitaAlteracaoDeItens =>
        Status is StatusPedido.Aberto or StatusPedido.Confirmado;

    /// <summary>
    /// <c>RN-VEN-003</c>, <c>RN-VEN-008</c>, <c>RN-VEN-010</c>, <c>RN-VEN-011</c>.
    /// O preço praticado é <b>copiado</b> aqui e não muda se o preço de tabela mudar depois.
    /// </summary>
    public ItemPedido AdicionarItem(Guid produtoId, string nomeProduto, decimal precoPraticado, int quantidade, string? observacao = null)
    {
        if (!AceitaAlteracaoDeItens)
        {
            throw new InvalidOperationException(
                $"RN-VEN-010 — pedido em {Status} não aceita alteração de itens.");
        }

        var item = new ItemPedido(UnidadeId, Id, produtoId, nomeProduto, precoPraticado, quantidade, observacao);
        _itens.Add(item);
        return item;
    }

    /// <summary>
    /// <c>RN-VEN-002</c> — pedido sem itens não pode ser confirmado.
    /// A confirmação é o gatilho de baixa de estoque e ordem de produção
    /// (<c>RN-EST-040</c>/<c>RN-PRO-008</c>), que ainda não existem nesta fatia.
    /// </summary>
    public void Confirmar()
    {
        if (_itens.Count == 0)
        {
            throw new InvalidOperationException(
                "RN-VEN-002 — um pedido sem itens não pode ser confirmado.");
        }

        Avancar(StatusPedido.Confirmado);
        ConfirmadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Avança um estado no ciclo — <c>RN-VEN-018</c>.</summary>
    public void AvancarStatus()
    {
        if (!Proximo.TryGetValue(Status, out var destino))
        {
            throw new InvalidOperationException(
                $"RN-VEN-019 — pedido em {Status} não avança para nenhum outro estado.");
        }

        if (destino == StatusPedido.Confirmado)
        {
            Confirmar();
            return;
        }

        Avancar(destino);
    }

    /// <summary>
    /// <c>RN-VEN-014</c> — cancelamento exige motivo.
    /// <para>
    /// <b>Não devolve estoque nesta fatia porque ainda não há estoque.</b> Quando houver:
    /// cancelado <i>após</i> entrar em produção gera <b>perda</b>, não devolução
    /// (<c>RN-EST-018</c>); cancelado <i>antes</i> não gera movimento (<c>RN-EST-019</c>).
    /// </para>
    /// </summary>
    public void Cancelar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException(
                "RN-VEN-014 — o cancelamento exige motivo.", nameof(motivo));
        }

        if (Status is StatusPedido.Finalizado or StatusPedido.Cancelado)
        {
            throw new InvalidOperationException(
                $"RN-VEN-019 — pedido {Status} não retorna a nenhum estado anterior.");
        }

        Status = StatusPedido.Cancelado;
        MotivoCancelamento = motivo.Trim();
        FinalizadoEm = DateTimeOffset.UtcNow;
    }

    private void Avancar(StatusPedido destino)
    {
        if (Status is StatusPedido.Finalizado or StatusPedido.Cancelado)
        {
            throw new InvalidOperationException(
                $"RN-VEN-019 — pedido {Status} não retorna a nenhum estado anterior.");
        }

        if (!Proximo.TryGetValue(Status, out var esperado) || esperado != destino)
        {
            throw new InvalidOperationException(
                $"RN-VEN-018 — não é possível ir de {Status} para {destino}.");
        }

        Status = destino;

        if (destino == StatusPedido.Finalizado)
        {
            FinalizadoEm = DateTimeOffset.UtcNow;
        }
    }
}
