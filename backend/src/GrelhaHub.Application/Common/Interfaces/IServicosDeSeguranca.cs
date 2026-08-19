using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary><c>RNF-005</c> — hash com salt; nunca a senha em texto claro.</summary>
public interface IHashDeSenha
{
    string Gerar(string senha);
    bool Confere(string senha, string hash);
}

/// <summary>ADR-0003 — autenticação stateless por JWT.</summary>
public interface IGeradorDeToken
{
    /// <summary>
    /// <c>RN-GER-010</c> — as permissoes entram como claim. Passadas por parametro e nao lidas da
    /// entidade de proposito: o token so carrega o que quem o emite carregou explicitamente, e um
    /// Include esquecido produziria token SEM permissao em vez de permissao a mais.
    /// </summary>
    (string Token, DateTimeOffset ExpiraEm) Gerar(Usuario usuario, IEnumerable<string> permissoes);
}

/// <summary>
/// O desfecho de uma verificação de credencial no login — <c>RN-GER-040</c>.
/// <para>
/// <paramref name="BloqueadaPor"/> só vem preenchido quando <paramref name="Bloqueada"/>: é o
/// tempo restante que a mensagem ao usuário anuncia (a fresta aceita da ADR-0022 §4).
/// </para>
/// </summary>
public sealed record VerificacaoDeCredencial(bool Valida, bool Bloqueada, TimeSpan? BloqueadaPor)
{
    public static VerificacaoDeCredencial Sucesso { get; } = new(true, false, null);

    public static VerificacaoDeCredencial SenhaIncorreta { get; } = new(false, false, null);

    public static VerificacaoDeCredencial Bloqueio(TimeSpan restante) => new(false, true, restante);
}

/// <summary>
/// <c>RN-GER-040</c> ⚠⚠ — confere a senha CONTANDO as falhas e aplicando o bloqueio.
/// <para>
/// Implementado na Api sobre <c>SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true)</c>
/// (ADR-0022 §4): o <c>SignInManager</c> vive no runtime do ASP.NET, que a Application não
/// referencia. <b>Não use <c>IHashDeSenha.Confere</c> no login</b> — ele confere sem contar, e
/// era exatamente a lacuna que a ADR-0021 encontrou. <c>IHashDeSenha</c> segue valendo para
/// gerar hash e para conferências que não são tentativa de login (a senha atual na troca).
/// </para>
/// </summary>
public interface IVerificadorDeCredenciais
{
    Task<VerificacaoDeCredencial> VerificarAsync(Usuario usuario, string senha);
}
