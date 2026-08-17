using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Cardapio.ListarCardapioInterno;

/// <summary>Cardápio visto pela gestão — inclui produtos inativos e indisponíveis.</summary>
public sealed record ListarCardapioInternoQuery : IRequest<IReadOnlyList<CategoriaInternaDto>>;

public sealed record CategoriaInternaDto(Guid Id, string Nome, int Ordem, IReadOnlyList<ProdutoInternoDto> Produtos);

public sealed record ProdutoInternoDto(
    Guid Id, string Nome, string? Descricao, decimal Preco, bool Ativo, bool Disponivel, Guid CategoriaId);

public sealed class ListarCardapioInternoHandler(IAppDbContext db)
    : IRequestHandler<ListarCardapioInternoQuery, IReadOnlyList<CategoriaInternaDto>>
{
    public async Task<IReadOnlyList<CategoriaInternaDto>> Handle(
        ListarCardapioInternoQuery request, CancellationToken cancellationToken)
    {
        // O filtro global por unidade (RN-GER-009) já se aplica — não há WHERE de unidade aqui.
        return await db.Categorias
            .AsNoTracking()
            .Where(c => c.Ativa)
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .Select(c => new CategoriaInternaDto(
                c.Id, c.Nome, c.Ordem,
                c.Produtos
                    .OrderBy(p => p.Nome)
                    .Select(p => new ProdutoInternoDto(
                        p.Id, p.Nome, p.Descricao, p.Preco, p.Ativo, p.Disponivel, p.CategoriaId))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }
}
