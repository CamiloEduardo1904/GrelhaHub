using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.ListarPedidos;

/// <summary>Pedidos da unidade. Sem filtro de data, traz o dia operacional corrente.</summary>
public sealed record ListarPedidosQuery(StatusPedido? Status, DateOnly? DataOperacional)
    : IRequest<IReadOnlyList<PedidoResumoDto>>;

public sealed record PedidoResumoDto(
    Guid Id,
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

public sealed class ListarPedidosHandler(IAppDbContext db, IRelogio relogio)
    : IRequestHandler<ListarPedidosQuery, IReadOnlyList<PedidoResumoDto>>
{
    public async Task<IReadOnlyList<PedidoResumoDto>> Handle(ListarPedidosQuery request, CancellationToken cancellationToken)
    {
        // RN-GER-006 — o recorte do dia é por data_operacional (coluna gerada), nunca por
        // data de criação: senão o pedido da 01h30 de domingo sumiria do painel de sábado.
        var dia = request.DataOperacional ?? relogio.DiaOperacionalDeHoje();

        var consulta = db.Pedidos.AsNoTracking().Where(p => p.DataOperacional == dia);

        if (request.Status is { } status)
        {
            consulta = consulta.Where(p => p.Status == status);
        }

        return await consulta
            .OrderByDescending(p => p.Numero)
            .Select(p => new PedidoResumoDto(
                p.Id,
                p.Numero,
                p.Canal.ToString(),
                p.Status.ToString(),
                db.Clientes.Where(c => c.Id == p.ClienteId).Select(c => c.Nome).FirstOrDefault(),
                db.Clientes.Where(c => c.Id == p.ClienteId).Select(c => c.Telefone).FirstOrDefault(),
                p.Itens.Sum(i => i.PrecoPraticado * i.Quantidade),
                p.Itens.Count,
                p.CriadoEm,
                p.DataOperacional,
                p.Observacao,
                p.Itens.Select(i => new ItemResumoDto(
                    i.NomeProduto, i.Quantidade, i.PrecoPraticado, i.Observacao)).ToList()))
            .ToListAsync(cancellationToken);
    }
}
