using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Cardapio;

public sealed class Categoria : Entidade, IPertenceAUnidade
{
    private readonly List<Produto> _produtos = [];

    private Categoria() { }

    public Categoria(Guid unidadeId, string nome, int ordem)
    {
        UnidadeId = unidadeId;
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome da categoria é obrigatório.", nameof(nome))
            : nome.Trim();
        Ordem = ordem;
    }

    public Guid UnidadeId { get; private set; }
    public string Nome { get; private set; } = null!;
    public int Ordem { get; private set; }
    public bool Ativa { get; private set; } = true;

    public IReadOnlyCollection<Produto> Produtos => _produtos;

    /// <summary><c>RN-GER-002</c> / <c>RN-005</c> — categoria com produtos não é excluída.</summary>
    public void Inativar() => Ativa = false;

    public void Renomear(string nome, int ordem)
    {
        Nome = nome.Trim();
        Ordem = ordem;
    }
}
