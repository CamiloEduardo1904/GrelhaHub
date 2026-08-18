using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Sessão de suporte da plataforma sobre uma unidade — <c>RN-GER-015</c> ⚠, ADR-0013.
/// <para>
/// <b>Ler para diagnosticar é rotina de suporte; alterar o dado de um cliente sem dizer por quê,
/// não.</b> Por isso a leitura de plataforma é livre (e registrada quando a trilha existir), e
/// toda <b>escrita</b> exige uma sessão aberta com motivo.
/// </para>
/// <para>
/// É a contrapartida do ADR-0013: o suporte ganhou o poder de enxergar todas as unidades, e este
/// registro é o que torna o uso desse poder explicável depois. Sem ele, a decisão de desligar o
/// filtro global não teria contrapeso nenhum.
/// </para>
/// </summary>
public sealed class SessaoDeSuporte : Entidade
{
    private SessaoDeSuporte() { }

    public SessaoDeSuporte(Guid usuarioId, Guid unidadeId, string motivo)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "RN-GER-015 — a sessão de suporte registra quem a abriu.", nameof(usuarioId));
        }

        if (unidadeId == Guid.Empty)
        {
            throw new ArgumentException(
                "RN-GER-015 — a sessão de suporte é sobre uma unidade específica.", nameof(unidadeId));
        }

        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 10)
        {
            // Dez caracteres não garantem um motivo bom, mas impedem o "." que alguém digita para
            // passar do campo — e o motivo aqui é o que justifica ter mexido no dado do cliente.
            throw new ArgumentException(
                "RN-GER-015 — a sessão de suporte exige motivo de ao menos 10 caracteres.",
                nameof(motivo));
        }

        UsuarioId = usuarioId;
        UnidadeId = unidadeId;
        Motivo = motivo.Trim();
        AbertaEm = DateTimeOffset.UtcNow;
    }

    /// <summary>O usuário de plataforma que abriu a sessão.</summary>
    public Guid UsuarioId { get; private set; }

    /// <summary>
    /// A unidade sobre a qual a sessão vale.
    /// <para>
    /// <b>Não implementa <c>IPertenceAUnidade</c>:</b> a sessão é registro <b>da plataforma</b>
    /// sobre um cliente, não registro do cliente. Sujeitá-la ao filtro global a esconderia de
    /// quem precisa auditá-la — quem opera o produto.
    /// </para>
    /// </summary>
    public Guid UnidadeId { get; private set; }

    public string Motivo { get; private set; } = null!;

    public DateTimeOffset AbertaEm { get; private set; }

    public DateTimeOffset? EncerradaEm { get; private set; }

    public bool EstaAberta => EncerradaEm is null;

    /// <summary>
    /// Aberta e ainda válida no instante informado?
    /// <para>
    /// A sessão <b>expira</b> por tempo, e não só por encerramento explícito: ninguém encerra
    /// sessão de suporte ao terminar o dia, e uma sessão aberta para sempre equivale a não ter
    /// sessão.
    /// </para>
    /// </summary>
    public bool ValidaEm(DateTimeOffset instante) =>
        EstaAberta && instante - AbertaEm < Duracao;

    /// <summary>
    /// Duas horas. Longo o suficiente para um atendimento inteiro, curto o suficiente para que
    /// uma sessão esquecida não autorize escrita amanhã.
    /// </summary>
    public static readonly TimeSpan Duracao = TimeSpan.FromHours(2);

    public void Encerrar() => EncerradaEm ??= DateTimeOffset.UtcNow;
}
