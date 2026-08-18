using FluentAssertions;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Domain.Tests.Gerais;

/// <summary>
/// <c>RN-GER-037</c> e <c>RN-VEN-048</c> — as duas respostas que a casa dá sobre si mesma.
/// <para>
/// ⚠⚠ <b>Por que estes testes existem separados de <c>Unidade</c>:</b> o ADR-0019 mandou as
/// consultas para um modelo de leitura que não carrega agregado, e estas duas perguntas passaram a
/// ser feitas dos DOIS lados. A alternativa era reescrever a regra em SQL — duas implementações
/// divergindo em silêncio, que é o defeito que <c>DiaOperacionalParidadeTests</c> existe para pegar.
/// Extraída a função pura, o teste dela cobre os dois chamadores.
/// </para>
/// </summary>
public sealed class ApresentacaoDaCasaTests
{
    [Fact(DisplayName = "RN-GER-037 — sem logradouro não há endereço, mesmo com cidade e UF")]
    public void SemLogradouro_NaoHaEndereco()
    {
        // "Centro · Curitiba/PR" não leva ninguém a lugar nenhum. Meia informação num cardápio é
        // pior do que informação nenhuma.
        ApresentacaoDaCasa.Resumir(null, "100", "Centro", "Curitiba", "PR")
            .Should().BeNull();

        ApresentacaoDaCasa.Resumir("   ", "100", "Centro", "Curitiba", "PR")
            .Should().BeNull();
    }

    [Theory(DisplayName = "RN-GER-037 — o endereço em uma linha, com o que houver")]
    [InlineData("Rua das Brasas", "42", "Centro", "Curitiba", "PR", "Rua das Brasas, 42 · Centro · Curitiba/PR")]
    [InlineData("Rua das Brasas", null, "Centro", "Curitiba", "PR", "Rua das Brasas · Centro · Curitiba/PR")]
    [InlineData("Rua das Brasas", "42", null, "Curitiba", "PR", "Rua das Brasas, 42 · Curitiba/PR")]
    [InlineData("Rua das Brasas", "42", "Centro", "Curitiba", null, "Rua das Brasas, 42 · Centro · Curitiba")]
    [InlineData("Rua das Brasas", "42", "Centro", null, "PR", "Rua das Brasas, 42 · Centro · PR")]
    [InlineData("Rua das Brasas", null, null, null, null, "Rua das Brasas")]
    public void Resumir_MontaComOQueHouver(
        string logradouro, string? numero, string? bairro, string? cidade, string? uf, string esperado) =>
        ApresentacaoDaCasa.Resumir(logradouro, numero, bairro, cidade, uf).Should().Be(esperado);

    [Fact(DisplayName = "RN-GER-036 — semana vazia responde ABERTA")]
    public void SemanaVazia_RespondeAberta() =>
        // A casa que ainda não cadastrou horário não deve sumir da vitrine por causa de um
        // cadastro que ninguém pediu para ela fazer.
        ApresentacaoDaCasa.AbertaEm([], "America/Sao_Paulo", DateTimeOffset.UtcNow)
            .Should().BeTrue();

    /// <summary>
    /// ⚠⚠ <b>O teste que a conversão para o fuso da casa existe para passar.</b> O contêiner roda
    /// em UTC: 23h de sexta em UTC são 20h de sexta em São Paulo. Perguntar com a hora de Londres
    /// fecharia a loja três horas antes, sem erro nenhum — só uma vitrine escura no melhor horário.
    /// </summary>
    [Fact(DisplayName = "RN-VEN-048 ⚠ — a pergunta é respondida no fuso DA CASA, não no do servidor")]
    public void AbertaEm_UsaOFusoDaCasa()
    {
        // 2026-08-21 é uma sexta-feira.
        var instante = new DateTimeOffset(2026, 8, 21, 23, 30, 0, TimeSpan.Zero);

        ApresentacaoDaCasa.DiaDaCasa[] semana =
        [
            new(DayOfWeek.Friday, new TimeOnly(18, 0), new TimeOnly(23, 0)),
        ];

        // 23h30 UTC = 20h30 em São Paulo, dentro de 18h–23h.
        ApresentacaoDaCasa.AbertaEm(semana, "America/Sao_Paulo", instante).Should().BeTrue();

        // No fuso do servidor a mesma casa estaria fechada — e é esse o erro que a conversão evita.
        ApresentacaoDaCasa.AbertaEm(semana, "UTC", instante).Should().BeFalse();
    }

