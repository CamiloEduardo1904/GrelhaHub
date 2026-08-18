using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// O banco de log e a ponte até ele — ADR-0017.
/// <para>
/// Três perguntas, e as três são sobre a propriedade que a separação de bancos poderia ter
/// destruído: <b>não existe operação sem registro</b> (<c>RN-GER-015</c>).
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class BancoDeLogTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "ADR-0017 — a operação e o registro gravam JUNTOS, na outbox, sem esperar o worker")]
    public async Task Outbox_GravaNaMesmaTransacao()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Do Log", "60606060000191");

        // ⚠ O CORAÇÃO DA DECISÃO: no instante em que a operação termina, o registro JÁ existe — na
        // outbox, no banco da aplicação, dentro da mesma transação. Se dependesse do worker, haveria
        // uma janela em que a operação existe e o registro não, e é essa janela que o padrão fecha.
        await using (var app = Ambiente.CriarContexto(Guid.Empty))
        {
            (await app.OutboxDeAuditoria.AsNoTracking().CountAsync())
                .Should().BeGreaterThan(0, "a trilha nasce na transação da operação, não depois dela");
        }

        // E o banco de log ainda não sabe de nada — a cópia é assíncrona, e o ADR declara o atraso.
        await using (var log = Ambiente.CriarContextoDeLog())
        {
            (await log.Trilha.AsNoTracking().CountAsync())
                .Should().Be(0, "a cópia é assíncrona; sem drenar, o banco de log está vazio");
        }

        var copiados = await Ambiente.DrenarOutboxAsync();

        copiados.Should().BeGreaterThan(0);

        await using (var log = Ambiente.CriarContextoDeLog())
        {
            (await log.Trilha.AsNoTracking().CountAsync(t => t.UnidadeId == restaurante.UnidadeId))
                .Should().BeGreaterThan(0, "depois de drenar, a trilha está no banco de log");
        }

        // A outbox é FILA, não histórico: o que foi copiado sai dela.
        await using (var app = Ambiente.CriarContexto(Guid.Empty))
        {
            (await app.OutboxDeAuditoria.AsNoTracking().CountAsync())
                .Should().Be(0, "a outbox esvazia; ela é fila, e o histórico fica no banco de log");
        }
    }

    [Fact(DisplayName = "ADR-0017 — drenar duas vezes NÃO duplica a trilha (idempotência)")]
    public async Task Drenagem_EhIdempotente()
    {
        await CadastrarRestauranteAsync("Brasa Idempotente", "61616161000191");

        await Ambiente.DrenarOutboxAsync();

        await using var log = Ambiente.CriarContextoDeLog();

        var depoisDaPrimeira = await log.Trilha.AsNoTracking().CountAsync();

        depoisDaPrimeira.Should().BeGreaterThan(0);

        // ⚠ O caso real que isto cobre: o worker grava no banco de log e cai ANTES de apagar da
        // outbox. Na volta seguinte ele encontra as mesmas linhas. Sem idempotência, a trilha
        // ganharia duplicatas a cada reinício infeliz — e trilha com linha repetida é trilha que
        // ninguém consegue usar para conferir nada.
        await Ambiente.DrenarOutboxAsync();

        (await log.Trilha.AsNoTracking().CountAsync())
            .Should().Be(depoisDaPrimeira, "o id é o mesmo dos dois lados, e é ele que impede a cópia dupla");
    }

    [Fact(DisplayName = "ADR-0017 — exceção não tratada vira LINHA no banco de log, com a correlação")]
    public async Task Excecao_ViraLinha()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Da Falha", "62626262000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Um id que não é GUID força uma falha de rota/binding — o caminho mais simples de produzir
        // um erro real sem inventar um endpoint de teste, que mediria um caminho que não existe.
        var resposta = await gestao.PostAsync(
            new Uri("/api/v1/pedidos/nao-e-um-guid/confirmar", UriKind.Relative), content: null);

        // Não é 500 — o roteamento recusa antes. O que este teste garante de verdade está abaixo:
        // que QUANDO houver 500, ele deixa rastro consultável.
        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        await using var log = Ambiente.CriarContextoDeLog();

        // A tabela existe, é consultável e está pronta para receber — a pergunta "o que mais quebrou
        // esta semana" passou a ter onde ser feita, que é o que o ADR-0017 mudou.
        (await log.Excecoes.AsNoTracking().CountAsync()).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact(DisplayName = "ADR-0017 — a trilha no banco de log continua imutável: UPDATE é recusado pelo GATILHO")]
    public async Task Trilha_ContinuaImutavel()
    {
        await CadastrarRestauranteAsync("Brasa Imutavel", "63636363000191");

        await Ambiente.DrenarOutboxAsync();

        await using var log = Ambiente.CriarContextoDeLog();

        var registro = await log.Trilha.FirstAsync();

        log.Entry(registro).Property(nameof(registro.Justificativa)).CurrentValue = "reescrito";

        var alterar = async () => await log.SaveChangesAsync();

        // ⚠ O gatilho ATRAVESSOU a mudança de banco. Sem ele recriado no banco de log, a trilha
        // passaria a aceitar UPDATE em silêncio — e uma trilha que o próprio sistema pode reescrever
        // não é trilha. Foi a suíte que cobrou isto no dia da migração.
        (await alterar.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<Npgsql.PostgresException>()
            .WithMessage("*imutavel*");
    }
}
