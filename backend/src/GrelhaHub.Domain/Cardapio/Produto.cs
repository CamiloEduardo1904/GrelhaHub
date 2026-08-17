using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Cardapio;

/// <summary>
/// Item vendável do cardápio.
/// <para>
/// <b>Simplificação declarada</b> (SPEC-VEN-001 §8): o preço está direto no produto. O modelo
/// definitivo tem <c>preco_produto</c> com vigência (<c>RN-VEN-010</c>) — alterar o preço cria
/// uma nova vigência e preserva a anterior. Nesta fatia, o histórico é preservado apenas do
/// lado da venda, pelo preço praticado copiado no item (<c>RN-VEN-008</c>), que é a metade
/// mais importante da regra.
/// </para>
/// </summary>
public sealed class Produto : Entidade, IPertenceAUnidade
{
    private Produto() { }

    public Produto(Guid unidadeId, Guid categoriaId, string nome, string? descricao, decimal preco)
    {
        UnidadeId = unidadeId;
        CategoriaId = categoriaId;
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome do produto é obrigatório.", nameof(nome))
            : nome.Trim();
        Descricao = descricao?.Trim();
        DefinirPreco(preco);
    }

    public Guid UnidadeId { get; private set; }
    public Guid CategoriaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }

    /// <summary><c>RN-VEN-032</c> — o preço vigente é sempre maior que zero.</summary>
    public decimal Preco { get; private set; }

    /// <summary><c>RN-VEN-020</c> — produto inativo não entra em pedido novo.</summary>
    public bool Ativo { get; private set; } = true;

    /// <summary>Indisponibilidade momentânea — "acabou hoje", sem inativar o cadastro.</summary>
    public bool Disponivel { get; private set; } = true;

    public string? UrlImagem { get; private set; }

    public Categoria? Categoria { get; private set; }

    public bool PodeSerVendido => Ativo && Disponivel;

    public void DefinirPreco(decimal preco)
    {
        if (preco <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preco), preco, "RN-VEN-032 — o preço deve ser maior que zero.");
        }

        // RN-GER-003 — valores monetários com duas casas decimais.
        Preco = Math.Round(preco, 2, MidpointRounding.AwayFromZero);
    }

    public void Atualizar(string nome, string? descricao, decimal preco, Guid categoriaId)
    {
        Nome = nome.Trim();
        Descricao = descricao?.Trim();
        CategoriaId = categoriaId;
        DefinirPreco(preco);
    }

    public void Inativar() => Ativo = false;
    public void Reativar() => Ativo = true;
    public void DefinirDisponibilidade(bool disponivel) => Disponivel = disponivel;
}
