using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// Ponto único de consulta de módulo habilitado — <c>RN-GER-017</c>, ADR-0014.
/// </summary>
public interface IModulos
{
    /// <summary>
    /// O módulo está habilitado na unidade, no instante informado?
    /// <para>
    /// <c>RN-GER-019</c> — o núcleo (<c>GER</c>) responde sempre <c>true</c> sem ir ao banco:
    /// não existe unidade operando sem autenticação, parâmetro e auditoria.
    /// </para>
    /// </summary>
    Task<bool> EstaHabilitadoAsync(
        Guid unidadeId,
        CodigoDeModulo modulo,
        DateTimeOffset? em = null,
        CancellationToken cancellationToken = default);

    /// <summary>Módulos habilitados na unidade, para a interface montar o menu.</summary>
    Task<IReadOnlyList<CodigoDeModulo>> HabilitadosAsync(
        Guid unidadeId, DateTimeOffset? em = null, CancellationToken cancellationToken = default);

    /// <summary>Descarta o cache da unidade. Chamado pela habilitação e pela desabilitação.</summary>
    void Invalidar(Guid unidadeId);
}
