using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.FilaDeMonitoramento;

/// <summary>
/// A fila de monitoramento — <c>RN-VEN-054</c> ⚠, <c>SPEC-VEN-004</c>.
/// <para>
/// <b>É a pergunta do meio do serviço, e ela não tinha tela:</b> o painel de operação
/// (<c>RN-VEN-049</c>) responde <b>quanto</b> a casa vendeu, e a lista de pedidos responde <b>o que
/// entrou no dia</b>. Nenhuma das duas responde <b>o que ainda tenho para entregar</b> — e a lista
/// responde pior conforme a noite avança, porque ela cresce: às onze horas, os três pendentes estão
/// espalhados entre quarenta comandas já finalizadas.
/// </para>
/// <para>
/// ⚠ <b>Permissão é <c>pedido.visualizar</c>, e não uma permissão nova.</b> <c>RN-GER-038</c>
/// separou <c>operacao.visualizar</c> porque o painel <b>agrega</b> venda, e agregado é o passo
/// anterior a margem (<c>RN-GER-012</c>). A fila não agrega nada: é um recorte da mesma lista, com
/// <b>menos</b> campos. Permissão nova aqui protegeria algo que a antiga já mostra por inteiro — e
/// permissão que não protege nada é pior que permissão nenhuma, porque alguém confia nela.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("pedido.visualizar")]
public sealed record FilaDeMonitoramentoQuery(
    /// <summary>
    /// <c>RN-GER-014</c> / <c>RN-GER-016</c> — só a plataforma informa; para o restaurante é
    /// <b>ignorado</b> por <see cref="UnidadeDaConsulta.Recorte"/>.
    /// </summary>
    Guid? UnidadeId = null) : IRequest<IReadOnlyList<PedidoNaFilaDto>>;

/// <summary>
/// O pedido como a fila o mostra — <c>RN-VEN-054</c> ⚠.
/// <para>
/// ⚠⚠ <b>Não há <c>Telefone</c> nem <c>Observacao</c> aqui, e a ausência é o cumprimento da
/// regra.</b> Não são <c>null</c>, não são mascarados: <b>não existem no contrato</b>. A fila é
/// tela de ambiente compartilhado — balcão, passagem, às vezes um monitor na parede —, e dado de
/// contato ali fica à vista de quem passa sem que ninguém precise dele para saber o que preparar. É
/// <c>CHAPA-05</c> aplicada ao servidor: não há cadeado na tela porque não há campo na resposta.
/// </para>
/// <para>
/// ⚠ <b>O nome do cliente FICA.</b> É como se chama quem vem buscar, e uma fila que não sabe o nome
/// de ninguém obriga a gritar o número — que reinicia todo dia.
/// </para>
/// <para>
/// A observação do pedido continua <b>inteira</b> na comanda da lista de pedidos
/// (<c>RN-VEN-052</c>): ela é instrução de preparo, e a fila <b>não substitui</b> a comanda de
/// preparo. Se um dia substituir, a regra muda antes do código.
/// </para>
/// </summary>
public sealed record PedidoNaFilaDto(
    /// <summary>Identidade. <c>Numero</c> é humano, reinicia a cada virada e repete entre unidades.</summary>
    Guid Id,

    /// <summary><c>RN-GER-009</c> — de qual unidade. A plataforma vê várias casas na mesma resposta.</summary>
    Guid UnidadeId,

    int Numero,
    string Canal,
    string Status,
    string? Cliente,
    decimal Total,
    int QuantidadeDeItens,

    /// <summary>Alimenta o tempo de espera exibido na comanda — o dado que a fila existe para dar.</summary>
    DateTimeOffset CriadoEm,

    /// <summary>
    /// <c>RN-GER-006</c> ⚠⚠ — vai no payload porque a fila <b>não é recortada por período</b>: o
    /// pedido que atravessou a virada continua nela, e a tela precisa marcá-lo. Sem esta coluna, a
    /// comanda nº 1 de ontem é indistinguível da nº 1 de hoje.
    /// </summary>
    DateOnly DataOperacional,

    IReadOnlyList<ItemNaFilaDto> Itens);

/// <summary>⚠ Sem <c>Observacao</c>, pela mesma razão de <see cref="PedidoNaFilaDto"/>.</summary>
public sealed record ItemNaFilaDto(string Produto, int Quantidade, decimal PrecoPraticado);

