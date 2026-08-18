using FluentAssertions;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Domain.Tests.Gerais;

/// <summary>
/// <c>RN-GER-036</c> ⚠⚠ — o horário cadastrado e a virada que sai dele.
/// <para>
/// Domínio, sem banco: a derivação é a regra, e ela precisa ser conferível sem subir nada.
/// </para>
/// </summary>
public sealed class HorarioDeFuncionamentoTests
{
    private static readonly Guid Casa = Guid.CreateVersion7();

    private static HorarioDeFuncionamento Dia(DayOfWeek dia, string? abre, string? fecha) =>
        new(Casa, dia,
            abre is null ? null : TimeOnly.Parse(abre, null),
            fecha is null ? null : TimeOnly.Parse(fecha, null));

    [Fact(DisplayName = "RN-GER-036 — sem horário cadastrado, a virada é PAR-GER-001")]
    public void SemHorario_ViradaPadrao()
    {
        ViradaOperacional.De([]).Should().Be(TimeSpan.FromHours(4));
    }

    [Fact(DisplayName = "RN-GER-036 — casa que fecha antes da meia-noite tem dia operacional igual ao civil")]
    public void SemAtravessarMeiaNoite_ViradaZero()
    {
        var semana = new[]
        {
            Dia(DayOfWeek.Tuesday, "18:00", "23:30"),
            Dia(DayOfWeek.Wednesday, "18:00", "23:30"),
        };

        // Zero, e não 04h00: forçar a virada padrão numa casa que fecha 23h30 jogaria a venda das
        // 23h50 para o dia seguinte — o erro que RN-GER-006 existe para evitar, ao contrário.
        ViradaOperacional.De(semana).Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "RN-GER-036 — a virada é o fechamento mais tarde entre os que atravessam a meia-noite")]
    public void ComAtravessamento_PegaOMaisTarde()
    {
        var semana = new[]
        {
            Dia(DayOfWeek.Thursday, "18:00", "02:00"),
            Dia(DayOfWeek.Friday, "18:00", "04:00"),
            Dia(DayOfWeek.Saturday, "18:00", "03:00"),
            Dia(DayOfWeek.Sunday, null, null),
        };

        // 04h00 e não 02h00: uma virada menor cortaria a noite de sexta no meio, e metade do
        // faturamento apareceria no sábado.
        ViradaOperacional.De(semana).Should().Be(TimeSpan.FromHours(4));
    }

    [Fact(DisplayName = "RN-GER-036 — abertura sem fechamento é recusada")]
    public void MeioHorario_Recusado()
    {
        var criar = () => Dia(DayOfWeek.Monday, "18:00", null);

        criar.Should().Throw<ArgumentException>().WithMessage("*RN-GER-036*");
    }

    [Fact(DisplayName = "RN-GER-036 — dia repetido na semana é recusado")]
    public void DiaRepetido_Recusado()
    {
        var unidade = new Unidade("Brasa LTDA", "Brasa", "11111111000191", "brasa", null);

        var definir = () => unidade.DefinirHorarios(
        [
            Dia(DayOfWeek.Friday, "18:00", "23:00"),
            Dia(DayOfWeek.Friday, "11:00", "15:00"),
        ]);

        definir.Should().Throw<ArgumentException>().WithMessage("*duas vezes*");
    }

    [Fact(DisplayName = "RN-GER-036 — definir o horário rederiva a virada da unidade")]
    public void DefinirHorarios_RederivaAVirada()
    {
        var unidade = new Unidade("Brasa LTDA", "Brasa", "11111111000191", "brasa", null);

        unidade.HoraDeViradaOperacional.Should().Be(TimeSpan.FromHours(4));

        unidade.DefinirHorarios([Dia(DayOfWeek.Saturday, "19:00", "03:00")]);

        unidade.HoraDeViradaOperacional.Should().Be(TimeSpan.FromHours(3));
    }

    [Fact(DisplayName = "RN-VEN-048 — unidade sem horário cadastrado está sempre aberta")]
    public void SemHorario_SempreAberta()
    {
        var unidade = new Unidade("Brasa LTDA", "Brasa", "11111111000191", "brasa", null);

        // A regra nova não pode fechar a loja de quem já vendia no dia em que entrou.
        unidade.AbertaEm(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Theory(DisplayName = "RN-VEN-048 — aberta ou fechada conforme o horário da casa")]
    // Sábado 19/09/2026: abre 19h00 e fecha 03h00 de domingo. Instantes em UTC (−3).
    [InlineData("2026-09-19T21:00:00Z", false)] // 18h00 de sábado — ainda não abriu
    [InlineData("2026-09-19T23:00:00Z", true)]  // 20h00 de sábado — aberta
    [InlineData("2026-09-20T04:00:00Z", true)]  // 01h00 de domingo — a noite de sábado continua
    [InlineData("2026-09-20T07:00:00Z", false)] // 04h00 de domingo — já fechou
    public void AbertaEm_ConfereODiaAnteriorQuandoAtravessa(string instante, bool esperado)
    {
        var unidade = new Unidade("Brasa LTDA", "Brasa", "11111111000191", "brasa", null);

        unidade.DefinirHorarios([Dia(DayOfWeek.Saturday, "19:00", "03:00")]);

        // ⚠ O caso da 01h00 de domingo é o que se erra: olhar só o dia corrente responderia
        // "fechada" com a casa cheia.
        unidade.AbertaEm(DateTimeOffset.Parse(instante, null)).Should().Be(esperado);
    }
}
