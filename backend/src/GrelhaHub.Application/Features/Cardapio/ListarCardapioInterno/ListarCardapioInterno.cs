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

/// <summary>
/// ADR-0019 — convertido ao modelo de leitura em 18/08/2026. O preço já chega em <c>decimal</c>
/// da visão, o que dispensa a montagem em memória que existia só para desembrulhar
/// <c>Dinheiro</c>.
/// </summary>
public sealed class ListarCardapioInternoHandler(ILeituraDb leitura, IContextoDeExecucao contexto)
    : IRequestHandler<ListarCardapioInternoQuery, IReadOnlyList<CategoriaInternaDto>>
{
    public async Task<IReadOnlyList<CategoriaInternaDto>> Handle(
        ListarCardapioInternoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Recorte opcional, pelo mesmo desenho de `ListarPedidos`: a plataforma vê todas ou uma,
        // o restaurante sempre a própria (RN-GER-016). Nunca o `request.UnidadeId` cru.
        var recorte = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        // ⚠ NÃO há `.Where(c => c.Ativa)` aqui, e a ausência é a correção de um beco
        // (SPEC-VEN-003 §4): categoria inativada desaparecia desta tela e NÃO HAVIA COMO
        // REATIVÁ-LA — os produtos ficavam presos num grupo invisível, e a única saída era UPDATE
        // na mão no banco de produção.
        //
        // Vitrine da gestão ≠ vitrine do cliente: o cliente vê o que pode comprar, a gestão vê o
        // que existe. Aplicar o filtro do público na consulta interna é o erro.
        var categorias = await leitura.Categorias
            .Where(c => recorte == null || c.UnidadeId == recorte)
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);

        var produtos = await leitura.ProdutosDoCardapio
            .Where(p => recorte == null || p.UnidadeId == recorte)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);

        var porCategoria = produtos
            .GroupBy(p => p.CategoriaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return [.. categorias.Select(categoria => new CategoriaInternaDto(
            categoria.Id,
            categoria.Nome,
            categoria.Ordem,
            categoria.Ativa,
            porCategoria.TryGetValue(categoria.Id, out var lista)
                ? [.. lista.Select(p => new ProdutoInternoDto(
                    p.Id, p.Nome, p.Descricao, p.Preco, p.Ativo, p.Disponivel,
                    p.CategoriaId, p.UrlImagem))]
                : []))];
    }
}
