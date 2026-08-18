using System.Globalization;

namespace GrelhaHub.Domain.Common;

/// <summary>
/// Valor monetário em reais — <c>RN-GER-003</c>: duas casas decimais.
/// <para>
/// <b>Por que um tipo e não <c>decimal</c> cru</b> (SPEC-GER-001 §4): um <c>decimal</c> solto
/// não distingue R$ 12,00 de 12 gramas, e o compilador não impede somar os dois. Com tipos
/// distintos, a precisão certa é aplicada por convenção do <c>DbContext</c> e a confusão vira
/// erro de compilação em vez de dado errado.
/// </para>
/// <para>
/// <b>Não há conversão implícita a partir de <c>decimal</c>.</b> Toda entrada passa por
/// <see cref="De"/>, para que o ponto em que um número virou dinheiro seja visível na leitura
/// do código — que é justamente o que o <c>decimal</c> solto esconde.
/// </para>
/// <para>
/// <b>Valor negativo é permitido.</b> Estorno, troco e ajuste são dinheiro negativo. Onde a
/// política proíbe negativo — o total de um pedido, por <c>RN-VEN-029</c> — a proibição é da
/// regra, não do tipo: um `CHECK` no modelo e a validação do agregado, não uma exceção aqui.
/// </para>
/// </summary>
public readonly record struct Dinheiro : IComparable<Dinheiro>
{
    /// <summary><c>RN-GER-003</c> — reais, duas casas.</summary>
    public const int CasasDecimais = 2;

    public static Dinheiro Zero => default;

    private Dinheiro(decimal valor) => Valor = valor;

    public decimal Valor { get; }

    /// <summary>
    /// Constrói arredondando para duas casas.
    /// <para>
    /// <b>Arredonda, não trunca:</b> truncar sistematicamente para baixo desvia o total de
    /// uma venda longa em favor de um dos lados. <c>AwayFromZero</c> é o arredondamento
    /// comercial esperado — 0,125 vira 0,13, não 0,12 como faria o padrão bancário do .NET.
    /// </para>
    /// </summary>
    public static Dinheiro De(decimal valor) => new(Arredondar(valor));

    public static decimal Arredondar(decimal valor) =>
        Math.Round(valor, CasasDecimais, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Soma uma sequência. Existe porque <c>Enumerable.Sum</c> não tem sobrecarga para tipo
    /// próprio, e escrever o <c>Aggregate</c> à mão em cada agregado convidaria a divergência.
    /// </summary>
    public static Dinheiro Somar(IEnumerable<Dinheiro> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        var total = 0m;

        foreach (var valor in valores)
        {
            total += valor.Valor;
        }

        return De(total);
    }

    public static Dinheiro operator +(Dinheiro esquerda, Dinheiro direita) =>
        De(esquerda.Valor + direita.Valor);

    public static Dinheiro operator -(Dinheiro esquerda, Dinheiro direita) =>
        De(esquerda.Valor - direita.Valor);

    public static Dinheiro operator -(Dinheiro valor) => De(-valor.Valor);

    /// <summary>Preço × quantidade vendida — <c>RN-VEN-009</c>.</summary>
    public static Dinheiro operator *(Dinheiro preco, int quantidade) =>
        De(preco.Valor * quantidade);

    /// <summary>Aplicação de percentual — desconto, taxa de serviço, comissão.</summary>
    public static Dinheiro operator *(Dinheiro valor, decimal fator) =>
        De(valor.Valor * fator);

    public static bool operator <(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor < direita.Valor;

    public static bool operator >(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor > direita.Valor;

    public static bool operator <=(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor <= direita.Valor;

    public static bool operator >=(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor >= direita.Valor;

    public bool EhZero => Valor == 0m;

    public bool EhPositivo => Valor > 0m;

    public int CompareTo(Dinheiro outro) => Valor.CompareTo(outro.Valor);

    /// <summary>
    /// Invariante e sem símbolo. <b>Formatação de moeda é da interface</b>, não do domínio: o
    /// front usa <c>Intl.NumberFormat('pt-BR')</c>, e um `R$` embutido aqui apareceria
    /// duplicado em log, em JSON e em mensagem de erro.
    /// </summary>
    public override string ToString() => Valor.ToString("0.00", CultureInfo.InvariantCulture);
}
