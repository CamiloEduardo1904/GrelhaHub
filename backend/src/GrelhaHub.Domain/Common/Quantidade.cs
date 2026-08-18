using System.Globalization;

namespace GrelhaHub.Domain.Common;

/// <summary>
/// Quantidade de insumo — <c>RN-GER-004</c>: até <b>quatro</b> casas decimais, para permitir
/// gramas e mililitros.
/// <para>
/// <b>É o tipo que existe para impedir uma falha silenciosa.</b> A convenção do
/// <c>DbContext</c> mapeia dinheiro em <c>numeric(12,2)</c>. Uma quantidade declarada como
/// <c>decimal</c> cru herdaria essa precisão e <c>0,1563</c> — resultado típico de
/// <c>líquida ÷ (1 − perda)</c> — seria gravado como <c>0,16</c> <b>sem erro nenhum</b>. Em
/// uma ficha técnica de 20 insumos o desvio entra direto no CMV, e o número continua
/// plausível.
/// </para>
/// <para>
/// <b>Não confundir com a quantidade vendida de um produto</b>, que é <c>int</c>: dois
/// lanches, não 2,0000 lanches (<c>RN-VEN-011</c>). Insumo se mede; produto se conta.
/// </para>
/// <para>
/// <b>Valor negativo é permitido.</b> O estoque é livro-razão e o saldo é soma algébrica dos
/// movimentos (<c>RN-EST-001</c>, ADR-0008): saída é quantidade negativa. Que o <i>saldo</i>
/// nunca fique negativo é <c>RN-EST-009</c> — regra do agregado e trigger no banco, não deste
/// tipo.
/// </para>
/// </summary>
public readonly record struct Quantidade : IComparable<Quantidade>
{
    /// <summary><c>RN-GER-004</c> — gramas e mililitros exigem quatro casas.</summary>
    public const int CasasDecimais = 4;

    public static Quantidade Zero => default;

    private Quantidade(decimal valor) => Valor = valor;

    public decimal Valor { get; }

    public static Quantidade De(decimal valor) => new(Arredondar(valor));

    public static decimal Arredondar(decimal valor) =>
        Math.Round(valor, CasasDecimais, MidpointRounding.AwayFromZero);

    public static Quantidade Somar(IEnumerable<Quantidade> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        var total = 0m;

        foreach (var valor in valores)
        {
            total += valor.Valor;
        }

        return De(total);
    }

    public static Quantidade operator +(Quantidade esquerda, Quantidade direita) =>
        De(esquerda.Valor + direita.Valor);

    public static Quantidade operator -(Quantidade esquerda, Quantidade direita) =>
        De(esquerda.Valor - direita.Valor);

    public static Quantidade operator -(Quantidade valor) => De(-valor.Valor);

    /// <summary>Consumo de insumo por porção × porções produzidas.</summary>
    public static Quantidade operator *(Quantidade quantidade, int fator) =>
        De(quantidade.Valor * fator);

    /// <summary>
    /// Fator de conversão de unidade de medida (<c>RN-EST-012</c>) e correção de perda
    /// (<c>RN-PRO-006</c>).
    /// </summary>
    public static Quantidade operator *(Quantidade quantidade, decimal fator) =>
        De(quantidade.Valor * fator);

    public static bool operator <(Quantidade esquerda, Quantidade direita) => esquerda.Valor < direita.Valor;

    public static bool operator >(Quantidade esquerda, Quantidade direita) => esquerda.Valor > direita.Valor;

    public static bool operator <=(Quantidade esquerda, Quantidade direita) => esquerda.Valor <= direita.Valor;

    public static bool operator >=(Quantidade esquerda, Quantidade direita) => esquerda.Valor >= direita.Valor;

    public bool EhZero => Valor == 0m;

    public bool EhPositiva => Valor > 0m;

    public int CompareTo(Quantidade outra) => Valor.CompareTo(outra.Valor);

    public override string ToString() => Valor.ToString("0.####", CultureInfo.InvariantCulture);
}
