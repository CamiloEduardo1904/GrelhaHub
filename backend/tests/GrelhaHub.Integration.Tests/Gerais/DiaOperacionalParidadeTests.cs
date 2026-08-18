using System.Data.Common;
using System.Globalization;
using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-002</c> — CA-2, CA-3, CA-4 e CA-5.
/// <para>
/// <b>O teste que o ADR-0009 obriga a existir.</b> A mesma regra (<c>RN-GER-006</c>) está
/// escrita duas vezes: em C#, em <c>DiaOperacional.De()</c>, para a aplicação exibir e
/// filtrar; e em SQL, na coluna gerada <c>data_operacional</c>, que é a fonte de verdade da
/// agregação. Duas implementações da mesma regra podem divergir — e o modo de falha do
/// dia operacional não é erro, é número plausível. Este arquivo é o que impede a divergência
/// de passar em silêncio.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class DiaOperacionalParidadeTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    /// <summary>Fuso da operação — <c>RN-GER-005</c>. Em agosto o Brasil não tem horário de verão.</summary>
    private static readonly TimeSpan OffsetEmAgostoDe2026 = TimeSpan.FromHours(-3);

    [Fact(DisplayName = "RN-GER-006 — o cálculo em C# e a coluna gerada no PostgreSQL concordam nas 96 amostras do dia")]
    public async Task Paridade_EntreCSharpEBanco_EmTodasAsAmostrasDoDia()
    {
        var restaurante = await CadastrarRestauranteAsync("Paridade Burger", "11111111000191");

        var relogio = RelogioDaApi();

        // 96 amostras: as 24 horas em intervalos de 15 minutos, começando à meia-noite do
        // sábado 15/08/2026. Cobre os dois lados da virada das 04h00.
        var inicio = new DateTimeOffset(2026, 8, 15, 0, 0, 0, OffsetEmAgostoDe2026);

        var amostras = Enumerable.Range(0, 96)
            .Select(passo => inicio.AddMinutes(15 * passo).ToUniversalTime())
            .ToList();

        await InserirPedidosAsync(restaurante.UnidadeId, amostras);

        var doBanco = await LerDiasOperacionaisAsync(restaurante.UnidadeId);

        doBanco.Should().HaveCount(amostras.Count, "todo pedido inserido precisa ter voltado");

        var divergencias = amostras
            .Select((instante, indice) => new
            {
                Instante = instante,
                Aplicacao = relogio.DiaOperacional(instante),
                Banco = doBanco[indice],
            })
            .Where(linha => linha.Aplicacao != linha.Banco)
            .Select(linha => string.Create(
                CultureInfo.InvariantCulture,
                $"{linha.Instante:yyyy-MM-dd HH:mm}Z → aplicação {linha.Aplicacao:yyyy-MM-dd}, banco {linha.Banco:yyyy-MM-dd}"))
            .ToList();

        divergencias.Should().BeEmpty(
            "C# e PostgreSQL implementam a mesma RN-GER-006 e não podem discordar em nenhum instante");
    }

    [Fact(DisplayName = "RN-GER-006 — a virada às 04h00 é o limite exato na coluna gerada")]
    public async Task Virada_AsQuatroDaManha_EhOLimiteExato()
    {
        var restaurante = await CadastrarRestauranteAsync("Virada Burger", "22222222000192");

        // Domingo 16/08/2026: um pedido às 03h59 e outro às 04h00.
        var tresCinquentaENove = new DateTimeOffset(2026, 8, 16, 3, 59, 0, OffsetEmAgostoDe2026);
        var quatroEmPonto = new DateTimeOffset(2026, 8, 16, 4, 0, 0, OffsetEmAgostoDe2026);

        await InserirPedidosAsync(
            restaurante.UnidadeId,
            [tresCinquentaENove.ToUniversalTime(), quatroEmPonto.ToUniversalTime()]);

        var dias = await LerDiasOperacionaisAsync(restaurante.UnidadeId);

        dias[0].Should().Be(new DateOnly(2026, 8, 15), "03h59 de domingo ainda é o sábado operacional");
        dias[1].Should().Be(new DateOnly(2026, 8, 16), "04h00 abre o domingo operacional");
    }

    [Fact(DisplayName = "RN-GER-006 — o cálculo usa o deslocamento histórico do fuso, não -03:00 fixo")]
    public async Task HorarioDeVerao_UsaODeslocamentoDaData()
    {
        var restaurante = await CadastrarRestauranteAsync("Verao Burger", "33333333000193");

        var relogio = RelogioDaApi();

        // 10/02/2018, 04h30 em São Paulo — havia horário de verão, e o deslocamento era -02:00.
        //
        // Este instante discrimina a implementação certa da errada: quem usasse -03:00 fixo
        // leria 03h30 local, ficaria ANTES da virada e devolveria 09/02. Com o deslocamento
        // histórico correto, são 04h30 locais e o dia operacional é 10/02.
        var duranteOHorarioDeVerao =
            new DateTimeOffset(2018, 2, 10, 4, 30, 0, TimeSpan.FromHours(-2)).ToUniversalTime();

        await InserirPedidosAsync(restaurante.UnidadeId, [duranteOHorarioDeVerao]);

        var dias = await LerDiasOperacionaisAsync(restaurante.UnidadeId);

        dias[0].Should().Be(
            new DateOnly(2018, 2, 10),
            "com -02:00 vigente na data, 04h30 local já é o próprio dia operacional");

        relogio.DiaOperacional(duranteOHorarioDeVerao).Should().Be(
            dias[0],
            "TimeZoneInfo e AT TIME ZONE precisam concordar também em data com horário de verão");
    }

    [Fact(DisplayName = "RN-GER-006 — data_operacional é coluna gerada: o banco recusa escrita da aplicação")]
    public async Task DataOperacional_NaoAceitaValorAtribuido()
    {
        var restaurante = await CadastrarRestauranteAsync("Gerada Burger", "44444444000194");

        await using var db = Contexto(restaurante);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        comando.CommandText = """
            INSERT INTO pedido (id, unidade_id, numero, canal, status, dh_criacao, data_operacional)
            VALUES (gen_random_uuid(), @unidade, 999, 3, 1, now(), DATE '1999-01-01')
            """;

        Adicionar(comando, "unidade", restaurante.UnidadeId);

        var escrita = async () => await comando.ExecuteNonQueryAsync();

        // A garantia é estrutural: não existe caminho para a aplicação mentir sobre o dia
        // operacional, nem por descuido nem de propósito.
        (await escrita.Should().ThrowAsync<DbException>())
            .Which.Message.Should().Contain("data_operacional");
    }

    private IRelogio RelogioDaApi()
    {
        // O IRelogio real, resolvido do contêiner da API: se alguém trocar a implementação
        // registrada, este teste passa a medir a nova — que é o que se quer.
        using var escopo = Ambiente.CriarEscopo();
        return escopo.ServiceProvider.GetRequiredService<IRelogio>();
    }

    /// <summary>
    /// Insere pedidos com <c>dh_criacao</c> controlado, por SQL.
    /// <para>
    /// Vai por SQL porque <c>Pedido.CriadoEm</c> é atribuído no construtor e não tem setter —
    /// e está certo que não tenha. Para exercitar a coluna gerada em 96 instantes diferentes,
    /// o único caminho é escrever o instante direto na tabela real. <c>data_operacional</c>
    /// continua sendo calculada pelo banco: é exatamente o que está sob teste.
    /// </para>
    /// </summary>
    private async Task InserirPedidosAsync(Guid unidadeId, List<DateTimeOffset> instantes)
    {
        await using var db = Ambiente.CriarContexto(unidadeId);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        for (var indice = 0; indice < instantes.Count; indice++)
        {
            using var comando = conexao.CreateCommand();

            comando.CommandText = """
                INSERT INTO pedido (id, unidade_id, numero, canal, status, dh_criacao)
                VALUES (gen_random_uuid(), @unidade, @numero, 3, 1, @criacao)
                """;

            Adicionar(comando, "unidade", unidadeId);
            Adicionar(comando, "numero", indice + 1);
            Adicionar(comando, "criacao", instantes[indice]);

            await comando.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Dias operacionais calculados pelo banco, na ordem em que foram inseridos.</summary>
    private async Task<List<DateOnly>> LerDiasOperacionaisAsync(Guid unidadeId)
    {
        await using var db = Ambiente.CriarContexto(unidadeId);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        comando.CommandText = "SELECT data_operacional FROM pedido ORDER BY numero";

        var dias = new List<DateOnly>();

        await using var leitor = await comando.ExecuteReaderAsync();

        while (await leitor.ReadAsync())
        {
            dias.Add(leitor.GetFieldValue<DateOnly>(0));
        }

        return dias;
    }

    private static void Adicionar(DbCommand comando, string nome, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
