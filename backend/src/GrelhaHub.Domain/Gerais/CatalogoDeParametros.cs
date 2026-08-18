namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Os parâmetros que o sistema conhece, com o valor inicial documentado —
/// transcrição de <c>docs/04-parametros-do-sistema.md</c>.
/// <para>
/// <b>Existe para que a carga inicial e o documento não divirjam.</b> O modo de falha mais
/// provável desta spec é um parâmetro documentado que ninguém semeou — e que só aparece como
/// exceção no meio do serviço. Há teste comparando <b>três</b> fontes: este catálogo, o
/// documento e a tabela <c>parametro</c> do banco. Se qualquer uma sair de linha, fica
/// vermelho (CA-7 de <c>SPEC-GER-003</c>).
/// </para>
/// <para>
/// Acrescentar parâmetro aqui <b>não</b> basta: a definição entra em <c>parametro</c> por
/// migration, porque migration é história e não pode mudar de conteúdo quando esta lista muda.
/// </para>
/// </summary>
public static class CatalogoDeParametros
{
    /// <param name="Codigo">`PAR-MOD-nnn`, igual ao documento.</param>
    /// <param name="ValorInicial">
    /// Texto canônico em cultura invariante — `10.00`, nunca `10,00`.
    /// </param>
    public sealed record Definicao(
        string Codigo,
        string Descricao,
        TipoDeValor TipoValor,
        string? Dominio,
        string ValorInicial);

    /// <summary>Os dez parâmetros de `docs/04-parametros-do-sistema.md` §1.</summary>
    public static IReadOnlyList<Definicao> Todos { get; } =
    [
        new("PAR-GER-001", "Hora de virada do dia operacional",
            TipoDeValor.Tempo, "00:00..23:59", "04:00"),

        new("PAR-VEN-001", "Desconto máximo sem autorização",
            TipoDeValor.Percentual, "0..100", "10.00"),

        new("PAR-VEN-002", "Percentual da taxa de serviço",
            TipoDeValor.Percentual, "0..100", "10.00"),

        // RN-VEN-035: o alerta é obrigatório nos três modos. O parâmetro decide a FRICÇÃO,
        // nunca se o operador é avisado.
        new("PAR-VEN-003", "Fricção na falta de saldo de insumo",
            TipoDeValor.Enumerado, "bloquear|avisar|permitir", "avisar"),

        // RN-VEN-046 — conta apenas produtos ATIVOS. RN-VEN-045 proíbe exclusão, então contar o
        // histórico entupiria a categoria para sempre.
        new("PAR-VEN-004", "Máximo de produtos ativos por categoria",
            TipoDeValor.Inteiro, "> 0", "50"),

        new("PAR-EST-001", "Validade mínima aceita no recebimento",
            TipoDeValor.Dias, ">= 0", "7"),

        new("PAR-EST-002", "Dias de estoque de segurança",
            TipoDeValor.Dias, ">= 0", "3"),

        new("PAR-COM-001", "Valor mínimo que exige cotação",
            TipoDeValor.Dinheiro, "> 0", "500.00"),

        new("PAR-CRM-001", "Multiplicador do intervalo médio para em risco",
            TipoDeValor.Fator, "> 0", "1.50"),

        new("PAR-FIS-001", "Prazo legal para cancelamento fiscal",
            TipoDeValor.Intervalo, "duração positiva", "00:30:00"),
    ];

    public static Definicao Obter(string codigo) =>
        Todos.FirstOrDefault(definicao => definicao.Codigo == codigo)
        ?? throw new ArgumentException(
            $"Parâmetro {codigo} não está no catálogo. Acrescente-o em " +
            "docs/04-parametros-do-sistema.md antes de usá-lo no código.", nameof(codigo));
}
