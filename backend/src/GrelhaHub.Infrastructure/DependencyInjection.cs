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

        services.AddSingleton<IRelogio, Relogio>();

        // SPEC-GER-003 — o cache atravessa requisições (singleton); o serviço depende do
        // DbContext e do contexto de execução, então é scoped.
        services.AddSingleton<CacheDeParametros>();
        services.AddScoped<IParametros, Parametros>();

        // SPEC-GER-006 — mesmo desenho: cache singleton, serviço scoped.
        services.AddSingleton<CacheDeModulos>();
        services.AddScoped<IModulos, Modulos>();
        // ADR-0015 / SPEC-VEN-002 — armazenamento de imagem em volume de disco. Singleton:
        // não guarda estado por requisição, só a raiz resolvida uma vez.
        // Lido pelo indexador, e não por `Bind`: `Bind` vem do pacote
        // Microsoft.Extensions.Configuration.Binder, e são duas chaves — a dependência custaria
        // mais do que resolve.
        services.Configure<Arquivos.OpcoesDeArquivo>(opcoes =>
        {
            var secao = configuration.GetSection(Arquivos.OpcoesDeArquivo.Secao);

            opcoes.Raiz = secao["Raiz"] ?? opcoes.Raiz;
            opcoes.PrefixoPublico = secao["PrefixoPublico"] ?? opcoes.PrefixoPublico;
        });

        services.AddSingleton<IArmazenamentoDeArquivo, Arquivos.ArmazenamentoEmDisco>();

        services.AddSingleton<IHashDeSenha, HashDeSenha>();
        services.AddScoped<IGeradorDeToken, GeradorDeTokenJwt>();

        return services;
    }
}
