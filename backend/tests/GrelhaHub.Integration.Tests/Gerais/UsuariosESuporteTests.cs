using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Features.Gerais.Autenticar;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-004</c> — CA-3, CA-5, CA-7, CA-8, CA-10 e CA-11.
/// <para>
/// Os quatro grupos que ficaram pendentes na primeira entrega, fechados aqui. Dois seguem abertos
/// por falta de tabela, e estão nomeados na §10 da spec: CA-4 (gerente vê custo) espera a ficha
/// técnica, e CA-6 (colaborador inativo) espera o módulo RH.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class UsuariosESuporteTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string SenhaInicial = "senha-inicial-123";

    [Fact(DisplayName = "RN-GER-012 — nenhuma resposta da API devolve custo, CMV ou margem (CA-3)")]
    public async Task CA3_NenhumaRespostaVazaCusto()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Vedada", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Costela", 62.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 2, Observacao = (string?)null } },
            });

        // Varredura em TODAS as leituras que existem hoje, no corpo BRUTO.
        //
        // RN-GER-012 é restrição de dado, não de tela: devolver e esconder no front continuaria
        // visível em qualquer inspeção de rede. Uma varredura, e não uma asserção por endpoint,
        // porque o endpoint novo entra na lista e o teste passa a cobri-lo de graça.
        (string Rota, HttpClient Cliente)[] leituras =
        [
            ($"/api/publico/{restaurante.Slug}/cardapio", anonimo),
            ("/api/v1/cardapio", gestao),
            ("/api/v1/pedidos", gestao),
            ("/api/v1/restaurante/meu", gestao),
            ("/api/v1/modulos", gestao),
            ("/api/v1/usuarios", gestao),
            ("/api/v1/parametros/PAR-VEN-002", gestao),
        ];

        string[] proibidos = ["custo", "cmv", "margem"];

        foreach (var (rota, cliente) in leituras)
        {
            var resposta = await cliente.GetAsync(new Uri(rota, UriKind.Relative));

            resposta.StatusCode.Should().Be(HttpStatusCode.OK, $"{rota} precisa responder");

            var corpo = await resposta.Content.ReadAsStringAsync();

            foreach (var proibido in proibidos)
            {
                // Procura a CHAVE, não a substring — é o que o CA-3 diz ("não contém as chaves").
                //
                // A primeira versão deste teste procurava substring e acusou o nome do próprio
                // restaurante, batizado "Custo Burger" pelo teste. Falso positivo que teria levado
                // a "consertar" produção que estava certa.
                var chave = new System.Text.RegularExpressions.Regex(
                    $@"""{proibido}[A-Za-z]*""\s*:",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                chave.IsMatch(corpo).Should().BeFalse(
                    $"{rota} não pode ter chave {proibido} no corpo — RN-GER-012");
            }
        }
    }

    [Fact(DisplayName = "RN-RH-006 — usuário desativado não autentica, com a mesma mensagem (CA-5)")]
    public async Task CA5_UsuarioDesativadoNaoAutentica()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var criado = await CriarUsuarioAsync(gestao, restaurante, "Atendente", "some@acesso.teste");

        // Autentica antes: prova que a credencial estava boa, e que é a DESATIVAÇÃO que a derruba.
        (await TentarLoginAsync("some@acesso.teste", SenhaInicial))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await gestao.PostAsync(
            new Uri($"/api/v1/usuarios/{criado.Id}/desativar", UriKind.Relative), content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var depois = await TentarLoginAsync("some@acesso.teste", SenhaInicial);

        depois.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A MESMA resposta de credencial inválida, de propósito: diferenciar as duas confirmaria a
        // existência da conta para quem está tentando adivinhar.
        var comSenhaErrada = await TentarLoginAsync("some@acesso.teste", "senha-completamente-errada");

        var corpoInativo = await depois.Content.ReadAsStringAsync();
        var corpoSenhaErrada = await comSenhaErrada.Content.ReadAsStringAsync();

        // ⚠ O código de suporte de RN-GER-027 é de CORRELAÇÃO, e muda a cada tentativa — então os
        // corpos não são byte a byte iguais. Comparar com o código mascarado é o que este caso
        // precisa afirmar: **tudo, exceto o código aleatório, é idêntico**.
        //
        // Este teste foi o que derrubou a primeira versão do código, que era fixo por motivo
        // (GH-LOGIN-01/02/03). Código por motivo É a distinção que a regra recusa: quem tem só o
        // e-mail de alguém descobriria que a conta existe e está inativa.
        static string SemOCodigo(string corpo) =>
            System.Text.RegularExpressions.Regex.Replace(corpo, "[0-9A-F]{8}", "XXXXXXXX");

        SemOCodigo(corpoInativo).Should().Be(SemOCodigo(corpoSenhaErrada),
            "nada além do código de correlação distingue as duas recusas");

        // E o motivo real NUNCA aparece na resposta — ele vai só para o log.
        foreach (var motivo in new[] { "Inativo", "UsuarioInativo", "UnidadeInativa", "Credencial" })
        {
            corpoInativo.Should().NotContain(motivo, "o motivo da recusa é dado de log, não de API");
        }

        // Os códigos existem e são DIFERENTES entre tentativas: se fossem iguais, o código estaria
        // identificando o motivo, e voltaríamos ao problema.
        var codigoA = System.Text.RegularExpressions.Regex.Match(corpoInativo, "[0-9A-F]{8}").Value;
        var codigoB = System.Text.RegularExpressions.Regex.Match(corpoSenhaErrada, "[0-9A-F]{8}").Value;

        codigoA.Should().NotBeNullOrEmpty();
        codigoA.Should().NotBe(codigoB, "correlação por tentativa, não catálogo de motivos");

        // RN-GER-002 — desativado, não excluído: o registro continua lá.
        await using var db = Contexto(restaurante);
        (await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == criado.Id)).Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "RNF-005 — a senha é gravada com hash e salt, nunca em texto claro (CA-7)")]
    public async Task CA7_SenhaNuncaEmTextoClaro()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var criado = await CriarUsuarioAsync(gestao, restaurante, "Cozinha", "cozinha@acesso.teste");

        await using var db = Contexto(restaurante);

        var usuario = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == criado.Id);

        usuario.SenhaHash.Should().NotContain(SenhaInicial, "a senha não pode aparecer no hash");
        usuario.SenhaHash.Length.Should().BeGreaterThan(40, "PBKDF2 com salt produz hash longo");

        // Salt por senha: dois usuários com a MESMA senha têm hashes diferentes. Sem isso, uma
        // tabela de hashes conhecidos quebraria as duas contas de uma vez.
        var segundo = await CriarUsuarioAsync(gestao, restaurante, "Cozinha", "cozinha2@acesso.teste");

        await using var conferencia = Contexto(restaurante);

        var outro = await conferencia.Usuarios.AsNoTracking().SingleAsync(u => u.Id == segundo.Id);

        outro.SenhaHash.Should().NotBe(usuario.SenhaHash, "o salt é por senha, não global");

        // E a senha não volta em resposta alguma — nem o valor, nem uma chave que a carregue.
        //
        // Procurar a substring "senha" acusaria `deveTrocarSenha`, que é chave legítima. O que não
        // pode aparecer é o VALOR e as chaves `senha`/`senhaHash`.
        var listados = await gestao.GetStringAsync(new Uri("/api/v1/usuarios", UriKind.Relative));

        listados.Should().NotContainEquivalentOf(SenhaInicial, "a senha em claro nunca sai do servidor");

        System.Text.RegularExpressions.Regex
            .IsMatch(listados, @"""senha(Hash)?""\s*:", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Should().BeFalse("nenhuma chave de senha na resposta");
    }

    [Fact(DisplayName = "RF-SEG-005 — usuário novo só troca a senha até trocá-la (CA-8)")]
    public async Task CA8_TrocaNoPrimeiroAcesso()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        await CriarUsuarioAsync(gestao, restaurante, "Atendente", "novo@acesso.teste");

        var login = await TentarLoginAsync("novo@acesso.teste", SenhaInicial);
        login.EnsureSuccessStatusCode();

        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>();
        sessao!.DeveTrocarSenha.Should().BeTrue("criado por outra pessoa: a senha inicial não é dele");

        using var novo = Ambiente.CriarCliente(sessao.Token);

        // Qualquer rota é recusada — inclusive uma que a permissão dele autorizaria.
        var bloqueada = await novo.GetAsync(new Uri("/api/v1/pedidos", UriKind.Relative));

        bloqueada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await bloqueada.Content.ReadAsStringAsync()).Should().Contain("RF-SEG-005");

        // A troca passa.
        var troca = await novo.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = SenhaInicial, NovaSenha = "senha-escolhida-por-mim-456" });

        troca.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Reautenticado, opera normalmente.
        var depois = await TentarLoginAsync("novo@acesso.teste", "senha-escolhida-por-mim-456");
        var sessaoNova = await depois.Content.ReadFromJsonAsync<AutenticarResponse>();

        sessaoNova!.DeveTrocarSenha.Should().BeFalse();

        using var liberado = Ambiente.CriarCliente(sessaoNova.Token);

        (await liberado.GetAsync(new Uri("/api/v1/pedidos", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <c>RN-GER-015</c> — <b>revisada em 18/08/2026</b>, ADR-0020.
    /// <para>
    /// A versão anterior deste teste provava o contrário: que a escrita de plataforma era RECUSADA
    /// sem sessão de suporte aberta. A sessão foi removida — era um portão aberto por quem ele
    /// deveria conter, e o motivo digitado para destravar um botão não é finalidade declarada.
    /// </para>
    /// <para>
    /// ⚠ <b>O que a regra garante continua igual, e é isto que o teste passa a afirmar:</b> nenhuma
    /// operação de plataforma sobre dado de cliente acontece sem rastro. O que mudou é o
    /// mecanismo — de pré-condição para registro —, e o registro sempre foi o controle forte.
    /// </para>
    /// </summary>
    [Fact(DisplayName = "RN-GER-015 — escrita de plataforma passa direto e FICA NA TRILHA (CA-10)")]
    public async Task CA10_EscritaDePlataformaFicaNaTrilha()
    {
        var restaurante = await CadastrarRestauranteAsync("Suporte Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Ancho", 78.00m);

        using var anonimo = Ambiente.CriarCliente();

        var pedido = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        var pedidoId = (await pedido.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var plataforma = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        using var suporte = Ambiente.CriarCliente(plataforma);

        // Ler continua sendo rotina de suporte.
        (await suporte.GetAsync(new Uri("/api/v1/pedidos", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // E escrever passa direto — sem portão, e SEM deixar de ser registrado.
        var cancelamento = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/cancelar", UriKind.Relative),
            new { Motivo = "Cliente relatou pedido travado por telefone" });

        cancelamento.StatusCode.Should().Be(
            HttpStatusCode.OK, await cancelamento.Content.ReadAsStringAsync());

        // ⚠ O CORAÇÃO DA REGRA REVISADA: a operação existe na trilha, com autor e valores.
        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var naTrilha = await db.Trilha
            .AsNoTracking()
            .Where(t => t.RegistroId == pedidoId)
            .ToListAsync();

        naTrilha.Should().NotBeEmpty(
            "escrita de plataforma sem rastro é o que RN-GER-015 sempre proibiu — o que mudou "
            + "foi o mecanismo, não a garantia");

        naTrilha.Should().Contain(
            t => t.UsuarioId != null && t.UsuarioId != Guid.Empty,
            "a trilha nomeia QUEM operou, e é isso que a sessão de suporte tentava garantir antes");

        naTrilha.Should().Contain(
            t => t.UnidadeId == restaurante.UnidadeId,
            "e sobre qual casa a plataforma operou");

        // ⚠ O motivo NÃO se perdeu: onde o porquê importa, ele é obrigatório no próprio comando —
        // cancelar exige motivo (RN-VEN-018) — e chega à trilha pelo caminho normal.
        naTrilha.Should().Contain(
            t => t.ValoresNovos != null && t.ValoresNovos.Contains("travado", StringComparison.OrdinalIgnoreCase),
            "o motivo do cancelamento fica gravado no próprio registro, e a trilha o carrega");
    }

    [Fact(DisplayName = "RN-GER-016 — o usuário de plataforma não aparece na listagem do restaurante (CA-11)")]
    public async Task CA11_PlataformaEhInvisivelAoRestaurante()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        await CriarUsuarioAsync(gestao, restaurante, "Gerente", "gerente@acesso.teste");

        var listados = await gestao.GetFromJsonAsync<List<UsuarioDto>>(
            new Uri("/api/v1/usuarios", UriKind.Relative));

        listados.Should().NotBeNull();

        // O administrador fundador e o gerente criado. O suporte da plataforma NÃO.
        listados!.Should().HaveCount(2);

        listados!.Select(u => u.Email)
            .Should().NotContain(
                AmbienteDeIntegracao.EmailDePlataforma,
                "RN-GER-016 — um usuário que enxerga todos os clientes não pode ser visto por um cliente");

        // Prova que ele existe de fato: a ausência acima é filtro, não banco vazio.
        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(u => u.EhPlataforma)).Should().BeTrue();
    }

    private async Task<HttpResponseMessage> TentarLoginAsync(string email, string senha)
    {
        using var anonimo = Ambiente.CriarCliente();

        return await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/login", UriKind.Relative),
            new AutenticarCommand(email, senha));
    }

    private async Task<UsuarioDto> CriarUsuarioAsync(
        HttpClient gestao, RestauranteDeTeste restaurante, string perfil, string email)
    {
        Guid perfilId;

        await using (var db = Contexto(restaurante))
        {
            perfilId = await db.Perfis.Where(p => p.Nome == perfil).Select(p => p.Id).SingleAsync();
        }

        var resposta = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new CriarUsuarioCommand(
                $"Usuario {perfil}",
                "Sobrenome",

                // RN-GER-023 — derivado do E-MAIL, nao do perfil. O CPF e unico em todo o
                // sistema, e o CA-7 cria DOIS usuarios do mesmo perfil de proposito (para
                // comparar hashes): derivar do perfil dava o mesmo CPF nos dois e o segundo
                // cadastro era recusado.
                CpfDeTeste.Gerar(email.GetHashCode(StringComparison.Ordinal)),
                email,
                perfilId,
                SenhaInicial));

        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<UsuarioDto>())!;
    }
}
