using System.Net;
using FluentAssertions;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>log_excecao</c> — ADR-0017, a segunda tabela do banco de log.
/// <para>
/// <b>Antes disto, o vestígio de uma falha em produção era a saída de log do contêiner</b> — que o
/// comando de logs trunca e o reinício apaga. Não dava para responder "quantas vezes isso aconteceu
/// esta semana", que é a primeira pergunta de qualquer investigação.
/// </para>
/// <para>
/// ⚠⚠ <b>Como este teste provoca um 500 de verdade:</b> corrompendo o <c>fuso_horario</c> da
/// unidade. Não é um cenário inventado — é o modo de falha real de <c>RN-VEN-048</c>: o cardápio
/// público pergunta "está aberta agora?" convertendo o instante para o fuso DA CASA, e
/// <c>TimeZoneInfo.FindSystemTimeZoneById</c> lança quando o identificador não existe no
/// <c>tzdata</c> do servidor. Um dado ruim gravado por qualquer caminho derruba a vitrine — e o
/// ponto do ADR-0017 é que a derrubada deixe rastro consultável em vez de sumir com o contêiner.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class LogDeExcecaoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "ADR-0017 — falha não tratada vira LINHA em log_excecao, com rota, método e correlação")]
    public async Task FalhaNaoTratada_ViraRegistro()
    {
        var restaurante = await CadastrarRestauranteAsync("Fuso Quebrado", "70707070000191");

        await CorromperFusoAsync(restaurante, "Marte/Olympus_Mons");

        using var anonimo = Ambiente.CriarCliente();

        var resposta = await anonimo.GetAsync(
            new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        await using var log = Ambiente.CriarContextoDeLog();

        var registro = await log.Excecoes.AsNoTracking()
            .OrderByDescending(e => e.OcorridoEm)
            .FirstOrDefaultAsync();

        registro.Should().NotBeNull("a falha precisa sobreviver ao reinício do contêiner");

        registro!.Tipo.Should().Contain("TimeZone");
        registro.Mensagem.Should().NotBeNullOrWhiteSpace();
        registro.Pilha.Should().NotBeNullOrWhiteSpace("sem a pilha não há por onde começar");
        registro.Rota.Should().Be($"/api/publico/{restaurante.Slug}/cardapio");
        registro.Metodo.Should().Be("GET");
        registro.StatusHttp.Should().Be(500);
        registro.OcorridoEm.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // ⚠ É a correlação que liga esta linha ao erro que o usuário viu: o corpo do
        // `ProblemDetails` carrega o mesmo valor, e é por ele que o suporte encontra a falha sem
        // adivinhar horário.
        registro.Correlacao.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// ⚠ A rota anônima é o caso que a modelagem precisou acomodar: boa parte das exceções acontece
    /// <b>antes</b> de haver unidade e usuário no contexto — no login, no cardápio público, na
    /// própria resolução do slug. Se <c>unidade_id</c> fosse obrigatório, justamente as falhas mais
    /// externas não teriam onde ser gravadas.
    /// </summary>
    [Fact(DisplayName = "ADR-0017 — a exceção de rota anônima é gravada mesmo sem usuário no contexto")]
    public async Task FalhaAnonima_GravaSemUsuario()
    {
        var restaurante = await CadastrarRestauranteAsync("Sem Sessao", "80808080000191");

        await CorromperFusoAsync(restaurante, "Fuso/Inexistente");

        using var anonimo = Ambiente.CriarCliente();

        await anonimo.GetAsync(new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        await using var log = Ambiente.CriarContextoDeLog();

        var registro = await log.Excecoes.AsNoTracking()
            .OrderByDescending(e => e.OcorridoEm)
            .FirstAsync();

        registro.UsuarioId.Should().BeNull("ninguém estava autenticado, e isso não impede o registro");
    }

    [Fact(DisplayName = "ADR-0017 — 4xx NÃO entra em log_excecao: recusa de regra não é falha do sistema")]
    public async Task Recusa_NaoEntraNoLog()
    {
        await CadastrarRestauranteAsync("Recusa Burger", "90909090000191");

        using var anonimo = Ambiente.CriarCliente();

        // 404 de casa inexistente: operação recusada, e recusa é comportamento previsto.
        (await anonimo.GetAsync(new Uri("/api/publico/casa-que-nao-existe/cardapio", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var log = Ambiente.CriarContextoDeLog();

        // ⚠ Se 4xx entrasse aqui, a tabela viraria ruído: toda senha errada e todo pedido de casa
        // fechada apareceriam como "falha do sistema", e a pergunta "o que quebrou?" deixaria de
        // ter resposta útil. A recusa já vive na TRILHA, que é outro banco e outra pergunta.
        (await log.Excecoes.AsNoTracking().CountAsync())
            .Should().Be(0, "recusa de regra não é exceção; ela não deve poluir o log de falhas");
    }

    /// <summary>
    /// Corrompe o fuso da casa e cadastra a semana.
    /// <para>
    /// ⚠⚠ <b>A semana é obrigatória para o teste falhar</b>, e descobri-lo custou uma rodada:
    /// <c>ApresentacaoDaCasa.AbertaEm</c> devolve <c>true</c> antes de qualquer conversão quando a
    /// casa não tem horário cadastrado (<c>RN-GER-036</c> — semana vazia responde ABERTA, para a
    /// vitrine não sumir por causa de um cadastro que ninguém pediu). Sem as sete linhas abaixo,
    /// <c>FindSystemTimeZoneById</c> nunca é chamado e a resposta é 200.
    /// </para>
    /// <para>
    /// ⚠ <c>ExecuteSqlRaw</c> e não o domínio: <c>Unidade</c> recusa fuso inválido, e é isso que se
    /// quer — a única forma de a linha ficar assim é por caminho que não passa pela regra. O teste
    /// simula o DADO ruim, não a regra frouxa.
    /// </para>
    /// </summary>
    private async Task CorromperFusoAsync(RestauranteDeTeste restaurante, string fuso)
    {
        await using var db = Contexto(restaurante);

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE unidade SET fuso_horario = {0} WHERE id = {1}", fuso, restaurante.UnidadeId);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO unidade_horario (id, unidade_id, dia_semana, abre, fecha)
            SELECT gen_random_uuid(), {0}, dia, TIME '10:00', TIME '23:00'
              FROM generate_series(0, 6) AS dia
             WHERE NOT EXISTS (SELECT 1 FROM unidade_horario h WHERE h.unidade_id = {0})
            """,
            restaurante.UnidadeId);
    }
}
