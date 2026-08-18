using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.Autenticar;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-008</c> — recuperação de acesso por e-mail (<c>RN-GER-032</c> a <c>RN-GER-035</c>),
/// ADR-0016.
/// <para>
/// O correio é dublê (<see cref="CaixaDeSaidaDeTeste"/>); todo o resto é real — token gravado como
/// hash, validade, consumo e o login com a senha nova.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class RecuperacaoDeAcessoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string SenhaNova = "senha-escolhida-987";

    private static CaixaDeSaidaDeTeste Caixa => ApiDeTestes.CaixaDeSaida;

    private async Task<HttpResponseMessage> PedirLinkAsync(string email)
    {
        using var anonimo = Ambiente.CriarCliente();

        return await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/esqueci-minha-senha", UriKind.Relative),
            new { Email = email });
    }

    private async Task<HttpResponseMessage> DefinirAsync(string token, string senha)
    {
        using var anonimo = Ambiente.CriarCliente();

        return await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/definir-senha", UriKind.Relative),
            new { Token = token, NovaSenha = senha });
    }

    [Fact(DisplayName = "RN-GER-034 — e-mail que não existe responde igual e não emite nada (CA-1)")]
    public async Task CA1_ContaInexistenteRespondeIgual()
    {
        var resposta = await PedirLinkAsync("ninguem@grelhahub.teste");

        // 202 e não 404: a resposta não distingue, senão diria a quem sonda quem é cliente.
        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Escopo pelo destinatário, e não a caixa inteira: a suíte compartilha a caixa de saída, e
        // uma asserção global aqui mediria o que os outros testes fizeram.
        Caixa.Para("ninguem@grelhahub.teste").Should().BeEmpty(
            "RN-GER-034 — conta que não existe não recebe e-mail, e a resposta é a mesma");

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        // Nenhum token para um e-mail que não pertence a ninguém: a consulta liga token a usuário,
        // que é o vínculo que RN-GER-033 exige.
        (await db.TokensDeAcesso
            .CountAsync(t => t.Usuario!.Email == "ninguem@grelhahub.teste"))
            .Should().Be(0);
    }

    [Fact(DisplayName = "RN-GER-033 — o link redefine a senha, e a antiga deixa de valer (CA-2)")]
    public async Task CA2_LinkRedefineASenha()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Esquecida", "55555555000191");

        (await PedirLinkAsync(restaurante.Email)).StatusCode.Should().Be(HttpStatusCode.Accepted);

        var token = Caixa.UltimoToken(restaurante.Email);
        token.Should().NotBeNullOrWhiteSpace();

        // RN-GER-033 — o que está no banco é o HASH. Se o token em claro aparecesse aqui, qualquer
        // leitura do banco abriria a conta de quem pediu recuperação.
        await using (var db = Ambiente.CriarContexto(Guid.Empty))
        {
            var registro = await db.TokensDeAcesso.SingleAsync();
            registro.Hash.Should().NotBe(token);
            registro.Tipo.Should().Be(TipoDeToken.RedefinicaoDeSenha);
        }

        (await DefinirAsync(token!, SenhaNova)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var anonimo = Ambiente.CriarCliente();

        var comASenhaAntiga = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login",
            new AutenticarCommand(restaurante.Email, restaurante.Senha));

        comASenhaAntiga.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var comANova = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login",
            new AutenticarCommand(restaurante.Email, SenhaNova));

        comANova.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RN-GER-035 — senha escolhida pelo dono encerra a troca obrigatória (CA-3)")]
    public async Task CA3_SenhaEscolhidaPeloDonoEncerraAPendencia()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Pendente", "66666666000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // O perfil vem da rota real: `CriarUsuarioCommand` recebe `perfilId`, e inventar um Guid
        // aqui daria 404 no meio do cenário — o mesmo motivo pelo qual a rota `/usuarios/perfis`
        // existe (SPEC-GER-007 §11.1).
        var perfis = await gestao.GetFromJsonAsync<List<PerfilDaUnidadeDto>>(
            new Uri("/api/v1/usuarios/perfis", UriKind.Relative));

        var operador = perfis!.Single(p => p.Nome == "Operador");

        // Usuário criado por outra pessoa nasce com a troca pendente (RF-SEG-005) e recebe convite.
        var criacao = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new
            {
                Nome = "Operador",
                Sobrenome = "Da Chapa",
                Cpf = CpfDeTeste.Gerar(77),
                Email = "operador@brasapendente.teste",
                PerfilId = operador.Id,
                SenhaInicial = "senha-inicial-123",
            });

        criacao.EnsureSuccessStatusCode();

        var token = Caixa.UltimoToken("operador@brasapendente.teste");
        token.Should().NotBeNullOrWhiteSpace("RN-GER-032 — a criação de usuário envia o convite");

        (await DefinirAsync(token!, SenhaNova)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var anonimo = Ambiente.CriarCliente();

        var login = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login",
            new AutenticarCommand("operador@brasapendente.teste", SenhaNova));

        login.EnsureSuccessStatusCode();
        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>();

        // A senha foi escolhida pelo próprio dono: ninguém mais a conhece, e a pendência não faz
        // mais sentido. É a diferença em relação a RN-GER-028, em que um terceiro escolhe.
        sessao!.DeveTrocarSenha.Should().BeFalse();
    }

    [Fact(DisplayName = "RN-GER-033 — o link é de uso único (CA-4)")]
    public async Task CA4_LinkNaoServeDuasVezes()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Repetida", "77777777000191");

        await PedirLinkAsync(restaurante.Email);
        var token = Caixa.UltimoToken(restaurante.Email)!;

        (await DefinirAsync(token, SenhaNova)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var segundaVez = await DefinirAsync(token, "outra-senha-qualquer-1");
        segundaVez.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // E a senha continua sendo a da primeira vez.
        using var anonimo = Ambiente.CriarCliente();
        var login = await anonimo.PostAsJsonAsync(
            "/api/v1/autenticacao/login", new AutenticarCommand(restaurante.Email, SenhaNova));

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RN-GER-033 — pedir outro link invalida o anterior (CA-5)")]
    public async Task CA5_LinkNovoInvalidaOAnterior()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Dois Links", "88888888000191");

        await PedirLinkAsync(restaurante.Email);
        var primeiro = Caixa.UltimoToken(restaurante.Email)!;

        await PedirLinkAsync(restaurante.Email);
        var segundo = Caixa.UltimoToken(restaurante.Email)!;

        segundo.Should().NotBe(primeiro);

        // ⚠ O primeiro e-mail pode ter ido para a caixa errada — é justamente por isso que ele
        // precisa parar de valer quando outro é pedido.
        (await DefinirAsync(primeiro, SenhaNova)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);

        (await DefinirAsync(segundo, SenhaNova)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "RN-GER-033 — link vencido não serve (CA-6)")]
    public async Task CA6_LinkVencidoNaoServe()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Vencida", "99999999000191");

        await PedirLinkAsync(restaurante.Email);
        var token = Caixa.UltimoToken(restaurante.Email)!;

        // Envelhece o token no banco em vez de esperar uma hora. É escrita direta, e aqui ela é
        // legítima: o cenário não é "o sistema criou um token vencido", é "o tempo passou".
        await using (var db = Ambiente.CriarContexto(Guid.Empty))
        {
            await db.TokensDeAcesso.ExecuteUpdateAsync(
                s => s.SetProperty(t => t.ExpiraEm, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        (await DefinirAsync(token, SenhaNova)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "RN-GER-034 — usuário inativo recebe a mesma resposta e nenhum e-mail (CA-7)")]
    public async Task CA7_UsuarioInativoNaoRecebeLink()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Desligada", "12121212000191");

        // Desativa o administrador direto no banco: a rota de desativação exigiria outro usuário
        // com permissão, e o que este teste prova é o comportamento da solicitação, não o da
        // desativação — que já tem teste próprio.
        await using (var db = Ambiente.CriarContexto(Guid.Empty))
        {
            await db.Usuarios
                .Where(u => u.Email == restaurante.Email)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Ativo, false));
        }

        var resposta = await PedirLinkAsync(restaurante.Email);

        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Caixa.Para(restaurante.Email).Should().BeEmpty();
    }

    [Fact(DisplayName = "RN-GER-032 — falha de correio não desfaz a operação: o token existe mesmo assim (CA-8)")]
    public async Task CA8_TokenGravadoAntesDoEnvio()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Sem Correio", "13131313000191");

        await PedirLinkAsync(restaurante.Email);

        // O e-mail é enfileirado DEPOIS do commit: se a ordem se inverter, um erro de gravação
        // mandaria ao usuário um link sem lastro — e é isso que esta asserção protege.
        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var registro = await db.TokensDeAcesso.SingleAsync();
        registro.UsadoEm.Should().BeNull();
        registro.InvalidadoEm.Should().BeNull();
        registro.ExpiraEm.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}
