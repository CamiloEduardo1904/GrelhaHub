using GrelhaHub.Application.Common.Interfaces;
using CalculoDiaOperacional = GrelhaHub.Domain.Gerais.DiaOperacional;

namespace GrelhaHub.Infrastructure.Services;

/// <summary>
/// Fonte de tempo do sistema. O cálculo do dia operacional vive no domínio
/// (<see cref="CalculoDiaOperacional"/>, <c>RN-GER-006</c>); aqui fica apenas a conversão de
/// fuso, que é detalhe de plataforma.
/// </summary>
public sealed class Relogio : IRelogio
{
    /// <summary><c>RN-GER-005</c> — a operação ocorre em America/Sao_Paulo; o armazenamento é em UTC.</summary>
    private static readonly TimeZoneInfo FusoDaOperacao =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateTimeOffset Agora => DateTimeOffset.UtcNow;

    public DateOnly DiaOperacional(DateTimeOffset instante) =>
        CalculoDiaOperacional.De(TimeZoneInfo.ConvertTime(instante, FusoDaOperacao));

    public DateOnly DiaOperacionalDeHoje() => DiaOperacional(Agora);
}
