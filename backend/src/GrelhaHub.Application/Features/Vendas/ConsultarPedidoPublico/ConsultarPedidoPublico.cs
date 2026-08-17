using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.ConsultarPedidoPublico;

/// <summary>Acompanhamento do pedido pelo cliente, sem autenticação.</summary>
public sealed record ConsultarPedidoPublicoQuery(string Slug, int Numero, string Telefone)
    : IRequest<PedidoPublicoResponse>;

public sealed record PedidoPublicoResponse(
    int Numero,
    string Status,
    decimal Total,
    DateTimeOffset CriadoEm,
    IReadOnlyList<ItemPublicoDto> Itens);

public sealed record ItemPublicoDto(string Produto, int Quantidade, decimal PrecoPraticado, decimal Total);

public sealed class ConsultarPedidoPublicoHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ConsultarPedidoPublicoQuery, PedidoPublicoResponse>
{
    public async Task<PedidoPublicoResponse> Handle(ConsultarPedidoPublicoQuery request, CancellationToken cancellationToken)
    {
        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();

        var unidade = await db.Unidades.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == slug && u.Ativa, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        contexto.DefinirUnidadePublica(unidade.Id);

        var telefone = new string((request.Telefone ?? string.Empty).Where(char.IsDigit).ToArray());

        // O número do pedido sozinho é adivinhável. Exigir o telefone junto impede que
        // alguém liste pedidos alheios variando o número — o mínimo para uma rota anônima.
        var pedido = await db.Pedidos.AsNoTracking()
            .Where(p => p.Numero == request.Numero)
            .Where(p => db.Clientes.Any(c => c.Id == p.ClienteId && c.Telefone == telefone))
            .OrderByDescending(p => p.CriadoEm)
            .Select(p => new PedidoPublicoResponse(
                p.Numero,
                p.Status.ToString(),
                p.Itens.Sum(i => i.PrecoPraticado * i.Quantidade),
                p.CriadoEm,
                p.Itens.Select(i => new ItemPublicoDto(
                        i.NomeProduto,
                        i.Quantidade,
                        i.PrecoPraticado,
                        i.PrecoPraticado * i.Quantidade))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException("Pedido não encontrado.");

        return pedido;
    }
}
