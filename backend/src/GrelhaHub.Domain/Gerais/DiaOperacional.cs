namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// <c>RN-GER-006</c> ⚠⚠ — o dia operacional não coincide com o dia civil: começa às 04h00
/// (<c>PAR-GER-001</c>) e termina às 03h59 do dia seguinte.
/// <para>
/// Uma venda registrada à 01h30 de domingo pertence ao dia operacional de sábado.
/// </para>
/// <para>
/// <b>Está no Domain, e não na Infrastructure, de propósito:</b> é regra de negócio, não
/// detalhe de plataforma. Fica testável sem banco e sem host.
/// </para>
/// <para>
/// A fonte de verdade da <i>agregação</i> continua sendo a coluna gerada
/// <c>data_operacional</c> no banco (ADR-0011) — esta classe existe para a aplicação
/// calcular e exibir o mesmo valor, e para que o cálculo esteja escrito em um lugar só.
/// </para>
/// </summary>
public static class DiaOperacional
{
    /// <summary>Valor inicial de <c>PAR-GER-001</c>.</summary>
    public static readonly TimeSpan HoraDeViradaPadrao = TimeSpan.FromHours(4);

    /// <summary>
    /// Dia operacional a que pertence um instante já expresso no fuso da operação
    /// (<c>America/Sao_Paulo</c>, <c>RN-GER-005</c>).
    /// </summary>
    public static DateOnly De(DateTimeOffset instanteLocal, TimeSpan horaDeVirada)
    {
        if (horaDeVirada < TimeSpan.Zero || horaDeVirada >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(horaDeVirada),
                horaDeVirada,
                "A hora de virada do dia operacional deve estar entre 00:00 e 23:59.");
        }

        return DateOnly.FromDateTime(instanteLocal.Subtract(horaDeVirada).Date);
    }

    /// <inheritdoc cref="De(DateTimeOffset, TimeSpan)"/>
    public static DateOnly De(DateTimeOffset instanteLocal) =>
        De(instanteLocal, HoraDeViradaPadrao);
}
