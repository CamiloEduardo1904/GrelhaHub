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
