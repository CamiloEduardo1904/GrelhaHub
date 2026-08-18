using System.Data.Common;
using System.Globalization;
using FluentAssertions;
using GrelhaHub.Domain.Common;
using GrelhaHub.Infrastructure.Persistence;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-001</c> — <b>CA-4</b>: quantidade de insumo em <c>numeric(14,4)</c>.
/// <para>
/// <b>O problema que este arquivo resolve.</b> Nenhuma tabela do modelo tem coluna de
/// quantidade ainda — <c>ficha_tecnica_item</c> e <c>movimento_estoque</c> nascem nas ondas 2 e
/// 3. Sem alguma forma de exercitar a convenção antes disso, o CA-4 ficaria em aberto
/// justamente até o momento em que errar já teria custado a remodelagem.
/// </para>
/// <para>
/// <b>Como fecha.</b> A DDL da tabela de prova é <b>derivada do próprio modelo</b>: o teste lê a
/// precisão e a escala que <see cref="ConvencoesDeTipo"/> atribui a uma propriedade
/// <see cref="Quantidade"/> e cria a coluna com esses valores. Se alguém trocar a convenção
/// para a precisão do dinheiro, a coluna nasce <c>numeric(12,2)</c>, o PostgreSQL arredonda
/// <c>0,15625</c> para <c>0,16</c> e o teste fica vermelho. É a convenção sob teste, não uma
/// constante repetida aqui.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class PrecisaoDeQuantidadeTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string Tabela = "teste_ca4_quantidade";

    [Fact(DisplayName = "RN-GER-004 — a convenção atribui numeric(14,4) a toda propriedade Quantidade")]
    public void CA4_AConvencaoAtribuiQuatroCasas()
    {
        using var contexto = ContextoDeConvencoes();

        var propriedade = contexto.Model
            .FindEntityType(typeof(LinhaDeProva))!
            .FindProperty(nameof(LinhaDeProva.Consumo))!;

        propriedade.GetPrecision().Should().Be(ConvencoesDeTipo.PrecisaoDeQuantidade);
        propriedade.GetScale().Should().Be(
            Quantidade.CasasDecimais,
            "quatro casas: gramas e mililitros não cabem em duas");

        propriedade.GetScale().Should().NotBe(
            Dinheiro.CasasDecimais, "quantidade não herda a precisão do dinheiro");
    }

    [Fact(DisplayName = "RN-GER-004 — a coluna que a convenção gera preserva 0,15625 como 0,1563 no PostgreSQL")]
    public async Task CA4_AColunaPreservaAsQuatroCasasNoBanco()
    {
        using var modelo = ContextoDeConvencoes();

        var propriedade = modelo.Model
            .FindEntityType(typeof(LinhaDeProva))!
            .FindProperty(nameof(LinhaDeProva.Consumo))!;

        var precisao = propriedade.GetPrecision()!.Value;
        var escala = propriedade.GetScale()!.Value;

        await using var db = Ambiente.CriarContexto(Guid.Empty);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        try
        {
            // A DDL vem do modelo. Trocar a convenção muda a coluna e quebra a asserção.
            await ExecutarAsync(
                conexao,
                $"CREATE TABLE {Tabela} (id uuid PRIMARY KEY, consumo numeric({precisao},{escala}))");

            // 0,15625 é o resultado típico de `líquida ÷ (1 − perda)` — o caso do CA-4.
            await ExecutarAsync(
                conexao,
                $"INSERT INTO {Tabela} (id, consumo) VALUES (gen_random_uuid(), 0.15625)");

            using var leitura = conexao.CreateCommand();
            leitura.CommandText = $"SELECT consumo FROM {Tabela}";

            var gravado = (decimal?)await leitura.ExecuteScalarAsync();

            gravado.Should().Be(
                0.1563m,
                "o PostgreSQL arredonda para a escala da coluna; com quatro casas o valor sobrevive");

            gravado.Should().NotBe(
                0.16m,
                "0,16 é o que a precisão do dinheiro produziria — o desvio que entra no CMV em silêncio");

            // A aplicação e o banco precisam concordar, como em RN-GER-006.
            Quantidade.De(0.15625m).Valor.Should().Be(gravado!.Value);
        }
        finally
        {
            await ExecutarAsync(conexao, $"DROP TABLE IF EXISTS {Tabela}");
        }
    }

    [Fact(DisplayName = "RN-GER-004 — a coluna recusa valor acima da precisão declarada")]
    public async Task CA4_ColunaRecusaValorForaDaPrecisao()
    {
        using var modelo = ContextoDeConvencoes();

        var propriedade = modelo.Model
            .FindEntityType(typeof(LinhaDeProva))!
            .FindProperty(nameof(LinhaDeProva.Consumo))!;

        var precisao = propriedade.GetPrecision()!.Value;
        var escala = propriedade.GetScale()!.Value;

        await using var db = Ambiente.CriarContexto(Guid.Empty);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        try
        {
            await ExecutarAsync(
                conexao,
                $"CREATE TABLE {Tabela} (id uuid PRIMARY KEY, consumo numeric({precisao},{escala}))");

            // 14 dígitos com 4 decimais: 10 na parte inteira. Onze estouram.
            var acimaDoLimite = new string('9', precisao - escala + 1);

            var escrita = () => ExecutarAsync(
                conexao,
                $"INSERT INTO {Tabela} (id, consumo) VALUES (gen_random_uuid(), {acimaDoLimite})");

            (await escrita.Should().ThrowAsync<DbException>())
                .Which.Message.Should().ContainEquivalentOf("numeric");
        }
        finally
        {
            await ExecutarAsync(conexao, $"DROP TABLE IF EXISTS {Tabela}");
        }
    }

    private static async Task ExecutarAsync(DbConnection conexao, string sql)
    {
        using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Contexto mínimo que aplica <b>as mesmas</b> convenções da produção. Não abre conexão: o
    /// modelo é construído a partir dos metadados.
    /// </summary>
    private ContextoDeProva ContextoDeConvencoes()
    {
        var opcoes = new DbContextOptionsBuilder<ContextoDeProva>()
            .UseNpgsql(Ambiente.StringDeConexao)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ContextoDeProva(opcoes);
    }

    /// <summary>Entidade que existe só para dar à convenção uma propriedade para configurar.</summary>
    internal sealed class LinhaDeProva
    {
        public Guid Id { get; set; }

        public Quantidade Consumo { get; set; }
    }

    internal sealed class ContextoDeProva(DbContextOptions<ContextoDeProva> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
            ConvencoesDeTipo.Aplicar(configurationBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<LinhaDeProva>(entidade =>
            {
                entidade.ToTable(Tabela);
                entidade.HasKey(linha => linha.Id);
            });
    }
}
