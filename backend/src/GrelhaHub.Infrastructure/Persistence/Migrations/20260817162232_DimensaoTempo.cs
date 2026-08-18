using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>SPEC-GER-002</c> — <c>dim_tempo</c>, a dimensão tempo do dia operacional
    /// (<c>RN-GER-006</c> ⚠⚠, ADR-0011).
    /// <para>
    /// Uma linha por dia operacional, para que toda apuração e todo relatório do módulo
    /// analítico (onda 9) façam <c>JOIN</c> em uma dimensão pronta em vez de recalcular a
    /// virada das 04h00 em cada consulta — que é como o erro se espalha.
    /// </para>
    /// <para>
    /// <b>A hora de virada está cravada em 4 horas aqui</b>, igual à coluna gerada
    /// <c>data_operacional</c>. Migration é história: ela descreve o que foi aplicado, e não
    /// pode mudar de significado porque uma constante do C# mudou. Alterar a virada é uma
    /// migration nova, com recomputação explícita do histórico.
    /// </para>
    /// </summary>
    public partial class DimensaoTempo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intervalo fixo, de 2024 (antes do projeto existir) a 2040.
            //
            // Por que fixo e não `now() + N anos`: uma definição que depende de `now()` faz a
            // view mudar de conteúdo a cada REFRESH, e o teste de um dia específico passaria a
            // depender da data em que roda. 6.210 linhas cabem em qualquer lugar, e estender o
            // intervalo é uma migration — decisão explícita, como deve ser.
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW dim_tempo AS
                WITH dias AS (
                    SELECT generate_series(DATE '2024-01-01', DATE '2040-12-31', INTERVAL '1 day')::date AS data_operacional
                )
                SELECT
                    d.data_operacional,

                    -- ISODOW: segunda = 1 ... domingo = 7. `dow` começaria no domingo com 0,
                    -- e a semana brasileira de operação de restaurante começa na segunda.
                    EXTRACT(ISODOW FROM d.data_operacional)::smallint      AS dia_da_semana,

                    -- CASE e não to_char: to_char depende de lc_time do servidor, e o nome do
                    -- dia sairia em inglês num contêiner com locale padrão.
                    CASE EXTRACT(ISODOW FROM d.data_operacional)
                        WHEN 1 THEN 'segunda-feira'
                        WHEN 2 THEN 'terça-feira'
                        WHEN 3 THEN 'quarta-feira'
                        WHEN 4 THEN 'quinta-feira'
                        WHEN 5 THEN 'sexta-feira'
                        WHEN 6 THEN 'sábado'
                        ELSE        'domingo'
                    END                                                     AS nome_dia_semana,

                    EXTRACT(ISODOW FROM d.data_operacional) IN (6, 7)       AS eh_fim_de_semana,

                    EXTRACT(WEEK    FROM d.data_operacional)::smallint      AS semana_do_ano,
                    EXTRACT(MONTH   FROM d.data_operacional)::smallint      AS mes,
                    EXTRACT(QUARTER FROM d.data_operacional)::smallint      AS trimestre,
                    EXTRACT(YEAR    FROM d.data_operacional)::smallint      AS ano,

                    -- Limites exatos do dia operacional, já em UTC.
                    --
                    -- Existem para que consulta por período use índice em dh_criacao sem
                    -- recalcular a virada: `dh_criacao >= inicio_utc AND dh_criacao < fim_utc`.
                    --
                    -- AT TIME ZONE sobre timestamp sem fuso interpreta o valor COMO horário de
                    -- São Paulo e devolve timestamptz — então o deslocamento histórico do dia
                    -- é respeitado, inclusive em datas com horário de verão.
                    ((d.data_operacional + TIME '04:00') AT TIME ZONE 'America/Sao_Paulo')            AS inicio_utc,

                    -- EXCLUSIVO: é o início do dia operacional seguinte.
                    --
                    -- Limite exclusivo de propósito. Um `fim_utc` inclusivo obrigaria a escolher
                    -- entre 03:59:59 e 03:59:59.999999, e a venda registrada na fração restante
                    -- desapareceria da apuração sem ninguém notar.
                    (((d.data_operacional + 1) + TIME '04:00') AT TIME ZONE 'America/Sao_Paulo')     AS fim_utc
                FROM dias d
                """);

            // UNIQUE, e não só um índice: é o que permite REFRESH MATERIALIZED VIEW
            // CONCURRENTLY, que não bloqueia leitura enquanto atualiza.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_dim_tempo ON dim_tempo (data_operacional)");

            migrationBuilder.Sql(
                "CREATE INDEX ix_dim_tempo_intervalo ON dim_tempo (inicio_utc, fim_utc)");

            migrationBuilder.Sql("COMMENT ON MATERIALIZED VIEW dim_tempo IS " +
                "'SPEC-GER-002 / RN-GER-006 — dimensão do dia operacional (04h00 as 03h59). " +
                "fim_utc é EXCLUSIVO. Intervalo 2024-2040; estender exige migration.'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS dim_tempo");
    }
}
