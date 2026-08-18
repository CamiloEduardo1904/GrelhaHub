using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.Autenticar;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Integration.Tests.Infraestrutura;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>RF-SEG-005</c> — a sequência inteira: criar usuário, entrar com a senha inicial, trocar, e
/// entrar com a nova. É o caminho que o usuário real percorre no primeiro acesso.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class TrocaDeSenhaAposCriacaoTests(AmbienteDeIntegracao ambiente)
    : TesteDeIntegracao(ambiente)
{
    private const string SenhaInicial = "senha-inicial-123";
    private const string SenhaNova = "senha-escolhida-456";

    [Fact(DisplayName = "RF-SEG-005 — criado, entra com a inicial, troca e entra com a nova")]
    public async Task SequenciaCompleta()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Primeiro Acesso", "31313131000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var perfis = await gestao.GetFromJsonAsync<List<PerfilDaUnidadeDto>>(
            new Uri("/api/v1/usuarios/perfis", UriKind.Relative));

        var operador = perfis!.Single(p => p.Nome == "Operador");

        var email = "primeiro.acesso@brasa.teste";

        var criacao = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new
            {
                Nome = "Primeiro",
                Sobrenome = "Acesso",
                Cpf = CpfDeTeste.Gerar(91),
                Email = email,
                PerfilId = operador.Id,
                SenhaInicial,
            });

        criacao.StatusCode.Should().Be(
            HttpStatusCode.OK, await criacao.Content.ReadAsStringAsync());

        // 1) Login com a senha inicial: a pendência de RF-SEG-005 vem marcada.
        using var anonimo = Ambiente.CriarCliente();

        var primeiroLogin = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login", new AutenticarCommand(email, SenhaInicial));

        primeiroLogin.StatusCode.Should().Be(
            HttpStatusCode.OK, await primeiroLogin.Content.ReadAsStringAsync());

        var sessao = await primeiroLogin.Content.ReadFromJsonAsync<AutenticarResponse>();
        sessao!.DeveTrocarSenha.Should().BeTrue();
        sessao.Email.Should().Be(email);

        // 2) A troca, com o token que carrega o claim `trocar_senha`.
        using var comPendencia = Ambiente.CriarCliente(sessao.Token);

        var troca = await comPendencia.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = SenhaInicial, NovaSenha = SenhaNova });

        troca.StatusCode.Should().Be(
            HttpStatusCode.NoContent, await troca.Content.ReadAsStringAsync());

        // 3) A reautenticação que a tela faz em seguida — é aqui que o token limpo aparece.
        var segundoLogin = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login", new AutenticarCommand(email, SenhaNova));

        segundoLogin.StatusCode.Should().Be(
            HttpStatusCode.OK, await segundoLogin.Content.ReadAsStringAsync());

        var limpa = await segundoLogin.Content.ReadFromJsonAsync<AutenticarResponse>();
        limpa!.DeveTrocarSenha.Should().BeFalse();

        // 4) E o token limpo opera de verdade.
        using var operando = Ambiente.CriarCliente(limpa.Token);

        var cardapio = await operando.GetAsync(new Uri("/api/v1/cardapio", UriKind.Relative));
        cardapio.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RF-SEG-005 — usuário criado PELA PLATAFORMA troca a senha e opera")]
    public async Task CriadoPelaPlataforma_TrocaESegue()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Pela Plataforma", "32323232000191");

        var tokenSuporte = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        using var suporte = Ambiente.CriarCliente(tokenSuporte);

        var email = "pela.plataforma@brasa.teste";

        var criacao = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}/usuarios",
                UriKind.Relative),
            new
            {
                Nome = "Criado",
                Sobrenome = "Pelo Suporte",
                Cpf = CpfDeTeste.Gerar(92),
                Email = email,
                PerfilNome = "Gerente",
                SenhaInicial,
            });

        criacao.StatusCode.Should().Be(
            HttpStatusCode.OK, await criacao.Content.ReadAsStringAsync());

        var token = await TrocarComOTokenPendenteAsync(email, SenhaInicial, SenhaNova);

        using var operando = Ambiente.CriarCliente(token);
        (await operando.GetAsync(new Uri("/api/v1/cardapio", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RN-GER-028 / RF-SEG-005 — senha redefinida na edição reativa a troca, e a troca funciona")]
    public async Task SenhaRedefinidaNaEdicao_ReativaEFunciona()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Redefinida", "33333333000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var perfis = await gestao.GetFromJsonAsync<List<PerfilDaUnidadeDto>>(
            new Uri("/api/v1/usuarios/perfis", UriKind.Relative));

        var email = "redefinido@brasa.teste";

        var criado = await (await gestao.PostAsJsonAsync(
                new Uri("/api/v1/usuarios", UriKind.Relative),
                new
                {
                    Nome = "Vai",
                    Sobrenome = "Trocar",
                    Cpf = CpfDeTeste.Gerar(93),
                    Email = email,
                    PerfilId = perfis!.Single(p => p.Nome == "Operador").Id,
                    SenhaInicial,
                }))
            .Content.ReadFromJsonAsync<UsuarioDto>();

        // Ele já troca uma vez: a pendência cai.
        await TrocarComOTokenPendenteAsync(email, SenhaInicial, SenhaNova);

        // Agora o administrador redefine — RN-GER-028: senha escolhida por terceiro REATIVA a
        // troca obrigatória. É o fluxo de "editei o usuário e substituí a senha".
        const string SenhaDoAdmin = "senha-do-admin-789";

        var redefinicao = await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/usuarios/{criado!.Id}/senha", UriKind.Relative),
            new { NovaSenha = SenhaDoAdmin });

        redefinicao.StatusCode.Should().Be(
            HttpStatusCode.NoContent, await redefinicao.Content.ReadAsStringAsync());

        using var anonimo = Ambiente.CriarCliente();

        var login = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login", new AutenticarCommand(email, SenhaDoAdmin));

        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>();

        // ⚠ A pendência volta: a senha que um terceiro conhece não é senha.
        sessao!.DeveTrocarSenha.Should().BeTrue();

        // E a segunda troca funciona igual à primeira.
        var tokenLimpo = await TrocarComOTokenPendenteAsync(email, SenhaDoAdmin, "senha-final-321");

        using var operando = Ambiente.CriarCliente(tokenLimpo);
        (await operando.GetAsync(new Uri("/api/v1/cardapio", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// O que a tela faz: troca com o token que carrega a pendência e **reautentica** para receber um
    /// token limpo — o claim `trocar_senha` viaja no token e não cai com a troca.
    /// </summary>
    private async Task<string> TrocarComOTokenPendenteAsync(
        string email, string senhaAtual, string senhaNova)
    {
        var pendente = await AutenticarAsync(email, senhaAtual);

        using var comPendencia = Ambiente.CriarCliente(pendente);

        var troca = await comPendencia.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = senhaAtual, NovaSenha = senhaNova });

        troca.StatusCode.Should().Be(
            HttpStatusCode.NoContent, await troca.Content.ReadAsStringAsync());

        return await AutenticarAsync(email, senhaNova);
    }

    [Fact(DisplayName = "RF-SEG-005 — senha atual errada devolve 422 com a mensagem, nunca 401")]
    public async Task SenhaAtualErrada_Devolve422()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Senha Errada", "34343434000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var troca = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = "nao-e-a-senha-atual", NovaSenha = "outra-senha-qualquer" });

        // ⚠ 401 aqui fazia o interceptor do cliente limpar a sessão e navegar para /entrar: a
        // mensagem nunca chegava à tela, e a pessoa voltava achando que a troca havia falhado.
        troca.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var corpo = await troca.Content.ReadAsStringAsync();
        corpo.Should().Contain("Senha atual inválida");

        // E a senha antiga continua valendo — nada foi alterado.
        using var anonimo = Ambiente.CriarCliente();

        (await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login",
            new AutenticarCommand(restaurante.Email, restaurante.Senha)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RN-GER-032 / RN-GER-033 — criado SEM senha recebe convite e define a própria")]
    public async Task CriadoSemSenha_DefinePeloConvite()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Sem Senha", "35353535000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var perfis = await gestao.GetFromJsonAsync<List<PerfilDaUnidadeDto>>(
            new Uri("/api/v1/usuarios/perfis", UriKind.Relative));

        var email = "convidado@brasa.teste";

        // SEM `senhaInicial`: é o caminho normal a partir de 18/08/2026.
        var criacao = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new
            {
                Nome = "Convidado",
                Sobrenome = "Da Casa",
                Cpf = CpfDeTeste.Gerar(94),
                Email = email,
                PerfilId = perfis!.Single(p => p.Nome == "Operador").Id,
            });

        criacao.StatusCode.Should().Be(
            HttpStatusCode.OK, await criacao.Content.ReadAsStringAsync());

        // O convite saiu.
        var token = ApiDeTestes.CaixaDeSaida.UltimoToken(email);
        token.Should().NotBeNullOrWhiteSpace("criar usuário dispara o convite (RN-GER-032)");

        // A credencial aleatória não é adivinhável: nada de senha padrão nem senha em branco.
        using var anonimo = Ambiente.CriarCliente();

        foreach (var tentativa in new[] { "", "12345678", "senha", email })
        {
            (await anonimo.PostAsJsonAsync(
                "/api/v1/autenticacao/login", new AutenticarCommand(email, tentativa)))
                .StatusCode.Should().NotBe(HttpStatusCode.OK);
        }

        // Ele define a própria pelo link, e entra com ela — sem pendência (RN-GER-035).
        (await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/definir-senha", UriKind.Relative),
            new { Token = token, NovaSenha = SenhaNova }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login", new AutenticarCommand(email, SenhaNova));

        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>();
        sessao!.DeveTrocarSenha.Should().BeFalse();
    }

    /// <summary>
    /// <c>RN-GER-032</c> — o mesmo convite, pela rota da PLATAFORMA.
    /// <para>
    /// ⚠ Este teste existe por causa de um defeito real: o corpo da rota declarava
    /// <c>string SenhaInicial</c> — sem <c>?</c> —, e tipo de referência não-anulável em corpo de
    /// requisição ganha um <c>[Required]</c> implícito do model binding. O comando aceitava a
    /// ausência e mandava o convite; a requisição morria em <c>400</c> antes de chegar nele.
    /// </para>
    /// <para>
    /// <b>O que deixou o defeito passar:</b> o único teste desta rota mandava <c>SenhaInicial</c>.
    /// O caminho da tela — sem senha — nunca foi exercido AQUI, só na rota de unidade, que liga no
    /// comando direto e por isso não tinha o problema. Rota que reembrulha o comando num corpo
    /// próprio precisa do seu próprio teste: o contrato dela não é o do comando.
    /// </para>
    /// </summary>
    [Fact(DisplayName = "RN-GER-032 — criado pela PLATAFORMA sem senha inicial recebe o convite")]
    public async Task CriadoPelaPlataformaSemSenha_RecebeConvite()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Convite Plataforma", "36363636000191");

        var tokenSuporte = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        using var suporte = Ambiente.CriarCliente(tokenSuporte);

        var email = "convidado.plataforma@brasa.teste";

        // SEM `senhaInicial` — o corpo que a tela manda.
        var criacao = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}/usuarios",
                UriKind.Relative),
            new
            {
                Nome = "Convidado",
                Sobrenome = "Pela Plataforma",
                Cpf = CpfDeTeste.Gerar(95),
                Email = email,
                PerfilNome = "Gerente",
            });

        criacao.StatusCode.Should().Be(
            HttpStatusCode.OK, await criacao.Content.ReadAsStringAsync());

        var token = ApiDeTestes.CaixaDeSaida.UltimoToken(email);
        token.Should().NotBeNullOrWhiteSpace("criar usuário dispara o convite (RN-GER-032)");

        using var anonimo = Ambiente.CriarCliente();

        (await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/definir-senha", UriKind.Relative),
            new { Token = token, NovaSenha = SenhaNova }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login", new AutenticarCommand(email, SenhaNova));

        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>();
        sessao!.DeveTrocarSenha.Should().BeFalse("a senha escolhida pelo dono encerra a pendência (RN-GER-035)");
    }
}
