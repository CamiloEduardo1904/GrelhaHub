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

    /// <summary>
    /// <c>RN-GER-010</c> — permissoes do token, uma claim por codigo.
    /// <para>
    /// A versao anterior derivava autorizacao do NOME do perfil (IsInRole("Gerente")), e era o
    /// que a regra proibe: criar o perfil Operador (RN-GER-022) exigiria cacar cada condicional.
    /// </para>
    /// </summary>
    public IReadOnlySet<string> Permissoes =>
        _permissoes ??= Usuario?.FindAll(ClaimPermissao)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal)
            ?? [];

    private HashSet<string>? _permissoes;

    /// <summary><c>RN-GER-010</c> — o usuario de plataforma opera acima das permissoes de unidade.</summary>
    public bool Pode(string permissao) => EhPlataforma || Permissoes.Contains(permissao);

    /// <summary><c>RN-GER-012</c> — custo, CMV e margem so saem do servidor com esta permissao.</summary>
    public bool PodeVerCusto => Pode(PermissaoDeCusto);

    public const string PermissaoDeCusto = "custo.visualizar";

    /// <summary>Precisa casar com GeradorDeTokenJwt.ClaimPermissao.</summary>
    public const string ClaimPermissao = "permissao";

    /// <summary><c>RF-SEG-005</c> — vem do token, emitido no login.</summary>
    public bool DeveTrocarSenha =>
        string.Equals(Usuario?.FindFirstValue(ClaimTrocarSenha), "true", StringComparison.Ordinal);

    public const string ClaimTrocarSenha = "trocar_senha";

    /// <summary>
    /// <c>RN-GER-014</c> — vem de UMA claim (<c>plataforma</c>), emitida só para usuário de
    /// plataforma. É o único ponto de onde o desligamento do filtro global pode nascer.
    /// </summary>
    public bool EhPlataforma =>
        string.Equals(Usuario?.FindFirstValue(ClaimPlataforma), "true", StringComparison.Ordinal);

    /// <summary>Nome da claim, para que emissor e leitor não divirjam por literal repetido.</summary>
    public const string ClaimPlataforma = "plataforma";

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
