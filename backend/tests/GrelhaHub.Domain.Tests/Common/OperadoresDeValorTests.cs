using FluentAssertions;
using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Tests.Common;

/// <summary>
/// Os operadores de <see cref="Dinheiro"/> e <see cref="Quantidade"/> — <c>RN-GER-003</c>,
/// <c>RN-GER-004</c>, <c>SPEC-GER-001</c> §4.
/// <para>
/// ⚠⚠ <b>Este arquivo existe porque a medição mostrou os dois em 52% e 43% de ramo</b> — os value
/// objects que o projeto trata como críticos, e cujos operadores quase não eram exercitados. O que
/// tinha teste era a CONSTRUÇÃO (<c>De</c>, arredondamento); o que faltava era a aritmética, que é
/// onde o dinheiro erra.
/// </para>
/// <para>
/// ⚠ <b>Por que operador merece teste próprio, se "só soma dois campos":</b> cada um deles
/// re-arredonda o resultado. Somar três parcelas de R$ 0,105 dá R$ 0,32 ou R$ 0,30 conforme o
/// arredondamento aconteça a cada passo ou no fim — e as duas respostas parecem certas isoladas.
/// </para>
/// </summary>
public sealed class OperadoresDeValorTests
{
    // ── Dinheiro ────────────────────────────────────────────────────────────────────

    [Theory(DisplayName = "RN-GER-003 — dinheiro arredonda AwayFromZero, o comercial, não o bancário")]
    [InlineData(0.125, 0.13)]
    [InlineData(0.135, 0.14)]
    [InlineData(-0.125, -0.13)]
    [InlineData(2.005, 2.01)]
    public void Dinheiro_ArredondaComercial(decimal entrada, decimal esperado)
    {
        // ⚠ O padrão do .NET é ToEven (bancário): 0,125 viraria 0,12 e 0,135 viraria 0,14 — o
        // mesmo terceiro decimal com respostas diferentes, dependendo da paridade do anterior.
        // Nota fiscal não arredonda assim, e a diferença aparece no fechamento do dia.
        Dinheiro.De(entrada).Valor.Should().Be(esperado);
    }

    [Fact(DisplayName = "RN-GER-003 — soma, subtração e negação preservam as duas casas")]
    public void Dinheiro_Aritmetica()
    {
        var dez = Dinheiro.De(10.00m);
        var tres = Dinheiro.De(3.33m);

        (dez + tres).Valor.Should().Be(13.33m);
        (dez - tres).Valor.Should().Be(6.67m);
        (-tres).Valor.Should().Be(-3.33m);
    }

    [Fact(DisplayName = "RN-VEN-011 — preço × quantidade INTEIRA: dois lanches, não 2,0000 lanches")]
    public void Dinheiro_VezesInteiro()
    {
        (Dinheiro.De(28.00m) * 2).Valor.Should().Be(56.00m);

        // Zero itens custa zero, e não é caso especial nenhum — mas é o limite que quebra
        // implementação que assume "pelo menos um".
        (Dinheiro.De(28.00m) * 0).Valor.Should().Be(0m);
    }

    [Fact(DisplayName = "RN-GER-003 — multiplicação por fator arredonda o RESULTADO")]
    public void Dinheiro_VezesFator()
    {
        // 10% sobre R$ 33,33 dá 3,333 — que precisa virar 3,33, e não ficar com três casas
        // esperando o próximo cálculo herdar o erro.
        (Dinheiro.De(33.33m) * 0.1m).Valor.Should().Be(3.33m);

        // E o arredondamento é o comercial também aqui: 0,125 sobe.
        (Dinheiro.De(2.50m) * 0.05m).Valor.Should().Be(0.13m);
    }

    [Fact(DisplayName = "RN-GER-003 — Somar de sequência vazia é zero, não erro")]
    public void Dinheiro_SomarVazio()
    {
        // Pedido sem item é estado transitório real (o cliente ainda está montando). Estourar
        // aqui transformaria uma tela em branco num 500.
        Dinheiro.Somar([]).Valor.Should().Be(0m);

        Dinheiro.Somar([Dinheiro.De(1.11m), Dinheiro.De(2.22m), Dinheiro.De(3.33m)])
            .Valor.Should().Be(6.66m);
    }

