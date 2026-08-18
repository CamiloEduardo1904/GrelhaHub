using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Habilitação de um módulo em uma unidade, com vigência — <c>RN-GER-017</c>, <c>RN-GER-021</c>.
/// <para>
/// <b>Vigência e não booleano.</b> Um `habilitado bool` responderia "tem VEN hoje?"; a vigência
/// responde "o que esta unidade tinha contratado em março", que é a pergunta da cobrança. É a
/// terceira aplicação do padrão de <see cref="ParametroValor"/>: intervalo `[inicio, fim)` e
/// `EXCLUDE` no banco contra sobreposição.
/// </para>
/// <para>
/// <b>Desabilitar fecha a vigência; nunca apaga linha</b> (<c>RN-GER-018</c>). O dado
/// transacional do módulo permanece e volta a ser acessível se o módulo for reabilitado —
/// desabilitar interrompe o acesso, não o histórico.
/// </para>
/// </summary>
public sealed class UnidadeModulo : Entidade, IPertenceAUnidade, IAuditavel, IAuditavelDetalhado
{
    private UnidadeModulo() { }

    public UnidadeModulo(
        Guid unidadeId, Guid moduloId, DateTimeOffset vigenciaInicio, string? justificativa)
    {
        UnidadeId = unidadeId;
        ModuloId = moduloId;
        VigenciaInicio = vigenciaInicio;
        Justificativa = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa.Trim();
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public Guid UnidadeId { get; private set; }

    public Guid ModuloId { get; private set; }

    public DateTimeOffset VigenciaInicio { get; private set; }

    /// <summary>Nulo enquanto habilitado. Limite <b>exclusivo</b> quando preenchido.</summary>
    public DateTimeOffset? VigenciaFim { get; private set; }

    /// <summary>Por que o módulo foi habilitado ou desabilitado — decisão comercial rastreável.</summary>
    public string? Justificativa { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary><c>RN-GER-001</c> — preenchido pelo interceptor.</summary>
    public Guid? CriadoPorUsuarioId { get; private set; }

    /// <summary><c>RN-GER-011</c> — a decisão comercial fica explicada na trilha.</summary>
    public string? JustificativaDaAuditoria => Justificativa;

    /// <summary>
    /// Desabilitar é <b>fechar a vigência</b> (<c>RN-GER-018</c>) — na trilha aparece como
    /// exclusão lógica, que é o que o cliente percebe: o módulo saiu.
    /// </summary>
    public OperacaoDeAuditoria ClassificarAuditoria(
        TipoDeMudanca mudanca, IReadOnlySet<string> propriedadesAlteradas)
    {
        ArgumentNullException.ThrowIfNull(propriedadesAlteradas);

        if (mudanca == TipoDeMudanca.Insercao)
        {
            return OperacaoDeAuditoria.Criacao;
        }

        return propriedadesAlteradas.Contains(nameof(VigenciaFim)) && VigenciaFim is not null
            ? OperacaoDeAuditoria.Exclusao
            : OperacaoDeAuditoria.Alteracao;
    }

    public Modulo? Modulo { get; private set; }

    public bool EstaHabilitado => VigenciaFim is null;

    /// <summary>Estava habilitado no instante informado? `[inicio, fim)`.</summary>
    public bool VigoraEm(DateTimeOffset instante) =>
        instante >= VigenciaInicio && (VigenciaFim is null || instante < VigenciaFim);

    public void Desabilitar(DateTimeOffset em, string? justificativa)
    {
        if (em <= VigenciaInicio)
        {
            throw new ArgumentException(
                "A desabilitação não pode ser anterior à habilitação.", nameof(em));
        }

        VigenciaFim = em;

        if (!string.IsNullOrWhiteSpace(justificativa))
        {
            Justificativa = justificativa.Trim();
        }
    }
}
