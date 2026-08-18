using System.Data.Common;
using FluentAssertions;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Crm;
using GrelhaHub.Infrastructure.Persistence;
using GrelhaHub.Infrastructure.Persistence.Interceptors;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-001</c> — CA-1 a CA-6: o vínculo a unidade e as convenções que <b>todas</b> as
/// tabelas seguintes vão herdar.
/// <para>
/// Esta spec quase não entrega funcionalidade visível. Entrega o formato de 40 tabelas — e é a
/// spec cujo erro sai mais caro, porque uma casa decimal errada aqui se propaga por todas
/// elas. Por isso as asserções aqui olham o <b>catálogo do PostgreSQL</b>, não só o
/// comportamento: o que precisa estar certo é o tipo da coluna.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ConvencoesDoModeloTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-009 — o banco recusa registro transacional sem unidade")]
    public async Task CA1_SemUnidade_OBancoRecusa()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        await using var db = Contexto(restaurante);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        comando.CommandText = """
            INSERT INTO cliente (id, nome, telefone, dh_criacao)
            VALUES (gen_random_uuid(), 'Sem Unidade', '11999998888', now())
            """;

        var escrita = async () => await comando.ExecuteNonQueryAsync();

        (await escrita.Should().ThrowAsync<DbException>())
            .Which.Message.Should().Contain("unidade_id");
    }

    [Fact(DisplayName = "RN-GER-009 — o interceptor preenche unidade_id: o handler nunca a atribui")]
    public async Task CA1_OInterceptorPreencheAUnidade()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        await using var db = Contexto(restaurante);

        // Guid.Empty de propósito: é o esquecimento que o interceptor existe para cobrir.
        // Sem ele, este registro seria gravado órfão e desapareceria do filtro global — a
        // gravação pareceria ter falhado quando na verdade criou um fantasma.
        db.Clientes.Add(new Cliente(Guid.Empty, "Cliente do Interceptor", "11977776666"));

        await db.SaveChangesAsync();

        await using var conferencia = Contexto(restaurante);

        var cliente = await conferencia.Clientes.AsNoTracking().SingleAsync();

        cliente.UnidadeId.Should().Be(restaurante.UnidadeId);
    }

    [Fact(DisplayName = "RN-GER-009 — a API registra o interceptor de unidade no seu DbContext")]
    public void CA1_OInterceptorEstaLigadoNaApi()
    {
        // Os outros casos exercitam o interceptor pelo contexto do teste, o que prova a
        // CLASSE e não a FIAÇÃO. Nenhum fluxo da API cria entidade sem unidade hoje — os
        // handlers a informam —, então sem esta asserção a registração em
        // AdicionarInfrastructure poderia ser removida sem nenhum teste ficar vermelho.
        using var escopo = Ambiente.CriarEscopo();

        var db = escopo.ServiceProvider.GetRequiredService<GrelhaHubDbContext>();

        // Interceptor adicionado por `AddInterceptors` vive na CoreOptionsExtension das opções,
        // não no contêiner interno do EF Core — resolver `IEnumerable<IInterceptor>` devolve
        // vazio mesmo quando a fiação está correta.
        var interceptors = db.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()!
            .Interceptors ?? [];

        interceptors.Should().ContainSingle(interceptor => interceptor is InterceptorDeUnidade);
    }

    [Fact(DisplayName = "RN-GER-009 — gravar sem unidade no escopo falha de forma ruidosa")]
    public async Task CA1_SemUnidadeNoEscopo_FalhaRuidosa()
    {
        await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        // Contexto sem unidade: nem a entidade nem o escopo sabem a qual unidade pertencem.
        await using var db = Ambiente.CriarContexto(Guid.Empty);

        db.Clientes.Add(new Cliente(Guid.Empty, "Cliente Órfão", "11966665555"));

        var gravar = async () => await db.SaveChangesAsync();

        (await gravar.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*RN-GER-009*");
    }

    [Fact(DisplayName = "RN-GER-009 — consulta sem filtro explícito não vê dado de outra unidade")]
    public async Task CA2_IsolamentoEntreUnidades()
    {
        var brasa = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var fumaca = await CadastrarRestauranteAsync("Fumaca Negra", "22222222000192");

        await using (var db = Contexto(brasa))
        {
            db.Clientes.Add(new Cliente(brasa.UnidadeId, "Cliente do Brasa", "11911111111"));
            await db.SaveChangesAsync();
        }

        await using (var db = Contexto(fumaca))
        {
            db.Clientes.Add(new Cliente(fumaca.UnidadeId, "Cliente da Fumaca", "11922222222"));
            await db.SaveChangesAsync();
        }

        await using var doBrasa = Contexto(brasa);

        // Sem WHERE de unidade: o filtro global é que precisa fazer o recorte.
        var clientes = await doBrasa.Clientes.AsNoTracking().ToListAsync();

        clientes.Should().ContainSingle();
        clientes[0].Nome.Should().Be("Cliente do Brasa");

        // Contagem e agregação também precisam respeitar o recorte — é onde um filtro
        // aplicado só na listagem deixaria vazar.
        (await doBrasa.Clientes.CountAsync()).Should().Be(1);

        (await doBrasa.Clientes.AnyAsync(c => c.Telefone == "11922222222"))
            .Should().BeFalse("o telefone da outra unidade não pode ser encontrado nem por busca direta");
    }

    [Fact(DisplayName = "RN-GER-003 — coluna monetária é numeric(12,2) e arredonda em vez de truncar")]
    public async Task CA3_PrecisaoMonetaria()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        var (precisao, escala) = await TipoNumericoAsync(restaurante, "produto", "preco");

        precisao.Should().Be(12);
        escala.Should().Be(Dinheiro.CasasDecimais);

        // 10,999 chega pela API como decimal e é arredondado por Dinheiro antes de descer.
        var produtoId = await CriarProdutoAsync(restaurante, "Teste de Arredondamento", 10.999m);

        await using var db = Contexto(restaurante);

        var produto = await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoId);

        produto.Preco.Should().Be(Dinheiro.De(11.00m), "arredondado, não truncado para 10,99");
    }

    [Fact(DisplayName = "RN-GER-004 — nenhuma propriedade do modelo é decimal cru")]
    public void CA4_ModeloNaoTemDecimalCru()
    {
        using var db = Ambiente.CriarContexto(Guid.Empty);

        var cruas = db.Model.GetEntityTypes()
            .SelectMany(tipo => tipo.GetDeclaredProperties()
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
                .Select(p => $"{tipo.ClrType.Name}.{p.Name}"))
            .ToList();

        // A guarda que torna esta asserção redundante está no DbContext e falha na partida.
        // O teste existe para que a intenção fique registrada e para pegar quem a remover.
        cruas.Should().BeEmpty(
            "dinheiro é Dinheiro (duas casas) e quantidade de insumo é Quantidade (quatro). " +
            "Um decimal solto herdaria a precisão errada em silêncio — SPEC-GER-001 §4");
    }

    [Fact(DisplayName = "RN-GER-005 — data e hora são timestamptz, gravadas em UTC")]
    public async Task CA5_DataComFuso()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        await using var db = Contexto(restaurante);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();

        using var tipo = conexao.CreateCommand();
        tipo.CommandText = """
            SELECT data_type FROM information_schema.columns
             WHERE table_name = 'pedido' AND column_name = 'dh_criacao'
            """;

        var tipoDaColuna = (string?)await tipo.ExecuteScalarAsync();
        tipoDaColuna.Should().Be("timestamp with time zone");

        // 22h00 de 16/08 em Brasília é 01h00 de 17/08 em UTC.
        var vinteEDuasHorasEmBrasilia = new DateTimeOffset(2026, 8, 16, 22, 0, 0, TimeSpan.FromHours(-3));

        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT @instante AT TIME ZONE 'UTC'";

        var parametro = comando.CreateParameter();
        parametro.ParameterName = "instante";
        parametro.Value = vinteEDuasHorasEmBrasilia.ToUniversalTime();
        comando.Parameters.Add(parametro);

        var emUtc = (DateTime?)await comando.ExecuteScalarAsync();

        emUtc.Should().Be(new DateTime(2026, 8, 17, 1, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact(DisplayName = "RN-GER-002 — unidade com registro vinculado não pode ser excluída")]
    public async Task CA6_AusenciaDeExclusaoFisica()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        await using var db = Contexto(restaurante);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        comando.CommandText = "DELETE FROM unidade WHERE id = @id";

        var parametro = comando.CreateParameter();
        parametro.ParameterName = "id";
        parametro.Value = restaurante.UnidadeId;
        comando.Parameters.Add(parametro);

        var exclusao = async () => await comando.ExecuteNonQueryAsync();

        // ON DELETE RESTRICT em toda FK para unidade. O cadastro já semeou usuário,
        // categoria e produto, então há dependente para segurar a exclusão.
        (await exclusao.Should().ThrowAsync<DbException>())
            .Which.Message.Should().Contain("unidade");
    }

    [Fact(DisplayName = "SPEC-GER-001 — razão social em branco é recusada pelo banco")]
    public async Task RazaoSocialEmBranco_EhRecusada()
    {
        await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        await using var db = Ambiente.CriarContexto(Guid.Empty);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        // NOT NULL não impede string em branco — e razão social vazia quebra qualquer
        // documento emitido a partir dela.
        comando.CommandText = """
            INSERT INTO unidade (id, razao_social, nome_fantasia, cnpj, slug, dh_criacao)
            VALUES (gen_random_uuid(), '   ', 'Fantasia', '99999999000199', 'em-branco', now())
            """;

        var escrita = async () => await comando.ExecuteNonQueryAsync();

        (await escrita.Should().ThrowAsync<DbException>())
            .Which.Message.Should().Contain("ck_unidade_razao_social");
    }

    /// <summary>Precisão e escala de uma coluna numérica, pelo catálogo do PostgreSQL.</summary>
    private async Task<(int Precisao, int Escala)> TipoNumericoAsync(
        RestauranteDeTeste restaurante, string tabela, string coluna)
    {
        await using var db = Contexto(restaurante);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        comando.CommandText = $"""
            SELECT numeric_precision, numeric_scale
              FROM information_schema.columns
             WHERE table_name = '{tabela}' AND column_name = '{coluna}'
            """;

        await using var leitor = await comando.ExecuteReaderAsync();

        (await leitor.ReadAsync()).Should().BeTrue($"a coluna {tabela}.{coluna} precisa existir");

        return (leitor.GetInt32(0), leitor.GetInt32(1));
    }
}
