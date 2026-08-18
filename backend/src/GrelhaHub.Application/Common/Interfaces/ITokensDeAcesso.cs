namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// Gera e confere o token do link — <c>RN-GER-033</c> ⚠⚠, ADR-0016.
/// <para>
/// <b>São duas coisas diferentes com nomes parecidos:</b> o <i>token</i> vai no e-mail e existe uma
/// vez; o <i>hash</i> vai para o banco e é o que a consulta de resgate procura. Guardar o token em
/// claro faria de qualquer leitura do banco — dump, backup, tela de suporte — uma porta para
/// entrar na conta de quem pediu recuperação.
/// </para>
/// <para>
/// O hash aqui é <b>determinístico</b> (SHA-256), ao contrário do hash de senha, que tem salt por
/// registro. A diferença é deliberada e vem do que cada um protege: senha é escolhida por gente,
/// tem pouca entropia e precisa resistir a força bruta em cima do banco vazado; token é 32 bytes
/// aleatórios, não se adivinha, e **precisa ser procurável** — o resgate recebe o token e tem de
/// achar a linha. Salt por registro obrigaria a varrer a tabela inteira comparando um a um.
/// </para>
/// </summary>
public interface ITokensDeAcesso
{
    /// <summary>Um token novo: o valor em claro, para o e-mail, e o hash, para o banco.</summary>
    (string Token, string Hash) Gerar();

    /// <summary>O hash de um token recebido, para procurar a linha correspondente.</summary>
    string HashDe(string token);
}
