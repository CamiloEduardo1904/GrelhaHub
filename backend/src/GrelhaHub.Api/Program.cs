using System.Globalization;
using System.Security.Claims;
using System.Text;
using GrelhaHub.Api.Seguranca;
using GrelhaHub.Application;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Infrastructure;
using GrelhaHub.Infrastructure.Identity;
using GrelhaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// RNF-019 — log estruturado. Cultura invariante: log com vírgula decimal é hostil para
// grep e para qualquer agregador.
builder.Host.UseSerilog((contexto, config) => config
    .ReadFrom.Configuration(contexto.Configuration)
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services.AdicionarApplication();
builder.Services.AdicionarInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IContextoDeExecucao, ContextoDeExecucaoHttp>();
builder.Services.AddExceptionHandler<TratadorDeExcecoes>();
builder.Services.AddProblemDetails();

// ADR-0022 — o ASP.NET Identity como modelo de acesso: Usuario e Perfil são os POCOs do
// domínio, o store é o GrelhaHubDbContext, e o SignInManager é quem conta as falhas de login.
//
// AddIdentityCore, e NÃO AddIdentity: o pacote completo registraria autenticação por cookie e
// redirecionamento para /Account/Login — a autenticação daqui é JWT (ADR-0003), e o Identity
// entra só como gestão de credencial e papel.
builder.Services
    .AddIdentityCore<Usuario>(options =>
    {
        // RN-GER-040 ⚠⚠ — o lockout. As constantes NOMEIAM os parâmetros que copiam
        // (PAR-GER-004, PAR-GER-005): escopo global, porque quem tenta entrar ainda não tem
        // unidade — ver docs/04-parametros-do-sistema.md §1.1.
        options.Lockout.MaxFailedAccessAttempts = PoliticaDeBloqueio.TentativasAntesDoBloqueio;
        options.Lockout.DefaultLockoutTimeSpan = PoliticaDeBloqueio.DuracaoDoBloqueio;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<Perfil>()
    .AddEntityFrameworkStores<GrelhaHubDbContext>()
    .AddSignInManager();

// RN-GER-040 — a ponte que o AutenticarHandler usa: a Application não referencia o runtime do
// ASP.NET, então o SignInManager fica atrás desta interface.
builder.Services.AddScoped<IVerificadorDeCredenciais, VerificadorDeCredenciais>();

// ADR-0003 — autenticação stateless por JWT.
var chaveJwt = builder.Configuration["Jwt:Chave"] ?? string.Empty;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Emissor"],
            ValidAudience = builder.Configuration["Jwt:Audiencia"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(chaveJwt.PadRight(32, '_'))),
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        options.Events = new JwtBearerEvents
        {
            // RN-GER-041 ⚠⚠ — trocar a senha invalida as sessões anteriores.
            //
            // O SecurityStamp viaja no token (claim `carimbo`) e é conferido AQUI, a cada
            // requisição autenticada, contra o que está gravado: trocar a senha regenera o
            // carimbo, e todo token emitido antes falha nesta comparação — 401 na requisição
            // seguinte, que é o enunciado da regra.
            //
            // ⚠ O custo é assumido e registrado (ADR-0022 §5, emenda à ADR-0003): uma consulta
            // por requisição, e o JWT deixa de ser puramente stateless. A alternativa era o
            // buraco que a ADR-0021 encontrou — senha vazada, senha trocada, e o atacante
            // dentro por mais oito horas. Sessão que não morre com a senha não é sessão, é
            // chave copiada.
            OnTokenValidated = async contexto =>
            {
                var principal = contexto.Principal;

                var sub = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal?.FindFirstValue("sub");

                var carimboDoToken = principal?.FindFirstValue(GeradorDeTokenJwt.ClaimCarimbo);

                // Token sem carimbo é token de antes desta regra — e aceitar seria manter viva
                // exatamente a sessão que a regra existe para matar. Falha fechado.
                if (!Guid.TryParse(sub, out var usuarioId) || string.IsNullOrEmpty(carimboDoToken))
                {
                    contexto.Fail("RN-GER-041 — token sem carimbo de seguranca.");
                    return;
                }

                // `usuario` está fora do filtro global (não implementa IPertenceAUnidade), e a
                // consulta lê UMA coluna pela chave primária: é o preço por requisição, inteiro.
                var db = contexto.HttpContext.RequestServices
                    .GetRequiredService<GrelhaHubDbContext>();

                var carimboAtual = await db.Usuarios
                    .AsNoTracking()
                    .Where(u => u.Id == usuarioId)
                    .Select(u => u.SecurityStamp)
                    .FirstOrDefaultAsync(contexto.HttpContext.RequestAborted);

                if (!string.Equals(carimboAtual, carimboDoToken, StringComparison.Ordinal))
                {
                    contexto.Fail("RN-GER-041 — sessao invalidada por troca de senha.");
                }
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    // ADR-0022 §3 / RN-GER-010 — UMA política por permissão, registrada a partir do
    // CatalogoDeAcesso: a política tem o MESMO nome do código e exige a claim `permissao` com
    // esse valor. Quem DECLARA a exigência continua sendo o request ([ExigePermissao]); o
    // PermissaoBehavior resolve a política pelo IAuthorizationService. Permissão nova no
    // catálogo ganha política sozinha — não há segunda lista a manter.
    foreach (var permissao in CatalogoDeAcesso.Permissoes)
    {
        options.AddPolicy(permissao.Codigo, politica =>
            politica.RequireClaim(CatalogoDeAcesso.TipoDeClaimDePermissao, permissao.Codigo));
    }
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ADR-0003 — em produção, front e API vivem no mesmo domínio via Nginx e não há CORS.
// Esta política existe apenas para o desenvolvimento local, em que o Vite roda em outra porta.
const string PoliticaDesenvolvimento = "desenvolvimento-local";
builder.Services.AddCors(options => options.AddPolicy(
    PoliticaDesenvolvimento,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// ADR-0006 — em produção as migrations rodam como STEP DO PIPELINE, e estes são os
// modos que os steps usam: executam e SAEM, sem subir o servidor.
//
// Sem isto, `docker compose run --rm api dotnet GrelhaHub.Api.dll --migrate`
// subiria a API e ficaria escutando — o deploy travaria para sempre esperando
// um processo que nunca termina.
//
// ⚠ SÃO DOIS MODOS SEPARADOS, e a separação é o ponto (RN-GER-014):
//
//   --migrate   aplica o schema
//   --semear    cria o usuário de suporte da plataforma
//
// Enquanto a semeadura era um apêndice do `--migrate`, o resultado dela aparecia
// no meio do log de um step chamado "Migrations EF Core" — e num deploy sem
// migration pendente, que é o caso comum, ninguém tinha motivo para abrir esse
// log. Esta instalação subiu sem acesso de suporte nenhum, com o aviso registrado a
// cada deploy, lido por ninguém. Step próprio, nome próprio, log curto.
//
// Os dois podem ser passados juntos (`--migrate --semear`), e nessa ordem: semear
// antes de aplicar o schema é semear contra tabela que pode não existir.
var modoMigrar = args.Contains("--migrate");
var modoSemear = args.Contains("--semear");

if (modoMigrar || modoSemear)
{
    try
    {
        using var escopoMigracao = app.Services.CreateScope();
        var banco = escopoMigracao.ServiceProvider.GetRequiredService<GrelhaHubDbContext>();

        if (modoMigrar)
        {
            var pendentes = (await banco.Database.GetPendingMigrationsAsync()).ToList();

            if (pendentes.Count == 0)
            {
                Log.Information("Nenhuma migration pendente.");
            }
            else
            {
                Log.Information("Aplicando {Total} migration(s): {Nomes}",
                    pendentes.Count, string.Join(", ", pendentes));
                await banco.Database.MigrateAsync();
                Log.Information("Migrations aplicadas.");
            }

            // ADR-0017 — o BANCO DE LOG tem migrations PRÓPRIAS, e elas não vêm de carona.
            //
            // ⚠⚠ Esquecer estas linhas é o defeito mais provável desta mudança: a API sobe (a
            // connection string existe), a operação grava na outbox, e o worker falha em toda
            // volta porque log_auditoria não existe do outro lado. A trilha ficaria acumulando na
            // outbox sem ninguém notar — que é exatamente o cenário que o contador de tentativas
            // da outbox existe para denunciar.
            var bancoDeLog = escopoMigracao.ServiceProvider
                .GetRequiredService<GrelhaHubLogDbContext>();

            var pendentesDeLog = (await bancoDeLog.Database.GetPendingMigrationsAsync()).ToList();

            if (pendentesDeLog.Count == 0)
            {
                Log.Information("Banco de log: nenhuma migration pendente.");
            }
            else
            {
                Log.Information("Banco de log: aplicando {Total} migration(s): {Nomes}",
                    pendentesDeLog.Count, string.Join(", ", pendentesDeLog));
                await bancoDeLog.Database.MigrateAsync();
                Log.Information("Banco de log: migrations aplicadas.");
            }

            // ADR-0018 — o BANCO DE ARQUIVO, pelo mesmo motivo do de log: migrations próprias,
            // que não vêm de carona. Esquecer aqui deixa a API subindo e o primeiro upload
            // falhando com 500 — para um usuário, no meio do serviço.
            var bancoDeArquivo = escopoMigracao.ServiceProvider
                .GetRequiredService<GrelhaHubArquivoDbContext>();

            var pendentesDeArquivo =
                (await bancoDeArquivo.Database.GetPendingMigrationsAsync()).ToList();

            if (pendentesDeArquivo.Count == 0)
            {
                Log.Information("Banco de arquivo: nenhuma migration pendente.");
            }
            else
            {
                Log.Information("Banco de arquivo: aplicando {Total} migration(s): {Nomes}",
                    pendentesDeArquivo.Count, string.Join(", ", pendentesDeArquivo));
                await bancoDeArquivo.Database.MigrateAsync();
                Log.Information("Banco de arquivo: migrations aplicadas.");
            }
        }

        if (modoSemear)
        {
            // RN-GER-014 — o usuário de suporte da plataforma. Idempotente: pode rodar a cada
            // deploy e não faz nada se já existir, nem sobrescreve a senha de quem existe. A
            // senha vem do .env da VPS (RNF-008), nunca do repositório.
            //
            // Todo desfecho — criado, já existia, não configurado — sai como log do semeador.
            await SemeadorDePlataforma.SemearAsync(
                banco,
                escopoMigracao.ServiceProvider.GetRequiredService<IHashDeSenha>(),
                app.Configuration,
                escopoMigracao.ServiceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(nameof(SemeadorDePlataforma)));
        }

        return 0;
    }
    catch (Exception excecao)
    {
        // Falha ruidosa: o pipeline precisa parar aqui, não seguir para o
        // `up -d` e subir uma API contra um schema desatualizado.
        Log.Fatal(excecao, "Falha no modo de linha de comando ({Modos}).",
            string.Join(" ", args));
        return 1;
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(PoliticaDesenvolvimento);
}

// ADR-0018 — as imagens NÃO são mais servidas do disco.
//
// ⚠ Este trecho montava um diretório estático (ADR-0015). Os bytes agora vivem no banco de
// arquivo e saem pelo `ArquivosController`, que consulta o METADADO antes — e é isso que faz a
// exclusão lógica valer também para a imagem, coisa que um diretório não sabia fazer.
//
// O `location /arquivos/` do Nginx continua no ar, e é deliberado: as referências gravadas antes
// de 18/08/2026 apontam para lá. Ele é um atalho de LEITURA para o passado, não um caminho de
// escrita — nada no código escreve mais naquele volume.

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health")
   .AllowAnonymous();

// ADR-0006 — em produção as migrations rodam como STEP DO PIPELINE, não no startup:
// migration no startup falha em silêncio ou trava o boot quando dá conflito, e a falha
// aparece como "a API não sobe". Em desenvolvimento, aplicar na partida é conveniente.
if (app.Environment.IsDevelopment())
{
    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<GrelhaHubDbContext>();
    await db.Database.MigrateAsync();

    await SemeadorDePlataforma.SemearAsync(
        db,
        escopo.ServiceProvider.GetRequiredService<IHashDeSenha>(),
        app.Configuration,
        escopo.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(SemeadorDePlataforma)));
}

await app.RunAsync();

return 0;

/// <summary>Exposto para os testes de integração (<c>WebApplicationFactory&lt;Program&gt;</c>).</summary>
public partial class Program;
