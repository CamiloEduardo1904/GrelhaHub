using GrelhaHub.Infrastructure.Persistence;
using GrelhaHub.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace GrelhaHub.Integration.Tests.Infraestrutura;

/// <summary>
/// Ambiente compartilhado dos testes de integração: um PostgreSQL real em contêiner e a API
/// apontada para ele.
/// <para>
/// <b>Por que PostgreSQL real e não o provider in-memory</b> — ADR-0009. O in-memory do EF
/// Core não executa <c>CHECK</c>, <c>EXCLUDE</c>, trigger nem coluna gerada. Como boa parte
/// das regras do GrelhaHub vive exatamente nesses mecanismos, o in-memory daria confiança
/// falsa no ponto em que ela custa mais caro: <c>data_operacional</c> voltaria o valor que a
/// aplicação chutou, não o que o banco calculou — e a divergência entre os dois é justamente
/// o que estes testes existem para pegar.
/// </para>
/// <para>
/// O contêiner sobe <b>uma vez por assembly</b>: subir por teste custaria segundos a cada
/// caso e ninguém rodaria a suíte.
/// </para>
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification =
        "O descarte acontece em DisposeAsync, chamado pelo xUnit por IAsyncLifetime. " +
        "Contêiner e host são recursos assíncronos: um Dispose síncrono teria de bloquear, " +
        "que é pior do que a regra que a análise aponta.")]
public sealed class AmbienteDeIntegracao : IAsyncLifetime
{
    /// <summary>
    /// Mínimo de 32 bytes — <c>GeradorDeTokenJwt</c> recusa chave menor, e recusa com razão
    /// (<c>RNF-008</c>). Valor fixo aqui porque é teste; em produção vem de segredo.
    /// </summary>
    private const string ChaveJwt = "chave-exclusiva-de-teste-do-grelhahub-nunca-em-producao";

    /// <summary>
    /// Credenciais do usuário de plataforma (<c>RN-GER-014</c>) nesta suíte.
    /// <para>
    /// Semeadas pelo <c>SemeadorDePlataforma</c> a partir da configuração da API de teste, pelo
    /// mesmo caminho da produção — e não por <c>INSERT</c>. Se a semeadura quebrar, os testes de
    /// plataforma quebram junto, que é o que se quer.
    /// </para>
    /// </summary>
    public const string EmailDePlataforma = "suporte@grelhahub.teste";

    public const string SenhaDePlataforma = "senha-de-suporte-de-teste-123";

    /// <summary>
    /// Versão fixada de propósito: a expressão de <c>data_operacional</c> depende do tzdata
    /// do servidor, e "a última" mudaria o resultado do teste sem ninguém ter mudado código.
    /// </summary>
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("grelhahub_testes")
        .WithUsername("grelhahub")
        .WithPassword("grelhahub")
        .Build();

    private ApiDeTestes? _api;

    public string StringDeConexao => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CriarContexto(Guid.Empty);
        await db.Database.MigrateAsync();

        _api = new ApiDeTestes(StringDeConexao, ChaveJwt);

        // Força a construção do host agora: se a configuração estiver errada, o erro aparece
        // aqui e não disfarçado como falha do primeiro teste da suíte.
        _ = _api.Server;

