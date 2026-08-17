using System.Globalization;
using System.Text;
using GrelhaHub.Api.Seguranca;
using GrelhaHub.Application;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Infrastructure;
using GrelhaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(PoliticaDesenvolvimento);
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
}

await app.RunAsync();

/// <summary>Exposto para os testes de integração (<c>WebApplicationFactory&lt;Program&gt;</c>).</summary>
public partial class Program;
