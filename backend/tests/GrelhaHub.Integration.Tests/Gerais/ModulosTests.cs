using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Application.Features.Gerais.Modulos;
using GrelhaHub.Application.Features.Gerais.Parametros;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Integration.Tests.Infraestrutura;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-006</c> — CA-1 a CA-7: modularidade comercial. ADR-0014.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ModulosTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-017 — toda operação declara o seu módulo, e a partida recusa sem isso")]
    public void CA2_TodaOperacaoDeclaraOModulo()
    {
        // Este é o teste que faz o gate valer. Um behavior que só verifica quem declarou falha
        // ABERTO: a operação nova, sem atributo, passaria sem gate e ninguém descobriria.
        var assembly = typeof(ObterParametroQuery).Assembly;

        var acao = () => ValidadorDeDeclaracaoDeModulo.Validar(assembly);

        acao.Should().NotThrow("nenhum IRequest pode ficar sem [ExigeModulo] ou [SemModulo]");

        // E a validação tem dentes: um IRequest sem declaração é detectado.
        var semDeclaracao = () => ValidadorDeDeclaracaoDeModulo.Validar(
            typeof(OperacaoSemDeclaracao).Assembly);

        semDeclaracao.Should().Throw<InvalidOperationException>()
            .WithMessage("*RN-GER-017*")
            .And.Message.Should().Contain(nameof(OperacaoSemDeclaracao));
    }

    [Fact(DisplayName = "RN-GER-017 — operação de módulo não habilitado é recusada, mesmo com permissão")]
    public async Task CA1_ModuloNaoHabilitadoEhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Modulo Burger", "11111111000191");

        // O restaurante nasce com GER + VEN. FIN não foi contratado.
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var modulos = await gestao.GetFromJsonAsync<List<ModuloDaUnidadeDto>>(
            new Uri("/api/v1/modulos", UriKind.Relative));

        modulos.Should().NotBeNull();

        var porCodigo = modulos!.ToDictionary(m => m.Codigo, StringComparer.Ordinal);

        porCodigo["GER"].Habilitado.Should().BeTrue("núcleo, RN-GER-019");
        porCodigo["VEN"].Habilitado.Should().BeTrue("mínimo para vender");
        porCodigo["FIN"].Habilitado.Should().BeFalse("não contratado");

        // O usuário é Administrador — tem TODA permissão. Módulo não contratado não é questão
        // de perfil, e é isso que este caso prova.
        using var escopo = Ambiente.CriarEscopo();

        var contexto = escopo.ServiceProvider.GetRequiredService<IContextoDeExecucao>();
        contexto.DefinirUnidadePublica(restaurante.UnidadeId);

        var servico = escopo.ServiceProvider.GetRequiredService<IModulos>();

        (await servico.EstaHabilitadoAsync(restaurante.UnidadeId, CodigoDeModulo.Financeiro))
            .Should().BeFalse();

        (await servico.EstaHabilitadoAsync(restaurante.UnidadeId, CodigoDeModulo.Vendas))
            .Should().BeTrue();
    }

    [Fact(DisplayName = "RN-GER-017 — cardápio de unidade sem VEN devolve 404, não 403")]
    public async Task CA7_CardapioSemVendasDevolve404()
    {
        var restaurante = await CadastrarRestauranteAsync("Modulo Burger", "11111111000191");
        var plataforma = await AutenticarPlataformaAsync();

        using var anonimo = Ambiente.CriarCliente();
        using var suporte = Ambiente.CriarCliente(plataforma);

        // Com VEN habilitado, o cardápio responde.
        (await anonimo.GetAsync(new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var desabilitacao = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/unidades/{restaurante.UnidadeId}/modulos/VEN/desabilitar", UriKind.Relative),
            new { Justificativa = "Cliente cancelou o modulo de vendas" });

        desabilitacao.EnsureSuccessStatusCode();

        var semVendas = await anonimo.GetAsync(
            new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        // 404 e não 403: para quem está do lado de fora, o restaurante simplesmente não tem
        // cardápio online. 403 anunciaria que existe algo ali que não foi contratado.
        semVendas.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "RN-GER-018 — desabilitar módulo não apaga dado, e reabilitar devolve o acesso")]
    public async Task CA3_DesabilitarNaoApagaDado()
    {
        var restaurante = await CadastrarRestauranteAsync("Modulo Burger", "11111111000191");
        var plataforma = await AutenticarPlataformaAsync();

        var produtoId = await CriarProdutoAsync(restaurante, "Costela Premium", 68.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var suporte = Ambiente.CriarCliente(plataforma);

        var pedido = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente Antes",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        pedido.EnsureSuccessStatusCode();

        await AlterarModuloAsync(suporte, restaurante.UnidadeId, "VEN", habilitar: false);

        // RN-GER-018 — o dado permanece. Desabilitar interrompe o acesso, não o histórico.
        await using (var db = Contexto(restaurante))
        {
            (await db.Pedidos.AsNoTracking().CountAsync()).Should().Be(1);
            (await db.Produtos.AsNoTracking().CountAsync(p => p.Id == produtoId)).Should().Be(1);
        }

        await AlterarModuloAsync(suporte, restaurante.UnidadeId, "VEN", habilitar: true);

        var cardapio = await anonimo.GetAsync(
            new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        cardapio.StatusCode.Should().Be(HttpStatusCode.OK, "reabilitar devolve o acesso");
    }

    [Fact(DisplayName = "RN-GER-019 — o módulo núcleo GER não pode ser desabilitado")]
    public async Task CA4_NucleoNaoEhDesabilitavel()
    {
        var restaurante = await CadastrarRestauranteAsync("Modulo Burger", "11111111000191");
        var plataforma = await AutenticarPlataformaAsync();

        using var suporte = Ambiente.CriarCliente(plataforma);

        var resposta = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/unidades/{restaurante.UnidadeId}/modulos/GER/desabilitar", UriKind.Relative),
            new { Justificativa = "Tentativa de desabilitar o nucleo" });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("RN-GER-019");
    }

    [Fact(DisplayName = "RN-GER-020 — dependência é verificada nas duas direções")]
    public async Task CA5_DependenciaNasDuasDirecoes()
    {
        var restaurante = await CadastrarRestauranteAsync("Modulo Burger", "11111111000191");
        var plataforma = await AutenticarPlataformaAsync();

        using var suporte = Ambiente.CriarCliente(plataforma);

        // EST depende de PRO, que não está habilitado.
        var semDependencia = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/unidades/{restaurante.UnidadeId}/modulos/EST/habilitar", UriKind.Relative),
            new { Justificativa = "Cliente contratou controle de estoque" });

        semDependencia.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var erro = await semDependencia.Content.ReadAsStringAsync();
        erro.Should().Contain("PRO").And.Contain("RN-GER-020");

        // Habilitando PRO primeiro, EST passa.
        await AlterarModuloAsync(suporte, restaurante.UnidadeId, "PRO", habilitar: true);
        await AlterarModuloAsync(suporte, restaurante.UnidadeId, "EST", habilitar: true);

        // E agora PRO não pode sair enquanto EST estiver de pé — a outra direção.
        var comDependente = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/unidades/{restaurante.UnidadeId}/modulos/PRO/desabilitar", UriKind.Relative),
            new { Justificativa = "Tentativa de remover producao com estoque ativo" });

        comDependente.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await comDependente.Content.ReadAsStringAsync()).Should().Contain("EST");
    }

    [Fact(DisplayName = "RN-GER-021 — a habilitação tem vigência, e o banco recusa sobreposição")]
    public async Task CA6_HistoricoDeContratacao()
    {
        var restaurante = await CadastrarRestauranteAsync("Modulo Burger", "11111111000191");
        var plataforma = await AutenticarPlataformaAsync();

        using var suporte = Ambiente.CriarCliente(plataforma);

        await AlterarModuloAsync(suporte, restaurante.UnidadeId, "PRO", habilitar: true);

        await using var db = Contexto(restaurante);

        var vigencias = await db.ModulosDaUnidade
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(h => h.Modulo)
            .Where(h => h.UnidadeId == restaurante.UnidadeId)
            .ToListAsync();

        vigencias.Should().HaveCount(3, "GER e VEN da abertura, mais PRO");
        vigencias.Should().OnlyContain(h => h.VigenciaFim == null, "todas abertas");

        await AlterarModuloAsync(suporte, restaurante.UnidadeId, "PRO", habilitar: false);

        await using var depois = Contexto(restaurante);

        var producao = await depois.ModulosDaUnidade
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(h => h.Modulo)
            .Where(h => h.UnidadeId == restaurante.UnidadeId && h.Modulo!.Codigo == CodigoDeModulo.Producao)
            .SingleAsync();

        producao.VigenciaFim.Should().NotBeNull("desabilitar fecha a vigência, não apaga a linha");
        producao.Justificativa.Should().NotBeNullOrWhiteSpace("a decisão comercial fica rastreável");
    }

    /// <summary>Autentica o usuário de plataforma semeado pelo ambiente de teste.</summary>
    private Task<string> AutenticarPlataformaAsync() =>
        AutenticarAsync(AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

    private static async Task AlterarModuloAsync(
        HttpClient suporte, Guid unidadeId, string sigla, bool habilitar)
    {
        var acao = habilitar ? "habilitar" : "desabilitar";

        var resposta = await suporte.PostAsJsonAsync(
            new Uri($"/api/v1/plataforma/unidades/{unidadeId}/modulos/{sigla}/{acao}", UriKind.Relative),
            new { Justificativa = $"Teste de integracao: {acao} {sigla}" });

        resposta.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Existe apenas para provar que <see cref="ValidadorDeDeclaracaoDeModulo"/> detecta a
    /// ausência de declaração. Vive no assembly de teste, então não afeta a partida da API.
    /// </summary>
    internal sealed record OperacaoSemDeclaracao : IRequest<string>;
}
