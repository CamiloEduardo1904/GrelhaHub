using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Infrastructure.Identity;
using GrelhaHub.Infrastructure.Persistence;
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

        services.AddDbContext<GrelhaHubDbContext>(options =>
            options
                .UseNpgsql(conexao)
                // ADR-0004: identificadores snake_case no banco, PascalCase no C#.
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<GrelhaHubDbContext>());

        services.AddSingleton<IRelogio, Relogio>();
        services.AddSingleton<IHashDeSenha, HashDeSenha>();
        services.AddScoped<IGeradorDeToken, GeradorDeTokenJwt>();

        return services;
    }
}
