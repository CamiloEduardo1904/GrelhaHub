using FluentAssertions;
using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Tests.Common;

/// <summary>
/// <c>RN-GER-023</c> — o CPF. <c>SPEC-GER-007</c> CA-1 a CA-3.
/// </summary>
public sealed class CpfTests
{
    // CPFs válidos conhecidos, com dígitos verificadores corretos.
    [Theory(DisplayName = "RN-GER-023 — CPF com dígito verificador correto é aceito")]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    [InlineData("398.334.454-20")]
    public void Valido_EhAceito(string entrada)
    {
        Cpf.TentarDe(entrada, out var cpf).Should().BeTrue();

        cpf.Valor.Should().HaveLength(11);
    }

    [Theory(DisplayName = "RN-GER-023 — CPF com dígito verificador errado é recusado")]
    [InlineData("529.982.247-26")] // último dígito trocado
    [InlineData("529.982.247-15")] // penúltimo trocado
    [InlineData("111.444.777-34")]
    [InlineData("123.456.789-00")]
    public void VerificadorErrado_EhRecusado(string entrada)
    {
        Cpf.TentarDe(entrada, out _).Should().BeFalse(
            "o dígito verificador é o que separa CPF de sequência de onze números");
    }

    /// <summary>
    /// Este é o caso que uma validação "só a conta" deixaria passar — e é o primeiro CPF que
    /// alguém digita para testar um formulário.
    /// </summary>
    [Theory(DisplayName = "RN-GER-023 — sequência repetida é recusada mesmo com o cálculo fechando")]
    [InlineData("111.111.111-11")]
    [InlineData("000.000.000-00")]
    [InlineData("99999999999")]
    [InlineData("55555555555")]
    public void SequenciaRepetida_EhRecusada(string entrada)
    {
        // Prova a premissa: o algoritmo de módulo 11 ACEITA estas sequências. A recusa vem da
        // checagem explícita, não do cálculo — se alguém remover a checagem achando-a redundante,
        // este teste cai.
        VerificadoresFecham(new string(entrada.Where(char.IsAsciiDigit).ToArray()))
            .Should().BeTrue("o cálculo fecha — é justamente por isso que a checagem existe");

        Cpf.TentarDe(entrada, out _).Should().BeFalse();
    }

    [Theory(DisplayName = "RN-GER-013 — o CPF é guardado somente com dígitos, sem máscara")]
    [InlineData("529.982.247-25")]
    [InlineData("529 982 247 25")]
    [InlineData("  52998224725  ")]
    public void Mascara_NaoEntraNoValor(string entrada)
    {
        Cpf.De(entrada).Valor.Should().Be("52998224725");
    }

    [Theory(DisplayName = "RN-GER-023 — quantidade de dígitos diferente de 11 é recusada")]
    [InlineData("5299822472")]      // 10
    [InlineData("529982247251")]    // 12
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void QuantidadeErrada_EhRecusada(string? entrada)
    {
        Cpf.TentarDe(entrada, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "RN-GER-023 — De lança quando o CPF é inválido, citando a regra")]
    public void De_LancaComRegraCitada()
    {
        var acao = () => Cpf.De("123.456.789-00");

        acao.Should().Throw<ArgumentException>().WithMessage("*RN-GER-023*");
    }

    [Fact(DisplayName = "RN-GER-013 — Formatado é só apresentação e não volta para o banco")]
    public void Formatado_NaoAlteraOValor()
    {
        var cpf = Cpf.De("52998224725");

        cpf.Formatado().Should().Be("529.982.247-25");
        cpf.Valor.Should().Be("52998224725", "o valor guardado continua sem máscara");
    }

    [Fact(DisplayName = "RN-GER-023 — dois CPFs com os mesmos dígitos são iguais")]
    public void Igualdade_EhPorValor()
    {
        Cpf.De("529.982.247-25").Should().Be(Cpf.De("52998224725"),
            "value object compara por valor: a máscara não faz duas pessoas");
    }

    /// <summary>
    /// Módulo 11 reimplementado aqui de propósito — para provar, no teste de sequência repetida,
    /// que o cálculo sozinho aceita `111.111.111-11`.
    /// </summary>
    private static bool VerificadoresFecham(string digitos)
    {
        if (digitos.Length != 11)
        {
            return false;
        }

        return digitos[9] == Digito(digitos, 9, 10) && digitos[10] == Digito(digitos, 10, 11);
    }

    private static char Digito(string digitos, int ate, int pesoInicial)
    {
        var soma = 0;

        for (var i = 0; i < ate; i++)
        {
            soma += (digitos[i] - '0') * (pesoInicial - i);
        }

        var resto = soma * 10 % 11;

        return (char)('0' + (resto == 10 ? 0 : resto));
    }
}
