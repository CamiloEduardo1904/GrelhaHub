namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// O horário de um dia da semana na unidade — <c>RN-GER-036</c> ⚠⚠.
/// <para>
/// <b>Dia fechado é <see cref="Abre"/> nulo</b>, e não um intervalo de duração zero: "não abre" e
/// "abre e fecha no mesmo instante" são coisas diferentes, e a segunda não existe na operação.
/// </para>
/// </summary>
public sealed class HorarioDeFuncionamento
{
    private HorarioDeFuncionamento()
    {
    }

    public HorarioDeFuncionamento(Guid unidadeId, DayOfWeek diaDaSemana, TimeOnly? abre, TimeOnly? fecha)
    {
        if (unidadeId == Guid.Empty)
        {
            throw new ArgumentException("O horário pertence a uma unidade.", nameof(unidadeId));
        }

        // Um dos dois sem o outro é cadastro pela metade — e o efeito seria pior que recusar:
        // "abre 18h e nunca fecha" viraria uma casa aberta para sempre.
        if (abre.HasValue != fecha.HasValue)
        {
            throw new ArgumentException(
                "RN-GER-036 — informe abertura e fechamento juntos, ou marque o dia como fechado.",
                nameof(abre));
        }

        UnidadeId = unidadeId;
        DiaDaSemana = diaDaSemana;
        Abre = abre;
        Fecha = fecha;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UnidadeId { get; private set; }
    public DayOfWeek DiaDaSemana { get; private set; }

    /// <summary>Nulo quando a casa não abre neste dia.</summary>
    public TimeOnly? Abre { get; private set; }

    public TimeOnly? Fecha { get; private set; }

    public bool Fechado => Abre is null;

    /// <summary>
    /// O fechamento cai no dia seguinte — abre 18h00 e fecha 03h00.
    /// <para>
    /// <b>É esta propriedade que produz o dia operacional.</b> Casa que fecha antes da meia-noite
    /// não precisa de virada nenhuma: o dia dela já é o dia civil.
    /// </para>
    /// </summary>
    public bool AtravessaMeiaNoite => Abre is { } a && Fecha is { } f && f <= a;
}

/// <summary>
/// Deriva a virada do dia operacional a partir do horário cadastrado — <c>RN-GER-036</c>,
/// <c>RN-GER-006</c> ⚠⚠.
/// <para>
/// <b>Existe como função pura, num lugar só, porque o valor é gravado em três lugares:</b> a
/// unidade guarda o derivado, o pedido copia o aplicado, e a tela mostra o vigente. Três cópias de
/// um cálculo repetido divergem; três cópias de um resultado calculado aqui, não.
/// </para>
/// </summary>
public static class ViradaOperacional
{
    /// <summary>
    /// <c>PAR-GER-001</c> — o valor de quem ainda não cadastrou horário.
    /// <para>
    /// Não é chute: 04h00 era a constante do sistema inteiro até 18/08/2026, e mantê-la como padrão
    /// é o que faz a mudança **não mexer** em quem já operava.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Padrao = TimeSpan.FromHours(4);

    /// <summary>
    /// A virada da semana inteira: o <b>fechamento que atravessa a meia-noite</b> mais tarde.
    /// </summary>
    /// <remarks>
    /// Três casos, e o do meio é o que costuma ser esquecido:
    /// <list type="bullet">
    /// <item><b>Nenhum horário cadastrado</b> → <see cref="Padrao"/>. Ausência não pode mudar a
    /// apuração de quem já vendia.</item>
    /// <item><b>Ninguém atravessa a meia-noite</b> → <c>00:00</c>. Casa que fecha às 23h30 não tem
    /// madrugada para reclassificar, e forçar 04h00 nela jogaria a venda das 23h50 para o dia
    /// seguinte — que é exatamente o erro que <c>RN-GER-006</c> existe para evitar, ao contrário.</item>
    /// <item><b>Alguém atravessa</b> → o fechamento mais tarde entre os que atravessam. Se um dia
    /// fecha 02h00 e o sábado fecha 04h00, a virada é 04h00: uma virada menor cortaria a noite de
    /// sábado no meio.</item>
    /// </list>
    /// </remarks>
    public static TimeSpan De(IEnumerable<HorarioDeFuncionamento> horarios)
    {
        ArgumentNullException.ThrowIfNull(horarios);

        var lista = horarios.ToList();

        if (lista.Count == 0)
        {
            return Padrao;
        }

        var atravessam = lista
            .Where(h => h.AtravessaMeiaNoite)
            .Select(h => h.Fecha!.Value.ToTimeSpan())
            .ToList();

        return atravessam.Count == 0 ? TimeSpan.Zero : atravessam.Max();
    }
}
