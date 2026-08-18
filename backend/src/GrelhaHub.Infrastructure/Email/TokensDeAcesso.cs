using System.Security.Cryptography;
using System.Text;
using GrelhaHub.Application.Common.Interfaces;

namespace GrelhaHub.Infrastructure.Email;

/// <summary><c>RN-GER-033</c> — token aleatório, hash determinístico. Ver <see cref="ITokensDeAcesso"/>.</summary>
public sealed class TokensDeAcesso : ITokensDeAcesso
{
    /// <summary>
    /// 32 bytes. Não é número escolhido por gosto: é o tamanho a partir do qual adivinhar deixa de
    /// ser uma estratégia, e ainda cabe numa URL sem quebrar linha no cliente de e-mail.
    /// </summary>
    private const int Bytes = 32;

    public (string Token, string Hash) Gerar()
    {
        // RandomNumberGenerator, nunca `Random`: `Random` é previsível a partir da semente, e um
        // token previsível é um link de redefinição que qualquer um monta.
        var bruto = RandomNumberGenerator.GetBytes(Bytes);

        // Base64Url: vai inteiro numa URL sem escape. Base64 comum traz `+`, `/` e `=`, que viram
        // `%2B` no caminho e quebram quando um cliente de e-mail "ajuda" reescrevendo o link.
        var token = Base64UrlEncode(bruto);

        return (token, HashDe(token));
    }

    public string HashDe(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        return Convert.ToHexStringLower(digest);
    }

    private static string Base64UrlEncode(byte[] bruto) =>
        Convert.ToBase64String(bruto).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
