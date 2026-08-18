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

    public Produto(Guid unidadeId, Guid categoriaId, string nome, string? descricao, Dinheiro preco)
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
    public Dinheiro Preco { get; private set; }

    /// <summary>
    /// <c>RN-VEN-044</c> — o estado <b>cadastral</b>: "este produto ainda faz parte do nosso
    /// cardápio?". Quem decide é quem cadastra, o horizonte é a temporada, e não volta sozinho.
    /// <c>RN-VEN-020</c> — produto inativo não entra em pedido novo.
    /// </summary>
    public bool Ativo { get; private set; } = true;

    /// <summary>
    /// <c>RN-VEN-044</c> — o estado <b>do turno</b>: "tem hoje?". Quem decide é quem está no
    /// salão, e amanhã volta.
    /// <para>
    /// <b>É outro estado, não o mesmo com outro nome.</b> O efeito no cliente é idêntico — o
    /// produto não aparece —, e é por isso que fundir os dois num booleano é tentador. No dia em
    /// que existir relatório de mix, "saiu do cardápio em março" e "acabou na terça" são
    /// histórias diferentes que um único campo já teria perdido.
    /// </para>
    /// </summary>
    public bool Disponivel { get; private set; } = true;

    /// <summary>
    /// <c>RN-VEN-042</c> — o caminho da imagem do produto. <b>Uma só</b>, e nula é estado válido.
    /// <para>
    /// A coluna existia desde a migration <c>Fundacao</c> sem caminho de escrita nem de leitura —
    /// coluna morta. <c>SPEC-VEN-002</c> a ligou.
    /// </para>
    /// </summary>
    public string? UrlImagem { get; private set; }

    /// <summary>
    /// <c>RN-VEN-042</c> — aponta para a imagem nova. <b>Não apaga a anterior</b>
    /// (<c>RN-GER-002</c>): apagar arquivo é a exclusão física mais irreversível que existe, e a
    /// trilha guarda o caminho antigo — trilha que aponta para arquivo apagado é trilha inútil.
    /// A coleta de órfãos é rotina de manutenção, não efeito colateral de um <c>POST</c>.
    /// </summary>
    public void DefinirImagem(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            throw new ArgumentException("O caminho da imagem é obrigatório.", nameof(caminho));
        }

        UrlImagem = caminho;
    }

    /// <summary>Limpa a referência. O arquivo permanece no disco, por <c>RN-GER-002</c>.</summary>
    public void RemoverImagem() => UrlImagem = null;

    public Categoria? Categoria { get; private set; }

    public bool PodeSerVendido => Ativo && Disponivel;

    public void DefinirPreco(Dinheiro preco)
    {
        if (!preco.EhPositivo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preco), preco, "RN-VEN-032 — o preço deve ser maior que zero.");
        }

        // RN-GER-003 — o arredondamento para duas casas já é garantido por Dinheiro.
        Preco = preco;
    }

    public void Atualizar(string nome, string? descricao, Dinheiro preco, Guid categoriaId)
    {
        Nome = nome.Trim();
        Descricao = descricao?.Trim();
        CategoriaId = categoriaId;
        DefinirPreco(preco);
    }

    /// <summary>
    /// <c>RN-VEN-044</c>, <c>RN-VEN-045</c> — sai do cardápio e <b>não é excluído</b>: o item de
    /// pedidos históricos continua apontando para ele. <b>Não mexe em <c>Disponivel</c></b> — os
    /// dois estados são independentes.
    /// </summary>
    public void Inativar() => Ativo = false;

    public void Reativar() => Ativo = true;

    /// <summary>
    /// <c>RN-VEN-044</c> — o "acabou hoje". <b>Não mexe em <c>Ativo</c>.</b>
    /// </summary>
    public void DefinirDisponibilidade(bool disponivel) => Disponivel = disponivel;
}
