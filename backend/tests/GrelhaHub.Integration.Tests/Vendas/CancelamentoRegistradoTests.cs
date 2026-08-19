using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Vendas.ListarPedidos;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Vendas;

/// <summary>
/// O cancelamento com <b>motivo, instante e responsável</b> — <c>RN-VEN-058</c> ⚠⚠,
/// <c>RN-GER-002</c>, <c>SPEC-VEN-005</c> §5.
/// <para>
/// ⚠⚠ <b>Duas dívidas antigas morreram aqui, e nenhuma tinha teste que a denunciasse:</b>
/// <c>Pedido.Cancelar</c> gravava o instante em <c>dh_finalizacao</c> — deixando cancelado e
/// finalizado indistinguíveis por data — e o resumo XML do método afirmava "com motivo e
/// responsável" quando o responsável não era gravado em lugar nenhum. Documentação que afirma o que
/// o código não faz é o mesmo defeito que a linha de <c>RN-VEN-047</c> no catálogo carregou por um
/// dia; a diferença é que este agora tem quem o meça.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class CancelamentoRegistradoTests(AmbienteDeIntegracao ambiente)
    : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-VEN-058 — o cancelamento grava motivo, instante e QUEM cancelou")]
    public async Task Cancelamento_GravaOsTres()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Que Cancela", "56565656000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato Cancelado", 30.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var pedido = await CriarPedidoAsync(anonimo, restaurante.Slug, produto);

        const string motivo = "Cliente desistiu na porta";
        var antes = DateTimeOffset.UtcNow.AddSeconds(-1);

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedido}/cancelar", UriKind.Relative), new { Motivo = motivo }))
            .EnsureSuccessStatusCode();

        await using var db = Contexto(restaurante);
        var gravado = await db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == pedido);

        gravado.MotivoCancelamento.Should().Be(motivo);
        gravado.CanceladoEm.Should().NotBeNull().And.Subject.As<DateTimeOffset>()
            .Should().BeAfter(antes);
        gravado.CanceladoPorUsuarioId.Should().NotBeNull();

        // ⚠⚠ O coração da regra: cancelar NÃO é finalizar. O pedido cancelado não passou pelo fim
        // do ciclo — ele saiu dele.
        gravado.FinalizadoEm.Should().BeNull();

        // E quem cancelou é o usuário da SESSÃO, não um id qualquer que a rota aceitasse.
        var admin = await db.Usuarios.AsNoTracking()
            .SingleAsync(u => u.Email == restaurante.Email);

        gravado.CanceladoPorUsuarioId.Should().Be(admin.Id);
    }

    [Fact(DisplayName = "RN-VEN-058 — a lista devolve o motivo, o instante e o NOME COMPLETO de quem cancelou")]
    public async Task Lista_DevolveOResponsavelComNomeESobrenome()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Do Nome", "57575757000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato Do Nome", 30.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var pedido = await CriarPedidoAsync(anonimo, restaurante.Slug, produto);

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedido}/cancelar", UriKind.Relative),
            new { Motivo = "Faltou ingrediente" }))
            .EnsureSuccessStatusCode();

        var lista = await gestao.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        var cancelado = lista!.Single(p => p.Id == pedido);

        cancelado.MotivoCancelamento.Should().Be("Faltou ingrediente");
        cancelado.CanceladoEm.Should().NotBeNull();

        // O harness cadastra o administrador como "Dona do Restaurante" + "Da Brasa" — nome E
        // sobrenome, que é o que `RN-VEN-058` pede. Sobrenome vazio devolveria só o nome, sem espaço
        // sobrando: é o que o `NULLIF(TRIM(...))` da visão garante.
        cancelado.CanceladoPor.Should().Be("Dona do Restaurante Da Brasa");
    }

    [Fact(DisplayName = "RN-VEN-058 — pedido NÃO cancelado não traz nada dos três campos")]
    public async Task PedidoVivo_NaoTrazCancelamento()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Sem Cancelar", "58585858000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato Vivo", 30.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        await CriarPedidoAsync(anonimo, restaurante.Slug, produto);

        var lista = await gestao.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        var vivo = lista!.Single();

        // ⚠ Os três andam juntos: motivo sem instante não diz se foi agora ou na semana passada,
        // e instante sem responsável não diz a quem perguntar. A comanda exibe os três ou não
        // exibe o bloco.
        vivo.MotivoCancelamento.Should().BeNull();
        vivo.CanceladoEm.Should().BeNull();
        vivo.CanceladoPor.Should().BeNull();
    }

    [Fact(DisplayName = "RN-VEN-058 — pedido cancelado SEM responsável continua aparecendo na lista")]
    public async Task CanceladoSemResponsavel_NaoSomeDaLista()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Legada", "59595959000191");
        var produto = await CriarProdutoAsync(restaurante, "Prato Legado", 30.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var pedido = await CriarPedidoAsync(anonimo, restaurante.Slug, produto);

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedido}/cancelar", UriKind.Relative),
            new { Motivo = "Cancelamento antigo" }))
            .EnsureSuccessStatusCode();

        // ⚠⚠ Simula a LINHA ANTIGA — cancelada antes de RN-VEN-058, sem responsável. É o estado em
        // que a migration deixa todo cancelamento já existente, e o único jeito de exercitá-lo.
        await using (var db = Contexto(restaurante))
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE pedido SET cancelado_por_usuario_id = NULL WHERE id = {0}", pedido);
        }

        var lista = await gestao.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        // ⚠⚠ O ponto: a visão usa LEFT JOIN. Com `INNER`, este pedido SUMIRIA da lista — e consulta
        // que perde linha ao ganhar coluna é o defeito mais caro que uma visão pode ter, porque
        // ninguém procura o que não aparece.
        var cancelado = lista!.Single(p => p.Id == pedido);

        cancelado.MotivoCancelamento.Should().Be("Cancelamento antigo");
        cancelado.CanceladoEm.Should().NotBeNull();
        cancelado.CanceladoPor.Should().BeNull("a tela omite a linha em vez de inventar \"sistema\"");
    }

    private static async Task<Guid> CriarPedidoAsync(HttpClient anonimo, string slug, Guid produtoId)
    {
        var resposta = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente Do Cancelamento",
                Telefone = "11966665555",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        resposta.EnsureSuccessStatusCode();

        var criado = await resposta.Content.ReadFromJsonAsync<PedidoCriado>()
            ?? throw new InvalidOperationException("O pedido não devolveu corpo.");

        return criado.Id;
    }

    private sealed record PedidoCriado(Guid Id, int Numero);
}
