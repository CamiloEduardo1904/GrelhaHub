using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Leitura;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Vendas.ListarPedidos;

/// <summary>Pedidos da unidade. Sem filtro de data, traz o dia operacional corrente.</summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("pedido.visualizar")]
public sealed record ListarPedidosQuery(
    StatusPedido? Status,

    /// <summary>
    /// <c>RN-GER-006</c> ⚠⚠ — o intervalo é de **dia operacional**, não de data de calendário.
    /// <para>
    /// "De 10 a 12" são <b>três dias operacionais</b>, cada um das 04h00 às 03h59 — e não 72 horas
    /// corridas. O pedido da 01h30 do dia 13 pertence ao dia 12, e é assim que o restaurante conta
    /// a noite dele. Filtrar por <c>dh_criacao</c> daria outro número, plausível e errado.
    /// </para>
    /// <para>
    /// Só <c>De</c>: aquele dia. Nenhum dos dois: o dia operacional corrente.
    /// </para>
    /// </summary>
    DateOnly? De,
    DateOnly? Ate,

    Guid? UnidadeId = null) : IRequest<IReadOnlyList<PedidoResumoDto>>;

public sealed record PedidoResumoDto(
    /// <summary>Identidade do registro. <b>GUID</b>: `numero` é identificador HUMANO, sequencial
    /// por dia operacional e repetido entre unidades — nunca serve de chave.</summary>
    Guid Id,

    /// <summary>
    /// <c>RN-GER-009</c> — de qual unidade este pedido é.
    /// <para>
    /// <b>Vai no payload sempre</b>, e não só quando a plataforma consulta sem recorte. Para o
    /// restaurante é redundante (ele só recebe os seus); para o suporte olhando a base inteira é a
    /// única coisa que distingue o pedido nº 1 de um cliente do pedido nº 1 do outro — e o número
    /// se repete entre unidades por desenho.
    /// </para>
    /// </summary>
    Guid UnidadeId,

    int Numero,
    string Canal,
    string Status,
    string? Cliente,
    string? Telefone,
    decimal Total,
    int QuantidadeDeItens,
    DateTimeOffset CriadoEm,
    DateOnly DataOperacional,
    string? Observacao,

    /// <summary>
    /// <c>RN-GER-002</c> / <c>RN-VEN-058</c> ⚠ — os três do cancelamento, e eles andam juntos.
    /// <para>
    /// Motivo sem instante não diz se foi agora ou na semana passada; instante sem responsável não
    /// diz a quem perguntar. A comanda exibe os três ou não exibe o bloco.
    /// </para>
    /// <para>
    /// ⚠ <c>CanceladoPor</c> é <c>null</c> em pedido cancelado antes de 19/08/2026, e a tela
    /// <b>omite a linha</b> em vez de inventar "sistema".
    /// </para>
    /// </summary>
    string? MotivoCancelamento,
    DateTimeOffset? CanceladoEm,
    string? CanceladoPor,

    IReadOnlyList<ItemResumoDto> Itens);

public sealed record ItemResumoDto(string Produto, int Quantidade, decimal PrecoPraticado, string? Observacao);

/// <summary>
/// ADR-0019 — lê do modelo de <b>leitura</b>.
/// <para>
/// ⚠ <b>O total sai da visão, e isso não recria a segunda definição que a versão anterior evitava.</b>
/// O comentário de antes dizia: "carrega o agregado em vez de projetar o total em SQL, senão o
/// <c>Sum</c> vira uma SEGUNDA definição de <c>RN-VEN-039</c>". A preocupação estava certa — duas
/// fórmulas divergem no dia em que taxa de serviço entrar. O que muda é que agora a fórmula tem
/// <b>um lugar só de novo</b>: a visão <c>vw_pedido_resumo</c>. Não é o handler somando por conta
/// própria; é o banco somando no único lugar onde a soma está escrita para leitura.
/// </para>
/// <para>
/// ⚠ Quando <c>RN-VEN-039</c> ganhar taxa ou desconto, muda <b>a visão</b> — e o teste de paridade
/// entre ela e <c>Pedido.Total</c> é o que impede a divergência de passar despercebida.
/// </para>
/// </summary>
public sealed class ListarPedidosHandler(
    ILeituraDb leitura, IRelogio relogio, IContextoDeExecucao contexto)
    : IRequestHandler<ListarPedidosQuery, IReadOnlyList<PedidoResumoDto>>
{
    public async Task<IReadOnlyList<PedidoResumoDto>> Handle(
        ListarPedidosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // RN-GER-006 — o recorte é por data_operacional (coluna GERADA), nunca por data de
        // criação: senão o pedido da 01h30 de domingo sumiria do painel de sábado.
        var de = request.De ?? relogio.DiaOperacionalDeHoje();
        var ate = request.Ate ?? de;

        if (ate < de)
        {
            throw new RegraDeNegocioException(
                "A data final do período não pode ser anterior à inicial.", "RN-GER-006");
        }

        // ⚠⚠ RN-GER-016 — o recorte passa por `UnidadeDaConsulta.Recorte`, e NÃO pelo
        // `request.UnidadeId` cru. A diferença aparece quando um RESTAURANTE informa a unidade do
        // vizinho: o helper IGNORA o parâmetro e devolve a própria; usar o valor cru faria o filtro
        // do handler (`= vizinho`) intersectar com o filtro global do contexto (`= minha`) e
        // devolver ZERO — o restaurante veria a própria lista vazia por ter mandado um id que não é
        // dele. Foi assim que a conversão quebrou `AcessoTests`, e é por isso que o helper existe.
        var unidade = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);
        var status = (int?)request.Status;

        var pedidos = await leitura.PedidosResumo
            .Where(p => unidade == null || p.UnidadeId == unidade)
            .Where(p => p.DataOperacional >= de && p.DataOperacional <= ate)
            .Where(p => status == null || p.Status == status)
            .OrderByDescending(p => p.DataOperacional)
            .ThenByDescending(p => p.Numero)
            .ToListAsync(cancellationToken);

        if (pedidos.Count == 0)
        {
            return [];
        }

        // Uma consulta para os itens de todos os pedidos, em vez de uma por pedido — o mesmo
        // cuidado que a versão anterior tinha com os clientes.
        var ids = pedidos.Select(p => p.Id).ToList();

        var itens = await leitura.ItensDePedido
            .Where(i => ids.Contains(i.PedidoId))
            .ToListAsync(cancellationToken);

        var porPedido = itens.GroupBy(i => i.PedidoId).ToDictionary(g => g.Key, g => g.ToList());

        return
        [
            .. pedidos.Select(p => new PedidoResumoDto(
                p.Id,
                p.UnidadeId,
                p.Numero,
                ((CanalVenda)p.Canal).ToString(),
                ((StatusPedido)p.Status).ToString(),
                p.NomeCliente,
                p.TelefoneCliente,
                p.Total,
                p.QuantidadeDeItens,
                p.CriadoEm,
                p.DataOperacional,
                p.Observacao,
                p.MotivoCancelamento,
                p.CanceladoEm,
                p.CanceladoPor,
                porPedido.TryGetValue(p.Id, out var doPedido)
                    ? [.. doPedido.Select(i => new ItemResumoDto(
                        i.NomeProduto, i.Quantidade, i.PrecoPraticado, i.Observacao))]
                    : []))
        ];
    }
}
