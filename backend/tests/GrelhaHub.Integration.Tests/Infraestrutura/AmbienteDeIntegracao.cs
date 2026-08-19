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

    /// <summary>
    /// ADR-0017 — o banco de LOG, no mesmo contêiner e com nome próprio.
    /// <para>
    /// ⚠ Mesma instância porque é assim que a produção roda (VPS única, ADR-0006): subir um
    /// segundo contêiner no teste mediria uma topologia que não existe.
    /// </para>
    /// </summary>
    public string StringDeLog =>
        new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            // ⚠ MONTADO, e não uma substituição de texto na connection string. A primeira versão
            // trocava a substring "Database=grelhahub_testes" — e quando o Testcontainers devolve
            // a chave com outro nome ou outra caixa, a troca não acontece EM SILÊNCIO: o contexto
            // de log passa a apontar para o banco da APLICAÇÃO, e o erro que aparece é
            // "relation log_auditoria does not exist", que manda investigar a migration errada.
            // Aconteceu em 18/08/2026, e custou uma rodada inteira da suíte para diagnosticar.
            Database = "grelhahub_testes_log",
        }.ConnectionString;

    /// <summary>ADR-0018 — o banco de ARQUIVO, no mesmo contêiner e com nome próprio.</summary>
    public string StringDeArquivo =>
        new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = "grelhahub_testes_arquivo",
        }.ConnectionString;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CriarContexto(Guid.Empty);
        await db.Database.MigrateAsync();

        // ⚠ O banco de log não nasce com o contêiner — o Testcontainers cria um só —, e não
        // precisa de CREATE DATABASE: o MigrateAsync do EF Core cria o banco quando ele não
        // existe. Testado removendo o CREATE daqui, e é o que permite o deploy funcionar sem passo
        // manual na VPS. As migrations dele são OUTRO conjunto (ADR-0017).
        await using (var log = CriarContextoDeLog())
        {
            await log.Database.MigrateAsync();
        }

        await using (var arquivo = CriarContextoDeArquivo())
        {
            await arquivo.Database.MigrateAsync();
        }

        _api = new ApiDeTestes(StringDeConexao, StringDeLog, StringDeArquivo, ChaveJwt);

        // Força a construção do host agora: se a configuração estiver errada, o erro aparece
        // aqui e não disfarçado como falha do primeiro teste da suíte.
        _ = _api.Server;

        await SemearPlataformaAsync();

        // ADR-0016 — a caixa de saída também é resíduo entre testes: uma mensagem da rodada
        // anterior faria `UltimoToken` devolver um link que já não vale.
        ApiDeTestes.CaixaDeSaida.Limpar();
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
    /// O contexto do banco de LOG, para asserção direta na trilha — ADR-0017.
    /// <para>
    /// ⚠ Sem interceptor nenhum: este banco não audita a si mesmo, e não tem filtro de unidade.
    /// </para>
    /// </summary>
    /// <summary>ADR-0018 — o contexto do banco de arquivo, para asserção direta nos bytes.</summary>
    public GrelhaHubArquivoDbContext CriarContextoDeArquivo() =>
        new(new DbContextOptionsBuilder<GrelhaHubArquivoDbContext>()
            .UseNpgsql(StringDeArquivo)
            .UseSnakeCaseNamingConvention()
            .Options);

    public GrelhaHubLogDbContext CriarContextoDeLog() =>
        new(new DbContextOptionsBuilder<GrelhaHubLogDbContext>()
            .UseNpgsql(StringDeLog)
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>
    /// Copia a outbox para o banco de log <b>agora</b>, e devolve quantas linhas passaram.
    /// <para>
    /// ⚠⚠ <b>O teste não pode esperar o worker.</b> A drenagem roda a cada cinco segundos em
    /// produção (ADR-0017), e um teste que asserta a trilha logo depois da operação passaria ou
    /// falharia conforme o instante — o pior tipo de teste, porque a falha parece regressão.
    /// </para>
    /// <para>
    /// Chama o <b>mesmo código</b> que o worker chama, e não uma cópia: se a cópia mudar, o teste
    /// muda junto.
    /// </para>
    /// </summary>
    public async Task<int> DrenarOutboxAsync()
    {
        using var escopo = CriarEscopo();

        return await Infrastructure.Persistence.ServicoDeDrenagemDaOutbox.CopiarAsync(
            escopo.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IAppDbContext>(),
            escopo.ServiceProvider.GetRequiredService<Application.Common.Interfaces.ILogDb>(),
            tamanhoDoLote: 1000,
            CancellationToken.None);
    }

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

        // ⚠⚠ O BANCO DE LOG TAMBÉM É RESÍDUO — ADR-0017. A trilha de um teste anterior faria o
        // seguinte contar registros que não são dele, e o sintoma seria "passa sozinho, falha na
        // suíte": a pior assinatura de defeito que existe.
        //
        // ⚠ O gatilho de imutabilidade recusa DELETE e TRUNCATE sobre a trilha — que é exatamente
        // o que a limpeza precisa fazer. Desligá-lo AQUI, e só aqui, é concessão de teste: a
        // alternativa seria não criar o gatilho, e aí a PRODUÇÃO ficaria sem a garantia para que o
        // harness ficasse cômodo.
        await using (var log = CriarContextoDeLog())
        {
            await log.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE log_auditoria DISABLE TRIGGER USER;
                TRUNCATE TABLE log_auditoria, log_excecao RESTART IDENTITY CASCADE;
                ALTER TABLE log_auditoria ENABLE TRIGGER USER;
                """);
        }

        // ⚠ O banco de ARQUIVO também é resíduo — ADR-0018. Um binário da rodada anterior faria
        // uma contagem de órfãos passar quando deveria falhar.
        await using (var arquivo = CriarContextoDeArquivo())
        {
            await arquivo.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE arquivo_binario RESTART IDENTITY CASCADE;");
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

            // ADR-0022 — a tabela `permissao` saiu da lista porque saiu do BANCO: o catálogo do
            // que existe é código (CatalogoDeAcesso), e a concessão (`perfil_permissao`, claim
            // de papel) é por unidade — dado de teste, truncado como `perfil`.
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

            IF alvos IS NOT NULL THEN
                EXECUTE 'TRUNCATE TABLE ' || alvos || ' RESTART IDENTITY CASCADE';
            END IF;
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
