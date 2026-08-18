using System.Globalization;
using System.Text;
using GrelhaHub.Api.Seguranca;
using GrelhaHub.Application;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Infrastructure;
using GrelhaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
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
    });

builder.Services.AddAuthorization();

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

// ADR-0015 / SPEC-VEN-002 — as imagens.
//
// Em produção quem serve é o NGINX (`location /arquivos/`), que faz isto melhor e sem passar
// pelo pipeline do ASP.NET. Aqui existe para desenvolvimento e para o teste de integração, que
// sobe a API sem Nginx na frente.
//
// ANTES de UseAuthentication, e de propósito: a imagem do cardápio é pública (RN-VEN-037), e um
// arquivo estático não deve atravessar autenticação para ser lido. O controle está em quem
// GRAVA, não em quem lê.
{
    var raiz = Path.GetFullPath(
        builder.Configuration["Arquivos:Raiz"] ?? "arquivos");

    Directory.CreateDirectory(raiz);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(raiz),
        RequestPath = builder.Configuration["Arquivos:PrefixoPublico"] ?? "/arquivos",

        // Só os tipos que RN-VEN-042 aceita. Um provedor que servisse qualquer extensão
        // devolveria, como download, qualquer arquivo que acabasse na pasta por outro caminho.
        ContentTypeProvider = new FileExtensionContentTypeProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
            }),

        ServeUnknownFileTypes = false,

        OnPrepareResponse = contexto =>
        {
            // Nome gerado por arquivo (SPEC-VEN-002 §4): trocar a imagem gera caminho novo, então
            // o conteúdo de um caminho nunca muda e pode ser cacheado longe.
            contexto.Context.Response.Headers.CacheControl = "public,max-age=604800,immutable";
        },
    });
}

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
