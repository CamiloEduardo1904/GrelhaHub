using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GrelhaHub.Infrastructure.Identity;

/// <summary>
/// <c>RNF-005</c> — senhas com hash + salt. Usa o <c>PasswordHasher</c> do ASP.NET Core
/// Identity (PBKDF2, salt por senha), e não um algoritmo próprio.
/// </summary>
public sealed class HashDeSenha : IHashDeSenha
{
    private readonly PasswordHasher<string> _hasher = new();

    public string Gerar(string senha) => _hasher.HashPassword(string.Empty, senha);

    public bool Confere(string senha, string hash) =>
        _hasher.VerifyHashedPassword(string.Empty, hash, senha) != PasswordVerificationResult.Failed;
}

/// <summary>Emite o JWT — ADR-0003, autenticação stateless.</summary>
public sealed class GeradorDeTokenJwt(IConfiguration configuration) : IGeradorDeToken
{
    public const string ClaimUnidade = "unidade";
    public const string ClaimSlug = "slug";

    public (string Token, DateTimeOffset ExpiraEm) Gerar(Usuario usuario)
    {
        var chave = configuration["Jwt:Chave"];

        if (string.IsNullOrWhiteSpace(chave) || Encoding.UTF8.GetByteCount(chave) < 32)
        {
            // Falha ruidosa na partida é melhor que token assinado com chave fraca:
            // RNF-008 exige que a chave venha de segredo, e uma chave curta invalida a
            // assinatura HS256 na prática.
            throw new InvalidOperationException(
                "Jwt:Chave não configurada ou menor que 32 bytes. Ver .env.example.");
        }

        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
            SecurityAlgorithms.HmacSha256);

        var minutos = int.TryParse(configuration["Jwt:ExpiracaoEmMinutos"], out var m) ? m : 480;
        var expiraEm = DateTimeOffset.UtcNow.AddMinutes(minutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(ClaimTypes.Name, usuario.Nome),

            // RN-GER-009 — a unidade viaja no token; é ela que alimenta o filtro global.
            new(ClaimUnidade, usuario.UnidadeId.ToString()),

            // RN-GER-010 — o perfil determina quais operações o usuário pode realizar.
            new(ClaimTypes.Role, usuario.Perfil.ToString()),
        };

        if (usuario.Unidade is not null)
        {
            claims.Add(new Claim(ClaimSlug, usuario.Unidade.Slug));
        }

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Emissor"],
            audience: configuration["Jwt:Audiencia"],
            claims: claims,
            expires: expiraEm.UtcDateTime,
            signingCredentials: credenciais);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }
}