    /// <summary>
    /// <c>RN-GER-036</c> — <c>18:00–02:00</c> é UM turno, e não dois. Comparar
    /// <c>hora &lt; fecha</c> nele fecharia a loja às 18h01.
    /// </summary>
    [Fact(DisplayName = "RN-GER-036 ⚠⚠ — o turno que atravessa a meia-noite continua o mesmo dia")]
    public void AtravessaMeiaNoite_ContinuaAberta()
    {
        ApresentacaoDaCasa.DiaDaCasa[] semana =
        [
            new(DayOfWeek.Saturday, new TimeOnly(18, 0), new TimeOnly(2, 0)),
        ];

        // Sábado 22h, em UTC para não depender de tzdata regional.
        ApresentacaoDaCasa.AbertaEm(
            semana, "UTC", new DateTimeOffset(2026, 8, 22, 22, 0, 0, TimeSpan.Zero))
            .Should().BeTrue("22h está dentro do turno que começou às 18h");

        // Domingo 01h — o turno de SÁBADO ainda não terminou. É o caso que o dia operacional
        // (RN-GER-006) trata em outro lugar, e que aqui decide se o botão de pedir aparece.
        ApresentacaoDaCasa.AbertaEm(
            semana, "UTC", new DateTimeOffset(2026, 8, 23, 1, 0, 0, TimeSpan.Zero))
            .Should().BeTrue("à 01h de domingo quem está aberta é a casa de sábado");

        // Domingo 03h — o turno acabou às 02h e domingo não tem horário.
        ApresentacaoDaCasa.AbertaEm(
            semana, "UTC", new DateTimeOffset(2026, 8, 23, 3, 0, 0, TimeSpan.Zero))
            .Should().BeFalse();
    }

    [Fact(DisplayName = "RN-GER-036 — dia sem horário é dia FECHADO, e não dia ausente")]
    public void DiaSemHorario_EhFechado()
    {
        ApresentacaoDaCasa.DiaDaCasa[] semana =
        [
            new(DayOfWeek.Saturday, null, null),
            new(DayOfWeek.Sunday, new TimeOnly(11, 0), new TimeOnly(15, 0)),
        ];

        ApresentacaoDaCasa.AbertaEm(
            semana, "UTC", new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero))
            .Should().BeFalse("sábado está na semana com o par nulo: a casa declarou que fecha");
    }
}

/// <summary>
/// <c>Cpf.Formatar</c> — ADR-0019, a máscara aplicada sobre o dígito cru que a visão devolve.
/// </summary>
public sealed class FormatacaoDeCpfTests
{
    [Fact(DisplayName = "ADR-0019 — a máscara sobre dígito cru é a MESMA do value object")]
    public void Formatar_EhAMesmaMascaraDoValueObject()
    {
        // ⚠ É esta igualdade que impede a segunda implementação: o lado da leitura devolve string,
        // o lado da escrita devolve `Cpf`, e os dois precisam exibir o mesmo texto.
        var cpf = Cpf.De("52998224725");

        Cpf.Formatar("52998224725").Should().Be(cpf.Formatado());
        Cpf.Formatar("52998224725").Should().Be("529.982.247-25");
    }

    [Theory(DisplayName = "ADR-0019 — vazio vira nulo; o que não tem 11 dígitos volta como veio")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    // Mascarar o que não é CPF produziria um texto que PARECE documento e não é — pior do que
    // mostrar o valor cru, porque some com o sinal de que o dado está errado.
    [InlineData("123", "123")]
    [InlineData("529982247251234", "529982247251234")]
    public void Formatar_ForaDoPadrao(string? entrada, string? esperado) =>
        Cpf.Formatar(entrada).Should().Be(esperado);
}
