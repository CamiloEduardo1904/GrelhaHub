using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Vendas.ConsultarPedidoPublico;

/// <summary>Acompanhamento do pedido pelo cliente, sem autenticação.</summary>
[SemModulo("pertence a VEN, mas o gate vive no handler: a unidade só é conhecida depois de resolver o slug, e RN-VEN-037 exige 404 e não 403 para quem está fora")]
[SemPermissao("rota pública anônima: o par número + telefone é o que autoriza a leitura")]
public sealed record ConsultarPedidoPublicoQuery(string Slug, int Numero, string Telefone)
    : IRequest<PedidoPublicoResponse>;

public sealed record PedidoPublicoResponse(
    int Numero,
    string Status,
    decimal Total,
    DateTimeOffset CriadoEm,
    IReadOnlyList<ItemPublicoDto> Itens);

public sealed record ItemPublicoDto(string Produto, int Quantidade, decimal PrecoPraticado, decimal Total);

public sealed class ConsultarPedidoPublicoHandler(IAppDbContext db, IContextoDeExecucao contexto, IModulos modulos)
    : IRequestHandler<ConsultarPedidoPublicoQuery, PedidoPublicoResponse>
{
    public async Task<PedidoPublicoResponse> Handle(ConsultarPedidoPublicoQuery request, CancellationToken cancellationToken)
    {
        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();

        var unidade = await db.Unidades.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == slug && u.Ativa, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        contexto.DefinirUnidadePublica(unidade.Id);
        // RN-GER-017 / SPEC-GER-006 CA-7 — gate de módulo AQUI, e não no pipeline.
        //
        // O behavior roda antes do handler, quando a unidade ainda não está no contexto: ela só
        // é conhecida depois de resolver o slug. E a resposta certa é 404, não 403: para quem
        // está do lado de fora, o restaurante simplesmente não tem cardápio online — 403
        // anunciaria que existe algo ali que não foi contratado.
        if (!await modulos.EstaHabilitadoAsync(unidade.Id, CodigoDeModulo.Vendas, em: null, cancellationToken))
        {
            throw new NaoEncontradoException("Restaurante não encontrado.");
        }


        var telefone = new string((request.Telefone ?? string.Empty).Where(char.IsDigit).ToArray());

        // O número do pedido sozinho é adivinhável. Exigir o telefone junto impede que
        // alguém liste pedidos alheios variando o número — o mínimo para uma rota anônima.
        // Carrega o agregado: o total vem de `Pedido.Total`, que é a única definição de
        // RN-VEN-039. Projetar um `Sum` aqui duplicaria a regra na consulta.
        var pedido = await db.Pedidos.AsNoTracking()
            .Include(p => p.Itens)
            .Where(p => p.Numero == request.Numero)
            .Where(p => db.Clientes.Any(c => c.Id == p.ClienteId && c.Telefone == telefone))
            .OrderByDescending(p => p.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException("Pedido não encontrado.");

        return new PedidoPublicoResponse(
            pedido.Numero,
            pedido.Status.ToString(),
            pedido.Total.Valor,
            pedido.CriadoEm,
            pedido.Itens
                .Select(item => new ItemPublicoDto(
                    item.NomeProduto,
                    item.Quantidade,
                    item.PrecoPraticado.Valor,
                    item.Total.Valor))
                .ToList());
    }
}
