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
