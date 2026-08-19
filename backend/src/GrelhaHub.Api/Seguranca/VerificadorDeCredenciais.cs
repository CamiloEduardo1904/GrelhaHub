using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using Microsoft.AspNetCore.Identity;

namespace GrelhaHub.Api.Seguranca;

/// <summary>
/// <c>RN-GER-040</c> ⚠⚠ — a conferência de senha que CONTA as falhas e bloqueia na quinta
/// (<c>PAR-GER-004</c>), por <c>PAR-GER-005</c> minutos. ADR-0022 §4.
/// <para>
/// Vive na Api, e não na Infrastructure, porque o <c>SignInManager</c> mora no runtime do
/// ASP.NET — é o mesmo motivo de o <c>ContextoDeExecucaoHttp</c> viver aqui. O
/// <c>lockoutOnFailure: true</c> é o mecanismo inteiro: falha incrementa
/// <c>falhas_de_acesso</c>, a quinta grava <c>bloqueado_ate</c>, e o acerto zera o contador —
/// tudo do framework, nada nosso.
/// </para>
/// <para>
/// ⚠ O bloqueio expira sozinho (<c>bloqueado_ate</c> no passado volta a aceitar tentativa) —
/// nunca vira fila de desbloqueio para alguém atender.
/// </para>
/// </summary>
public sealed class VerificadorDeCredenciais(
    SignInManager<Usuario> signInManager,
    IRelogio relogio) : IVerificadorDeCredenciais
{
    public async Task<VerificacaoDeCredencial> VerificarAsync(Usuario usuario, string senha)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var resultado = await signInManager.CheckPasswordSignInAsync(
            usuario, senha, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            return VerificacaoDeCredencial.Sucesso;
        }

        if (resultado.IsLockedOut)
        {
            // Quanto FALTA, não a duração cheia: quem tenta de novo aos 10 minutos lê "5
            // minutos", não um novo "15" — a mensagem tem de encolher junto com o bloqueio.
            var restante = usuario.LockoutEnd is { } fim
                ? fim - relogio.Agora
                : PoliticaDeBloqueio.DuracaoDoBloqueio;

            return VerificacaoDeCredencial.Bloqueio(
                restante > TimeSpan.Zero ? restante : PoliticaDeBloqueio.DuracaoDoBloqueio);
        }

        return VerificacaoDeCredencial.SenhaIncorreta;
    }
}
