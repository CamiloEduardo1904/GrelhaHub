using FluentAssertions;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;

namespace GrelhaHub.Domain.Tests.Vendas;

/// <summary>
/// Regras de ciclo de vida e de congelamento de preço — <c>SPEC-VEN-001</c>.
/// O ID da regra no <c>DisplayName</c> é o que torna a rastreabilidade real.
/// </summary>
public class PedidoTests
{
    private static readonly Guid Unidade = Guid.CreateVersion7();

    private static Pedido PedidoComUmItem(decimal preco = 28.00m, int quantidade = 1)
    {
        var pedido = new Pedido(
            Unidade, 1, CanalVenda.DeliveryProprio, Guid.CreateVersion7(), null,
            ViradaOperacional.Padrao, "America/Sao_Paulo");
        pedido.AdicionarItem(Guid.CreateVersion7(), "X-Burger", Dinheiro.De(preco), quantidade);
        return pedido;
    }

    [Fact(DisplayName = "RN-VEN-038 — pedido do cardápio público nasce Aberto no canal delivery próprio")]
    public void PedidoPublico_NasceAbertoEmDeliveryProprio()
    {
        var pedido = PedidoComUmItem();

        pedido.Status.Should().Be(StatusPedido.Aberto);
        pedido.Canal.Should().Be(CanalVenda.DeliveryProprio);
        pedido.ConfirmadoEm.Should().BeNull("a confirmação é ato do restaurante, não do cliente");
    }

    [Fact(DisplayName = "RN-VEN-002 — pedido sem itens não pode ser confirmado")]
    public void PedidoSemItens_NaoConfirma()
    {
        var pedido = new Pedido(
            Unidade, 1, CanalVenda.Balcao, null, null,
            ViradaOperacional.Padrao, "America/Sao_Paulo");

        var acao = pedido.Confirmar;

        acao.Should().Throw<InvalidOperationException>().WithMessage("*RN-VEN-002*");
    }

    [Fact(DisplayName = "RN-VEN-018 — o pedido percorre Aberto, Confirmado, Em produção, Pronto, Entregue, Finalizado")]
    public void CicloDeVida_PercorreOsSeisEstados()
    {
        var pedido = PedidoComUmItem();

        var percorridos = new List<StatusPedido> { pedido.Status };

        for (var i = 0; i < 5; i++)
        {
            pedido.AvancarStatus();
            percorridos.Add(pedido.Status);
        }

        percorridos.Should().Equal(
            StatusPedido.Aberto,
            StatusPedido.Confirmado,
            StatusPedido.EmProducao,
            StatusPedido.Pronto,
            StatusPedido.Entregue,
            StatusPedido.Finalizado);
    }

    [Fact(DisplayName = "RN-VEN-018 — a confirmação registra o instante que dispara produção e baixa de estoque")]
    public void Confirmacao_RegistraOInstante()
    {
        var pedido = PedidoComUmItem();

        pedido.Confirmar();

        pedido.Status.Should().Be(StatusPedido.Confirmado);
        pedido.ConfirmadoEm.Should().NotBeNull();
    }

    [Fact(DisplayName = "RN-VEN-019 — pedido Finalizado não retorna a nenhum estado anterior")]
    public void PedidoFinalizado_NaoRetorna()
    {
        var pedido = PedidoComUmItem();
        for (var i = 0; i < 5; i++)
        {
            pedido.AvancarStatus();
        }

        pedido.Status.Should().Be(StatusPedido.Finalizado);

        pedido.Invoking(p => p.AvancarStatus())
            .Should().Throw<InvalidOperationException>().WithMessage("*RN-VEN-019*");

        pedido.Invoking(p => p.Cancelar("cliente desistiu"))
            .Should().Throw<InvalidOperationException>().WithMessage("*RN-VEN-019*");
    }

    [Fact(DisplayName = "RN-VEN-019 — pedido Cancelado não retorna a nenhum estado anterior")]
    public void PedidoCancelado_NaoRetorna()
    {
        var pedido = PedidoComUmItem();
        pedido.Cancelar("cliente desistiu");

        pedido.Status.Should().Be(StatusPedido.Cancelado);

        pedido.Invoking(p => p.AvancarStatus())
            .Should().Throw<InvalidOperationException>().WithMessage("*RN-VEN-019*");
    }

    [Fact(DisplayName = "RN-GER-002 — o cancelamento exige motivo")]
    public void Cancelamento_ExigeMotivo()
    {
        var pedido = PedidoComUmItem();

        pedido.Invoking(p => p.Cancelar("  "))
            .Should().Throw<ArgumentException>().WithMessage("*RN-GER-002*");

        pedido.Status.Should().Be(StatusPedido.Aberto, "o pedido não pode mudar de estado quando a operação falha");
    }

    [Fact(DisplayName = "RN-VEN-008 — o preço praticado é copiado na venda e não muda depois")]
    public void PrecoPraticado_EhCopiadoNaVenda()
    {
        var produtoId = Guid.CreateVersion7();
        var pedido = new Pedido(
            Unidade, 1, CanalVenda.DeliveryProprio, Guid.CreateVersion7(), null,
            ViradaOperacional.Padrao, "America/Sao_Paulo");

        // Vendido a 32,00 hoje.
        var item = pedido.AdicionarItem(produtoId, "X-Bacon", Dinheiro.De(32.00m), 2);

        // Amanhã o preço de tabela do produto sobe para 38,00 — o item não acompanha,
        // porque guarda uma cópia, não uma referência.
        item.PrecoPraticado.Should().Be(Dinheiro.De(32.00m));
        item.Total.Should().Be(Dinheiro.De(64.00m));
        pedido.Total.Should().Be(Dinheiro.De(64.00m));
    }

    [Fact(DisplayName = "RN-VEN-011 — a quantidade de um item é sempre maior que zero")]
    public void Quantidade_DeveSerMaiorQueZero()
    {
        var pedido = new Pedido(
            Unidade, 1, CanalVenda.Balcao, null, null,
            ViradaOperacional.Padrao, "America/Sao_Paulo");

        pedido.Invoking(p => p.AdicionarItem(Guid.CreateVersion7(), "X-Burger", Dinheiro.De(28m), 0))
            .Should().Throw<ArgumentOutOfRangeException>().WithMessage("*RN-VEN-011*");
    }

    [Fact(DisplayName = "RN-VEN-040 — pedido em produção não aceita alteração de itens")]
    public void PedidoEmProducao_NaoAceitaNovosItens()
    {
        var pedido = PedidoComUmItem();
        pedido.AvancarStatus(); // Confirmado
        pedido.AvancarStatus(); // Em produção

        pedido.Invoking(p => p.AdicionarItem(Guid.CreateVersion7(), "Batata", Dinheiro.De(18m), 1))
            .Should().Throw<InvalidOperationException>().WithMessage("*RN-VEN-040*");
    }

    [Fact(DisplayName = "RN-VEN-039 — o total do pedido é a soma dos itens")]
    public void Total_EhASomaDosItens()
    {
        var pedido = new Pedido(
            Unidade, 1, CanalVenda.Balcao, null, null,
            ViradaOperacional.Padrao, "America/Sao_Paulo");
        pedido.AdicionarItem(Guid.CreateVersion7(), "X-Burger", Dinheiro.De(28.00m), 2);
        pedido.AdicionarItem(Guid.CreateVersion7(), "Batata frita", Dinheiro.De(18.00m), 1);
        pedido.AdicionarItem(Guid.CreateVersion7(), "Refrigerante", Dinheiro.De(7.00m), 2);

        pedido.Total.Should().Be(Dinheiro.De(88.00m));
    }
}
