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

    /// <summary><c>RN-GER-010</c> — uma claim por permissao concedida.</summary>
    public const string ClaimPermissao = "permissao";

    /// <summary><c>RF-SEG-005</c> — precisa casar com ContextoDeExecucaoHttp.ClaimTrocarSenha.</summary>
    public const string ClaimTrocarSenha = "trocar_senha";

    /// <summary><c>RN-GER-014</c> — ver ADR-0013. Precisa casar com ContextoDeExecucaoHttp.</summary>
    public const string ClaimPlataforma = "plataforma";

    public (string Token, DateTimeOffset ExpiraEm) Gerar(Usuario usuario, IEnumerable<string> permissoes)
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

        };

        // RN-GER-010 — as PERMISSOES no token, uma claim por codigo.
        //
        // Nao o nome do perfil: era assim antes, e verificar perfil == "Gerente" espalhado pelo
        // codigo e exatamente o que impediria criar o perfil Operador (RN-GER-022) sem cacar
        // condicionais. O perfil vai tambem, mas so como rotulo para a interface.
        foreach (var permissao in permissoes ?? [])
        {
            claims.Add(new Claim(ClaimPermissao, permissao));
        }

        if (usuario.Perfil is { } perfil)
        {
            claims.Add(new Claim(ClaimTypes.Role, perfil.Nome));
        }

        // RN-GER-009 — a unidade viaja no token; é ela que alimenta o filtro global.
        // O usuário de plataforma não tem unidade, e a AUSÊNCIA da claim é o que o mantém sem
        // escopo: emitir Guid.Empty aqui faria o filtro procurar uma unidade que não existe.
        if (usuario.UnidadeId is { } unidadeId)
        {
            claims.Add(new Claim(ClaimUnidade, unidadeId.ToString()));
        }

        // RN-GER-014 ⚠⚠ / ADR-0013 — a claim que desliga o recorte por unidade.
        //
        // Emitida SÓ aqui, e só quando a marca está na entidade. É o único ponto de origem do
        // maior risco do modelo: um usuário comum que a recebesse passaria a ver todos os
        // clientes. Há teste de integração provando que ele não a recebe.
        if (usuario.EhPlataforma)
        {
            claims.Add(new Claim(ClaimPlataforma, "true"));
        }

        // RF-SEG-005 — a pendencia viaja no token, e por isso o behavior nao precisa consultar o
        // banco a cada requisicao. O token e curto (8h) e a troca reemite: nao ha janela relevante
        // em que a claim fique desatualizada a favor do usuario.
        if (usuario.DeveTrocarSenha)
        {
            claims.Add(new Claim(ClaimTrocarSenha, "true"));
        }

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
