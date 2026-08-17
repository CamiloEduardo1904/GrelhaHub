using FluentAssertions;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Domain.Tests.Gerais;

/// <summary>
/// Primeiro teste do projeto — serve também de <b>exemplo da convenção obrigatória</b>:
/// o ID da regra vai no <c>DisplayName</c>.
/// <para>
/// É isso que torna a rastreabilidade real. Quando <c>RN-GER-006</c> mudar de estado na
/// entrevista, um <c>grep RN-GER-006</c> encontra a spec, o código e este teste.
/// Ver <c>docs/06-guia-de-specs.md</c>.
/// </para>
/// </summary>
public class DiaOperacionalTests
{
    [Fact(DisplayName = "RN-GER-006 — venda de madrugada pertence ao dia operacional anterior")]
    public void VendaDeMadrugada_PertenceAoDiaAnterior()
    {
        // Domingo, 01h30 — dentro do turno de sábado à noite.
        var instante = new DateTimeOffset(2026, 8, 16, 1, 30, 0, TimeSpan.FromHours(-3));

        DiaOperacional.De(instante)
            .Should().Be(new DateOnly(2026, 8, 15), "a venda ainda é do turno de sábado");
    }

    [Fact(DisplayName = "RN-GER-006 — venda após a virada pertence ao próprio dia")]
    public void VendaAposAVirada_PertenceAoProprioDia()
    {
        // Domingo, 04h01 — já é o novo dia operacional.
        var instante = new DateTimeOffset(2026, 8, 16, 4, 1, 0, TimeSpan.FromHours(-3));

        DiaOperacional.De(instante).Should().Be(new DateOnly(2026, 8, 16));
    }

    [Theory(DisplayName = "RN-GER-006 — a virada às 04h00 é o limite exato do dia operacional")]
    [InlineData(3, 59, "2026-08-15")]
    [InlineData(4, 0, "2026-08-16")]
    public void ViradaAsQuatro_EhOLimiteExato(int hora, int minuto, string diaEsperado)
    {
        var instante = new DateTimeOffset(2026, 8, 16, hora, minuto, 0, TimeSpan.FromHours(-3));

        DiaOperacional.De(instante).Should().Be(DateOnly.Parse(diaEsperado,
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "RN-GER-006 — hora de virada fora de um dia é rejeitada")]
    public void HoraDeViradaInvalida_EhRejeitada()
    {
        var instante = DateTimeOffset.UnixEpoch;

        var acao = () => DiaOperacional.De(instante, TimeSpan.FromHours(25));

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }
}
