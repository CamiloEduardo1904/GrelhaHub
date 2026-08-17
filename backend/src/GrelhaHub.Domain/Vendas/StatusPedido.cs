namespace GrelhaHub.Domain.Vendas;

/// <summary>
/// <c>RN-VEN-018</c> ⚠ — ciclo de vida do pedido.
/// <para>
/// <b>Confirmado e Em produção são estados distintos de propósito:</b> a confirmação é o
/// instante em que o pedido vira compromisso — e é ela que dispara a baixa de estoque e a
/// ordem de produção. A produção é quando a cozinha efetivamente começa. Entre os dois pode
/// haver fila.
/// </para>
/// </summary>
public enum StatusPedido
{
    Aberto = 1,
    Confirmado = 2,
    EmProducao = 3,
    Pronto = 4,
    Entregue = 5,
    Finalizado = 6,
    Cancelado = 99,
}

/// <summary><c>RN-VEN-001</c> — todo pedido pertence a exatamente um canal de venda.</summary>
public enum CanalVenda
{
    Salao = 1,
    Balcao = 2,
    DeliveryProprio = 3,
    Marketplace = 4,
}
