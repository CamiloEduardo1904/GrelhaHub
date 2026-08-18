using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
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
    IReadOnlyList<ItemResumoDto> Itens);

public sealed record ItemResumoDto(string Produto, int Quantidade, decimal PrecoPraticado, string? Observacao);

public sealed class ListarPedidosHandler(
    IAppDbContext db, IRelogio relogio, IContextoDeExecucao contexto)
    : IRequestHandler<ListarPedidosQuery, IReadOnlyList<PedidoResumoDto>>
{
    public async Task<IReadOnlyList<PedidoResumoDto>> Handle(ListarPedidosQuery request, CancellationToken cancellationToken)
    {
        // RN-GER-006 — o recorte é por data_operacional (coluna GERADA), nunca por data de
        // criação: senão o pedido da 01h30 de domingo sumiria do painel de sábado.
        var de = request.De ?? relogio.DiaOperacionalDeHoje();
        var ate = request.Ate ?? de;

        if (ate < de)
        {
            throw new RegraDeNegocioException(
                "A data final do período não pode ser anterior à inicial.", "RN-GER-006");
        }

        // RN-GER-014 / ADR-0013 — o recorte por unidade passou a ser POSSÍVEL, e continua
        // opcional para a plataforma.
        //
        // Ver a base inteira numa lista é o que o ADR-0013 decidiu, e `AcessoTests` CA-9 afirma.
        // O que faltava era o suporte conseguir focar em UMA casa: sem isso, atender um cliente
        // por telefone significava olhar os pedidos de três clientes de uma vez, sem coluna
        // dizendo de quem era cada linha. Para o restaurante o parâmetro é ignorado (RN-GER-016).
        var recorte = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        var consulta = db.Pedidos
            .AsNoTracking()
            .Where(p => recorte == null || p.UnidadeId == recorte)
            .Where(p => p.DataOperacional >= de && p.DataOperacional <= ate);

        if (request.Status is { } status)
        {
            consulta = consulta.Where(p => p.Status == status);
        }

        // Carrega o agregado em vez de projetar o total em SQL.
        //
        // Antes, o total saía de um `Sum` na consulta — uma SEGUNDA definição de RN-VEN-039,
        // que passaria a divergir de `Pedido.Total` no dia em que taxa de serviço ou desconto
        // entrassem na fórmula. Agora a regra tem um lugar só. O recorte que importa continua
        // no banco: dia operacional e status.
        var pedidos = await consulta
            .Include(p => p.Itens)
            .OrderByDescending(p => p.Numero)
            .ToListAsync(cancellationToken);

        var idsDeClientes = pedidos
            .Where(pedido => pedido.ClienteId.HasValue)
            .Select(pedido => pedido.ClienteId!.Value)
            .Distinct()
            .ToList();

        // Uma consulta para todos os clientes do dia, em vez de uma por pedido.
        var clientes = await db.Clientes
            .AsNoTracking()
            .Where(cliente => idsDeClientes.Contains(cliente.Id))
            .ToDictionaryAsync(cliente => cliente.Id, cancellationToken);

        return pedidos.Select(pedido =>
        {
            var cliente = pedido.ClienteId is { } id && clientes.TryGetValue(id, out var achado)
                ? achado
                : null;

            return new PedidoResumoDto(
                pedido.Id,
                pedido.UnidadeId,
                pedido.Numero,
                pedido.Canal.ToString(),
                pedido.Status.ToString(),
                cliente?.Nome,
                cliente?.Telefone,
                pedido.Total.Valor,
                pedido.Itens.Count,
                pedido.CriadoEm,
                pedido.DataOperacional,
                pedido.Observacao,
                pedido.Itens
                    .Select(item => new ItemResumoDto(
                        item.NomeProduto, item.Quantidade, item.PrecoPraticado.Valor, item.Observacao))
                    .ToList());
        }).ToList();
    }
}
