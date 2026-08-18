using System.Net.Http.Json;
using GrelhaHub.Application.Features.Cardapio.ListarCardapioInterno;
using GrelhaHub.Application.Features.Gerais.Autenticar;
using GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;
using GrelhaHub.Infrastructure.Persistence;

namespace GrelhaHub.Integration.Tests.Infraestrutura;

/// <summary>
/// Base dos testes de integração: limpa o banco antes de cada caso e oferece os atalhos do
/// fluxo real — cadastrar restaurante, autenticar, criar produto.
/// <para>
/// Os atalhos passam pela <b>API</b>, não pelo banco. Semear o cenário com <c>INSERT</c>
/// direto seria mais rápido e provaria menos: o dado entraria por um caminho que o sistema
/// não tem, escapando de validator, de autorização e do preenchimento de unidade.
/// </para>
/// </summary>
public abstract class TesteDeIntegracao(AmbienteDeIntegracao ambiente) : IAsyncLifetime
{
    protected AmbienteDeIntegracao Ambiente { get; } = ambiente;

    public Task InitializeAsync() => Ambiente.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Restaurante já cadastrado e autenticado, pronto para o cenário do teste.</summary>
    protected sealed record RestauranteDeTeste(
        Guid UnidadeId,
        string Slug,
        string Email,
        string Senha,
        string Token);

    private const string SenhaPadrao = "senha-de-teste-123";

    /// <summary>
    /// Cadastra o restaurante pela rota real e autentica.
    /// <para>
    /// O cadastro semeia um cardápio de exemplo de hamburgueria — sete produtos em três
    /// categorias. Os testes que precisam de preço específico criam o seu próprio produto em
    /// vez de depender desses valores: acoplar a asserção ao cardápio de exemplo faria o teste
    /// quebrar quando alguém trocasse o preço do X-Burger.
    /// </para>
    /// </summary>
    protected async Task<RestauranteDeTeste> CadastrarRestauranteAsync(
        string nomeFantasia,
        string cnpj,
        string? telefone = null)
    {
        // ⚠ Pela rota de PLATAFORMA, e nao mais por uma anonima.
        //
        // A porta anonima de cadastro foi FECHADA em 17/08/2026 (ninguem ganha conta sem contrato
        // assinado), e o harness acompanhou em vez de manter um atalho: a propria doutrina deste
        // arquivo diz que o cenario se monta pelas rotas que o sistema TEM. Um helper cadastrando
        // por caminho inexistente escaparia do gate de plataforma e testaria uma criacao que nao
        // existe mais.
        var tokenDePlataforma = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        using var suporte = Ambiente.CriarCliente(tokenDePlataforma);

        var email = $"admin.{cnpj}@grelhahub.teste";

        var cadastro = await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            new
            {
                RazaoSocial = $"{nomeFantasia} LTDA",
                NomeFantasia = nomeFantasia,
                Cnpj = cnpj,
                Telefone = telefone,
                NomeAdministrador = "Dona do Restaurante",
                SobrenomeAdministrador = "Da Brasa",

                // RN-GER-023 — CPF valido derivado do CNPJ: cada restaurante do teste tem o seu,
                // porque o CPF e unico em todo o sistema e repetir colidiria entre cenarios.
                CpfAdministrador = CpfDeTeste.Gerar(cnpj.GetHashCode(StringComparison.Ordinal)),

                Email = email,
                Senha = SenhaPadrao,

                // Nulo = modulos padrao (GER + VEN), o mesmo conjunto com que o auto-servico
                // nascia. Os testes que precisam de outro conjunto informam a lista.
                Modulos = (string[]?)null,
            });

        cadastro.EnsureSuccessStatusCode();

        var criado = await cadastro.Content.ReadFromJsonAsync<CadastrarRestauranteResponse>()
            ?? throw new InvalidOperationException("O cadastro não devolveu corpo.");

        var token = await AutenticarAsync(email, SenhaPadrao);

        return new RestauranteDeTeste(criado.UnidadeId, criado.Slug, email, SenhaPadrao, token);
    }

    protected async Task<string> AutenticarAsync(string email, string senha)
    {
        using var anonimo = Ambiente.CriarCliente();

        var login = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login", new AutenticarCommand(email, senha));

        login.EnsureSuccessStatusCode();

        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>()
            ?? throw new InvalidOperationException("O login não devolveu corpo.");

        return sessao.Token;
    }

    /// <summary>Id da primeira categoria do cardápio, para pendurar um produto de teste.</summary>
    protected async Task<Guid> PrimeiraCategoriaAsync(RestauranteDeTeste restaurante)
    {
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var categorias = await gestao.GetFromJsonAsync<List<CategoriaInternaDto>>("/api/v1/cardapio")
            ?? throw new InvalidOperationException("O cardápio interno não devolveu corpo.");

        return categorias[0].Id;
    }

    /// <summary>
    /// Cria um produto com preço controlado pelo teste, pela rota de gerência.
    /// </summary>
    protected async Task<Guid> CriarProdutoAsync(
        RestauranteDeTeste restaurante,
        string nome,
        decimal preco,
        Guid? categoriaId = null)
    {
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var categoria = categoriaId ?? await PrimeiraCategoriaAsync(restaurante);

        var resposta = await gestao.PostAsJsonAsync(
            "/api/v1/cardapio/produtos",
            new
            {
                Id = (Guid?)null,
                CategoriaId = categoria,
                Nome = nome,
                Descricao = "Produto criado pelo teste de integração",
                Preco = preco,
            });

        resposta.EnsureSuccessStatusCode();

        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Contexto escopado na unidade do restaurante, para conferir o dado gravado.</summary>
    protected GrelhaHubDbContext Contexto(RestauranteDeTeste restaurante) =>
        Ambiente.CriarContexto(restaurante.UnidadeId);
}