/// <summary>
/// ADR-0019 — lê do modelo de <b>leitura</b>, sobre as mesmas visões que a lista de pedidos usa.
/// <para>
/// <b>Nenhuma visão nova, e a ausência é decisão:</b> visão se justifica por agregação
/// (<c>vw_operacao_dia</c> existe porque <c>RN-VEN-049</c> mora nela), e a fila não agrega — ela
/// filtra. Uma <c>vw_fila</c> seria a mesma soma escrita num segundo lugar, com o mesmo destino de
/// toda segunda escrita: divergir em silêncio.
/// </para>
/// </summary>
public sealed class FilaDeMonitoramentoHandler(ILeituraDb leitura, IContextoDeExecucao contexto)
    : IRequestHandler<FilaDeMonitoramentoQuery, IReadOnlyList<PedidoNaFilaDto>>
{
    /// <summary>
    /// <c>RN-VEN-054</c> / <c>RN-VEN-018</c> — os quatro estados de onde o pedido <b>ainda sai</b>.
    /// <para>
    /// ⚠ A lista é escrita pelo <b>enum</b>, e não pelos números 1 a 4: renumerar
    /// <see cref="StatusPedido"/> um dia quebraria a constante literal em silêncio, devolvendo uma
    /// fila plausível e errada. <c>Entregue</c>, <c>Finalizado</c> e <c>Cancelado</c> ficam de fora
    /// — e <c>Entregue</c> fica de fora <b>de propósito</b>: dali ainda se vai para Finalizado, mas
    /// o que falta é fechamento administrativo, não preparo, e a cozinha não tem o que fazer com
    /// ele. É a mesma distinção de <c>pedidoEncerrado</c> em <c>ciclo.ts</c>, com outro corte.
    /// </para>
    /// </summary>
    private static readonly int[] EstadosNaFila =
    [
        (int)StatusPedido.Aberto,
        (int)StatusPedido.Confirmado,
        (int)StatusPedido.EmProducao,
        (int)StatusPedido.Pronto,
    ];

    public async Task<IReadOnlyList<PedidoNaFilaDto>> Handle(
        FilaDeMonitoramentoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ⚠⚠ RN-GER-016 — o recorte passa pelo helper, e não pelo `request.UnidadeId` cru: um
        // restaurante que informasse a unidade do vizinho intersectaria o filtro do handler com o
        // filtro global do contexto e receberia a PRÓPRIA fila vazia. Ver `ListarPedidos`.
        var unidade = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        // ⚠ SEM filtro de período, e a ausência é RN-VEN-054: o que está vivo está na fila. Um
        // pedido aberto às 23h50 que atravessou a virada (RN-GER-006) continua para ser entregue, e
        // sumiria se o recorte fosse o dia operacional corrente.
        //
        // A ordem é FIFO — quem espera há mais tempo primeiro. É a única ordem que a fila pode ter:
        // ordenar pelo número mais recente poria o pedido que acabou de entrar na frente de quem
        // está esperando há vinte minutos.
        var pedidos = await leitura.PedidosResumo
            .Where(p => unidade == null || p.UnidadeId == unidade)
            .Where(p => EstadosNaFila.Contains(p.Status))
            .OrderBy(p => p.CriadoEm)
            .ToListAsync(cancellationToken);

        if (pedidos.Count == 0)
        {
            return [];
        }

        // Uma consulta para os itens de todos os pedidos — não uma por pedido. A fila é consultada
        // a cada 15 s (RN-VEN-055), e é o lugar do sistema onde N+1 custa mais caro.
        var ids = pedidos.Select(p => p.Id).ToList();

        var itens = await leitura.ItensDePedido
            .Where(i => ids.Contains(i.PedidoId))
            .ToListAsync(cancellationToken);

        var porPedido = itens.GroupBy(i => i.PedidoId).ToDictionary(g => g.Key, g => g.ToList());

        return
        [
            .. pedidos.Select(p => new PedidoNaFilaDto(
                p.Id,
                p.UnidadeId,
                p.Numero,
                ((CanalVenda)p.Canal).ToString(),
                ((StatusPedido)p.Status).ToString(),
                p.NomeCliente,
                p.Total,
                p.QuantidadeDeItens,
                p.CriadoEm,
                p.DataOperacional,
                porPedido.TryGetValue(p.Id, out var doPedido)
                    ? [.. doPedido.Select(i => new ItemNaFilaDto(
                        i.NomeProduto, i.Quantidade, i.PrecoPraticado))]
                    : []))
        ];
    }
}
