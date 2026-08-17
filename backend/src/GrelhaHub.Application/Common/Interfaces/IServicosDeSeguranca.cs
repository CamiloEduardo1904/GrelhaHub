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
    (string Token, DateTimeOffset ExpiraEm) Gerar(Usuario usuario);
}
