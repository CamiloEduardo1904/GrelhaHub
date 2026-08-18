using FluentAssertions;
using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Tests.Common;

/// <summary><c>SPEC-GER-001</c> — <c>RN-GER-003</c>: reais com duas casas decimais.</summary>
public class DinheiroTests
{
    [Theory(DisplayName = "RN-GER-003 — valor monetário é arredondado para duas casas, não truncado")]
    [InlineData(10.999, 11.00)]
    [InlineData(10.994, 10.99)]
    [InlineData(0.125, 0.13)]      // AwayFromZero: o padrão bancário do .NET daria 0,12
    [InlineData(0.135, 0.14)]
    [InlineData(-0.125, -0.13)]
    public void Arredonda_ParaDuasCasas(decimal entrada, decimal esperado)
    {
        Dinheiro.De(entrada).Valor.Should().Be(esperado);
    }

    [Fact(DisplayName = "RN-GER-003 — a soma de uma sequência mantém duas casas")]
    public void Somar_MantemDuasCasas()
    {
        var parcelas = new[] { Dinheiro.De(28.00m), Dinheiro.De(18.00m), Dinheiro.De(7.00m) };

        Dinheiro.Somar(parcelas).Should().Be(Dinheiro.De(53.00m));
    }

    [Fact(DisplayName = "RN-VEN-009 — dinheiro multiplicado por quantidade inteira dá o total do item")]
    public void Multiplicacao_PorQuantidade()
    {
        (Dinheiro.De(32.50m) * 3).Should().Be(Dinheiro.De(97.50m));
    }

    [Fact(DisplayName = "RN-GER-003 — dinheiro e quantidade são tipos distintos e não se somam")]
    public void DinheiroEQuantidade_NaoSaoIntercambiaveis()
    {
        // A garantia real é de compilação: `Dinheiro.De(1m) + Quantidade.De(1m)` não compila,
        // e é isso que o decimal cru não oferece. O teste registra a intenção e falha se
        // alguém acrescentar uma conversão implícita entre os dois.
        typeof(Dinheiro).GetMethods()
            .Where(metodo => metodo.Name is "op_Implicit" or "op_Explicit")
            .Should().BeEmpty("conversão silenciosa entre dinheiro e número anula o propósito do tipo");
    }

    [Fact(DisplayName = "RN-GER-003 — a formatação não embute símbolo de moeda")]
    public void ToString_NaoEmbuteSimbolo()
    {
        // Moeda é apresentação: o front usa Intl.NumberFormat('pt-BR'). Um "R$" aqui
        // apareceria duplicado em log, em JSON e em mensagem de erro.
        Dinheiro.De(1234.5m).ToString().Should().Be("1234.50");
    }
}
