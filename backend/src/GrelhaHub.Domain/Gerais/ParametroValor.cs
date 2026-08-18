using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Valor de um parâmetro em um período — <c>SPEC-GER-003</c>, <c>RN-GER-009</c>.
/// <para>
/// <b>Este é o padrão de vigência do projeto.</b> `preco_produto` (<c>RN-VEN-010</c>) e
/// `ficha_tecnica` (<c>RN-PRO-002</c>) usam exatamente o mesmo desenho: intervalo
/// <c>[inicio, fim)</c>, `fim` nulo para a vigência aberta, e uma `EXCLUDE` no banco impedindo
/// sobreposição.
/// </para>
/// <para>
/// <b>Alterar um valor nunca é <c>UPDATE</c>.</b> Fecha-se a vigência corrente e insere-se uma
/// nova linha, na mesma transação. Sobrescrever o valor apagaria a explicação de qualquer
/// cálculo passado — o mesmo erro que <c>RN-VEN-008</c> evita para preço praticado.
/// </para>
/// </summary>
public sealed class ParametroValor : Entidade, IPertenceAUnidade, IAuditavel, IAuditavelDetalhado
{
    private ParametroValor() { }

    public ParametroValor(
        Guid parametroId,
        Guid unidadeId,
        string valor,
        DateTimeOffset vigenciaInicio,
        string? justificativa)
    {
        ParametroId = parametroId;
        UnidadeId = unidadeId;
        Valor = string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor do parâmetro é obrigatório.", nameof(valor))
            : valor.Trim();
        VigenciaInicio = vigenciaInicio;
        Justificativa = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa.Trim();
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public Guid ParametroId { get; private set; }

    public Guid UnidadeId { get; private set; }

    /// <summary>
    /// Texto canônico, sempre em cultura invariante.
    /// <para>
    /// Guardar `10.00` e não `10,00` não é detalhe: um valor gravado com vírgula em um
    /// servidor e lido com ponto em outro devolve mil vezes o número certo, sem erro.
    /// </para>
    /// </summary>
    public string Valor { get; private set; } = null!;

    public DateTimeOffset VigenciaInicio { get; private set; }

    /// <summary>Nulo enquanto a vigência está aberta. Limite <b>exclusivo</b> quando preenchido.</summary>
    public DateTimeOffset? VigenciaFim { get; private set; }

    /// <summary>
    /// Por que o valor mudou.
    /// <para>
    /// <b>Divergência declarada em relação à §6 original da spec</b>, que a previa apenas no
    /// `log_auditoria`: a trilha completa (<c>RN-GER-011</c>) só nasce em `SPEC-GER-005`, e
    /// validar a justificativa para depois descartá-la seria pedir ao usuário um dado que o
    /// sistema joga fora. Fica aqui, na própria linha que ela explica.
    /// </para>
    /// </summary>
    public string? Justificativa { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary><c>RN-GER-001</c> — preenchido pelo interceptor.</summary>
    public Guid? CriadoPorUsuarioId { get; private set; }

    /// <summary><c>SPEC-GER-003</c> CA-6 — a justificativa da alteração entra na trilha.</summary>
    public string? JustificativaDaAuditoria => Justificativa;

    public Parametro? Parametro { get; private set; }

    public bool EstaAberta => VigenciaFim is null;

    /// <summary>
    /// Fecha a vigência no instante em que a próxima começa.
    /// <para>
    /// O limite é exclusivo: `[inicio, fim)`. Fechar em `fim - 1 tick` deixaria um vão em que
    /// nenhum valor vigora, e a leitura devolveria "parâmetro sem valor" no pior momento.
    /// </para>
    /// </summary>
    public void Fechar(DateTimeOffset fim)
    {
        if (fim <= VigenciaInicio)
        {
            throw new ArgumentException(
                "A vigência não pode terminar antes de começar.", nameof(fim));
        }

        VigenciaFim = fim;
    }

    /// <summary>Vigora no instante informado? `[inicio, fim)`.</summary>
    public bool VigoraEm(DateTimeOffset instante) =>
        instante >= VigenciaInicio && (VigenciaFim is null || instante < VigenciaFim);
}
