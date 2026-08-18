using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Infrastructure.Identity;
using GrelhaHub.Infrastructure.Persistence;
using GrelhaHub.Infrastructure.Persistence.Interceptors;
using GrelhaHub.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var conexao = configuration.GetConnectionString("GrelhaHub")
            ?? throw new InvalidOperationException(
                "Connection string 'GrelhaHub' não configurada. Ver .env.example.");

        // RN-GER-009 e SPEC-GER-005 — scoped, como o contexto de execução de que dependem.
        services.AddScoped<InterceptorDeUnidade>();
        services.AddScoped<InterceptorDeAuditoria>();

        services.AddDbContext<GrelhaHubDbContext>((provedor, options) =>
            options
                .UseNpgsql(conexao)
                // ADR-0004: identificadores snake_case no banco, PascalCase no C#.
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    // A ORDEM importa: o de unidade preenche `unidade_id` ANTES de o de auditoria
                    // ler a entidade para montar a trilha. Invertido, a trilha registraria
                    // Guid.Empty como unidade do registro que acabou de nascer.
                    provedor.GetRequiredService<InterceptorDeUnidade>(),
                    provedor.GetRequiredService<InterceptorDeAuditoria>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<GrelhaHubDbContext>());

        // ADR-0019 — o lado da LEITURA, com modelo próprio.
        //
        // ⚠ Mesma connection string, contexto DIFERENTE, e é isso que separa: este não conhece
        // agregado nenhum, só visões, e a interface que ele expõe não tem `SaveChangesAsync`. Um
        // handler de `Query` recebe `ILeituraDb` e não tem como alcançar o outro lado — a
        // separação deixa de depender de disciplina.
        //
        // ⚠ NÃO registra `GrelhaHubLeituraDbContext` como `IAppDbContext`, nem o contrário: são
        // dois modelos do mesmo banco, e cruzá-los devolveria o acoplamento que o ADR-0019
        // desfez. O `ArquiteturaDeCqrsTests` recusa a build se alguém tentar.
        services.AddDbContext<GrelhaHubLeituraDbContext>(options =>
            options.UseNpgsql(conexao).UseSnakeCaseNamingConvention());

        services.AddScoped<ILeituraDb>(sp => sp.GetRequiredService<GrelhaHubLeituraDbContext>());

        // ADR-0017 — o BANCO DE LOG: trilha de auditoria e exceções.
        //
        // ⚠ Connection string PRÓPRIA. Sem ela configurada o sistema não sobe, e isso é
        // deliberado: diferente do SMTP (RN-GER-032), que é ausência tolerável, um sistema sem
        // onde gravar a trilha não deveria aceitar a primeira requisição — a operação gravaria e
        // o registro não, que é exatamente o que RN-GER-015 proíbe.
        var conexaoDeLog = configuration.GetConnectionString("GrelhaHubLog")
            ?? throw new InvalidOperationException(
                "Connection string 'GrelhaHubLog' não configurada (ADR-0017). Ver .env.example.");

        services.AddDbContext<GrelhaHubLogDbContext>(options =>
            options.UseNpgsql(conexaoDeLog).UseSnakeCaseNamingConvention());

        services.AddScoped<ILogDb>(sp => sp.GetRequiredService<GrelhaHubLogDbContext>());

        // O worker que leva a trilha da outbox para o banco de log.
        services.AddHostedService<ServicoDeDrenagemDaOutbox>();

        services.AddSingleton<IRelogio, Relogio>();

        // SPEC-GER-003 — o cache atravessa requisições (singleton); o serviço depende do
        // DbContext e do contexto de execução, então é scoped.
        services.AddSingleton<CacheDeParametros>();
        services.AddScoped<IParametros, Parametros>();

        // SPEC-GER-006 — mesmo desenho: cache singleton, serviço scoped.
        services.AddSingleton<CacheDeModulos>();
        services.AddScoped<IModulos, Modulos>();
        // ADR-0018 / SPEC-VEN-002 — o BANCO DE ARQUIVO: os binários das imagens.
        //
        // ⚠ Connection string própria e obrigatória, pelo mesmo motivo do banco de log: um
        // sistema que aceita upload sem ter onde gravar o binário só descobre isso no primeiro
        // envio de um usuário real.
        var conexaoDeArquivo = configuration.GetConnectionString("GrelhaHubArquivo")
            ?? throw new InvalidOperationException(
                "Connection string 'GrelhaHubArquivo' não configurada (ADR-0018). Ver .env.example.");

        services.AddDbContext<GrelhaHubArquivoDbContext>(options =>
            options.UseNpgsql(conexaoDeArquivo).UseSnakeCaseNamingConvention());

        services.AddScoped<IArquivoDb>(sp => sp.GetRequiredService<GrelhaHubArquivoDbContext>());

        // ⚠ SCOPED, e o antecessor era SINGLETON. A diferença não é estilo: esta implementação
        // depende de dois `DbContext`, que são scoped — registrá-la como singleton capturaria o
        // contexto da primeira requisição e o reusaria para sempre, que é o defeito mais difícil
        // de diagnosticar que o contêiner de DI sabe produzir.
        services.AddScoped<IArmazenamentoDeArquivo, Arquivos.ArmazenamentoEmBanco>();

        // ADR-0016 — o correio. Mesma leitura por indexador do bloco acima.
        //
        // ⚠⚠ A LISTA ABAIXO É MANUAL, e é por isso que ela merece atenção: propriedade nova em
        // `OpcoesDeEmail` NÃO chega aqui sozinha. Quem esquecer a linha ganha um valor vazio em
        // produção, sem erro, sem aviso e sem falha de teste — o rodapé do e-mail saiu com
        // "· CNPJ" e nada depois, em 18/08/2026, exatamente assim. O dublê da suíte tem os
        // valores escritos nele, então o teste continuava verde.
        //
        // `OpcoesDeEmailTests` cobre esse buraco: ele varre as propriedades da classe por
        // reflexão e falha quando uma delas não é lida da configuração.
        services.Configure<Email.OpcoesDeEmail>(opcoes =>
        {
            var secao = configuration.GetSection(Email.OpcoesDeEmail.Secao);

            opcoes.Host = secao["Host"] ?? opcoes.Host;
            opcoes.Usuario = secao["Usuario"] ?? opcoes.Usuario;
            opcoes.Senha = secao["Senha"] ?? opcoes.Senha;
            opcoes.Remetente = secao["Remetente"] ?? opcoes.Remetente;
            opcoes.NomeDoRemetente = secao["NomeDoRemetente"] ?? opcoes.NomeDoRemetente;
            opcoes.BaseDoSite = secao["BaseDoSite"] ?? opcoes.BaseDoSite;
            opcoes.Cnpj = secao["Cnpj"] ?? opcoes.Cnpj;

            if (int.TryParse(secao["Porta"], out var porta))
            {
                opcoes.Porta = porta;
            }
        });

        // A fila é SINGLETON e o serviço que a consome também: são o mesmo canal, e dois canais
        // seriam uma fila que ninguém lê.
        services.AddSingleton<Email.FilaDeEmail>();
        services.AddSingleton<IEnviadorDeEmail, Email.EnviadorDeEmail>();
        services.AddHostedService<Email.ServicoDeEnvioDeEmail>();

        services.AddSingleton<ITokensDeAcesso, Email.TokensDeAcesso>();

        services.AddSingleton<IHashDeSenha, HashDeSenha>();
        services.AddScoped<IGeradorDeToken, GeradorDeTokenJwt>();

        return services;
    }
}
