using GrelhaHub.Application.Common.Interfaces;

namespace GrelhaHub.Integration.Tests.Infraestrutura;

/// <summary>
/// <see cref="IContextoDeExecucao"/> controlado pelo teste, para quando a asserção precisa
/// falar com o banco direto, sem passar por HTTP.
/// <para>
/// Existe porque o <c>GrelhaHubDbContext</c> depende do contexto de execução para aplicar o
/// filtro global de unidade (<c>RN-GER-009</c>). Um teste que quer conferir o dado gravado
/// precisa declarar <b>em qual unidade</b> está olhando — e é justamente isso que o filtro
/// torna obrigatório.
/// </para>
/// <para>
/// <b>Não use isto para verificar isolamento.</b> Conferir que a unidade B não vê o dado da
/// unidade A exige o caminho real: token ou <c>slug</c>. Um contexto fixo provaria apenas
/// que o teste soube preencher um campo.
/// </para>
/// </summary>
internal sealed class ContextoFixo : IContextoDeExecucao
{
    public Guid? UsuarioId { get; set; }

    public Guid UnidadeId { get; set; }

    public bool Autenticado { get; set; }

    /// <summary>ADR-0022 — o teste que precisar de política monta o principal; o resto não usa.</summary>
    public System.Security.Claims.ClaimsPrincipal? Principal { get; set; }

    public bool PodeVerCusto { get; set; }

    /// <summary><c>RF-SEG-005</c> — o teste decide se ha senha pendente.</summary>
    public bool DeveTrocarSenha { get; set; }

    /// <summary><c>RN-GER-010</c> — permissoes que o teste quer conceder.</summary>
    public HashSet<string> PermissoesConcedidas { get; } = new(StringComparer.Ordinal);

    public IReadOnlySet<string> Permissoes => PermissoesConcedidas;

    public bool Pode(string permissao) => EhPlataforma || PermissoesConcedidas.Contains(permissao);

    /// <summary><c>RN-GER-014</c> — desliga o recorte por unidade. Ver ADR-0013.</summary>
    public bool EhPlataforma { get; set; }

    public void DefinirUnidadePublica(Guid unidadeId) => UnidadeId = unidadeId;
}
