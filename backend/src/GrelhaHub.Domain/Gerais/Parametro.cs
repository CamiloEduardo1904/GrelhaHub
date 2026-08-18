using System.Globalization;
using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Tipo do valor de um parâmetro — define como o texto guardado é interpretado e validado.
/// <para>
/// O nome no banco é minúsculo e estável, porque entra no <c>CHECK</c> de <c>tipo_valor</c>:
/// renomear o membro do C# não pode invalidar dado já gravado.
/// </para>
/// </summary>
public enum TipoDeValor
{
    Percentual = 1,
    Dinheiro = 2,
    Dias = 3,
    Tempo = 4,
    Intervalo = 5,
    Enumerado = 6,
    Inteiro = 7,

    /// <summary>
    /// Multiplicador adimensional — `PAR-CRM-001` é 1,5 vezes o intervalo médio.
    /// <para>
    /// <b>Tipo descoberto na implementação.</b> A §4 de `SPEC-GER-003` não o previa, e nenhum
    /// dos outros serve: não é percentual, não é dinheiro e não é contagem. Sem ele, o
    /// parâmetro seria gravado como `percentual` com valor 1,5 — e alguém, mais tarde,
    /// leria 1,5% onde a regra quer 150%.
    /// </para>
    /// </summary>
    Fator = 8,
}

/// <summary>
/// Definição de um parâmetro configurável — <c>SPEC-GER-003</c>, `docs/04-parametros-do-sistema.md`.
/// <para>
/// <b>É catálogo, não valor.</b> A definição é global e o valor vive em
/// <see cref="ParametroValor"/>, por unidade e com vigência — porque trocar a taxa de serviço
/// não pode reescrever o passado, pelo mesmo princípio de <c>RN-VEN-010</c>.
/// </para>
/// </summary>
public sealed class Parametro : Entidade
{
    private Parametro() { }

    public Parametro(string codigo, string descricao, TipoDeValor tipoValor, string? dominio)
    {
        Codigo = ValidarCodigo(codigo);
        Descricao = string.IsNullOrWhiteSpace(descricao)
            ? throw new ArgumentException("A descrição do parâmetro é obrigatória.", nameof(descricao))
            : descricao.Trim();
        TipoValor = tipoValor;
        Dominio = string.IsNullOrWhiteSpace(dominio) ? null : dominio.Trim();
    }

    /// <summary>Formato `PAR-MOD-nnn`, o mesmo do documento de parâmetros.</summary>
    public string Codigo { get; private set; } = null!;

    public string Descricao { get; private set; } = null!;

    public TipoDeValor TipoValor { get; private set; }

    /// <summary>
    /// Descrição textual da restrição — `0..100`, `> 0`, `bloquear|avisar|permitir`.
    /// <para>
    /// Serve para validar <b>e</b> para compor a mensagem de erro: dizer ao usuário apenas
    /// "valor inválido" o obriga a adivinhar qual é o válido.
    /// </para>
    /// </summary>
    public string? Dominio { get; private set; }

    /// <summary>
    /// O valor cabe no tipo e no domínio declarados?
    /// <para>
    /// Mora aqui porque é o parâmetro que conhece o próprio domínio — espalhar essa checagem
    /// pelos validators faria cada novo tipo precisar ser lembrado em vários lugares.
    /// </para>
    /// <para>
    /// <b>Valida domínio, nunca política.</b> `percentual entre 0 e 100` é domínio;
    /// `percentual <= 10` seria política — e é justamente o valor que o parâmetro existe para
    /// deixar configurável.
    /// </para>
    /// </summary>
    public bool ValorEhValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var texto = valor.Trim();
        var invariante = CultureInfo.InvariantCulture;

        return TipoValor switch
        {
            TipoDeValor.Percentual =>
                decimal.TryParse(texto, NumberStyles.Number, invariante, out var percentual)
                && percentual is >= 0 and <= 100,

            TipoDeValor.Dinheiro =>
                decimal.TryParse(texto, NumberStyles.Number, invariante, out var dinheiro)
                && dinheiro > 0,

            TipoDeValor.Fator =>
                decimal.TryParse(texto, NumberStyles.Number, invariante, out var fator)
                && fator > 0,

            TipoDeValor.Dias or TipoDeValor.Inteiro =>
                int.TryParse(texto, NumberStyles.Integer, invariante, out var inteiro)
                && inteiro >= 0,

            TipoDeValor.Tempo => TimeOnly.TryParse(texto, invariante, out _),

            TipoDeValor.Intervalo =>
                TimeSpan.TryParse(texto, invariante, out var intervalo) && intervalo > TimeSpan.Zero,

            // O domínio de um enum É a lista de valores aceitos, separada por barra vertical.
            TipoDeValor.Enumerado =>
                Dominio is not null
                && Dominio.Split('|', StringSplitOptions.TrimEntries)
                    .Contains(texto, StringComparer.Ordinal),

            _ => false,
        };
    }

    private static string ValidarCodigo(string codigo)
    {
        var limpo = (codigo ?? string.Empty).Trim().ToUpperInvariant();

        return System.Text.RegularExpressions.Regex.IsMatch(limpo, "^PAR-[A-Z]{3}-[0-9]{3}$")
            ? limpo
            : throw new ArgumentException(
                "O código do parâmetro segue o formato PAR-MOD-nnn.", nameof(codigo));
    }
}
