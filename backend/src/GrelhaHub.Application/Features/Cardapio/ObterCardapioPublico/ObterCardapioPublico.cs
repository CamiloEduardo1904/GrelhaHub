using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Cardapio.ObterCardapioPublico;

/// <summary>
/// Cardápio público do restaurante — <c>RN-VEN-036</c>, <c>RN-VEN-037</c>.
/// Acessível <b>sem autenticação</b>.
/// </summary>
public sealed record ObterCardapioPublicoQuery(string Slug) : IRequest<CardapioPublicoResponse>;

public sealed record CardapioPublicoResponse(
    string Slug,
    string NomeFantasia,
    string? Telefone,
    IReadOnlyList<CategoriaPublicaDto> Categorias);

public sealed record CategoriaPublicaDto(Guid Id, string Nome, IReadOnlyList<ProdutoPublicoDto> Produtos);

/// <summary>
/// <c>RN-GER-012</c> — <b>não existe campo de custo, CMV ou margem neste DTO.</b>
/// A restrição é de dado, não de tela: o cardápio público é onde vazar custo seria mais
/// grave, porque qualquer pessoa o acessa. Devolver e esconder no front continuaria
/// visível em qualquer inspeção de rede.
/// </summary>
public sealed record ProdutoPublicoDto(Guid Id, string Nome, string? Descricao, decimal Preco);

public sealed class ObterCardapioPublicoHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ObterCardapioPublicoQuery, CardapioPublicoResponse>
{
    public async Task<CardapioPublicoResponse> Handle(ObterCardapioPublicoQuery request, CancellationToken cancellationToken)
    {
        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();

        // `unidade` não é filtrada por unidade_id — ela É a unidade.
        var unidade = await db.Unidades
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == slug && u.Ativa, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        // Fixa o escopo: a partir daqui o filtro global do EF Core garante que nada de outra
        // unidade apareça (RN-VEN-037). Sem isto, cada consulta pública precisaria filtrar a
        // unidade a mão — e o esquecimento vazaria o cardápio de outro restaurante.
        contexto.DefinirUnidadePublica(unidade.Id);

        var categorias = await db.Categorias
            .AsNoTracking()
            .Where(c => c.Ativa && c.Produtos.Any(p => p.Ativo && p.Disponivel))
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .Select(c => new CategoriaPublicaDto(
                c.Id, c.Nome,
                c.Produtos
                    // RN-VEN-020 — produto inativo não aparece no cardápio.
                    .Where(p => p.Ativo && p.Disponivel)
                    .OrderBy(p => p.Nome)
                    .Select(p => new ProdutoPublicoDto(p.Id, p.Nome, p.Descricao, p.Preco))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new CardapioPublicoResponse(unidade.Slug, unidade.NomeFantasia, unidade.Telefone, categorias);
    }
}
