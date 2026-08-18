using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Cardapio.ListarCardapioInterno;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Application.Features.Vendas.ListarPedidos;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// O suporte olhando o fluxo de <b>uma</b> casa — <c>RN-GER-014</c>, ADR-0013.
/// <para>
/// A plataforma já enxergava a base inteira (`AcessoTests` CA-9, que é o ADR-0013). O que faltava
/// era o contrário: <b>focar em um cliente</b>. Sem recorte, atender alguém por telefone
/// significava olhar os pedidos de três clientes de uma vez, sem coluna dizendo de quem era cada
/// linha — e a listagem de usuários era pior, porque respondia "unidade não encontrada" para a
/// plataforma.
/// </para>
/// <para>
/// <b>Ler não exige sessão de suporte</b> (<c>RN-GER-015</c>): ler para diagnosticar é rotina,
/// alterar sem dizer por quê não é.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class LeituraDePlataformaTests(AmbienteDeIntegracao ambiente)
    : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-014 — o suporte lista os usuários de UMA casa")]
    public async Task Usuarios_DeUmaCasa()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Leitura", "52222222000192");

        using var suporte = await SuporteAsync();

        var doBrasa = await suporte.GetFromJsonAsync<List<UsuarioDto>>(
            new Uri($"/api/v1/usuarios?unidadeId={brasa.UnidadeId}", UriKind.Relative));

        doBrasa.Should().NotBeNull().And.HaveCount(1, "cada casa nasce com o seu administrador");

        // A prova é pelo dado do OUTRO, não por contagem: contar provaria só que o número bateu.
        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var emailDaFumaca = await db.Usuarios.AsNoTracking()
            .Where(u => u.UnidadeId == fumaca.UnidadeId)
            .Select(u => u.Email)
            .SingleAsync();

        doBrasa!.Select(u => u.Email).Should().NotContain(emailDaFumaca);
    }

    [Fact(DisplayName = "RN-GER-014 — sem recorte, o suporte vê os usuários da base inteira")]
    public async Task Usuarios_SemRecorte_VeTudo()
    {
        await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");
        await CadastrarRestauranteAsync("Fumaca Leitura", "52222222000192");

        using var suporte = await SuporteAsync();

        var todos = await suporte.GetFromJsonAsync<List<UsuarioDto>>(
            new Uri("/api/v1/usuarios", UriKind.Relative));

        // ADR-0013 — e RN-GER-016: o próprio usuário de plataforma NÃO aparece, nem aqui.
        todos.Should().NotBeNull().And.HaveCount(2);
        todos!.Select(u => u.Email).Should().NotContain(AmbienteDeIntegracao.EmailDePlataforma);
    }

    [Fact(DisplayName = "RN-GER-014 — o suporte lista os pedidos de UMA casa")]
    public async Task Pedidos_DeUmaCasa()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Leitura", "52222222000192");

        await CriarPedidoAsync(brasa, "Brisket do Brasa");
        await CriarPedidoAsync(fumaca, "Costela da Fumaca");

        using var suporte = await SuporteAsync();

        var doBrasa = await suporte.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri($"/api/v1/pedidos?unidadeId={brasa.UnidadeId}", UriKind.Relative));

        doBrasa.Should().NotBeNull().And.HaveCount(1);
        doBrasa!.Single().Itens.Single().Produto.Should().Be("Brisket do Brasa");

        // Sem recorte continua vendo os dois — ADR-0013, e é o que `AcessoTests` CA-9 afirma.
        var todos = await suporte.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        todos.Should().NotBeNull().And.HaveCount(2);
    }

    [Fact(DisplayName = "RN-GER-014 — o suporte lê o cardápio de UMA casa")]
    public async Task Cardapio_DeUmaCasa()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Leitura", "52222222000192");

        await CriarProdutoAsync(brasa, "Exclusivo do Brasa", 40.00m);
        await CriarProdutoAsync(fumaca, "Exclusivo da Fumaca", 42.00m);

        using var suporte = await SuporteAsync();

        var doBrasa = await suporte.GetFromJsonAsync<List<CategoriaInternaDto>>(
            new Uri($"/api/v1/cardapio?unidadeId={brasa.UnidadeId}", UriKind.Relative));

        var nomes = doBrasa!.SelectMany(c => c.Produtos).Select(p => p.Nome).ToList();

        nomes.Should().Contain("Exclusivo do Brasa");
        nomes.Should().NotContain("Exclusivo da Fumaca");
    }

    [Fact(DisplayName = "RN-GER-014 — o suporte lê os perfis de UMA casa, para montar o seletor")]
    public async Task Perfis_DeUmaCasa()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");
        await CadastrarRestauranteAsync("Fumaca Leitura", "52222222000192");

        using var suporte = await SuporteAsync();

        var perfis = await suporte.GetFromJsonAsync<List<PerfilDaUnidadeDto>>(
            new Uri($"/api/v1/usuarios/perfis?unidadeId={brasa.UnidadeId}", UriKind.Relative));

        // Seis por unidade. Sem o recorte viriam doze, e o seletor da tela ofereceria o perfil de
        // um restaurante para criar usuário em outro — que o servidor recusaria por FK.
        perfis.Should().NotBeNull().And.HaveCount(6);
    }

    [Fact(DisplayName = "RN-GER-015 — ler o fluxo de um cliente NÃO exige sessão de suporte")]
    public async Task Leitura_NaoExigeSessaoDeSuporte()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");

        await CriarPedidoAsync(restaurante, "Para diagnosticar");

        using var suporte = await SuporteAsync();

        // Nenhuma sessão foi aberta. As quatro leituras passam.
        foreach (var rota in new[]
                 {
                     $"/api/v1/usuarios?unidadeId={restaurante.UnidadeId}",
                     $"/api/v1/usuarios/perfis?unidadeId={restaurante.UnidadeId}",
                     $"/api/v1/pedidos?unidadeId={restaurante.UnidadeId}",
                     $"/api/v1/cardapio?unidadeId={restaurante.UnidadeId}",
                 })
        {
            (await suporte.GetAsync(new Uri(rota, UriKind.Relative)))
                .StatusCode.Should().Be(HttpStatusCode.OK, $"ler é rotina de suporte: {rota}");
        }
    }

    [Fact(DisplayName = "RN-GER-016 — o restaurante informando outra unidade é IGNORADO")]
    public async Task Restaurante_NaoEscolheAUnidade()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Leitura", "52222222000192");

        await CriarPedidoAsync(brasa, "Brisket do Brasa");
        await CriarPedidoAsync(fumaca, "Costela da Fumaca");

        using var doBrasa = Ambiente.CriarCliente(brasa.Token);

        // ⚠ ESTA é a metade que importa. O parâmetro existe na rota, e para o restaurante ele não
        // vale nada: honrá-lo daria a um cliente uma forma de ler o pedido do vizinho trocando um
        // GUID na barra de endereço.
        var tentandoOVizinho = await doBrasa.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri($"/api/v1/pedidos?unidadeId={fumaca.UnidadeId}", UriKind.Relative));

        tentandoOVizinho.Should().NotBeNull().And.HaveCount(1);
        tentandoOVizinho!.Single().Itens.Single().Produto.Should().Be("Brisket do Brasa",
            "o parâmetro foi ignorado e a consulta devolveu a PRÓPRIA unidade");

        var usuarios = await doBrasa.GetFromJsonAsync<List<UsuarioDto>>(
            new Uri($"/api/v1/usuarios?unidadeId={fumaca.UnidadeId}", UriKind.Relative));

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var emailDaFumaca = await db.Usuarios.AsNoTracking()
            .Where(u => u.UnidadeId == fumaca.UnidadeId)
            .Select(u => u.Email)
            .SingleAsync();

        usuarios!.Select(u => u.Email).Should().NotContain(emailDaFumaca);
    }

    [Fact(DisplayName = "RN-GER-009 — a lista sem recorte diz de qual unidade é cada linha")]
    public async Task SemRecorte_CadaLinhaDizAUnidade()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Leitura", "52222222000192");

        await CriarPedidoAsync(brasa, "Brisket do Brasa");
        await CriarPedidoAsync(fumaca, "Costela da Fumaca");

        using var suporte = await SuporteAsync();

        var todos = await suporte.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        todos.Should().NotBeNull().And.HaveCount(2);

        // ⚠ O `numero` é identificador HUMANO: sequencial por dia operacional e **repetido entre
        // unidades**. Os dois pedidos aqui são o nº 1 da sua casa. Sem `unidadeId` no payload, a
        // lista da base inteira mostraria duas linhas "nº 1" indistinguíveis — e o suporte
        // atribuiria o pedido de um cliente ao outro.
        todos!.Select(p => p.Numero).Should().AllBeEquivalentTo(1,
            "o número se repete entre unidades por desenho");

        todos!.Select(p => p.UnidadeId).Should().BeEquivalentTo(
            new[] { brasa.UnidadeId, fumaca.UnidadeId },
            "é o GUID da unidade que distingue as duas linhas");

        // E o `Id` do pedido também é GUID, único em todo o sistema.
        todos!.Select(p => p.Id).Distinct().Should().HaveCount(2);
    }

    [Fact(DisplayName = "RN-GER-009 — a lista de usuários sem recorte também traz a unidade")]
    public async Task SemRecorte_UsuariosTrazemAUnidade()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa Leitura", "51111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Leitura", "52222222000192");

        using var suporte = await SuporteAsync();

        var todos = await suporte.GetFromJsonAsync<List<UsuarioDto>>(
            new Uri("/api/v1/usuarios", UriKind.Relative));

        todos!.Select(u => u.UnidadeId).Should().BeEquivalentTo(
            new Guid?[] { brasa.UnidadeId, fumaca.UnidadeId });
    }

    // ── auxiliares ──────────────────────────────────────────────────────────────────

    private async Task<HttpClient> SuporteAsync()
    {
        var token = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        return Ambiente.CriarCliente(token);
    }

    private async Task CriarPedidoAsync(RestauranteDeTeste restaurante, string produto)
    {
        var produtoId = await CriarProdutoAsync(restaurante, produto, 39.90m);

        using var anonimo = Ambiente.CriarCliente();

        (await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            }))
            .EnsureSuccessStatusCode();
    }
}
