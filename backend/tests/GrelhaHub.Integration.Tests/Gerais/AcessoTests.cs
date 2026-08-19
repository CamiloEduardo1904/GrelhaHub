using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Features.Gerais.Autenticar;
using GrelhaHub.Application.Features.Vendas.ListarPedidos;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-004</c> — perfis, permissões e acesso de plataforma. ADR-0013.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class AcessoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-010 — a carga inicial cria os seis perfis com as permissões do catálogo")]
    public async Task PerfisSaoSemeadosComAsPermissoesDoCatalogo()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        await using var db = Contexto(restaurante);

        var perfis = await db.Perfis.AsNoTracking().ToListAsync();

        // ADR-0022 §3 — a concessão agora é claim de papel (perfil_permissao, tipo `permissao`).
        var concessoes = await db.PermissoesDePerfil
            .AsNoTracking()
            .Where(claim => claim.ClaimType == CatalogoDeAcesso.TipoDeClaimDePermissao)
            .ToListAsync();

        perfis.Should().HaveCount(CatalogoDeAcesso.Perfis.Count);
        perfis.Should().OnlyContain(p => p.Sistema, "perfis da carga são do sistema: não se renomeiam");

        foreach (var esperado in CatalogoDeAcesso.Perfis)
        {
            var perfil = perfis.Single(p => p.Name == esperado.Nome);

            concessoes.Where(claim => claim.RoleId == perfil.Id)
                .Select(claim => claim.ClaimValue)
                .Should().BeEquivalentTo(
                    esperado.Permissoes,
                    $"o perfil {esperado.Nome} recebe exatamente o que CatalogoDeAcesso declara");
        }

        // O Entregador nasce vazio de propósito: as permissões dele são `entrega.*`, do módulo
        // LOG, que não existe. Perfil com permissão inventada seria pior que perfil vazio.
        var entregador = perfis.Single(p => p.Name == "Entregador");
        concessoes.Where(claim => claim.RoleId == entregador.Id).Should().BeEmpty();
    }

    [Fact(DisplayName = "RN-GER-010 — o token do administrador carrega as permissões, não o nome do perfil")]
    public async Task TokenCarregaPermissoes()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        using var anonimo = Ambiente.CriarCliente();

        var login = await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/login", UriKind.Relative),
            new AutenticarCommand(restaurante.Email, restaurante.Senha));

        login.EnsureSuccessStatusCode();

        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>();

        sessao!.Permissoes.Should().Contain("pedido.cancelar").And.Contain("custo.visualizar");
        sessao.DeveTrocarSenha.Should().BeFalse("o fundador escolheu a própria senha no cadastro");
        sessao.EhPlataforma.Should().BeFalse();
    }

    [Fact(DisplayName = "RN-GER-010 — operação sem permissão é recusada com 403, antes do handler")]
    public async Task CA2_SemPermissaoEhRecusadoAntesDoHandler()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Pernil", 44.00m);

        var atendente = await CriarUsuarioAsync(restaurante, "Atendente", "atendente@acesso.teste");

        using var anonimo = Ambiente.CriarCliente();
        using var comoAtendente = Ambiente.CriarCliente(atendente);

        var criado = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        criado.EnsureSuccessStatusCode();

        var pedidoId = (await criado.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        // O atendente PODE avançar o pedido…
        (await comoAtendente.PostAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/confirmar", UriKind.Relative), content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // …e NÃO pode cancelar: `pedido.cancelar` não está no perfil dele.
        var cancelamento = await comoAtendente.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/cancelar", UriKind.Relative),
            new { Motivo = "Tentativa sem permissao" });

        cancelamento.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cancelamento.Content.ReadAsStringAsync()).Should().Contain("RN-GER-010");

        // O handler não executou: o pedido continua Confirmado, não Cancelado.
        await using var db = Contexto(restaurante);

        (await db.Pedidos.AsNoTracking().SingleAsync()).Status
            .Should().Be(Domain.Vendas.StatusPedido.Confirmado);
    }

    [Fact(DisplayName = "RN-GER-014 — o usuário de plataforma enxerga todas as unidades; o comum continua isolado")]
    public async Task CA9_PlataformaEnxergaTodasEOComumSegueIsolado()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa Acesso", "11111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Acesso", "22222222000192");

        await CriarPedidoEmAsync(brasa, "Brisket do Brasa");
        await CriarPedidoEmAsync(fumaca, "Costela da Fumaca");

        var plataforma = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        using var suporte = Ambiente.CriarCliente(plataforma);
        using var doBrasa = Ambiente.CriarCliente(brasa.Token);

        var vistosPelaPlataforma = await suporte.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        vistosPelaPlataforma.Should().NotBeNull();
        vistosPelaPlataforma!.Should().HaveCount(2, "ADR-0013 — o suporte enxerga a base instalada");

        // ⚠⚠ ESTA é a metade que importa. Ela prova que o recorte foi AFROUXADO PARA UM PERFIL,
        // e não removido para todos. Se este caso ficar verde com a lista de dois pedidos, o
        // ADR-0013 deixou de valer e há vazamento entre clientes.
        var vistosPeloRestaurante = await doBrasa.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        vistosPeloRestaurante.Should().NotBeNull();
        vistosPeloRestaurante!.Should().HaveCount(1, "o restaurante vê apenas os seus pedidos");
        vistosPeloRestaurante![0].Itens[0].Produto.Should().Be("Brisket do Brasa");
    }

    [Fact(DisplayName = "RN-GER-014 — usuário comum não recebe a claim de plataforma")]
    public async Task UsuarioComumNaoRecebeAClaimDePlataforma()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        // A claim `plataforma` é a origem ÚNICA do desligamento do filtro global. Um usuário comum
        // que a recebesse veria todos os clientes — é o risco central do ADR-0013, e este caso é a
        // mitigação declarada lá.
        using var anonimo = Ambiente.CriarCliente();

        var login = await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/login", UriKind.Relative),
            new AutenticarCommand(restaurante.Email, restaurante.Senha));

        var sessao = await login.Content.ReadFromJsonAsync<AutenticarResponse>();

        sessao!.EhPlataforma.Should().BeFalse();
        sessao.UnidadeId.Should().Be(restaurante.UnidadeId, "usuário de restaurante tem unidade");

        var plataforma = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        using var suporte = Ambiente.CriarCliente(plataforma);

        var doSuporte = await suporte.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        doSuporte.Should().NotBeNull("o suporte autentica sem unidade e ainda consulta");
    }

    [Fact(DisplayName = "RN-VEN-041 — exclusão lógica retira o pedido da consulta sem apagar o registro")]
    public async Task ExclusaoLogicaDePedido()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");
        var pedidoId = await CriarPedidoEmAsync(restaurante, "Lancado por engano");

        using var admin = Ambiente.CriarCliente(restaurante.Token);

        var exclusao = await admin.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/excluir", UriKind.Relative),
            new { Motivo = "Lancamento em duplicidade no balcao" });

        exclusao.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Sai da consulta — pelo filtro global, sem nenhum handler ter escrito um WHERE.
        var listados = await admin.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        listados!.Should().BeEmpty("pedido excluído não compõe consulta nem apuração");

        // RN-GER-002 — mas o REGISTRO permanece, com quem, quando e por quê.
        await using var db = Contexto(restaurante);

        var comFiltroDesligado = await db.Pedidos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(p => p.Id == pedidoId);

        comFiltroDesligado.ExcluidoEm.Should().NotBeNull();
        comFiltroDesligado.ExcluidoPorUsuarioId.Should().NotBeNull("RN-VEN-041 registra quem excluiu");
        comFiltroDesligado.MotivoExclusao.Should().Be("Lancamento em duplicidade no balcao");

        // E não virou cancelamento: o estado que ele tinha é preservado, porque a trilha precisa
        // dizer O QUE foi excluído, não apenas que algo foi.
        comFiltroDesligado.Status.Should().Be(Domain.Vendas.StatusPedido.Aberto);
        comFiltroDesligado.MotivoCancelamento.Should().BeNull();
    }

    [Fact(DisplayName = "RN-VEN-041 — excluir pedido exige a permissão pedido.excluir")]
    public async Task ExclusaoExigePermissao()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");
        var pedidoId = await CriarPedidoEmAsync(restaurante, "Pedido do gerente");

        // O Gerente cancela, mas NÃO exclui: excluir tira da apuração, e isso é poder de
        // administrador.
        var gerente = await CriarUsuarioAsync(restaurante, "Gerente", "gerente@acesso.teste");

        using var comoGerente = Ambiente.CriarCliente(gerente);

        var exclusao = await comoGerente.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/excluir", UriKind.Relative),
            new { Motivo = "Gerente tentando excluir pedido" });

        exclusao.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var cancelamento = await comoGerente.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/cancelar", UriKind.Relative),
            new { Motivo = "Cliente desistiu" });

        cancelamento.StatusCode.Should().Be(HttpStatusCode.OK, "cancelar o gerente pode");
    }

    [Fact(DisplayName = "RN-GER-040 — força bruta é bloqueada na quinta falha, com mensagem que diz por quanto tempo")]
    public async Task ForcaBrutaEhBloqueada()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        using var anonimo = Ambiente.CriarCliente();

        // As quatro primeiras falhas respondem a MESMA mensagem genérica de RN-GER-027: até o
        // teto, quem sonda não descobre nem que a conta existe.
        for (var tentativa = 1; tentativa < PoliticaDeBloqueio.TentativasAntesDoBloqueio; tentativa++)
        {
            var recusa = await anonimo.PostAsJsonAsync(
                new Uri("/api/v1/autenticacao/login", UriKind.Relative),
                new AutenticarCommand(restaurante.Email, $"senha-errada-{tentativa}"));

            recusa.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await recusa.Content.ReadAsStringAsync()).Should().NotContainEquivalentOf(
                "bloquead", $"na tentativa {tentativa} a resposta ainda é a genérica de RN-GER-027");
        }

        // A quinta (PAR-GER-004) bloqueia — e a mensagem MUDA: diz que está bloqueada e por
        // quanto tempo. É a fresta em RN-GER-027 aceita e declarada pela ADR-0022 §4.
        var quinta = await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/login", UriKind.Relative),
            new AutenticarCommand(restaurante.Email, "senha-errada-final"));

        quinta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var corpo = await quinta.Content.ReadAsStringAsync();
        corpo.Should().ContainEquivalentOf("bloqueada", "RN-GER-040 manda DIZER que bloqueou");
        corpo.Should().ContainEquivalentOf("minuto", "e por quanto tempo (PAR-GER-005)");
        corpo.Should().Contain("RN-GER-040");

        // ⚠ A metade que faz o bloqueio valer: a senha CERTA também é recusada enquanto ele
        // dura. Sem isto, o lockout só atrasaria quem já errou — não quem acerta na sexta.
        var comSenhaCerta = await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/login", UriKind.Relative),
            new AutenticarCommand(restaurante.Email, restaurante.Senha));

        comSenhaCerta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await comSenhaCerta.Content.ReadAsStringAsync()).Should().ContainEquivalentOf("bloqueada");
    }

    [Fact(DisplayName = "RN-GER-040 — o contador de falhas zera no primeiro acerto")]
    public async Task ContadorDeFalhasZeraNoAcerto()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        using var anonimo = Ambiente.CriarCliente();

        var quaseBloquear = PoliticaDeBloqueio.TentativasAntesDoBloqueio - 1;

        // Erra até UMA falha antes do teto…
        for (var tentativa = 0; tentativa < quaseBloquear; tentativa++)
        {
            (await anonimo.PostAsJsonAsync(
                new Uri("/api/v1/autenticacao/login", UriKind.Relative),
                new AutenticarCommand(restaurante.Email, "senha-errada")))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // …acerta — o SignInManager zera o contador…
        (await anonimo.PostAsJsonAsync(
            new Uri("/api/v1/autenticacao/login", UriKind.Relative),
            new AutenticarCommand(restaurante.Email, restaurante.Senha)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // …e o mesmo tanto de falhas de novo NÃO bloqueia: sem o zeramento, o operador que
        // tropeça na senha duas manhãs seguidas acumularia rumo ao bloqueio para sempre.
        for (var tentativa = 0; tentativa < quaseBloquear; tentativa++)
        {
            var recusa = await anonimo.PostAsJsonAsync(
                new Uri("/api/v1/autenticacao/login", UriKind.Relative),
                new AutenticarCommand(restaurante.Email, "senha-errada"));

            recusa.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await recusa.Content.ReadAsStringAsync()).Should().NotContainEquivalentOf("bloquead");
        }
    }

    [Fact(DisplayName = "RN-GER-041 — trocar a senha derruba o token antigo na requisição seguinte")]
    public async Task TrocarSenhaInvalidaSessoesAnteriores()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        const string senhaNova = "senha-nova-que-derruba-1";

        // A sessão antiga funciona…
        using var sessaoAntiga = Ambiente.CriarCliente(restaurante.Token);

        (await sessaoAntiga.GetAsync(new Uri("/api/v1/cardapio", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // …a senha é trocada…
        var troca = await sessaoAntiga.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = restaurante.Senha, NovaSenha = senhaNova });

        troca.StatusCode.Should().Be(
            HttpStatusCode.NoContent, await troca.Content.ReadAsStringAsync());

        // …e o MESMO token morre na requisição seguinte: o SecurityStamp girou na troca, o
        // carimbo do token não casa mais, e a conferência por requisição responde 401. Antes
        // desta regra o token antigo seguia vivo até expirar — senha vazada trocada deixava o
        // atacante dentro por até oito horas (a lacuna que a ADR-0021 encontrou).
        (await sessaoAntiga.GetAsync(new Uri("/api/v1/cardapio", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Quem entra com a senha nova recebe token com o carimbo novo, e opera normalmente.
        var tokenNovo = await AutenticarAsync(restaurante.Email, senhaNova);

        using var sessaoNova = Ambiente.CriarCliente(tokenNovo);

        (await sessaoNova.GetAsync(new Uri("/api/v1/cardapio", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "RN-GER-041 / RN-GER-028 — senha redefinida por terceiro derruba a sessão do usuário")]
    public async Task SenhaRedefinidaPorTerceiroDerrubaASessao()
    {
        var restaurante = await CadastrarRestauranteAsync("Acesso Burger", "11111111000191");

        var tokenDaCozinha = await CriarUsuarioAsync(restaurante, "Cozinha", "cozinha@acesso.teste");

        using var cozinha = Ambiente.CriarCliente(tokenDaCozinha);

        (await cozinha.GetAsync(new Uri("/api/v1/pedidos/fila", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // O administrador redefine a senha do usuário — o caso em que a invalidação mais
        // importa: se a redefinição foi porque a conta caiu em mão errada, é ESSA sessão que
        // precisa morrer agora, não daqui a oito horas.
        await using var db = Contexto(restaurante);

        var usuarioId = await db.Usuarios.AsNoTracking()
            .Where(u => u.Email == "cozinha@acesso.teste")
            .Select(u => u.Id)
            .SingleAsync();

        using var admin = Ambiente.CriarCliente(restaurante.Token);

        var redefinicao = await admin.PostAsJsonAsync(
            new Uri($"/api/v1/usuarios/{usuarioId}/senha", UriKind.Relative),
            new { NovaSenha = "senha-redefinida-pelo-admin" });

        redefinicao.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await cozinha.GetAsync(new Uri("/api/v1/pedidos/fila", UriKind.Relative)))
            .StatusCode.Should().Be(
                HttpStatusCode.Unauthorized, "RN-GER-041 — a sessão anterior morre com a senha");
    }

    /// <summary>
    /// Cria um usuário com um dos perfis semeados e devolve o token dele.
    /// <para>
    /// Vai pelo <c>DbContext</c> porque a rota de criação de usuário entra depois; o que este
    /// teste precisa provar é o <b>gate de permissão</b>, não o cadastro.
    /// </para>
    /// </summary>
    private async Task<string> CriarUsuarioAsync(
        RestauranteDeTeste restaurante, string perfil, string email)
    {
        const string senha = "senha-de-teste-123";

        using var escopo = Ambiente.CriarEscopo();
        var hash = escopo.ServiceProvider.GetRequiredService<IHashDeSenha>();

        await using (var db = Contexto(restaurante))
        {
            var perfilId = await db.Perfis
                .Where(p => p.Name == perfil)
                .Select(p => p.Id)
                .SingleAsync();

            db.Usuarios.Add(new Usuario(
                restaurante.UnidadeId, perfil, email, hash.Gerar(senha), perfilId, deveTrocarSenha: false));

            await db.SaveChangesAsync();
        }

        return await AutenticarAsync(email, senha);
    }

    private async Task<Guid> CriarPedidoEmAsync(RestauranteDeTeste restaurante, string nomeDoProduto)
    {
        var produtoId = await CriarProdutoAsync(restaurante, nomeDoProduto, 39.00m);

        using var anonimo = Ambiente.CriarCliente();

        var criado = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente de Teste",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        criado.EnsureSuccessStatusCode();

        return (await criado.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();
    }
}
