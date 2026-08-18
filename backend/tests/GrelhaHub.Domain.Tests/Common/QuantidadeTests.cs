using FluentAssertions;
using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Tests.Common;

/// <summary>
/// <c>SPEC-GER-001</c> — <c>RN-GER-004</c>: quantidade de insumo com <b>quatro</b> casas
/// decimais.
/// <para>
/// Este é o teste que guarda a armadilha descrita na <c>SPEC-GER-001</c> §4: uma quantidade
/// tratada com a precisão do dinheiro perde as duas últimas casas <b>sem dar erro</b>, e o
/// desvio entra direto no CMV com um número plausível.
/// </para>
/// </summary>
public class QuantidadeTests
{
    [Theory(DisplayName = "RN-GER-004 — quantidade de insumo preserva quatro casas decimais")]
    [InlineData(0.15625, 0.1563)]   // líquida ÷ (1 − perda), o caso do CA-4 da spec
    [InlineData(0.0001, 0.0001)]    // um décimo de grama ainda é representável
    [InlineData(2.5, 2.5)]
    [InlineData(1234.56789, 1234.5679)]
    public void Preserva_QuatroCasas(decimal entrada, decimal esperado)
    {
        Quantidade.De(entrada).Valor.Should().Be(esperado);
    }

    [Fact(DisplayName = "RN-GER-004 — a quarta casa NÃO é perdida como aconteceria com a precisão de moeda")]
    public void NaoSofre_ArredondamentoDeMoeda()
    {
        var consumo = Quantidade.De(0.15625m);

        consumo.Valor.Should().Be(0.1563m);
        consumo.Valor.Should().NotBe(0.16m, "0,16 é o valor que a precisão de dinheiro produziria");
    }

    [Fact(DisplayName = "RN-EST-001 — quantidade admite valor negativo: o saldo é soma algébrica dos movimentos")]
    public void Admite_Negativo()
    {
        // Saída de estoque é movimento negativo (ADR-0008, livro-razão). Que o SALDO nunca
        // fique negativo é RN-EST-009 — regra do agregado e trigger no banco, não do tipo.
        var entrada = Quantidade.De(10.0000m);
        var saida = Quantidade.De(-2.5000m);

        Quantidade.Somar([entrada, saida]).Should().Be(Quantidade.De(7.5m));
    }

    [Fact(DisplayName = "RN-EST-012 — fator de conversão de unidade de medida preserva a precisão")]
    public void Conversao_DeUnidade()
    {
        // 1,5 kg comprado, consumido em gramas: 1,5 × 1000.
        (Quantidade.De(1.5m) * 1000m).Should().Be(Quantidade.De(1500m));
    }
}
