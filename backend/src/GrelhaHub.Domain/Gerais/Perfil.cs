using Microsoft.AspNetCore.Identity;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Perfil de acesso da unidade — <c>RN-GER-010</c>, agora como papel do Identity (ADR-0022 §2).
/// <para>
/// <b>O perfil agrupa permissões; ele não É a permissão.</b> Nenhum ponto do sistema pergunta
/// "o perfil é Gerente?" — pergunta "tem `pedido.cancelar`?". É o que permite `RN-GER-022`
/// acrescentar o perfil Operador sem caçar condicional nenhuma.
/// </para>
/// <para>
/// <b>As permissões viraram claims de papel</b> (<c>perfil_permissao</c>, tipo
/// <c>permissao</c>, valor <c>recurso.acao</c>): não há mais entidade <c>Permissao</c> nem
/// coleção aqui. O <see cref="CatalogoDeAcesso"/> continua sendo a lista do que existe; o banco
/// guarda o que foi concedido.
/// </para>
/// <para>
/// ⚠ O nome vive em <c>Name</c>/<c>NormalizedName</c>, que são do framework — e é por isso que
/// não há uma propriedade <c>Nome</c> em paralelo: duas propriedades para a mesma coluna é o
/// tipo de espelho que diverge em silêncio, e <c>Name</c> em consulta LINQ traduz para a coluna
/// <c>nome</c> do banco.
/// </para>
/// <para>
/// ⚠⚠ O papel do Identity é global por desenho (índice único em <c>NormalizedName</c>); aqui o
/// índice único é <b><c>(unidade_id, normalized_name)</c></b> — exatamente o que
/// <c>RN-GER-009</c> exige de toda chave única. Era o conflito nº 1 da ADR-0021, resolvido na
/// configuração do modelo.
/// </para>
/// </summary>
public sealed class Perfil : IdentityRole<Guid>
{
    private Perfil() { }

    public Perfil(Guid? unidadeId, string nome, string descricao, bool sistema)
    {
        // Chave gerada pela aplicação, como toda Entidade (ADR-0004) — ver
        // AplicarChaveGeradaPelaAplicacao no DbContext.
        Id = Guid.CreateVersion7();

        Name = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome do perfil é obrigatório.", nameof(nome))
            : nome.Trim();

        // O RoleStore procura pelo normalizado; como a criação não passa por
        // RoleManager.CreateAsync, ninguém o preencheria por nós.
        NormalizedName = Name.ToUpperInvariant();

        UnidadeId = unidadeId;
        Descricao = descricao?.Trim() ?? string.Empty;
        Sistema = sistema;
    }

    /// <summary>
    /// <c>RN-GER-009</c> — a unidade dona do perfil. <b>Anulável</b> pela mesma exceção
    /// declarada de <c>usuario.unidade_id</c>: um eventual perfil da própria plataforma
    /// (<c>RN-GER-014</c>) não pertence a restaurante nenhum. Hoje todo perfil semeado tem
    /// unidade; a nulidade é a porta que a ADR-0022 deixou aberta, não um caso em uso.
    /// </summary>
    public Guid? UnidadeId { get; private set; }

    public string Descricao { get; private set; } = null!;

    /// <summary>
    /// Perfil semeado pela carga inicial. <c>RN-GER-002</c> — não se exclui; e por ser do
    /// sistema, também não se renomeia: relatórios e suporte referenciam o nome.
    /// </summary>
    public bool Sistema { get; private set; }
}
