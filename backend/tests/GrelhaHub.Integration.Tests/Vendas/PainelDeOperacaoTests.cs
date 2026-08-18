using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Application.Features.Vendas.ResumoDaOperacao;
using GrelhaHub.Integration.Tests.Infraestrutura;

namespace GrelhaHub.Integration.Tests.Vendas;

/// <summary>
/// O painel de operação — <c>RN-VEN-049</c> ⚠⚠, <c>RN-VEN-050</c>, <c>RN-VEN-051</c>,
/// <c>RN-GER-038</c>.
/// <para>
/// Todos os cenários montam o dado pelas <b>rotas da API</b>: pedido nasce no cardápio público e
/// anda pelo ciclo de <c>RN-VEN-018</c>. Inserir pedido por <c>INSERT</c> pularia a coluna gerada
/// <c>data_operacional</c>, que é justamente o que estes testes verificam.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class PainelDeOperacaoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string TelefoneDoCliente = "11988887777";

    [Fact(DisplayName = "RN-VEN-049 — faturamento conta só FINALIZADO; em andamento vai em contador separado")]
    public async Task Faturamento_SoFinalizado()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa do Painel", "40404040000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato do Painel", 25.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Um pedido percorre o ciclo inteiro: é o único que vira faturamento.
        var finalizado = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 2);
        await LevarAoFimAsync(gestao, finalizado);

        // Outro para no meio do caminho — está no salão, e não é faturamento.
        var emAndamento = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);
        await AvancarAsync(gestao, emAndamento, "confirmar");

        // E um cancelado, que não entra em lugar nenhum além do próprio contador.
        var cancelado = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 4);
        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{cancelado}/cancelar", UriKind.Relative),
            new { Motivo = "Cliente desistiu no telefone" }))
            .EnsureSuccessStatusCode();

        var resumo = await ObterResumoAsync(gestao);

        resumo.Faturamento.Should().Be(50.00m, "2 × R$ 25,00 do único pedido finalizado");
        resumo.PedidosFinalizados.Should().Be(1);

        // ⚠ O coração da regra: o que ainda pode ser cancelado NÃO soma no faturamento — número
        // que encolhe sozinho depois destrói a confiança no painel inteiro —, mas também não
        // desaparece da tela.
        resumo.PedidosEmAndamento.Should().Be(1);
        resumo.ValorEmAndamento.Should().Be(25.00m);
        resumo.PedidosCancelados.Should().Be(1);

        // RN-VEN-051 — faturamento dividido por pedidos finalizados.
        resumo.TicketMedio.Should().Be(50.00m);
    }

    [Fact(DisplayName = "RN-VEN-051 — sem venda o ticket médio é nulo, e não zero")]
    public async Task TicketMedio_NuloSemVenda()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Sem Venda", "41414141000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resumo = await ObterResumoAsync(gestao);

        resumo.Faturamento.Should().Be(0m);
        resumo.PedidosFinalizados.Should().Be(0);

        // Zero afirmaria "o ticket foi zero"; ausência afirma "não houve venda", e são leituras
        // diferentes para quem compara semanas.
        resumo.TicketMedio.Should().BeNull();
    }

    [Fact(DisplayName = "RN-VEN-050 — a série traz TODOS os dias do período, inclusive os sem pedido")]
    public async Task Serie_TrazDiaSemPedidoComZero()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Da Serie", "42424242000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var de = hoje.AddDays(-4);

        var resumo = await ObterResumoAsync(gestao, de, hoje);

        // ⚠ Cinco dias, e não "os dias que tiveram pedido". Série com dia faltando faz o olho ler
        // continuidade onde houve fechamento.
        resumo.Serie.Should().HaveCount(5);
        resumo.Serie[0].Dia.Should().Be(de);
        resumo.Serie[^1].Dia.Should().Be(hoje);
        resumo.Serie.Should().OnlyContain(d => d.Faturamento == 0m && d.Pedidos == 0);
    }

    [Fact(DisplayName = "RN-GER-006 — período invertido é recusado")]
    public async Task PeriodoInvertido_Recusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Invertida", "43434343000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var resposta = await gestao.GetAsync(
            new Uri($"/api/v1/pedidos/resumo?de={hoje:yyyy-MM-dd}&ate={hoje.AddDays(-1):yyyy-MM-dd}",
                UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "RN-GER-038 — atendente vê pedido e NÃO vê o painel de operação")]
    public async Task Atendente_NaoVeOPainel()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Do Atendente", "44444444000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var perfis = await gestao.GetFromJsonAsync<List<PerfilDaUnidadeDto>>(
            new Uri("/api/v1/usuarios/perfis", UriKind.Relative));

        const string email = "atendente.painel@brasa.teste";
        const string senha = "senha-do-atendente-123";

        (await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new
            {
                Nome = "Atendente",
                Sobrenome = "Do Balcão",
                Cpf = CpfDeTeste.Gerar(96),
                Email = email,
                PerfilId = perfis!.Single(p => p.Nome == "Atendente").Id,
                SenhaInicial = senha,
            }))
            .EnsureSuccessStatusCode();

        // ⚠ RF-SEG-005 antes de tudo: com a pendência de troca aberta, TODA rota responde 403 —
        // inclusive a que ele pode. Sem trocar aqui, este teste passaria pelo motivo errado,
        // afirmando "o atendente não vê o painel" quando na verdade ele não vê nada.
        using var atendente = Ambiente.CriarCliente(
            await TrocarSenhaEEntrarAsync(email, senha, "senha-escolhida-do-atendente-456"));

        // Ele VÊ o pedido: é a rotina dele.
        (await atendente.GetAsync(new Uri("/api/v1/pedidos", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // ⚠ E NÃO vê o agregado. É a separação inteira de RN-GER-038: agregado de venda é o passo
        // anterior a margem, e concedê-lo junto com `pedido.visualizar` daria a todo atendente o
        // número que RN-GER-012 protege.
        (await atendente.GetAsync(new Uri("/api/v1/pedidos/resumo", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Troca a senha do primeiro acesso e devolve um token limpo — <c>RF-SEG-005</c>.
    /// <para>
    /// Reautentica no fim porque o claim <c>trocar_senha</c> viaja no token e não cai com a troca:
    /// seguir com o token antigo continuaria batendo em 403.
    /// </para>
    /// </summary>
    private async Task<string> TrocarSenhaEEntrarAsync(string email, string atual, string nova)
    {
        using var comPendencia = Ambiente.CriarCliente(await AutenticarAsync(email, atual));

        (await comPendencia.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = atual, NovaSenha = nova }))
            .EnsureSuccessStatusCode();

        return await AutenticarAsync(email, nova);
    }

    private static async Task<ResumoDaOperacaoDto> ObterResumoAsync(
        HttpClient gestao, DateOnly? de = null, DateOnly? ate = null)
    {
        var filtro = de is null
            ? string.Empty
            : $"?de={de:yyyy-MM-dd}&ate={ate ?? de:yyyy-MM-dd}";

        var resposta = await gestao.GetAsync(
            new Uri($"/api/v1/pedidos/resumo{filtro}", UriKind.Relative));

        resposta.StatusCode.Should().Be(
            HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<ResumoDaOperacaoDto>()
            ?? throw new InvalidOperationException("O resumo não devolveu corpo.");
    }

    private static async Task<Guid> CriarPedidoAsync(
        HttpClient anonimo, string slug, Guid produtoId, int quantidade)
    {
        var resposta = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente do Painel",
                Telefone = TelefoneDoCliente,
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = quantidade, Observacao = (string?)null } },
            });

        resposta.EnsureSuccessStatusCode();

        var criado = await resposta.Content.ReadFromJsonAsync<PedidoCriado>()
            ?? throw new InvalidOperationException("O pedido não devolveu corpo.");

        return criado.Id;
    }

    /// <summary>Confirmado → Em produção → Pronto → Entregue → Finalizado (<c>RN-VEN-018</c>).</summary>
    private static async Task LevarAoFimAsync(HttpClient gestao, Guid pedidoId)
    {
        await AvancarAsync(gestao, pedidoId, "confirmar");

        for (var passo = 0; passo < 4; passo += 1)
        {
            await AvancarAsync(gestao, pedidoId, "avancar");
        }
    }

    private static async Task AvancarAsync(HttpClient gestao, Guid pedidoId, string acao)
    {
        var resposta = await gestao.PostAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/{acao}", UriKind.Relative), content: null);

        resposta.EnsureSuccessStatusCode();
    }

    private sealed record PedidoCriado(Guid Id, int Numero);
}
