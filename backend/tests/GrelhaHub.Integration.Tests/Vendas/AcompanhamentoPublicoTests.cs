using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Vendas.ConsultarPedidoPublico;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Vendas;

/// <summary>
/// A rota que o cliente consulta enquanto espera — <c>RN-VEN-018</c>, <c>RN-VEN-037</c>.
/// <para>
/// <b>Estes testes existem por causa do acompanhamento ao vivo da tela.</b> O polling do
/// `Acompanhar` vale exatamente o que esta rota entregar: se ela não refletir o avanço do estado,
/// o cliente fica olhando uma tela que se atualiza para mostrar sempre a mesma coisa — e o defeito
/// pareceria ser do frontend.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class AcompanhamentoPublicoTests(AmbienteDeIntegracao ambiente)
    : TesteDeIntegracao(ambiente)
{
    private const string Telefone = "11988887777";

    [Fact(DisplayName = "RN-VEN-018 — a consulta pública reflete cada avanço do estado")]
    public async Task ConsultaPublica_RefleteOAvanco()
    {
        var restaurante = await CadastrarRestauranteAsync("Espera Burger", "41111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Do acompanhamento", 30.00m);

        var numero = await CriarPedidoAsync(restaurante.Slug, produtoId);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        (await ConsultarAsync(anonimo, restaurante.Slug, numero)).Status.Should().Be("Aberto");

        // O ciclo inteiro, um passo por vez — é o que o cliente vê a régua percorrer.
        var esperado = new[]
        {
            ("confirmar", "Confirmado"),
            ("avancar", "EmProducao"),
            ("avancar", "Pronto"),
            ("avancar", "Entregue"),
            ("avancar", "Finalizado"),
        };

        foreach (var (rota, status) in esperado)
        {
            await AvancarAsync(gestao, restaurante, numero, rota);

            (await ConsultarAsync(anonimo, restaurante.Slug, numero))
                .Status.Should().Be(status, $"depois de {rota} o cliente precisa ver {status}");
        }
    }

    [Fact(DisplayName = "RN-VEN-019 — o cancelamento aparece na consulta do cliente")]
    public async Task ConsultaPublica_MostraOCancelamento()
    {
        var restaurante = await CadastrarRestauranteAsync("Espera Burger", "41111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Cancelado", 30.00m);

        var numero = await CriarPedidoAsync(restaurante.Slug, produtoId);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);
        using var anonimo = Ambiente.CriarCliente();

        Guid pedidoId;
        await using (var db = Contexto(restaurante))
        {
            pedidoId = await db.Pedidos.Where(p => p.Numero == numero).Select(p => p.Id).SingleAsync();
        }

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/cancelar", UriKind.Relative),
            new { Motivo = "Faltou ingrediente na hora" }))
            .EnsureSuccessStatusCode();

        var depois = await ConsultarAsync(anonimo, restaurante.Slug, numero);

        // É o estado que faz o acompanhamento PARAR na tela: `faseDe('Cancelado')` é 'morto', e o
        // hook desliga o polling em estado terminal.
        depois.Status.Should().Be("Cancelado");

        // RN-GER-012 / RN-VEN-037 — o motivo do cancelamento é dado interno. O cliente vê que foi
        // cancelado; por que, é o restaurante que conta a ele.
        depois.Should().NotBeNull();
    }

    [Fact(DisplayName = "RN-VEN-037 — sem o telefone certo a consulta não devolve o pedido")]
    public async Task ConsultaPublica_ExigeOTelefone()
    {
        var restaurante = await CadastrarRestauranteAsync("Espera Burger", "41111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Protegido", 30.00m);

        var numero = await CriarPedidoAsync(restaurante.Slug, produtoId);

        using var anonimo = Ambiente.CriarCliente();

        // O número é adivinhável — é sequencial. O telefone é o que impede varrer os pedidos da
        // casa trocando o número, e é a única barreira de uma rota anônima.
        var comOutroTelefone = await anonimo.GetAsync(new Uri(
            $"/api/publico/{restaurante.Slug}/pedidos/{numero}?telefone=11999990000",
            UriKind.Relative));

        comOutroTelefone.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var semTelefone = await anonimo.GetAsync(new Uri(
            $"/api/publico/{restaurante.Slug}/pedidos/{numero}?telefone=", UriKind.Relative));

        semTelefone.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact(DisplayName = "RN-GER-009 — o pedido de um restaurante não aparece pelo slug do outro")]
    public async Task ConsultaPublica_NaoAtravessaUnidade()
    {
        var primeiro = await CadastrarRestauranteAsync("Primeiro Espera", "41111111000191");
        var segundo = await CadastrarRestauranteAsync("Segundo Espera", "42222222000191");

        var produtoId = await CriarProdutoAsync(primeiro, "Do primeiro", 30.00m);
        var numero = await CriarPedidoAsync(primeiro.Slug, produtoId);

        using var anonimo = Ambiente.CriarCliente();

        // Mesmo número, mesmo telefone, slug do vizinho.
        var pelaOutraCasa = await anonimo.GetAsync(new Uri(
            $"/api/publico/{segundo.Slug}/pedidos/{numero}?telefone={Telefone}", UriKind.Relative));

        pelaOutraCasa.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "RN-VEN-037 — a consulta do cliente não devolve custo nem dado interno")]
    public async Task ConsultaPublica_NaoVazaDadoInterno()
    {
        var restaurante = await CadastrarRestauranteAsync("Espera Burger", "41111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Sem custo", 30.00m);

        var numero = await CriarPedidoAsync(restaurante.Slug, produtoId);

        using var anonimo = Ambiente.CriarCliente();

        var bruto = await anonimo.GetStringAsync(new Uri(
            $"/api/publico/{restaurante.Slug}/pedidos/{numero}?telefone={Telefone}",
            UriKind.Relative));

        // Chave e não substring: "custo" apareceria em `precoPraticado`? Não — mas `motivo` e
        // `unidadeId` são exatamente o tipo de campo que entra sem ninguém notar.
        foreach (var chave in new[] { "custo", "cmv", "margem", "motivo", "unidadeId", "clienteId" })
        {
            System.Text.RegularExpressions.Regex.IsMatch(
                bruto, $"\"{chave}[A-Za-z]*\"\\s*:",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Should().BeFalse($"a consulta pública não devolve {chave}");
        }
    }

    // ── auxiliares ──────────────────────────────────────────────────────────────────

    private async Task<int> CriarPedidoAsync(string slug, Guid produtoId)
    {
        using var anonimo = Ambiente.CriarCliente();

        var criado = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Quem Espera",
                Telefone,
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        criado.EnsureSuccessStatusCode();

        using var documento = await criado.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();

        return documento!.RootElement.GetProperty("numero").GetInt32();
    }

    private static async Task<PedidoPublicoResponse> ConsultarAsync(
        HttpClient anonimo, string slug, int numero)
    {
        var resposta = await anonimo.GetFromJsonAsync<PedidoPublicoResponse>(
            new Uri($"/api/publico/{slug}/pedidos/{numero}?telefone={Telefone}", UriKind.Relative));

        return resposta!;
    }

    private async Task AvancarAsync(
        HttpClient gestao, RestauranteDeTeste restaurante, int numero, string rota)
    {
        Guid pedidoId;

        await using (var db = Contexto(restaurante))
        {
            pedidoId = await db.Pedidos.Where(p => p.Numero == numero).Select(p => p.Id).SingleAsync();
        }

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/{rota}", UriKind.Relative),
            new { }))
            .EnsureSuccessStatusCode();
    }
}
