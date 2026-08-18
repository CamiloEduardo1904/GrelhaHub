using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.Autenticar;
using GrelhaHub.Application.Features.Gerais.Plataforma;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Integration.Tests.Infraestrutura;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-007</c> — CA-13 a CA-19: a plataforma administrando os seus próprios usuários
/// (<c>RN-GER-030</c>, <c>RN-GER-031</c>).
/// <para>
/// A regra nasceu de uma falha de operação: <c>RN-GER-016</c> sempre previu "criado por outro
/// usuário de plataforma <b>ou</b> pela carga inicial", e só a carga inicial existia. Quando a
/// instalação subiu sem usuário de plataforma nenhum, não havia porta.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class UsuariosDePlataformaTests(AmbienteDeIntegracao ambiente)
    : TesteDeIntegracao(ambiente)
{
    private const string SenhaInicial = "senha-inicial-123";

    private async Task<HttpClient> SuporteAsync()
    {
        var token = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        return Ambiente.CriarCliente(token);
    }

    private static object Corpo(string nome, string email) =>
        new { Nome = nome, Email = email, SenhaInicial };

    [Fact(DisplayName = "RN-GER-030 — plataforma cria outro usuário de plataforma, sem unidade e sem perfil (CA-13)")]
    public async Task CA13_PlataformaCriaUsuarioDePlataforma()
    {
        using var suporte = await SuporteAsync();

        var resposta = await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/usuarios", UriKind.Relative),
            Corpo("Segundo Suporte", "segundo@grelhahub.teste"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var criado = await resposta.Content.ReadFromJsonAsync<UsuarioDeSuporteDto>();
        criado.Should().NotBeNull();
        criado!.Email.Should().Be("segundo@grelhahub.teste");
        criado.Ativo.Should().BeTrue();

        // RF-SEG-005 — duas pessoas conhecem esta senha. Difere da carga inicial, que nasce sem a
        // pendência porque a senha veio do .env e não foi entregue a ninguém.
        criado.DeveTrocarSenha.Should().BeTrue();

        // RN-GER-014 — o login confirma o que o cadastro prometeu: é plataforma e não tem unidade.
        using var anonimo = Ambiente.CriarCliente();
        var login = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login",
            new AutenticarCommand("segundo@grelhahub.teste", SenhaInicial));

        login.EnsureSuccessStatusCode();
        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>();

        sessao.Should().NotBeNull();
        sessao!.EhPlataforma.Should().BeTrue();
        sessao.UnidadeId.Should().BeNull();
        sessao.DeveTrocarSenha.Should().BeTrue();
    }

    /// <summary>
    /// <c>RN-GER-030</c> — <b>reescrito em 18/08/2026</b>, ADR-0020.
    /// <para>
    /// A versão anterior provava um PAR: criar usuário de plataforma era isento de sessão de
    /// suporte, e alterar dado de cliente continuava recusado sem ela. Era o par que mostrava que a
    /// isenção pertencia à operação e não a um gate desligado por acidente.
    /// </para>
    /// <para>
    /// ⚠ Com a sessão removida, o par perdeu o segundo lado — e a pergunta que sobra é outra, mais
    /// simples e igualmente importante: <b>a plataforma escreve sobre o cliente, e o rastro fica</b>.
    /// A recusa por falta de sessão virou registro na trilha (ver <c>CA-10</c> em
    /// <c>UsuariosESuporteTests</c>).
    /// </para>
    /// </summary>
    [Fact(DisplayName = "RN-GER-030 — a plataforma cria o próprio usuário e escreve sobre o cliente (CA-14)")]
    public async Task CA14_PlataformaCriaEEscreveSobreOCliente()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa da Isencao", "22222222000191");

        using var suporte = await SuporteAsync();

        // Criar colega de plataforma: dado da própria casa, sem unidade envolvida.
        var criacao = await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/usuarios", UriKind.Relative),
            Corpo("Suporte Sem Sessao", "sem.sessao@grelhahub.teste"));

        criacao.StatusCode.Should().Be(HttpStatusCode.OK);

        // E alterar dado de um CLIENTE passa direto — sem portão (ADR-0020).
        var alteracao = await suporte.PutAsJsonAsync(
            new Uri($"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}", UriKind.Relative),
            new { RazaoSocial = "Outra Razao LTDA", NomeFantasia = "Outro Nome", Telefone = (string?)null });

        alteracao.StatusCode.Should().Be(
            HttpStatusCode.OK, await alteracao.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "RN-GER-030 — o usuário de plataforma aparece na lista da plataforma e em nenhuma do restaurante (CA-15)")]
    public async Task CA15_ApareceParaAPlataformaEEInvisivelAoRestaurante()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Invisivel", "33333333000191");

        using var suporte = await SuporteAsync();

        await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/usuarios", UriKind.Relative),
            Corpo("Terceiro Suporte", "terceiro@grelhahub.teste"));

        var daPlataforma = await suporte.GetFromJsonAsync<List<UsuarioDeSuporteDto>>(
            "/api/v1/plataforma/usuarios");

        daPlataforma.Should().NotBeNull();
        daPlataforma!.Select(u => u.Email).Should().Contain("terceiro@grelhahub.teste");

        // RN-GER-016 — o restaurante não vê usuário de plataforma em listagem alguma.
        using var gestao = Ambiente.CriarCliente(restaurante.Token);
        var daUnidade = await gestao.GetFromJsonAsync<List<UsuarioDto>>("/api/v1/usuarios");

        daUnidade.Should().NotBeNull();
        daUnidade!.Select(u => u.Email)
            .Should().NotContain(e => e.EndsWith("@grelhahub.teste", StringComparison.Ordinal)
                                   && e.StartsWith("terceiro", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "RN-GER-031 — ninguém inativa o próprio acesso de plataforma (CA-16)")]
    public async Task CA16_NaoInativaASiMesmo()
    {
        using var suporte = await SuporteAsync();

        var lista = await suporte.GetFromJsonAsync<List<UsuarioDeSuporteDto>>(
            "/api/v1/plataforma/usuarios");

        var eu = lista!.Single(u => u.EhVoce);

        var resposta = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/usuarios/{eu.Id}/situacao", UriKind.Relative),
            new { Ativo = false });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var depois = await suporte.GetFromJsonAsync<List<UsuarioDeSuporteDto>>(
            "/api/v1/plataforma/usuarios");

        depois!.Single(u => u.Id == eu.Id).Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "RN-GER-031 — o último usuário de plataforma ativo não pode ser inativado (CA-17)")]
    public async Task CA17_NaoInativaOUltimoAtivo()
    {
        using var suporte = await SuporteAsync();

        var criado = await (await suporte.PostAsJsonAsync(
                new Uri("/api/v1/plataforma/usuarios", UriKind.Relative),
                Corpo("Suporte Substituto", "substituto@grelhahub.teste")))
            .Content.ReadFromJsonAsync<UsuarioDeSuporteDto>();

        // O novo troca a senha e guarda um token válido.
        var tokenDoNovo = await TrocarSenhaEReautenticarAsync("substituto@grelhahub.teste");
        using var novo = Ambiente.CriarCliente(tokenDoNovo);

        // A carga inicial inativa o novo: sobra um ativo, é permitido.
        var inativacao = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/usuarios/{criado!.Id}/situacao", UriKind.Relative),
            new { Ativo = false });

        inativacao.StatusCode.Should().Be(HttpStatusCode.OK);

        // ⚠ E aqui está o caminho REAL da regra, que não é hipotético: `Ativo` é conferido no
        // LOGIN (RN-RH-006) e em nenhuma requisição depois — o token do usuário recém-inativado
        // continua funcionando até expirar. Com ele, o inativado tentaria desligar o único ativo
        // que restou, e a instalação ficaria sem nenhum acesso de suporte.
        //
        // A recusa de auto-inativação não cobre este caso: o alvo é outro usuário.
        var ultimo = (await suporte.GetFromJsonAsync<List<UsuarioDeSuporteDto>>(
            "/api/v1/plataforma/usuarios"))!.Single(u => u.Ativo);

        var tentativa = await novo.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/usuarios/{ultimo.Id}/situacao", UriKind.Relative),
            new { Ativo = false });

        tentativa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var depois = await suporte.GetFromJsonAsync<List<UsuarioDeSuporteDto>>(
            "/api/v1/plataforma/usuarios");

        depois!.Count(u => u.Ativo).Should().Be(1);
    }

    [Fact(DisplayName = "RN-GER-031 — inativar não apaga: reativar devolve o acesso (CA-17b)")]
    public async Task CA17b_ReativarDevolveOAcesso()
    {
        using var suporte = await SuporteAsync();

        var criado = await (await suporte.PostAsJsonAsync(
                new Uri("/api/v1/plataforma/usuarios", UriKind.Relative),
                Corpo("Suporte Efemero", "efemero@grelhahub.teste")))
            .Content.ReadFromJsonAsync<UsuarioDeSuporteDto>();

        await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/usuarios/{criado!.Id}/situacao", UriKind.Relative),
            new { Ativo = false });

        // Inativado, não autentica (RN-RH-006) — é o efeito percebido de RN-GER-002.
        using var anonimo = Ambiente.CriarCliente();
        var recusado = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login",
            new AutenticarCommand("efemero@grelhahub.teste", SenhaInicial));

        recusado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var reativacao = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/usuarios/{criado.Id}/situacao", UriKind.Relative),
            new { Ativo = true });

        reativacao.StatusCode.Should().Be(HttpStatusCode.OK);

        var reativado = await reativacao.Content.ReadFromJsonAsync<UsuarioDeSuporteDto>();
        reativado!.Ativo.Should().BeTrue();

        // Nada foi restaurado porque nada foi removido: a senha inicial continua valendo.
        var aceito = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login",
            new AutenticarCommand("efemero@grelhahub.teste", SenhaInicial));

        aceito.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RF-SEG-005 — usuário de plataforma novo troca a senha e opera; a troca não exige sessão de suporte (CA-18)")]
    public async Task CA18_TrocaDeSenhaDoUsuarioDePlataformaNaoEntraEmDeadlock()
    {
        using var suporte = await SuporteAsync();

        await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/usuarios", UriKind.Relative),
            Corpo("Suporte Recem Criado", "recem@grelhahub.teste"));

        // ⚠ É o caso que estava a um passo do deadlock: TrocaDeSenhaBehavior só libera
        // TrocarSenhaCommand, e SessaoDeSuporteBehavior exigiria sessão justamente dele — sessão
        // que não pode ser aberta porque a troca está pendente. Sem a isenção, este teste falha
        // com 403 e a conta nasce inutilizável.
        var token = await TrocarSenhaEReautenticarAsync("recem@grelhahub.teste");

        using var novo = Ambiente.CriarCliente(token);

        var lista = await novo.GetAsync(new Uri("/api/v1/plataforma/usuarios", UriKind.Relative));
        lista.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RN-GER-016 — administrador de restaurante não cria usuário de plataforma (CA-19)")]
    public async Task CA19_RestauranteNaoCriaUsuarioDePlataforma()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Sem Poder", "44444444000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/usuarios", UriKind.Relative),
            Corpo("Auto Promocao", "promovido@grelhahub.teste"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Troca a senha inicial e devolve um token <b>limpo</b> — <c>RF-SEG-005</c>.
    /// <para>
    /// A reautenticação não é detalhe de teste: o claim <c>trocar_senha</c> viaja no token, e
    /// trocar a senha não derruba a pendência do token que já está na mão. É o mesmo passo que a
    /// tela faz.
    /// </para>
    /// </summary>
    private async Task<string> TrocarSenhaEReautenticarAsync(string email)
    {
        const string SenhaNova = "senha-trocada-456";

        var tokenPendente = await AutenticarAsync(email, SenhaInicial);

        using var comPendencia = Ambiente.CriarCliente(tokenPendente);

        var troca = await comPendencia.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = SenhaInicial, NovaSenha = SenhaNova });

        troca.StatusCode.Should().Be(HttpStatusCode.NoContent);

        return await AutenticarAsync(email, SenhaNova);
    }
}