    [Fact(DisplayName = "RN-GER-003 — as quatro comparações e o EhZero")]
    public void Dinheiro_Comparacoes()
    {
        var menor = Dinheiro.De(10.00m);
        var maior = Dinheiro.De(20.00m);
        var igual = Dinheiro.De(10.00m);

        (menor < maior).Should().BeTrue();
        (maior > menor).Should().BeTrue();
        (menor <= igual).Should().BeTrue();
        (menor >= igual).Should().BeTrue();
        (maior <= menor).Should().BeFalse();
        (menor >= maior).Should().BeFalse();

        Dinheiro.Zero.EhZero.Should().BeTrue();
        menor.EhZero.Should().BeFalse();
    }

    // ── Quantidade ──────────────────────────────────────────────────────────────────

    [Theory(DisplayName = "RN-GER-004 — quantidade guarda QUATRO casas, e é por isso que não é dinheiro")]
    [InlineData(0.1563, 0.1563)]
    [InlineData(0.15635, 0.1564)]
    [InlineData(1.00005, 1.0001)]
    public void Quantidade_QuatroCasas(decimal entrada, decimal esperado)
    {
        // ⚠⚠ O CASO QUE JUSTIFICA O TIPO: com a precisão do dinheiro, 0,1563 kg viraria 0,16 —
        // sem erro nenhum, sem aviso, e o desvio entra direto no CMV com um número plausível.
        Quantidade.De(entrada).Valor.Should().Be(esperado);
    }

    [Fact(DisplayName = "RN-GER-004 — aritmética de quantidade preserva as quatro casas")]
    public void Quantidade_Aritmetica()
    {
        var meio = Quantidade.De(0.5000m);
        var quarto = Quantidade.De(0.2500m);

        (meio + quarto).Valor.Should().Be(0.7500m);
        (meio - quarto).Valor.Should().Be(0.2500m);
        (-quarto).Valor.Should().Be(-0.2500m);
        (meio * 3).Valor.Should().Be(1.5000m);
        (meio * 0.5m).Valor.Should().Be(0.2500m);
    }

    [Fact(DisplayName = "RN-GER-004 — Somar de sequência vazia é zero")]
    public void Quantidade_SomarVazio()
    {
        Quantidade.Somar([]).Valor.Should().Be(0m);

        Quantidade.Somar([Quantidade.De(0.3333m), Quantidade.De(0.3333m), Quantidade.De(0.3334m)])
            .Valor.Should().Be(1.0000m);
    }

    [Fact(DisplayName = "RN-GER-004 — as comparações de quantidade")]
    public void Quantidade_Comparacoes()
    {
        var pouco = Quantidade.De(0.5000m);
        var muito = Quantidade.De(2.0000m);

        (pouco < muito).Should().BeTrue();
        (muito > pouco).Should().BeTrue();
        (pouco <= Quantidade.De(0.5000m)).Should().BeTrue();
        (pouco >= Quantidade.De(0.5000m)).Should().BeTrue();
        (muito <= pouco).Should().BeFalse();
        (pouco >= muito).Should().BeFalse();
    }

    // ── CPF ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RN-GER-023 — ValorOuNulo devolve o CPF, ou nulo quando não há")]
    public void Cpf_ValorOuNulo()
    {
        // A extensão existe para o mapeamento: `Cpf?` é struct anulável, e `cpf?.Valor` dentro de
        // árvore de expressão do EF Core não é o mesmo que num `if`. Estava em ZERO por cento.
        Cpf? ausente = null;
        ausente.ValorOuNulo().Should().BeNull();

        Cpf.TentarDe("52998224725", out var valido).Should().BeTrue();

        Cpf? presente = valido;
        presente.ValorOuNulo().Should().Be("52998224725");
    }
}
