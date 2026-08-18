using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Cardapio.ListarCardapioInterno;

/// <summary>Cardápio visto pela gestão — inclui produtos inativos e indisponíveis.</summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.visualizar")]
public sealed record ListarCardapioInternoQuery(Guid? UnidadeId = null)
    : IRequest<IReadOnlyList<CategoriaInternaDto>>;

public sealed record CategoriaInternaDto(
    Guid Id,
    string Nome,
    int Ordem,

    /// <summary>
    /// <c>RN-VEN-043</c> — <b>é este campo que torna reativar possível.</b> A consulta traz
    /// categoria inativa; a tela a mostra apagada, com botão de reativar.
    /// </summary>
    bool Ativa,

    IReadOnlyList<ProdutoInternoDto> Produtos);

public sealed record ProdutoInternoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    decimal Preco,
    bool Ativo,
    bool Disponivel,
    Guid CategoriaId,

    /// <summary><c>RN-VEN-042</c> — para a gestão saber quais produtos já têm foto.</summary>
    string? UrlImagem);

public sealed class ListarCardapioInternoHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ListarCardapioInternoQuery, IReadOnlyList<CategoriaInternaDto>>
{
    public async Task<IReadOnlyList<CategoriaInternaDto>> Handle(
        ListarCardapioInternoQuery request, CancellationToken cancellationToken)
    {
        // O filtro global por unidade (RN-GER-009) já se aplica — não há WHERE de unidade aqui.
        //
        // Filtro e ordenação continuam no SQL; só a conversão de Dinheiro para o decimal do
        // contrato acontece em memória. `Preco` é propriedade com conversor de valor
        // (SPEC-GER-001 §4), e o EF Core não traduz acesso a membro dela — `p.Preco.Valor`
        // dentro da árvore de expressão não vira SQL.
        // Recorte opcional, pelo mesmo desenho de `ListarPedidos`: a plataforma vê todas ou uma,
        // o restaurante sempre a própria (RN-GER-016).
        var recorte = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        var categorias = await db.Categorias
            .AsNoTracking()
            .Where(c => recorte == null || c.UnidadeId == recorte)
            // ⚠ Havia aqui um `.Where(c => c.Ativa)`, e ele era um beco (SPEC-VEN-003 §4):
            // categoria inativada desaparecia desta tela e NÃO HAVIA COMO REATIVÁ-LA — os
            // produtos ficavam presos num grupo invisível, e a única saída era UPDATE na mão no
            // banco de produção.
            //
            // Vitrine da gestão ≠ vitrine do cliente: o cliente vê o que pode comprar, a gestão
            // vê o que existe. Aplicar o filtro do público na consulta interna é o erro.
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .Select(c => new
            {
                c.Id,
                c.Nome,
                c.Ordem,
                c.Ativa,
                Produtos = c.Produtos
                    .OrderBy(p => p.Nome)
                    .Select(p => new
                    {
                        p.Id,
                        p.Nome,
                        p.Descricao,
                        p.Preco,
                        p.Ativo,
                        p.Disponivel,
                        p.CategoriaId,
                        p.UrlImagem,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return categorias
            .Select(categoria => new CategoriaInternaDto(
                categoria.Id,
                categoria.Nome,
                categoria.Ordem,
                categoria.Ativa,
                categoria.Produtos
                    .Select(produto => new ProdutoInternoDto(
                        produto.Id,
                        produto.Nome,
                        produto.Descricao,
                        produto.Preco.Valor,
                        produto.Ativo,
                        produto.Disponivel,
                        produto.CategoriaId,
                        produto.UrlImagem))
                    .ToList()))
            .ToList();
    }
}
