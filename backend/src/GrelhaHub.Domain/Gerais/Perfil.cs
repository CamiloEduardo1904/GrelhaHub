using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Permissão granular — <c>RN-GER-010</c>. Catálogo **global**, como <see cref="Modulo"/>: o que
/// pertence à unidade é o perfil que a concede.
/// </summary>
public sealed class Permissao : Entidade
{
    private Permissao() { }

    public Permissao(string codigo, string descricao, CodigoDeModulo modulo)
    {
        Codigo = ValidarCodigo(codigo);
        Descricao = string.IsNullOrWhiteSpace(descricao)
            ? throw new ArgumentException("A descrição da permissão é obrigatória.", nameof(descricao))
            : descricao.Trim();
        Modulo = modulo;
    }

    /// <summary>Formato <c>recurso.acao</c> — `pedido.cancelar`, `custo.visualizar`.</summary>
    public string Codigo { get; private set; } = null!;

    public string Descricao { get; private set; } = null!;

    /// <summary>
    /// Módulo a que a permissão pertence.
    /// <para>
    /// Permissão e módulo respondem perguntas diferentes (<c>RN-GER-010</c> e
    /// <c>RN-GER-017</c>): "você pode?" e "o restaurante contratou?". Saber o módulo de cada
    /// permissão permite não conceder o que o cliente não comprou.
    /// </para>
    /// </summary>
    public CodigoDeModulo Modulo { get; private set; }

    private static string ValidarCodigo(string codigo)
    {
        var limpo = (codigo ?? string.Empty).Trim().ToLowerInvariant();

        return System.Text.RegularExpressions.Regex.IsMatch(limpo, "^[a-z_]+\\.[a-z_]+$")
            ? limpo
            : throw new ArgumentException(
                "A permissão segue o formato recurso.acao, em minúsculas.", nameof(codigo));
    }
}

/// <summary>
/// Perfil de acesso da unidade — <c>RN-GER-010</c>.
/// <para>
/// <b>O perfil agrupa permissões; ele não É a permissão.</b> Nenhum ponto do sistema pergunta
/// "o perfil é Gerente?" — pergunta "tem `pedido.cancelar`?". É o que permite `RN-GER-022`
/// acrescentar o perfil Operador sem caçar condicional nenhuma.
/// </para>
/// </summary>
public sealed class Perfil : Entidade, IPertenceAUnidade
{
    private readonly List<PerfilPermissao> _permissoes = [];

    private Perfil() { }

    public Perfil(Guid unidadeId, string nome, string descricao, bool sistema)
    {
        UnidadeId = unidadeId;
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome do perfil é obrigatório.", nameof(nome))
            : nome.Trim();
        Descricao = descricao?.Trim() ?? string.Empty;
        Sistema = sistema;
    }

    public Guid UnidadeId { get; private set; }

    public string Nome { get; private set; } = null!;

    public string Descricao { get; private set; } = null!;

    /// <summary>
    /// Perfil semeado pela carga inicial. <c>RN-GER-002</c> — não se exclui; e por ser do
    /// sistema, também não se renomeia: relatórios e suporte referenciam o nome.
    /// </summary>
    public bool Sistema { get; private set; }

    public IReadOnlyCollection<PerfilPermissao> Permissoes => _permissoes;

    public void Conceder(Guid permissaoId)
    {
        if (_permissoes.All(concedida => concedida.PermissaoId != permissaoId))
        {
            _permissoes.Add(new PerfilPermissao(Id, permissaoId));
        }
    }

    public void Revogar(Guid permissaoId) =>
        _permissoes.RemoveAll(concedida => concedida.PermissaoId == permissaoId);
}

/// <summary>Concessão de uma permissão a um perfil — chave composta.</summary>
public sealed class PerfilPermissao
{
    private PerfilPermissao() { }

    internal PerfilPermissao(Guid perfilId, Guid permissaoId)
    {
        PerfilId = perfilId;
        PermissaoId = permissaoId;
    }

    public Guid PerfilId { get; private set; }

    public Guid PermissaoId { get; private set; }

    public Permissao? Permissao { get; private set; }
}