        await SemearPlataformaAsync();
    }

    public async Task DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>Cliente HTTP anônimo — o que o cliente do restaurante usa.</summary>
    public HttpClient CriarCliente() => _api!.CreateClient();

    /// <summary>Cliente HTTP com o token no cabeçalho.</summary>
    public HttpClient CriarCliente(string token)
    {
        var cliente = _api!.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    public IServiceScope CriarEscopo() => _api!.CriarEscopo();

    /// <summary>
    /// Contexto de persistência para asserção direta no banco, escopado em
    /// <paramref name="unidadeId"/> — o filtro global de <c>RN-GER-009</c> continua valendo.
    /// </summary>
    public GrelhaHubDbContext CriarContexto(Guid unidadeId)
    {
        var contexto = new ContextoFixo { UnidadeId = unidadeId };

        var construtor = new DbContextOptionsBuilder<GrelhaHubDbContext>()
            .UseNpgsql(StringDeConexao)
            .UseSnakeCaseNamingConvention()
            // O mesmo interceptor de produção (RN-GER-009): sem ele, o contexto do teste
            // gravaria de um jeito que a aplicação não grava, e o teste passaria a medir
            // um comportamento que não existe.
            .AddInterceptors(
                new InterceptorDeUnidade(contexto),

                // SPEC-GER-005 — a mesma trilha da produção. Sem ela, o contexto do teste gravaria
                // sem auditar, e o teste passaria a medir um comportamento que não existe.
                new InterceptorDeAuditoria(contexto, new Infrastructure.Services.Relogio()));

        // Diagnóstico sob demanda: `GRELHAHUB_LOG_SQL=<arquivo> dotnet test ...` grava o SQL
        // que o EF Core realmente emitiu.
        //
        // Existe porque a asserção de um teste de integração diz *que* falhou, não *o que o
        // EF fez*. Foi assim que o CA-9 revelou um `UPDATE item_pedido` onde devia haver um
        // `INSERT` — sem o SQL na mão, o DbUpdateConcurrencyException parece conflito de
        // concorrência e manda investigar o lugar errado.
        //
        // Desligado por padrão: o log é volumoso e tem dado sensível (EnableSensitiveData).
        var destinoDoLog = Environment.GetEnvironmentVariable("GRELHAHUB_LOG_SQL");

        if (!string.IsNullOrWhiteSpace(destinoDoLog))
        {
            construtor
                .EnableSensitiveDataLogging()
                .LogTo(linha => File.AppendAllText(destinoDoLog, linha + Environment.NewLine));
        }

        var opcoes = construtor.Options;

        return new GrelhaHubDbContext(opcoes, contexto);
    }

    /// <summary>
    /// Esvazia as tabelas entre testes.
    /// <para>
    /// A lista de tabelas é <b>descoberta no catálogo do PostgreSQL</b>, não escrita a mão:
    /// as ondas seguintes acrescentam dezenas de tabelas, e uma lista fixa passaria a deixar
    /// resíduo silencioso — que aparece como teste que só falha quando roda depois de outro.
    /// </para>
    /// </summary>
    public async Task LimparAsync()
    {
        await using (var db = CriarContexto(Guid.Empty))
        {
            await db.Database.ExecuteSqlRawAsync(SqlDeLimpeza);
        }

        await SemearPlataformaAsync();
    }

    /// <summary>
    /// RN-GER-014 — (res)semeia o usuário de plataforma, pelo mesmo caminho da produção.
    /// Idempotente: se já existir, não faz nada.
    /// </summary>
    private async Task SemearPlataformaAsync()
    {
        using var escopo = CriarEscopo();

        await SemeadorDePlataforma.SemearAsync(
            escopo.ServiceProvider.GetRequiredService<GrelhaHubDbContext>(),
            escopo.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IHashDeSenha>(),
            escopo.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            escopo.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger(nameof(SemeadorDePlataforma)));
    }

    /// <summary>
    /// Tabelas que a limpeza <b>não</b> esvazia: dado de referência semeado por migration, não
    /// dado de teste.
    /// <para>
    /// <b>Encontrado ao implementar SPEC-GER-003.</b> A limpeza descobria as tabelas no catálogo
    /// do PostgreSQL e truncava todas — inclusive <c>parametro</c>, que é o catálogo dos nove
    /// parâmetros do sistema. Resultado: a partir do segundo teste o cadastro de restaurante
    /// falhava com "o parâmetro está no catálogo do C# e não existe na tabela", e a causa
    /// aparente era a migration, não a limpeza.
    /// </para>
    /// <para>
    /// A lista é explícita de propósito: uma tabela de referência nova precisa ser lembrada
    /// aqui, e é melhor que isso seja uma decisão visível do que uma heurística que erra em
    /// silêncio. <c>parametro_valor</c> <b>não</b> entra — o valor é por unidade e é dado de
    /// teste.
    /// </para>
    /// </summary>
    private static readonly string[] TabelasDeReferencia =
        [
            "__EFMigrationsHistory",
            "parametro",
            "modulo",
            "modulo_dependencia",

            // SPEC-GER-004 — catálogo global das permissões. `perfil` e `perfil_permissao` NÃO
            // entram: o perfil é por unidade e é dado de teste.
            "permissao",
        ];

        // ⚠ `usuario` NÃO entra na lista acima, e a tentativa de colocá-lo lá foi um erro
        // instrutivo: `TRUNCATE unidade ... CASCADE` propaga para toda tabela que referencia
        // `unidade`, e `usuario.unidade_id` é uma delas. Excluir da lista não protege — o
        // CASCADE alcança de qualquer forma.
        //
        // `parametro` e `modulo` sobrevivem porque são PAIS, não filhos: truncar
        // `parametro_valor` ou `unidade_modulo` não sobe para eles.
        //
        // Por isso o usuário de plataforma é RESSEMEADO depois de cada limpeza, e não protegido
        // dela. A semeadura é idempotente, então repetir é barato.

    private static string SqlDeLimpeza =>
        $"""
        DO $$
        DECLARE
            alvos text;
        BEGIN
            SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
              INTO alvos
              FROM pg_tables
             WHERE schemaname = 'public'
               AND tablename <> ALL (ARRAY[{string.Join(", ", TabelasDeReferencia.Select(t => $"'{t}'"))}]);

            -- SPEC-GER-005 — `trg_log_auditoria_sem_truncate` recusa TRUNCATE sobre a trilha, que é
            -- exatamente o que a limpeza precisa fazer. Desligar o gatilho AQUI, e só aqui, é
            -- concessão de teste: a alternativa seria não criar o gatilho, e aí a produção ficaria
            -- sem a garantia para que o harness ficasse cômodo.
            ALTER TABLE log_auditoria DISABLE TRIGGER USER;

            IF alvos IS NOT NULL THEN
                EXECUTE 'TRUNCATE TABLE ' || alvos || ' RESTART IDENTITY CASCADE';
            END IF;

            ALTER TABLE log_auditoria ENABLE TRIGGER USER;
        END $$;
        """;
}

/// <summary>
/// Coleção única para toda a suíte de integração: um contêiner só, e testes que não correm em
/// paralelo entre si — cada um limpa o banco antes de começar.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoDeIntegracao : ICollectionFixture<AmbienteDeIntegracao>
{
    public const string Nome = "integracao";
}
