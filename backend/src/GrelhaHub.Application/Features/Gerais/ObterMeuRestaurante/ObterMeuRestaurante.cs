using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.ObterMeuRestaurante;

public sealed record ObterMeuRestauranteQuery : IRequest<MeuRestauranteResponse>;

public sealed record MeuRestauranteResponse(
    Guid Id,
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string? Telefone,
    string Slug,
    string LinkDoCardapio,
    int TotalDeProdutos,
    int PedidosDoDia);

public sealed class ObterMeuRestauranteHandler(
    IAppDbContext db, IContextoDeExecucao contexto, IRelogio relogio)
    : IRequestHandler<ObterMeuRestauranteQuery, MeuRestauranteResponse>
{
    public async Task<MeuRestauranteResponse> Handle(ObterMeuRestauranteQuery request, CancellationToken cancellationToken)
    {
        var unidade = await db.Unidades
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == contexto.UnidadeId, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        var totalProdutos = await db.Produtos.CountAsync(p => p.Ativo, cancellationToken);

        // RN-GER-006 ⚠⚠ — a contagem do dia usa data_operacional, NUNCA a data de criação.
        var hoje = relogio.DiaOperacionalDeHoje();
        var pedidosDoDia = await db.Pedidos.CountAsync(p => p.DataOperacional == hoje, cancellationToken);

        return new MeuRestauranteResponse(
            unidade.Id, unidade.RazaoSocial, unidade.NomeFantasia, unidade.Cnpj,
            unidade.Telefone, unidade.Slug, $"/c/{unidade.Slug}", totalProdutos, pedidosDoDia);
    }
}
