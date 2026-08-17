namespace GrelhaHub.Application.Common.Exceptions;

/// <summary>
/// Violação de regra de negócio detectada pela aplicação.
/// <para>
/// ADR-0009 — <b>a mensagem de erro ao usuário vem sempre da aplicação</b>. Erro de
/// constraint que vaza para o usuário é defeito da aplicação, não do banco.
/// </para>
/// </summary>
public sealed class RegraDeNegocioException(string mensagem, string? regra = null)
    : Exception(mensagem)
{
    /// <summary>ID da regra violada, quando houver — ex.: <c>RN-VEN-002</c>.</summary>
    public string? Regra { get; } = regra;
}

public sealed class NaoEncontradoException(string mensagem) : Exception(mensagem);

public sealed class NaoAutorizadoException(string mensagem) : Exception(mensagem);
