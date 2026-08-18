using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.Plataforma;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>RN-GER-027</c>, <c>RN-GER-028</c> e <c>RN-GER-029</c> — administrar a loja e a pessoa.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class AdministracaoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string Senha = "senha-de-teste-1234";

    [Fact(DisplayName = "RN-GER-029 — inativar a loja é ato da plataforma, com justificativa")]
    public async Task Inativar_ExigeJustificativaEPlataforma()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // O administrador do restaurante não inativa a própria loja: seria autodesligar-se, e a
        // decisão é comercial (RN-GER-014).
        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}/situacao", UriKind.Relative),
            new { Ativa = false, Justificativa = "Quero desligar a minha loja" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var suporte = await SuporteAsync();

        // Sem justificativa de dez caracteres, nem a plataforma.
        (await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}/situacao", UriKind.Relative),
            new { Ativa = false, Justificativa = "curto" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "RN-GER-027 ⚠ — com a loja inativa, NINGUÉM dela autentica")]
    public async Task LojaInativa_NinguemAutentica()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        // Um segundo usuário, para provar que não é só o fundador que cai.
        var atendente = await CriarUsuarioAsync(restaurante, "Atendente", "atendente@loja.teste");

        // Antes: os dois entram. Prova que a credencial estava boa e é a INATIVAÇÃO que derruba.
        (await TentarLoginAsync(restaurante.Email, restaurante.Senha))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await TentarLoginAsync("atendente@loja.teste", Senha))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var suporte = await SuporteAsync();

        var inativada = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}/situacao", UriKind.Relative),
            new { Ativa = false, Justificativa = "Contrato encerrado em 17/08/2026" });

        inativada.EnsureSuccessStatusCode();
        (await inativada.Content.ReadFromJsonAsync<EstabelecimentoDto>())!.Ativa.Should().BeFalse();

        // Depois: nenhum dos dois entra.
        foreach (var (email, senha) in new[]
                 {
                     (restaurante.Email, restaurante.Senha),
                     ("atendente@loja.teste", Senha),
                 })
        {
            var recusado = await TentarLoginAsync(email, senha);

            recusado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var corpo = await recusado.Content.ReadAsStringAsync();

            // ⚠ A mensagem NÃO diz que a loja foi desativada. Dizer isso contaria a situação
            // comercial de um cliente a quem só tem o e-mail dele.
            corpo.Should().NotContainEquivalentOf("loja");
            corpo.Should().NotContainEquivalentOf("desativad");
            corpo.Should().NotContainEquivalentOf("inativ");

            // E carrega o código de correlação, que é o que o cliente dita ao suporte.
            Regex.IsMatch(corpo, "[0-9A-F]{8}").Should().BeTrue("RN-GER-027 — código de suporte");
        }

        _ = atendente;

        // RN-GER-002 / RN-GER-029 — nada foi apagado.
        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await db.Usuarios.AsNoTracking().CountAsync(u => u.UnidadeId == restaurante.UnidadeId))
            .Should().Be(2, "inativar a loja não remove usuário nenhum");
    }

    [Fact(DisplayName = "RN-GER-029 — reativar devolve o acesso, sem restaurar nada")]
    public async Task Reativar_DevolveOAcesso()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        using var suporte = await SuporteAsync();

        var rota = new Uri(
            $"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}/situacao", UriKind.Relative);

        (await suporte.PostAsJsonAsync(rota, new { Ativa = false, Justificativa = "Inadimplencia de agosto" }))
            .EnsureSuccessStatusCode();

        (await TentarLoginAsync(restaurante.Email, restaurante.Senha))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await suporte.PostAsJsonAsync(rota, new { Ativa = true, Justificativa = "Pagamento regularizado" }))
            .EnsureSuccessStatusCode();

        (await TentarLoginAsync(restaurante.Email, restaurante.Senha))
            .StatusCode.Should().Be(HttpStatusCode.OK, "nada foi removido, então nada precisa voltar");
    }

    [Fact(DisplayName = "RN-GER-029 — inativar duas vezes é recusado")]
    public async Task Inativar_DuasVezes_EhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        using var suporte = await SuporteAsync();

        var rota = new Uri(
            $"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}/situacao", UriKind.Relative);

        var corpo = new { Ativa = false, Justificativa = "Contrato encerrado em agosto" };

        (await suporte.PostAsJsonAsync(rota, corpo)).EnsureSuccessStatusCode();

        var repetido = await suporte.PostAsJsonAsync(rota, corpo);

        repetido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await repetido.Content.ReadAsStringAsync()).Should().Contain("RN-GER-029");
    }

    [Fact(DisplayName = "RN-GER-029 — alterar o estabelecimento não toca slug nem CNPJ")]
    public async Task Alterar_NaoTocaSlugNemCnpj()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        using var suporte = await SuporteAsync();

        // RN-GER-015 — alterar o cadastro de um cliente é SUPORTE, e exige sessão com motivo.
        // Inativar a loja não exige, porque é ato comercial e carrega a própria justificativa: a
        // linha divisória de `SessaoDeSuporteBehavior.OperacoesLiberadas` é comercial × suporte.
        await AbrirSessaoAsync(suporte, restaurante.UnidadeId);

        var alterado = await suporte.PutAsJsonAsync(
            new Uri($"/api/v1/plataforma/estabelecimentos/{restaurante.UnidadeId}", UriKind.Relative),
            new { RazaoSocial = "Loja Admin Alimentos LTDA", NomeFantasia = "Loja Renomeada", Telefone = "(11) 90000-0000" });

        alterado.EnsureSuccessStatusCode();

        var dto = (await alterado.Content.ReadFromJsonAsync<EstabelecimentoDto>())!;

        dto.NomeFantasia.Should().Be("Loja Renomeada");

        // RN-VEN-036 — o slug é imutável: ele circula em rede social e material impresso.
        dto.Slug.Should().Be(restaurante.Slug);
        dto.Cnpj.Should().Be("61111111000191");

        // RN-GER-013 — o telefone entrou com máscara e foi gravado só com dígitos.
        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await db.Unidades.AsNoTracking().SingleAsync(u => u.Id == restaurante.UnidadeId))
            .Telefone.Should().Be("11900000000");
    }

    [Fact(DisplayName = "RN-GER-028 — o gerente altera o cadastro de um usuário da sua unidade")]
    public async Task AlterarCadastro_DaMinhaUnidade()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        var criado = await CriarUsuarioAsync(restaurante, "Atendente", "antes@loja.teste");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var alterado = await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/usuarios/{criado.Id}", UriKind.Relative),
            new
            {
                Nome = "Nome",
                Sobrenome = "Corrigido",
                Cpf = "529.982.247-25",
                Email = "depois@loja.teste",
            });

        alterado.EnsureSuccessStatusCode();

        var dto = (await alterado.Content.ReadFromJsonAsync<UsuarioDto>())!;

        dto.Nome.Should().Be("Nome");
        dto.Sobrenome.Should().Be("Corrigido");
        dto.Email.Should().Be("depois@loja.teste");
        dto.Cpf.Should().Be("529.982.247-25");

        // RN-GER-013 — gravado sem máscara.
        await using var db = Contexto(restaurante);

        (await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == criado.Id))
            .Cpf!.Value.Valor.Should().Be("52998224725");
    }

    [Fact(DisplayName = "RN-GER-028 — salvar sem mudar o e-mail não acusa conflito consigo mesmo")]
    public async Task AlterarCadastro_MesmoEmail_NaoConflita()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        var criado = await CriarUsuarioAsync(restaurante, "Atendente", "mesmo@loja.teste");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Sem o `Id !=` na checagem de unicidade, isto acusaria "já existe um usuário com este
        // e-mail" — apontando para o próprio usuário sendo salvo.
        (await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/usuarios/{criado.Id}", UriKind.Relative),
            new
            {
                Nome = "Mesmo",
                Sobrenome = "Email",
                Cpf = criado.Cpf,
                Email = "mesmo@loja.teste",
            }))
            .EnsureSuccessStatusCode();
    }

    [Fact(DisplayName = "RN-GER-009 — não se altera usuário de outra unidade")]
    public async Task AlterarCadastro_DeOutraUnidade_Da404()
    {
        var alvo = await CadastrarRestauranteAsync("Alvo Admin", "61111111000191");
        var intruso = await CadastrarRestauranteAsync("Intruso Admin", "62222222000192");

        var doAlvo = await CriarUsuarioAsync(alvo, "Atendente", "doalvo@loja.teste");

        using var comoIntruso = Ambiente.CriarCliente(intruso.Token);

        var resposta = await comoIntruso.PutAsJsonAsync(
            new Uri($"/api/v1/usuarios/{doAlvo.Id}", UriKind.Relative),
            new { Nome = "Invadido", Sobrenome = "Por Outro", Cpf = "529.982.247-25", Email = "x@y.z" });

        // 404 e não 403: dizer "existe, mas não é seu" já conta que existe.
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "RN-GER-028 ⚠ — senha redefinida por outra pessoa REATIVA a troca obrigatória")]
    public async Task RedefinirSenha_ReativaATrocaObrigatoria()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        var criado = await CriarUsuarioAsync(restaurante, "Atendente", "senha@loja.teste");

        // A pessoa troca a senha dela: a pendência acaba.
        var inicial = await AutenticarAsync("senha@loja.teste", Senha);

        using (var dela = Ambiente.CriarCliente(inicial))
        {
            (await dela.PostAsJsonAsync(
                new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
                new { SenhaAtual = Senha, NovaSenha = Senha + "-dela" }))
                .EnsureSuccessStatusCode();
        }

        await using (var db = Contexto(restaurante))
        {
            (await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == criado.Id))
                .DeveTrocarSenha.Should().BeFalse("ela mesma escolheu a senha");
        }

        // Agora a gerência redefine. A pendência VOLTA.
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/usuarios/{criado.Id}/senha", UriKind.Relative),
            new { NovaSenha = "senha-redefinida-1234" }))
            .EnsureSuccessStatusCode();

        await using var conferencia = Contexto(restaurante);

        (await conferencia.Usuarios.AsNoTracking().SingleAsync(u => u.Id == criado.Id))
            .DeveTrocarSenha.Should().BeTrue(
                "a senha que um terceiro conhece não é senha — RF-SEG-005 volta a valer");

        // E a senha nova funciona: o gate de troca é do pipeline, não do login.
        (await TentarLoginAsync("senha@loja.teste", "senha-redefinida-1234"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RN-GER-028 — inativar e reativar usuário, sem apagar")]
    public async Task InativarEReativarUsuario()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        var criado = await CriarUsuarioAsync(restaurante, "Atendente", "vaivolta@loja.teste");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        (await gestao.PostAsync(
            new Uri($"/api/v1/usuarios/{criado.Id}/desativar", UriKind.Relative), content: null))
            .EnsureSuccessStatusCode();

        (await TentarLoginAsync("vaivolta@loja.teste", Senha))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await gestao.PostAsync(
            new Uri($"/api/v1/usuarios/{criado.Id}/reativar", UriKind.Relative), content: null))
            .EnsureSuccessStatusCode();

        (await TentarLoginAsync("vaivolta@loja.teste", Senha))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RF-SEG-005 — a pessoa sai do beco trocando a senha, e o token novo a libera")]
    public async Task PrimeiroAcesso_TrocaESaiDoBeco()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        var criado = await CriarUsuarioAsync(restaurante, "Atendente", "primeiro@loja.teste");

        // O login funciona e devolve a pendência + o E-MAIL, que é o que a tela usa para
        // reautenticar depois da troca.
        var entrada = await TentarLoginAsync("primeiro@loja.teste", Senha);

        entrada.StatusCode.Should().Be(HttpStatusCode.OK);

        using var documento = await entrada.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        var corpo = documento!.RootElement;

        corpo.GetProperty("deveTrocarSenha").GetBoolean().Should().BeTrue();
        corpo.GetProperty("email").GetString().Should().Be("primeiro@loja.teste",
            "sem o e-mail na sessão, a tela teria de pedir que o usuário o digitasse de novo");

        var tokenPendente = corpo.GetProperty("token").GetString()!;

        using var comPendencia = Ambiente.CriarCliente(tokenPendente);

        // ⚠ O BECO: com a pendência, TODAS as rotas são recusadas.
        var bloqueada = await comPendencia.GetAsync(new Uri("/api/v1/pedidos", UriKind.Relative));

        bloqueada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await bloqueada.Content.ReadAsStringAsync()).Should().Contain("RF-SEG-005");

        // A saída: a troca da própria senha é a ÚNICA operação isenta do gate.
        (await comPendencia.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = Senha, NovaSenha = "escolhida-por-mim-9876" }))
            .EnsureSuccessStatusCode();

        // ⚠ E o token ANTIGO continua bloqueando: o claim `trocar_senha` viaja nele. É por isso que
        // a tela reautentica em vez de só seguir para o painel.
        (await comPendencia.GetAsync(new Uri("/api/v1/pedidos", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "o token na mão não muda porque a senha mudou");

        // Reautenticando com a senha nova: token limpo, pendência encerrada, rotas liberadas.
        var novaEntrada = await TentarLoginAsync("primeiro@loja.teste", "escolhida-por-mim-9876");

        novaEntrada.StatusCode.Should().Be(HttpStatusCode.OK);

        using var novoDocumento = await novaEntrada.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();

        novoDocumento!.RootElement.GetProperty("deveTrocarSenha").GetBoolean().Should().BeFalse();

        using var liberado = Ambiente.CriarCliente(
            novoDocumento.RootElement.GetProperty("token").GetString()!);

        (await liberado.GetAsync(new Uri("/api/v1/pedidos", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // E a senha antiga não entra mais.
        (await TentarLoginAsync("primeiro@loja.teste", Senha))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        _ = criado;
    }

    [Fact(DisplayName = "RF-SEG-005 — a troca exige a senha atual correta")]
    public async Task Troca_ExigeASenhaAtual()
    {
        var restaurante = await CadastrarRestauranteAsync("Loja Admin", "61111111000191");

        await CriarUsuarioAsync(restaurante, "Atendente", "confere@loja.teste");

        var token = await AutenticarAsync("confere@loja.teste", Senha);

        using var cliente = Ambiente.CriarCliente(token);

        // Sem isto, um token roubado trocaria a senha e tomaria a conta sem conhecer a atual.
        var comSenhaErrada = await cliente.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = "nao-e-a-minha-senha", NovaSenha = "tentativa-de-tomar-1234" });

        comSenhaErrada.IsSuccessStatusCode.Should().BeFalse();

        // A senha original continua valendo.
        (await TentarLoginAsync("confere@loja.teste", Senha))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── auxiliares ──────────────────────────────────────────────────────────────────

    private static async Task AbrirSessaoAsync(HttpClient suporte, Guid unidadeId) =>
        (await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/sessoes-de-suporte", UriKind.Relative),
            new { UnidadeId = unidadeId, Motivo = "Cliente pediu correcao do cadastro por telefone" }))
            .EnsureSuccessStatusCode();

    /// <summary>Tenta o login e devolve a resposta crua — o teste quer o status e o corpo.</summary>
    private async Task<HttpResponseMessage> TentarLoginAsync(string email, string senha)
    {
        using var anonimo = Ambiente.CriarCliente();

        return await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/login", UriKind.Relative),
            new { Email = email, Senha = senha });
    }

    private async Task<HttpClient> SuporteAsync()
    {
        var token = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        return Ambiente.CriarCliente(token);
    }

    private async Task<UsuarioDto> CriarUsuarioAsync(
        RestauranteDeTeste restaurante, string perfil, string email)
    {
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        Guid perfilId;
        await using (var db = Contexto(restaurante))
        {
            perfilId = await db.Perfis.Where(p => p.Nome == perfil).Select(p => p.Id).SingleAsync();
        }

        var resposta = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new CriarUsuarioCommand(
                $"Usuario {perfil}",
                "De Teste",
                CpfDeTeste.Gerar(email.GetHashCode(StringComparison.Ordinal)),
                email,
                perfilId,
                Senha));

        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<UsuarioDto>())!;
    }
}
