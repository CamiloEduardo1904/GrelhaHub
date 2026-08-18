using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;
using GrelhaHub.Application.Features.Gerais.Plataforma;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>RN-GER-020</c> no <b>cadastro assistido</b> — a lacuna declarada na §11.5 de
/// <c>SPEC-GER-007</c>, agora fechada.
/// <para>
/// A regra já era imposta ao alterar módulo de uma unidade existente (<c>SPEC-GER-006</c>, CA-5).
/// O caminho de <b>criação com lista de módulos vendidos</b> a contornava: quem cadastrasse pelo
/// telefone poderia vender EST sem PRO e produzir uma unidade que aceita pedido e não sabe o que
/// baixar.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class DependenciaDeModulosTests(AmbienteDeIntegracao ambiente)
    : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-020 — cadastro assistido recusa EST sem PRO")]
    public async Task Criacao_SemDependencia_EhRecusada()
    {
        using var suporte = await SuporteAsync();

        var resposta = await CadastrarAsync(suporte, "Estoque Solto", "31111111000191", ["EST"]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var corpo = await resposta.Content.ReadAsStringAsync();

        corpo.Should().Contain("RN-GER-020");

        // O FECHO TRANSITIVO, e não só a dependência direta: EST precisa de PRO, e PRO precisa de
        // VEN. Listar só PRO obrigaria o vendedor a descobrir a cadeia uma recusa por vez.
        corpo.Should().Contain("PRO").And.Contain("VEN");

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await db.Unidades.AsNoTracking().CountAsync(u => u.Cnpj == "31111111000191"))
            .Should().Be(0, "nenhuma unidade nasce de um contrato incoerente");
    }

    [Fact(DisplayName = "RN-GER-020 — cadastro assistido recusa PRO sem VEN")]
    public async Task Criacao_ProSemVen_EhRecusada()
    {
        using var suporte = await SuporteAsync();

        var resposta = await CadastrarAsync(suporte, "Producao Solta", "32222222000191", ["PRO"]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("VEN");
    }

    [Fact(DisplayName = "RN-GER-020 — cadastro assistido recusa LOG sem RH")]
    public async Task Criacao_LogSemRh_EhRecusada()
    {
        using var suporte = await SuporteAsync();

        // LOG tem DUAS dependências: VEN e RH (RN-LOG-005 — entregador é colaborador ativo).
        // Informar só VEN deixa RH faltando.
        var resposta = await CadastrarAsync(suporte, "Entrega Solta", "33333333000191", ["VEN", "LOG"]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("RH");
    }

    [Fact(DisplayName = "RN-GER-020 — cadastro assistido aceita a cadeia completa")]
    public async Task Criacao_ComCadeiaCompleta_EhAceita()
    {
        using var suporte = await SuporteAsync();

        var resposta = await CadastrarAsync(
            suporte, "Cadeia Completa", "34444444000191", ["VEN", "PRO", "EST"]);

        resposta.EnsureSuccessStatusCode();

        var criado = (await resposta.Content.ReadFromJsonAsync<CadastrarRestauranteResponse>())!;

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var siglas = await SiglasHabilitadasAsync(db, criado.UnidadeId);

        // GER entra sozinho — RN-GER-019, núcleo. Não foi pedido e não pode faltar.
        siglas.Should().BeEquivalentTo(["GER", "VEN", "PRO", "EST"]);
    }

    [Fact(DisplayName = "RN-GER-019 — o núcleo nunca falta, e não é reclamado como dependência")]
    public async Task Criacao_ComApenasVen_NaoReclamaDeGer()
    {
        using var suporte = await SuporteAsync();

        // VEN depende de GER. Como GER é núcleo, o fecho o considera presente: pedir só VEN
        // funciona, e a mensagem NÃO manda "habilitar GER" — que já está habilitado.
        var resposta = await CadastrarAsync(suporte, "So Vendas", "35555555000191", ["VEN"]);

        resposta.EnsureSuccessStatusCode();

        var criado = (await resposta.Content.ReadFromJsonAsync<CadastrarRestauranteResponse>())!;

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await SiglasHabilitadasAsync(db, criado.UnidadeId))
            .Should().BeEquivalentTo(["GER", "VEN"]);
    }

    [Fact(DisplayName = "RN-GER-017 — cadastro sem lista de módulos nasce com GER e VEN")]
    public async Task SemListaDeModulos_UsaOPadrao()
    {
        // `Modulos` nulo = o conjunto padrão. É o caminho que o harness usa em todos os cenários,
        // e este teste existe para provar que fechar RN-GER-020 na criação não o mexeu.
        var restaurante = await CadastrarRestauranteAsync("Auto Servico", "36666666000191");

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await SiglasHabilitadasAsync(db, restaurante.UnidadeId))
            .Should().BeEquivalentTo(["GER", "VEN"]);
    }

    [Fact(DisplayName = "RN-GER-020 — o catálogo devolve o grafo para a tela não copiá-lo")]
    public async Task Catalogo_ExpoeOGrafo()
    {
        using var suporte = await SuporteAsync();

        var catalogo = await suporte.GetFromJsonAsync<List<ModuloDoCatalogoDto>>(
            new Uri("/api/v1/plataforma/modulos", UriKind.Relative));

        catalogo.Should().NotBeNull();
        catalogo!.Should().HaveCount(10, "os dez módulos do catálogo de regras");

        var porSigla = catalogo!.ToDictionary(m => m.Sigla, StringComparer.Ordinal);

        porSigla["GER"].Nucleo.Should().BeTrue("RN-GER-019");
        porSigla["GER"].DependeDe.Should().BeEmpty("o núcleo não depende de ninguém");

        porSigla["VEN"].DependeDe.Should().BeEquivalentTo(["GER"]);
        porSigla["EST"].DependeDe.Should().BeEquivalentTo(["PRO"]);
        porSigla["LOG"].DependeDe.Should().BeEquivalentTo(["RH", "VEN"]);
    }

    [Fact(DisplayName = "RN-GER-014 — o catálogo de módulos é privativo da plataforma")]
    public async Task Catalogo_NaoEhDoRestaurante()
    {
        var restaurante = await CadastrarRestauranteAsync("Curioso Burger", "37777777000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await gestao.GetAsync(new Uri("/api/v1/plataforma/modulos", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "RN-GER-020 — módulo desconhecido na lista falha, não é ignorado")]
    public async Task Criacao_ComSiglaDesconhecida_Falha()
    {
        using var suporte = await SuporteAsync();

        var resposta = await CadastrarAsync(suporte, "Sigla Errada", "38888888000191", ["VEN", "XPTO"]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "módulo silenciosamente não habilitado é uma venda que o cliente não recebe");

        (await resposta.Content.ReadAsStringAsync()).Should().Contain("XPTO");
    }

    // ── auxiliares ──────────────────────────────────────────────────────────────────

    private async Task<HttpClient> SuporteAsync()
    {
        var token = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        return Ambiente.CriarCliente(token);
    }

    private static async Task<HttpResponseMessage> CadastrarAsync(
        HttpClient suporte, string nomeFantasia, string cnpj, string[] modulos) =>
        await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            new
            {
                RazaoSocial = $"{nomeFantasia} LTDA",
                NomeFantasia = nomeFantasia,
                Cnpj = cnpj,
                Telefone = (string?)null,
                NomeAdministrador = "Dono",
                SobrenomeAdministrador = "Da Casa",
                CpfAdministrador = CpfDeTeste.Gerar(cnpj.GetHashCode(StringComparison.Ordinal)),
                Email = $"dono.{cnpj}@assistido.teste",
                Senha = "senha-de-teste-1234",
                Modulos = modulos,
            });

    /// <summary>As siglas VIGENTES agora, lidas do banco — não da resposta da API.</summary>
    private static async Task<List<string>> SiglasHabilitadasAsync(
        Infrastructure.Persistence.GrelhaHubDbContext db, Guid unidadeId)
    {
        var agora = DateTimeOffset.UtcNow;

        var codigos = await db.ModulosDaUnidade
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(h => h.Modulo)
            .Where(h => h.UnidadeId == unidadeId
                     && h.VigenciaInicio <= agora
                     && (h.VigenciaFim == null || h.VigenciaFim > agora))
            .Select(h => h.Modulo!.Codigo)
            .ToListAsync();

        return codigos.Select(Modulo.Sigla).ToList();
    }
}
