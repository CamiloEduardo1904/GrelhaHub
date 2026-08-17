using System.Security.Claims;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Api.Seguranca;

/// <summary>
/// Contexto de execução a partir das claims do JWT — ADR-0003.
/// <para>
/// Registrado como <b>scoped</b>: o escopo é a requisição, e a unidade definida por
/// <see cref="DefinirUnidadePublica"/> numa rota pública não pode vazar para outra.
/// </para>
/// </summary>
public sealed class ContextoDeExecucaoHttp(IHttpContextAccessor acessor) : IContextoDeExecucao
{
    private Guid? _unidadePublica;

    private ClaimsPrincipal? Usuario => acessor.HttpContext?.User;

    public bool Autenticado => Usuario?.Identity?.IsAuthenticated == true;

    public Guid? UsuarioId =>
        Guid.TryParse(Usuario?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Usuario?.FindFirstValue("sub"), out var id)
            ? id
            : null;

    /// <summary>
    /// <c>RN-GER-009</c> — unidade do escopo. Vem do token quando autenticado; da resolução
    /// do <c>slug</c> quando a rota é pública.
    /// <para>
    /// Devolve <see cref="Guid.Empty"/> quando não há nem uma coisa nem outra — e isso é
    /// deliberado: o filtro global compara <c>unidade_id = Guid.Empty</c> e não retorna nada.
    /// <b>Falhar fechado</b> é o comportamento certo aqui; falhar aberto vazaria todas as
    /// unidades.
    /// </para>
    /// </summary>
    public Guid UnidadeId
    {
        get
        {
            if (_unidadePublica is { } publica)
            {
                return publica;
            }

            var claim = Usuario?.FindFirstValue("unidade");
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    /// <summary><c>RN-GER-012</c> — custo, CMV e margem só para perfis de gerência.</summary>
    public bool PodeVerCusto => EhGerencia;

    public bool EhGerencia =>
        Usuario?.IsInRole(nameof(PerfilUsuario.Administrador)) == true ||
        Usuario?.IsInRole(nameof(PerfilUsuario.Gerente)) == true;

    public void DefinirUnidadePublica(Guid unidadeId)
    {
        // Um usuário autenticado não pode ser reescopado por um slug de URL: seria um
        // caminho para ler dados de outra unidade com um token válido.
        if (Autenticado)
        {
            return;
        }

        _unidadePublica = unidadeId;
    }
}
