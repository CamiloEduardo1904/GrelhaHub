using System.Data.Common;
using FluentAssertions;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-002</c> — <c>dim_tempo</c>, a dimensão do dia operacional (<c>RN-GER-006</c> ⚠⚠).
/// <para>
/// O teste que decide se a dimensão presta é o de <b>coerência com a coluna gerada</b>: um
/// pedido cujo <c>dh_criacao</c> caia dentro de <c>[inicio_utc, fim_utc)</c> tem de ter
/// <c>data_operacional</c> igual àquele dia. Se a dimensão e a coluna discordarem, o relatório
/// e o fechamento de caixa passam a contar coisas diferentes — sem erro, com números
/// plausíveis, que é o modo de falha desta regra.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class DimensaoTempoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-006 — dim_tempo tem uma linha por dia operacional, sem falha nem repetição")]
    public async Task DimTempo_TemUmaLinhaPorDia()
    {
        await using var db = Ambiente.CriarContexto(Guid.Empty);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT count(*),
                   min(data_operacional),
                   max(data_operacional),
                   count(DISTINCT data_operacional)
              FROM dim_tempo
            """;

        await using var leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();

        var total = leitor.GetInt64(0);
        var primeiro = leitor.GetFieldValue<DateOnly>(1);
        var ultimo = leitor.GetFieldValue<DateOnly>(2);
        var distintos = leitor.GetInt64(3);

        primeiro.Should().Be(new DateOnly(2024, 1, 1));
        ultimo.Should().Be(new DateOnly(2040, 12, 31));

        distintos.Should().Be(total, "data_operacional é chave: não pode repetir");

        // Dias entre as duas pontas, inclusive. Se a série tiver falha, a contagem não fecha.
        var esperado = ultimo.DayNumber - primeiro.DayNumber + 1;
        total.Should().Be(esperado, "a série de dias não pode ter buraco");
    }

    [Theory(DisplayName = "RN-GER-006 — dim_tempo classifica o dia da semana em português e marca o fim de semana")]
    [InlineData("2026-08-15", 6, "sábado", true)]
    [InlineData("2026-08-16", 7, "domingo", true)]
    [InlineData("2026-08-17", 1, "segunda-feira", false)]
    [InlineData("2026-08-21", 5, "sexta-feira", false)]
    public async Task DimTempo_ClassificaODia(string dia, int diaDaSemana, string nome, bool fimDeSemana)
    {
        await using var db = Ambiente.CriarContexto(Guid.Empty);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT dia_da_semana, nome_dia_semana, eh_fim_de_semana, mes, trimestre, ano
              FROM dim_tempo WHERE data_operacional = @dia
            """;

        Adicionar(comando, "dia", DateOnly.Parse(dia, System.Globalization.CultureInfo.InvariantCulture));

        await using var leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue($"{dia} precisa existir na dimensão");

        leitor.GetInt16(0).Should().Be((short)diaDaSemana, "ISODOW: segunda = 1, domingo = 7");
        leitor.GetString(1).Should().Be(nome, "o nome vem de CASE, não de to_char — locale do servidor não influi");
        leitor.GetBoolean(2).Should().Be(fimDeSemana);
        leitor.GetInt16(3).Should().Be(8);
        leitor.GetInt16(4).Should().Be(3);
        leitor.GetInt16(5).Should().Be(2026);
    }

    [Fact(DisplayName = "RN-GER-006 — inicio_utc e fim_utc delimitam o dia operacional das 04h00 às 03h59")]
    public async Task DimTempo_DelimitaODiaOperacional()
    {
        var limites = await LimitesAsync(new DateOnly(2026, 8, 15));

        // 04h00 de 15/08 em São Paulo (-03:00) é 07h00 UTC.
        limites.Inicio.Should().Be(new DateTimeOffset(2026, 8, 15, 7, 0, 0, TimeSpan.Zero));

        // fim_utc é EXCLUSIVO: é o início do dia seguinte.
        limites.Fim.Should().Be(new DateTimeOffset(2026, 8, 16, 7, 0, 0, TimeSpan.Zero));

        (limites.Fim - limites.Inicio).Should().Be(TimeSpan.FromHours(24));
    }

    [Theory(DisplayName = "RN-GER-006 — a expressão de limite usa o deslocamento histórico do fuso, não -03:00 fixo")]
    [InlineData("2018-02-10", "2018-02-10T06:00:00Z")]  // horário de verão vigente: -02:00
    [InlineData("2018-06-10", "2018-06-10T07:00:00Z")]  // sem horário de verão: -03:00
    [InlineData("2026-08-15", "2026-08-15T07:00:00Z")]
    public async Task DimTempo_ExpressaoRespeitaHorarioDeVerao(string dia, string inicioEsperado)
    {
        // Avalia a MESMA expressão de `inicio_utc`, e não uma linha de dim_tempo, porque a
        // dimensão começa em 2024 e o Brasil não tem horário de verão desde 2019 — nenhuma
        // linha real exercita o caso. Estender o intervalo da dimensão para trás só para
        // acomodar o teste seria inventar calendário de anos em que o negócio não existia.
        //
        // O que precisa não regredir é a expressão: trocar `AT TIME ZONE` por aritmética de
        // fuso fixo derruba este teste. Dados históricos e a possibilidade de o horário de
        // verão voltar tornam a conversão fixa uma bomba-relógio (CA-5 da SPEC-GER-002).
        await using var db = Ambiente.CriarContexto(Guid.Empty);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT ((@dia::date + TIME '04:00') AT TIME ZONE 'America/Sao_Paulo')";

        Adicionar(comando, "dia", DateOnly.Parse(dia, System.Globalization.CultureInfo.InvariantCulture));

        var inicio = (DateTime?)await comando.ExecuteScalarAsync();

        new DateTimeOffset(inicio!.Value, TimeSpan.Zero)
            .Should().Be(DateTimeOffset.Parse(inicioEsperado, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "RN-GER-006 — o intervalo de dim_tempo concorda com a coluna gerada data_operacional")]
    public async Task DimTempo_ConcordaComAColunaGerada()
    {
        var restaurante = await CadastrarRestauranteAsync("Dimensao Burger", "11111111000191");

        var dia = new DateOnly(2026, 8, 15);
        var limites = await LimitesAsync(dia);

        // Quatro instantes dentro do dia operacional, incluindo as duas bordas.
        var dentro = new[]
        {
            limites.Inicio,                                 // 04h00 em ponto
            limites.Inicio.AddHours(12),                    // meio da tarde
            limites.Inicio.AddHours(21).AddMinutes(30),      // 01h30 do dia civil seguinte
            limites.Fim.AddTicks(-1),                       // último instante do dia operacional
        };

        await InserirPedidosAsync(restaurante.UnidadeId, dentro);

        await using var db = Contexto(restaurante);

        var dias = await db.Pedidos.AsNoTracking()
            .OrderBy(p => p.Numero)
            .Select(p => p.DataOperacional)
            .ToListAsync();

        dias.Should().OnlyContain(
            valor => valor == dia,
            "todo instante em [inicio_utc, fim_utc) pertence ao mesmo dia operacional");

        // E o instante EXATAMENTE em fim_utc já é do dia seguinte — é o que torna o limite
        // exclusivo correto em vez de conveniente.
        await InserirPedidosAsync(restaurante.UnidadeId, [limites.Fim], numeroInicial: 100);

        await using var conferencia = Contexto(restaurante);

        var doDiaSeguinte = await conferencia.Pedidos.AsNoTracking()
            .Where(p => p.Numero == 100)
            .Select(p => p.DataOperacional)
            .SingleAsync();

        doDiaSeguinte.Should().Be(dia.AddDays(1));
    }

    [Fact(DisplayName = "RN-GER-006 — dim_tempo aceita REFRESH CONCURRENTLY, sem bloquear leitura")]
    public async Task DimTempo_AceitaRefreshConcorrente()
    {
        await using var db = Ambiente.CriarContexto(Guid.Empty);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        // Só é possível porque existe índice UNIQUE em data_operacional. Sem ele o PostgreSQL
        // recusa o CONCURRENTLY, e o refresh passaria a bloquear quem estivesse lendo.
        using var comando = conexao.CreateCommand();
        comando.CommandText = "REFRESH MATERIALIZED VIEW CONCURRENTLY dim_tempo";

        var refresh = async () => await comando.ExecuteNonQueryAsync();

        await refresh.Should().NotThrowAsync();
    }

    private async Task<(DateTimeOffset Inicio, DateTimeOffset Fim)> LimitesAsync(DateOnly dia)
    {
        await using var db = Ambiente.CriarContexto(Guid.Empty);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT inicio_utc, fim_utc FROM dim_tempo WHERE data_operacional = @dia";

        Adicionar(comando, "dia", dia);

        await using var leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue($"{dia:yyyy-MM-dd} precisa existir na dimensão");

        return (leitor.GetFieldValue<DateTimeOffset>(0), leitor.GetFieldValue<DateTimeOffset>(1));
    }

    private async Task InserirPedidosAsync(
        Guid unidadeId, DateTimeOffset[] instantes, int numeroInicial = 1)
    {
        await using var db = Ambiente.CriarContexto(unidadeId);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        for (var indice = 0; indice < instantes.Length; indice++)
        {
            using var comando = conexao.CreateCommand();
            comando.CommandText = """
                INSERT INTO pedido (id, unidade_id, numero, canal, status, dh_criacao)
                VALUES (gen_random_uuid(), @unidade, @numero, 3, 1, @criacao)
                """;

            Adicionar(comando, "unidade", unidadeId);
            Adicionar(comando, "numero", numeroInicial + indice);
            Adicionar(comando, "criacao", instantes[indice].ToUniversalTime());

            await comando.ExecuteNonQueryAsync();
        }
    }

    private static void Adicionar(DbCommand comando, string nome, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
