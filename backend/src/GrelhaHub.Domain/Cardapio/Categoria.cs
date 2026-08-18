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

    /// <summary>
    /// <c>RN-VEN-045</c> — categoria não é excluída; inativa-se.
    /// <para>
    /// <b><c>RN-VEN-043</c> — não propaga para os produtos.</b> Inativar a categoria esconde o
    /// grupo do cardápio público e deixa o cadastro dos produtos intacto. Propagar seria
    /// irreversível: ao reativar, o sistema não teria como distinguir o produto que estava
    /// inativo <i>antes</i>, por decisão de alguém, daquele que a cascata desligou.
    /// </para>
    /// <para>
    /// O comentário anterior citava <c>RN-GER-002</c>, que fala de registro <b>transacional</b>.
    /// Categoria é cadastro — a citação era plausível e errada, e é o que motivou escrever
    /// <c>RN-VEN-045</c> (SPEC-VEN-003 §11.4).
    /// </para>
    /// </summary>
    public void Inativar() => Ativa = false;

    /// <summary>
    /// <c>RN-VEN-043</c> — devolve o grupo inteiro ao cardápio, sem tocar produto por produto.
    /// <para>
    /// <b>Sem este método, <c>Inativar</c> era caminho de mão única.</b> Ele existia desde a
    /// <c>SPEC-VEN-001</c> e nunca havia sido chamado — se alguém o tivesse chamado, a categoria
    /// ficaria inativa para sempre.
    /// </para>
    /// </summary>
    public void Reativar() => Ativa = true;

    public void Renomear(string nome, int ordem)
    {
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome da categoria é obrigatório.", nameof(nome))
            : nome.Trim();
        Ordem = ordem;
    }
}
