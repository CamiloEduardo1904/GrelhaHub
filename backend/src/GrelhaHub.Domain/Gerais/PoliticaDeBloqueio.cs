namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// <c>RN-GER-040</c> ⚠⚠ — o bloqueio por tentativa de login errada, aplicado pelo
/// <c>LockoutOptions</c> do Identity na partida da Api.
/// <para>
/// ⚠ Cópia dos parâmetros no código, nomeando o que copia — o mesmo desenho de
/// <c>PAR-GER-002</c>/<c>PAR-GER-003</c> em <c>EmissorDeLinkDeAcesso</c>: <c>parametro_valor</c>
/// é por unidade (<c>SPEC-GER-003</c>) e o login é <b>anônimo</b> — no instante da tentativa
/// ainda não se sabe de que unidade é a pessoa, e para o usuário de plataforma não há unidade
/// nenhuma. Escopo global, como <c>docs/04-parametros-do-sistema.md</c> §1.1 declara.
/// </para>
/// <para>
/// ⚠ <b>Bloqueio é temporário e nunca administrativo:</b> expira sozinho, e não vira fila de
/// desbloqueio para alguém atender. O contador zera no primeiro acerto — quem zera é o
/// <c>SignInManager</c>.
/// </para>
/// </summary>
public static class PoliticaDeBloqueio
{
    /// <summary><c>PAR-GER-004</c> — falhas seguidas antes de bloquear.</summary>
    public const int TentativasAntesDoBloqueio = 5;

    /// <summary><c>PAR-GER-005</c> — por quanto tempo a conta fica bloqueada.</summary>
    public static readonly TimeSpan DuracaoDoBloqueio = TimeSpan.FromMinutes(15);
}
