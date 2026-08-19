using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-007</c> — CPF, sobrenome e a matriz de delegação. CA-1 a CA-12.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class DelegacaoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string Senha = "senha-de-teste-1234";

    [Fact(DisplayName = "RN-GER-023 — CPF com dígito verificador errado é recusado no cadastro (CA-1)")]
    public async Task CA1_CpfInvalido_EhRecusado()
    {
        using var suporte = await SuporteAsync();

        var resposta = await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            CorpoDeCadastro("Invalido Burger", "22222222000191", "123.456.789-00"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await db.Unidades.AsNoTracking().CountAsync(u => u.Cnpj == "22222222000191"))
            .Should().Be(0, "nenhuma unidade nasce de um cadastro recusado");
    }

    [Fact(DisplayName = "RN-GER-023 — sequência repetida é recusada ainda que o cálculo feche (CA-2)")]
    public async Task CA2_SequenciaRepetida_EhRecusada()
    {
        using var suporte = await SuporteAsync();

        var resposta = await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            CorpoDeCadastro("Repetido Burger", "33333333000191", "111.111.111-11"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "RN-GER-013 — o CPF é gravado sem máscara, com 11 dígitos (CA-3)")]
    public async Task CA3_CpfEhGravadoSemMascara()
    {
        using var suporte = await SuporteAsync();

        var resposta = await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            CorpoDeCadastro("Mascara Burger", "44444444000191", "529.982.247-25"));

        resposta.EnsureSuccessStatusCode();

        var criado = (await resposta.Content.ReadFromJsonAsync<CadastrarRestauranteResponse>())!;

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var admin = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == criado.UsuarioId);

        admin.Cpf.Should().NotBeNull();
        admin.Cpf!.Value.Valor.Should().Be("52998224725");
        admin.Sobrenome.Should().Be("Da Brasa");
        admin.NomeCompleto.Should().Be("Dona do Restaurante Da Brasa");
    }

    [Fact(DisplayName = "RN-GER-023 — o mesmo CPF não entra em duas unidades (CA-4)")]
    public async Task CA4_CpfEhUnicoNoSistemaInteiro()
    {
        const string cpf = "529.982.247-25";

        using var suporte = await SuporteAsync();

        (await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            CorpoDeCadastro("Primeiro Burger", "55555555000191", cpf)))
            .EnsureSuccessStatusCode();

        // Outra unidade, outro CNPJ, outro e-mail — mesmo CPF.
        var segundo = await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            CorpoDeCadastro("Segundo Burger", "66666666000191", cpf));

        // 422 e não 409: `RegraDeNegocioException` é a convenção do projeto para regra violada,
        // e o CNPJ duplicado já respondia assim. A §3 da spec dizia 409 e foi corrigida — mudar o
        // mapeamento só por causa desta regra tornaria a API incoerente consigo mesma.
        segundo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await segundo.Content.ReadAsStringAsync()).Should().Contain("CPF");
    }

    [Fact(DisplayName = "SPEC-GER-007 — não existe rota anônima de cadastro de restaurante")]
    public async Task CadastroAnonimo_NaoExisteMais()
    {
        using var anonimo = Ambiente.CriarCliente();

        // Decisão de produto, 17/08/2026: ninguém ganha uma conta sem contrato assinado. A rota
        // anônima criava unidade, perfis, parâmetros e um administrador — um cliente inteiro — sem
        // nada assinado do outro lado.
        //
        // Este teste existe porque "removi o botão da tela" e "fechei a porta" são coisas
        // diferentes: a rota continuaria aceitando `POST` de quem a conhecesse.
        var resposta = await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/cadastrar-restaurante", UriKind.Relative),
            CorpoDeCadastro("Pela Porta Fechada", "99999999000191", "529.982.247-25"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a rota não existe — e 404 é o certo: 401 sugeriria que basta autenticar");

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await db.Unidades.AsNoTracking().CountAsync(u => u.Cnpj == "99999999000191"))
            .Should().Be(0);
    }

    [Fact(DisplayName = "RN-GER-014 — cadastrar estabelecimento é privativo da plataforma")]
    public async Task CadastrarEstabelecimento_NaoEhDoRestaurante()
    {
        var restaurante = await CadastrarRestauranteAsync("Curioso Burger", "88888888000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Fechada a porta anônima, a única que existe é a de plataforma — e um administrador de
        // restaurante que pudesse usá-la criaria clientes para o GrelhaHub.
        var resposta = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            CorpoDeCadastro("Criado Por Cliente", "77777777000191", "111.444.777-35"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "RN-GER-025 — o gerente cria Operador na própria unidade (CA-5)")]
    public async Task CA5_GerenteCriaOperador()
    {
        var restaurante = await CadastrarRestauranteAsync("Delegacao Burger", "11111111000191");

        var tokenDoGerente = await CriarEAutenticarAsync(restaurante, "Gerente", "gerente@deleg.teste");

        using var comoGerente = Ambiente.CriarCliente(tokenDoGerente);

        var criado = await CriarUsuarioAsync(
            comoGerente, restaurante, "Operador", "operador@deleg.teste");

        criado.Perfil.Should().Be("Operador");
        criado.DeveTrocarSenha.Should().BeTrue(
            "RF-SEG-005 — senha escolhida por outra pessoa não é senha");

        await using var db = Contexto(restaurante);

        var operador = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == criado.Id);

        operador.UnidadeId.Should().Be(restaurante.UnidadeId,
            "RN-GER-009 — a unidade vem de quem está autenticado, não do corpo do pedido");
    }

    [Fact(DisplayName = "RN-GER-024 — o gerente NÃO cria Administrador: seria escalação (CA-6)")]
    public async Task CA6_GerenteNaoCriaAdministrador()
    {
        var restaurante = await CadastrarRestauranteAsync("Delegacao Burger", "11111111000191");

        var tokenDoGerente = await CriarEAutenticarAsync(restaurante, "Gerente", "gerente@deleg.teste");

        using var comoGerente = Ambiente.CriarCliente(tokenDoGerente);

        var resposta = await TentarCriarAsync(
            comoGerente, restaurante, "Administrador", "novoadmin@deleg.teste");

        // 403 e não 422: o gerente TEM `usuario.gerenciar`. O que falta é delegação do perfil, e
        // as duas coisas levam a ações diferentes.
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("RN-GER-024");
        corpo.Should().Contain("Administrador");

        await using var db = Contexto(restaurante);

        (await db.Usuarios.AsNoTracking().CountAsync(u => u.Email == "novoadmin@deleg.teste"))
            .Should().Be(0, "nenhum usuário é criado quando a delegação recusa");
    }

    [Fact(DisplayName = "RN-GER-024 — o gerente não cria outro Gerente (CA-7)")]
    public async Task CA7_GerenteNaoCriaGerente()
    {
        var restaurante = await CadastrarRestauranteAsync("Delegacao Burger", "11111111000191");

        var tokenDoGerente = await CriarEAutenticarAsync(restaurante, "Gerente", "gerente@deleg.teste");

        using var comoGerente = Ambiente.CriarCliente(tokenDoGerente);

        var resposta = await TentarCriarAsync(
            comoGerente, restaurante, "Gerente", "outrogerente@deleg.teste");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a matriz não inclui Gerente na delegação do Gerente — multiplicar quem altera " +
            "parâmetro e vê custo é decisão do administrador");
    }

    [Fact(DisplayName = "RN-GER-024 — a matriz cobre todo perfil do catálogo, ou a aplicação não sobe (CA-8)")]
    public void CA8_MatrizCobreTodoPerfilDoCatalogo()
    {
        // Este é o teste que sustenta a decisão de LISTA FIXA. O time aceitou o custo de manter a
        // matriz à mão; a validação na partida é o que impede que esquecê-la seja silencioso.
        var validar = ValidadorDeDelegacao.Validar;

        validar.Should().NotThrow(
            "todo perfil de CatalogoDeAcesso.Perfis precisa declarar o que delega — inclusive nada");
    }

    [Fact(DisplayName = "RN-GER-009 — o gerente só lista usuários da própria unidade (CA-9)")]
    public async Task CA9_GerenteNaoAtravessaUnidade()
    {
        var primeiro = await CadastrarRestauranteAsync("Primeiro Burger", "11111111000191");
        var segundo = await CadastrarRestauranteAsync("Segundo Burger", "77777777000191");

        var tokenDoGerente = await CriarEAutenticarAsync(primeiro, "Gerente", "gerente@primeiro.teste");

        using var comoGerente = Ambiente.CriarCliente(tokenDoGerente);

        var listados = await comoGerente.GetFromJsonAsync<List<UsuarioDto>>(
            new Uri("/api/v1/usuarios", UriKind.Relative));

        listados.Should().NotBeNull();
        listados!.Should().NotBeEmpty();

        // Prova pelo dado do OUTRO restaurante, não por contagem: contar provaria apenas que o
        // número bateu.
        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var emailsDoSegundo = await db.Usuarios.AsNoTracking()
            .Where(u => u.UnidadeId == segundo.UnidadeId)
            .Select(u => u.Email)
            .ToListAsync();

        emailsDoSegundo.Should().NotBeEmpty("o segundo restaurante tem o seu administrador");

        listados!.Select(u => u.Email).Should().NotIntersectWith(emailsDoSegundo);
    }

    [Fact(DisplayName = "RN-GER-017 — administrador de restaurante não altera módulo (CA-10)")]
    public async Task CA10_SomentePlataformaAlteraModulo()
    {
        var restaurante = await CadastrarRestauranteAsync("Modulo Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // A rota é a de `SPEC-GER-006`, e não uma segunda que eu havia criado por descuido:
        // duas rotas para o mesmo comando é a mesma armadilha de duas implementações da regra.
        var resposta = await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/unidades/{restaurante.UnidadeId}/modulos/PRO/habilitar",
                UriKind.Relative),
            new { Justificativa = "Quero habilitar sozinho" });

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "habilitar módulo é ato comercial: um administrador que pudesse fazê-lo se " +
            "autocontrataria (RN-GER-014)");
    }

    [Fact(DisplayName = "RN-GER-024 — a plataforma não cria usuário pela rota de unidade")]
    public async Task PlataformaNaoUsaARotaDeUnidade()
    {
        await CadastrarRestauranteAsync("Plataforma Burger", "11111111000191");

        var token = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        using var comoPlataforma = Ambiente.CriarCliente(token);

        var resposta = await comoPlataforma.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new
            {
                Nome = "Pela rota errada",
                Sobrenome = "Da Plataforma",
                Cpf = CpfDeTeste.Gerar(918_273),
                Email = "errado@plataforma.teste",
                PerfilId = Guid.CreateVersion7(),
                SenhaInicial = Senha,
            });

        // ⚠ ERA 403, vindo de `RN-GER-015`: o gate da sessão de suporte recusava no pipeline e a
        // mensagem de "use a rota de plataforma" nunca era alcançada. Com a sessão removida
        // (ADR-0020), a recusa REAL aparece — 422 com o motivo de verdade.
        //
        // E o gate a mais escondia um defeito: esta recusa não carregava o ID da regra. Um erro
        // mascarado por outro erro é o pior tipo de cobertura, porque o teste ficava verde.
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            await resposta.Content.ReadAsStringAsync());

        var corpo = await resposta.Content.ReadAsStringAsync();

        corpo.Should().Contain("rota de plataforma", "a mensagem diz o que fazer, não só que não pode");
        corpo.Should().Contain("RN-GER-016", "o ID da regra liga o erro da tela ao catálogo");
    }

    // ── auxiliares ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// O cliente autenticado como plataforma.
    /// <para>
    /// A porta anônima de cadastro foi fechada em 17/08/2026 — <b>ninguém ganha conta sem contrato
    /// assinado</b> —, então até o teste de CPF inválido passa pela rota de plataforma. É a mesma
    /// doutrina do harness: cenário montado pelas rotas que o sistema tem.
    /// </para>
    /// </summary>
    private async Task<HttpClient> SuporteAsync()
    {
        var token = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        return Ambiente.CriarCliente(token);
    }

    private static object CorpoDeCadastro(string nomeFantasia, string cnpj, string cpf) => new
    {
        RazaoSocial = $"{nomeFantasia} LTDA",
        NomeFantasia = nomeFantasia,
        Cnpj = cnpj,
        Telefone = (string?)null,
        NomeAdministrador = "Dona do Restaurante",
        SobrenomeAdministrador = "Da Brasa",
        CpfAdministrador = cpf,
        Email = $"admin.{cnpj}@delegacao.teste",
        Senha = Senha,
    };

    private async Task<string> CriarEAutenticarAsync(
        RestauranteDeTeste restaurante, string perfil, string email)
    {
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var criado = await CriarUsuarioAsync(gestao, restaurante, perfil, email);

        // RF-SEG-005 — nasce com troca pendente, e o behavior bloqueia todas as rotas até ela
        // acontecer. Sem trocar, o teste mediria o gate de senha em vez da delegação.
        var token = await AutenticarAsync(email, Senha);

        using var primeiroAcesso = Ambiente.CriarCliente(token);

        (await primeiroAcesso.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = Senha, NovaSenha = Senha + "-nova" }))
            .EnsureSuccessStatusCode();

        _ = criado;

        return await AutenticarAsync(email, Senha + "-nova");
    }

    private async Task<UsuarioDto> CriarUsuarioAsync(
        HttpClient cliente, RestauranteDeTeste restaurante, string perfil, string email)
    {
        var resposta = await TentarCriarAsync(cliente, restaurante, perfil, email);

        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<UsuarioDto>())!;
    }

    private async Task<HttpResponseMessage> TentarCriarAsync(
        HttpClient cliente, RestauranteDeTeste restaurante, string perfil, string email)
    {
        Guid perfilId;

        await using (var db = Contexto(restaurante))
        {
            perfilId = await db.Perfis.Where(p => p.Name == perfil).Select(p => p.Id).SingleAsync();
        }

        return await cliente.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new CriarUsuarioCommand(
                $"Usuario {perfil}",
                "De Teste",
                CpfDeTeste.Gerar(email.GetHashCode(StringComparison.Ordinal)),
                email,
                perfilId,
                Senha));
    }
}
