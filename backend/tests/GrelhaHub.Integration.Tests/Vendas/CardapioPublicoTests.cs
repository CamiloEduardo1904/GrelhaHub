using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Cardapio.ObterCardapioPublico;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Vendas;

/// <summary>
/// <c>SPEC-VEN-001</c> — CA-1, CA-2 e CA-7: o cardápio público.
/// <para>
/// As três asserções aqui só valem contra a API real. Um teste de unidade no handler provaria
/// que a projeção não tem o campo de custo; não provaria que a rota é anônima, que o filtro
/// global está ativo, nem que o corpo que sai pela rede não carrega o dado.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class CardapioPublicoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-012 — o cardápio público não devolve custo, CMV nem margem")]
    public async Task CardapioPublico_NaoVazaCusto()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        using var anonimo = Ambiente.CriarCliente();

        var resposta = await anonimo.GetAsync(new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "a rota do cardápio é anônima (RN-VEN-037)");

        var corpoBruto = await resposta.Content.ReadAsStringAsync();

        // A verificação é no corpo BRUTO de propósito: RN-GER-012 é restrição de dado, não de
        // tela. Devolver e esconder no front continuaria visível em qualquer inspeção de rede.
        corpoBruto.Should().NotContainEquivalentOf("custo");
        corpoBruto.Should().NotContainEquivalentOf("cmv");
        corpoBruto.Should().NotContainEquivalentOf("margem");

        var cardapio = await resposta.Content.ReadFromJsonAsync<CardapioPublicoResponse>();

        cardapio.Should().NotBeNull();
        cardapio!.NomeFantasia.Should().Be("Brasa e Fogo");

        var produtos = cardapio.Categorias.SelectMany(categoria => categoria.Produtos).ToList();

        produtos.Should().NotBeEmpty("o cadastro semeia um cardápio de exemplo");
        produtos.Should().OnlyContain(produto => produto.Preco > 0, "o cardápio mostra preço de venda");
    }

    [Fact(DisplayName = "RN-GER-009 — o cardápio de um restaurante não mostra produto de outro")]
    public async Task CardapioPublico_IsolaEntreRestaurantes()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Negra", "22222222000192");

        brasa.Slug.Should().NotBe(fumaca.Slug, "RN-VEN-036 — o slug é único em todo o sistema");

        // Um produto que existe só no segundo restaurante. O cardápio de exemplo é igual nos
        // dois, então sem um item distintivo o teste passaria mesmo com o filtro desligado.
        await CriarProdutoAsync(fumaca, "Segredo da Fumaca", 47.50m);

        using var anonimo = Ambiente.CriarCliente();

        var doBrasa = await anonimo.GetFromJsonAsync<CardapioPublicoResponse>(
            new Uri($"/api/publico/{brasa.Slug}/cardapio", UriKind.Relative));

        var daFumaca = await anonimo.GetFromJsonAsync<CardapioPublicoResponse>(
            new Uri($"/api/publico/{fumaca.Slug}/cardapio", UriKind.Relative));

        var idsDoBrasa = doBrasa!.Categorias.SelectMany(c => c.Produtos).Select(p => p.Id).ToList();
        var idsDaFumaca = daFumaca!.Categorias.SelectMany(c => c.Produtos).Select(p => p.Id).ToList();

        idsDoBrasa.Should().NotBeEmpty();
        idsDaFumaca.Should().NotBeEmpty();

        idsDoBrasa.Should().NotIntersectWith(idsDaFumaca, "nenhum produto pertence a duas unidades");

        doBrasa.Categorias.SelectMany(c => c.Produtos).Select(p => p.Nome)
            .Should().NotContain("Segredo da Fumaca");

        daFumaca.Categorias.SelectMany(c => c.Produtos).Select(p => p.Nome)
            .Should().Contain("Segredo da Fumaca", "o produto aparece no cardápio de quem é dono dele");
    }

    [Fact(DisplayName = "RN-VEN-020 — produto indisponível sai do cardápio e é recusado no pedido")]
    public async Task ProdutoIndisponivel_SaiDoCardapioEEhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        var produtoId = await CriarProdutoAsync(restaurante, "Costela Defumada", 62.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);
        using var anonimo = Ambiente.CriarCliente();

        var acabouHoje = await gestao.PatchAsJsonAsync(
            new Uri($"/api/v1/cardapio/produtos/{produtoId}/disponibilidade", UriKind.Relative),
            new { Disponivel = false });

        acabouHoje.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cardapio = await anonimo.GetFromJsonAsync<CardapioPublicoResponse>(
            new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        cardapio!.Categorias.SelectMany(c => c.Produtos).Select(p => p.Id)
            .Should().NotContain(produtoId, "o que acabou não aparece no cardápio");

        var pedido = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente Teimoso",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        pedido.StatusCode.Should().Be(
            HttpStatusCode.UnprocessableEntity,
            "quem guardou o id e mandou direto também tem de ser recusado");

        var erro = await pedido.Content.ReadAsStringAsync();

        // O ID da regra volta ao cliente: é o que liga o erro visto na tela ao catálogo.
        erro.Should().Contain("RN-VEN-020");
    }

    [Fact(DisplayName = "RN-VEN-020 — produto inativado permanece nos pedidos históricos")]
    public async Task ProdutoInativado_ContinuaNoHistorico()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        var produtoId = await CriarProdutoAsync(restaurante, "Linguica Artesanal", 26.00m);

        using var anonimo = Ambiente.CriarCliente();

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, produtoId, quantidade: 1);

        criado.EnsureSuccessStatusCode();

        // Inativar não tem rota nesta fatia (SPEC-GER-004 traz a gestão de cadastro);
        // o que importa provar é que a inativação não apaga o passado.
        await using (var db = Contexto(restaurante))
        {
            var produto = await db.Produtos.FirstAsync(p => p.Id == produtoId);
            produto.Inativar();
            await db.SaveChangesAsync();
        }

        var cardapio = await anonimo.GetFromJsonAsync<CardapioPublicoResponse>(
            new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        cardapio!.Categorias.SelectMany(c => c.Produtos).Select(p => p.Id)
            .Should().NotContain(produtoId, "produto inativo não entra em pedido novo");

        await using var conferencia = Contexto(restaurante);

        var itens = await conferencia.ItensDePedido.Where(i => i.ProdutoId == produtoId).ToListAsync();

        itens.Should().ContainSingle("RN-GER-002 — o registro transacional não é excluído");
        itens[0].NomeProduto.Should().Be("Linguica Artesanal");
    }

    private static Task<HttpResponseMessage> CriarPedidoAsync(
        HttpClient anonimo, string slug, Guid produtoId, int quantidade) =>
        anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente de Teste",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = quantidade, Observacao = (string?)null } },
            });
}
