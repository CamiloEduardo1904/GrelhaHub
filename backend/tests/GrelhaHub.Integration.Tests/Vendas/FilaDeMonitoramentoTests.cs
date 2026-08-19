using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Vendas.FilaDeMonitoramento;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Vendas;

/// <summary>
/// A fila de monitoramento — <c>RN-VEN-054</c> ⚠, <c>RN-VEN-055</c>, <c>SPEC-VEN-004</c>.
/// <para>
/// Todos os cenários montam o dado pelas <b>rotas da API</b>: o pedido nasce no cardápio público e
/// anda pelo ciclo de <c>RN-VEN-018</c>. É o que torna os testes capazes de responder "este pedido
/// sai da fila?", que é a pergunta da regra — um <c>INSERT</c> com o estado já pronto provaria
/// apenas que o teste soube escrever o número do enum.
/// </para>
/// <para>
/// ⚠ <b>O que NÃO está aqui:</b> a cadência de <c>RN-VEN-055</c> (15 s com movimento, 60 s com a
/// fila vazia). Ela vive no cliente, e o frontend não tem runner — a dívida é a mesma de
/// <c>RN-VEN-047</c> e está declarada na <c>SPEC-VEN-004</c> §5. O que o servidor decide — <b>quem
/// entra na fila e o que a fila devolve</b> — é o que estes casos cobrem.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class FilaDeMonitoramentoTests(AmbienteDeIntegracao ambiente)
    : TesteDeIntegracao(ambiente)
{
    private const string TelefoneDoCliente = "11977776666";
    private const string ObservacaoDoCliente = "Sem cebola e capricha na maionese";

    [Fact(DisplayName = "RN-VEN-054 — a fila traz os QUATRO estados vivos: aberto, confirmado, em produção e pronto")]
    public async Task Fila_TrazOsQuatroEstadosVivos()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Da Fila", "50505050000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato Da Fila", 20.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Um pedido em cada estado do qual o pedido ainda SAI.
        var aberto = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);

        var confirmado = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);
        await AvancarAsync(gestao, confirmado, "confirmar");

        var emProducao = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);
        await AvancarAsync(gestao, emProducao, "confirmar");
        await AvancarAsync(gestao, emProducao, "avancar");

        var pronto = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);
        await AvancarAsync(gestao, pronto, "confirmar");
        await AvancarAsync(gestao, pronto, "avancar");
        await AvancarAsync(gestao, pronto, "avancar");

        var fila = await ObterFilaAsync(gestao);

        fila.Select(p => p.Id).Should().BeEquivalentTo(
            [aberto, confirmado, emProducao, pronto],
            "os quatro estados de RN-VEN-018 de onde o pedido ainda anda estão na casa");

        fila.Select(p => p.Status).Should().BeEquivalentTo(
            ["Aberto", "Confirmado", "EmProducao", "Pronto"]);

        // FIFO — quem espera há mais tempo primeiro. Ordenar pelo mais recente poria o pedido que
        // acabou de entrar na frente de quem está esperando há vinte minutos.
        //
        // ⚠ A afirmação é sobre a ORDEM DOS IDS, e não sobre `CriadoEm`: o campo saiu do contrato
        // com a revisão de RN-VEN-054 (o painel de senha exibe o número e nada mais). Os quatro
        // foram criados nesta sequência, então a ordem esperada é exatamente esta — o que afirma
        // mais do que a versão anterior, e não menos.
        fila.Select(p => p.Id).Should().Equal(aberto, confirmado, emProducao, pronto);
    }

    [Fact(DisplayName = "RN-VEN-054 — entregue, finalizado e cancelado SAEM da fila")]
    public async Task Fila_NaoTrazPedidoQueSaiu()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Que Sai", "51515151000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato Que Sai", 20.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // ⚠ Entregue é o corte que o teste existe para provar. Dali ainda se vai para Finalizado
        // (RN-VEN-018) — mas o que falta é fechamento administrativo, não preparo, e a cozinha não
        // tem o que fazer com ele. É outro corte que o de `pedidoEncerrado`, e é de propósito.
        var entregue = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);
        await AvancarAtePassosAsync(gestao, entregue, 4);

        var finalizado = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);
        await AvancarAtePassosAsync(gestao, finalizado, 5);

        var cancelado = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);
        await AvancarAsync(gestao, cancelado, "confirmar");

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{cancelado}/cancelar", UriKind.Relative),
            new { Motivo = "Cliente desistiu na porta" }))
            .EnsureSuccessStatusCode();

        // O que continua vivo, para o teste não passar por a fila estar simplesmente vazia.
        var vivo = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);

        var fila = await ObterFilaAsync(gestao);

        fila.Select(p => p.Id).Should().BeEquivalentTo([vivo]);
        fila.Select(p => p.Id).Should()
            .NotContain(entregue, "entregue sai da fila: não há mais nada a preparar")
            .And.NotContain(finalizado)
            .And.NotContain(cancelado);
    }

    [Fact(DisplayName = "RN-VEN-054 — a fila devolve CINCO campos: nem nome, nem telefone, nem observação, nem item")]
    public async Task Fila_DevolveApenasONumeroEOQuePrecisaParaDesenhaLo()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Discreta", "52525252000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato Discreto", 20.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);

        var resposta = await gestao.GetAsync(new Uri("/api/v1/pedidos/fila", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        // ⚠⚠ A asserção é sobre o CORPO CRU, e é a única forma de a afirmar. Desserializar para
        // `PedidoNaFilaDto` provaria só que o DTO não tem a propriedade — o que o compilador já
        // sabe. O que a regra promete é que o dado não ATRAVESSA a rede, e é o texto da resposta
        // que responde isso.
        var corpo = await resposta.Content.ReadAsStringAsync();

        // ⚠⚠ ESTE CASO INVERTEU em 19/08/2026, e a inversão é o registro da revisão de RN-VEN-054.
        // Ele afirmava `corpo.Should().Contain("Cliente Da Fila")`, com o comentário "e o nome
        // fica: é como se chama quem vem buscar". Passou a não ficar: quem vem buscar é chamado
        // pelo NÚMERO, e o nome era a segunda forma de dizer a mesma coisa. Um teste que inverte
        // junto com a regra é o que impede a regra de mudar sozinha.
        corpo.Should().NotContain("Cliente Da Fila", "o painel de senha exibe o número, e nada mais");
        corpo.Should().NotContain(TelefoneDoCliente, "a fila é tela de ambiente compartilhado");
        corpo.Should().NotContain(ObservacaoDoCliente, "a observação continua inteira na comanda");

        // Os NOMES dos campos, e não só os valores: o que a regra promete é que o campo não existe,
        // e campo nulo passaria pela asserção de valor sem passar por esta.
        foreach (var ausente in new[] { "cliente", "telefone", "observacao", "itens", "total", "canal", "criadoEm" })
        {
            corpo.Should().NotContain(ausente, $"`{ausente}` saiu do contrato com a revisão");
        }

        // E os cinco que ficam. `numero` é o que a parede exibe; `dataOperacional` é o que impede
        // dois "#1" de se confundirem nela (RN-GER-006).
        foreach (var presente in new[] { "id", "unidadeId", "numero", "status", "dataOperacional" })
        {
            corpo.Should().Contain(presente);
        }

        // A lista de pedidos, essa SIM devolve tudo — a fila não esconde de quem precisa, ela
        // apenas não é a tela onde esses campos pertencem.
        var comanda = await (await gestao.GetAsync(new Uri("/api/v1/pedidos", UriKind.Relative)))
            .Content.ReadAsStringAsync();

        comanda.Should().Contain("Cliente Da Fila");
        comanda.Should().Contain(TelefoneDoCliente);
        comanda.Should().Contain(ObservacaoDoCliente);
    }

    [Fact(DisplayName = "RN-VEN-054 — pedido que atravessou a virada CONTINUA na fila, com o dia dele")]
    public async Task Fila_MantemPedidoDeDiaOperacionalAnterior()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Da Virada", "53535353000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato Da Virada", 20.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var deOntem = await CriarPedidoAsync(anonimo, restaurante.Slug, produto, 1);
        await AvancarAsync(gestao, deOntem, "confirmar");

        // O pedido nasce com a hora real; envelhecê-lo por SQL é o caminho que `PedidoPublicoTests`
        // já usa — `Pedido.CriadoEm` não tem setter, e está certo que não tenha. `data_operacional`
        // é recalculada pelo PostgreSQL, porque a coluna é GENERATED ALWAYS AS ... STORED.
        var anteontem = DateTimeOffset.UtcNow.AddDays(-2);
        await ReescreverCriacaoAsync(restaurante.UnidadeId, deOntem, anteontem);

        var fila = await ObterFilaAsync(gestao);

        // ⚠ O ponto da regra: a fila NÃO tem recorte de período. O pedido que atravessou a virada
        // continua para ser entregue — some da fila por ESTADO, nunca por data.
        fila.Should().ContainSingle().Which.Id.Should().Be(deOntem);

        fila[0].DataOperacional.Should().BeBefore(
            DateOnly.FromDateTime(DateTime.UtcNow),
            "a tela precisa do dia para marcar a comanda: `numero` reinicia a cada virada, e a "
            + "nº 1 de ontem é indistinguível da nº 1 de hoje");
    }

    [Fact(DisplayName = "RN-GER-009 / RN-GER-016 — a fila é da unidade, e informar a do vizinho não muda nada")]
    public async Task Fila_NaoAtravessaUnidade()
    {
        var minha = await CadastrarRestauranteAsync("Brasa De Ca", "54545454000191");
        var vizinha = await CadastrarRestauranteAsync("Brasa De La", "55555555000191");

        var meuProduto = await CriarProdutoAsync(minha, "Prato De Ca", 20.00m);
        var produtoDela = await CriarProdutoAsync(vizinha, "Prato De La", 30.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var euGestao = Ambiente.CriarCliente(minha.Token);
        using var elaGestao = Ambiente.CriarCliente(vizinha.Token);

        var meuPedido = await CriarPedidoAsync(anonimo, minha.Slug, meuProduto, 1);
        var pedidoDela = await CriarPedidoAsync(anonimo, vizinha.Slug, produtoDela, 1);

        (await ObterFilaAsync(euGestao)).Select(p => p.Id).Should().BeEquivalentTo([meuPedido]);
        (await ObterFilaAsync(elaGestao)).Select(p => p.Id).Should().BeEquivalentTo([pedidoDela]);

        // ⚠⚠ RN-GER-016 — informar a unidade do vizinho não devolve a fila dele NEM esvazia a
        // minha. O helper `UnidadeDaConsulta.Recorte` IGNORA o parâmetro para o restaurante; honrar
        // o valor cru faria o filtro do handler intersectar com o filtro global e devolver zero — o
        // restaurante veria a PRÓPRIA fila vazia por ter mandado um id que não é dele.
        var comIdAlheio = await ObterFilaAsync(euGestao, vizinha.UnidadeId);

        comIdAlheio.Select(p => p.Id).Should().BeEquivalentTo([meuPedido]);
    }

    private static async Task<IReadOnlyList<PedidoNaFilaDto>> ObterFilaAsync(
        HttpClient gestao, Guid? unidadeId = null)
    {
        var filtro = unidadeId is null ? string.Empty : $"?unidadeId={unidadeId}";

        var resposta = await gestao.GetAsync(
            new Uri($"/api/v1/pedidos/fila{filtro}", UriKind.Relative));

        resposta.StatusCode.Should().Be(
            HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<List<PedidoNaFilaDto>>()
            ?? throw new InvalidOperationException("A fila não devolveu corpo.");
    }

    private static async Task<Guid> CriarPedidoAsync(
        HttpClient anonimo, string slug, Guid produtoId, int quantidade)
    {
        var resposta = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente Da Fila",
                Telefone = TelefoneDoCliente,
                Observacao = ObservacaoDoCliente,
                Itens = new[]
                {
                    new { ProdutoId = produtoId, Quantidade = quantidade, Observacao = (string?)null },
                },
            });

        resposta.EnsureSuccessStatusCode();

        var criado = await resposta.Content.ReadFromJsonAsync<PedidoCriado>()
            ?? throw new InvalidOperationException("O pedido não devolveu corpo.");

        return criado.Id;
    }

    /// <summary>Confirmar e depois <paramref name="passos"/> − 1 avanços — <c>RN-VEN-018</c>.</summary>
    private static async Task AvancarAtePassosAsync(HttpClient gestao, Guid pedidoId, int passos)
    {
        await AvancarAsync(gestao, pedidoId, "confirmar");

        for (var passo = 1; passo < passos; passo += 1)
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

    /// <summary>
    /// Reescreve <c>dh_criacao</c> por SQL — o mesmo caminho de <c>PedidoPublicoTests</c>.
    /// <c>Pedido.CriadoEm</c> não tem setter, e está certo que não tenha.
    /// </summary>
    private async Task ReescreverCriacaoAsync(Guid unidadeId, Guid pedidoId, DateTimeOffset criacao)
    {
        await using var db = Ambiente.CriarContexto(unidadeId);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        comando.CommandText = "UPDATE pedido SET dh_criacao = @criacao WHERE id = @id";

        Adicionar(comando, "criacao", criacao.ToUniversalTime());
        Adicionar(comando, "id", pedidoId);

        (await comando.ExecuteNonQueryAsync()).Should().Be(1);
    }

    private static void Adicionar(DbCommand comando, string nome, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }

    private sealed record PedidoCriado(Guid Id, int Numero);
}
