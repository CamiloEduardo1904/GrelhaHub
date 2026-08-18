using GrelhaHub.Application.Common.Leitura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// O lado da <b>leitura</b> — ADR-0019.
/// <para>
/// <b>Não é o contexto de escrita com outro nome.</b> Aqui não há entidade de domínio, não há
/// rastreamento e <b>não há <c>SaveChangesAsync</c></b> — o método não existe nesta interface, e é
/// essa ausência que torna a separação verificável em vez de combinada. Handler de <c>Query</c>
/// recebe isto e mais nada.
/// </para>
/// <para>
/// Cada propriedade aponta para uma <b>visão do banco</b>, projetada para a tela que a consome. É o
/// mecanismo <c>VW</c> da ordem do ADR-0009 (<c>MOD → constraints → GEN → TRG → VW → APP</c>) —
/// agregação pertence ao banco, e somar em memória no handler é o último lugar da lista.
/// </para>
/// <para>
/// ⚠ Os tipos abaixo <b>não têm chave</b>. Isso não é esquecimento: tipo sem chave não pode ser
/// rastreado nem gravado, então o EF Core recusa qualquer tentativa de escrever por aqui — a regra
/// passa a ser imposta pelo framework, e não pela lembrança de quem escreve o handler.
/// </para>
/// </summary>
public interface ILeituraDb
{
    /// <summary>
    /// <c>vw_pedido_resumo</c> — uma linha por pedido, com o total já somado e a contagem de itens.
    /// <para>
    /// ⚠ O total sai da <b>visão</b>, e é a razão de ela existir: <c>Sum(i =&gt; i.PrecoPraticado.Valor
    /// * i.Quantidade)</c> não vira SQL, porque o EF Core não traduz acesso a membro de propriedade
    /// com conversor de valor. O handler antigo carregava o agregado inteiro para somar dois campos.
    /// </para>
    /// </summary>
    IQueryable<PedidoResumoLeitura> PedidosResumo { get; }

    /// <summary>
    /// <c>vw_pedido_item</c> — os itens, já com o total da linha calculado.
    /// </summary>
    IQueryable<ItemDePedidoLeitura> ItensDePedido { get; }

    /// <summary>
    /// <c>vw_operacao_dia</c> — <c>RN-VEN-049</c> ⚠⚠, uma linha por (unidade, dia operacional) com
    /// faturamento, contagem e valor em andamento já separados.
    /// <para>
    /// A regra de <b>o que conta como faturamento</b> mora na visão, e é onde ela deve morar: o
    /// handler não soma nada, só recorta o período.
    /// </para>
    /// </summary>
    IQueryable<OperacaoDoDiaLeitura> OperacaoPorDia { get; }

    /// <summary>
    /// <c>vw_produto_vendido</c> — quanto de cada produto saiu, por unidade e dia operacional.
    /// <para>
    /// ⚠ Agrupa pelo <b>nome praticado na venda</b>, não pelo produto do cardápio: <c>RN-VEN-008</c>
    /// copia o nome no ato da venda, e é essa cópia que o relatório reflete. Produto renomeado
    /// depois não reescreve o que foi vendido.
    /// </para>
    /// </summary>
    IQueryable<ProdutoVendidoLeitura> ProdutosVendidos { get; }
}
