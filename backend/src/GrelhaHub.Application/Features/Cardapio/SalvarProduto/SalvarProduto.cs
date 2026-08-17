using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProdutoDomain = GrelhaHub.Domain.Cardapio.Produto;

namespace GrelhaHub.Application.Features.Cardapio.SalvarProduto;

/// <summary>Cria ou atualiza um produto. <c>Id</c> nulo cria.</summary>
public sealed record SalvarProdutoCommand(
    Guid? Id,
    Guid CategoriaId,
    string Nome,
    string? Descricao,
    decimal Preco) : IRequest<Guid>;

public sealed class SalvarProdutoValidator : AbstractValidator<SalvarProdutoCommand>
{
    public SalvarProdutoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CategoriaId).NotEmpty();
        RuleFor(x => x.Descricao).MaximumLength(500);

        // RN-VEN-032 — o preço vigente é sempre maior que zero.
        // O CHECK no banco valida o DOMÍNIO; a mensagem legível vem daqui (ADR-0009).
        RuleFor(x => x.Preco)
            .GreaterThan(0).WithMessage("O preço deve ser maior que zero.")
            .LessThan(100_000).WithMessage("Preço acima do limite aceito.");
    }
}

public sealed class SalvarProdutoHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<SalvarProdutoCommand, Guid>
{
    public async Task<Guid> Handle(SalvarProdutoCommand request, CancellationToken cancellationToken)
    {
        // O filtro global garante que só categorias desta unidade sejam encontradas
        // (RN-GER-009) — informar o id de outra unidade resulta em "não encontrada".
        var categoriaExiste = await db.Categorias.AnyAsync(c => c.Id == request.CategoriaId, cancellationToken);
        if (!categoriaExiste)
        {
            throw new RegraDeNegocioException("Categoria não encontrada.");
        }

        if (request.Id is null)
        {
            var novo = new ProdutoDomain(
                contexto.UnidadeId, request.CategoriaId, request.Nome, request.Descricao, request.Preco);

            db.Produtos.Add(novo);
            await db.SaveChangesAsync(cancellationToken);
            return novo.Id;
        }

        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NaoEncontradoException("Produto não encontrado.");

        // RN-VEN-008 — alterar o preço aqui NÃO altera pedidos já emitidos: o item guarda
        // uma cópia do preço praticado.
        produto.Atualizar(request.Nome, request.Descricao, request.Preco, request.CategoriaId);

        await db.SaveChangesAsync(cancellationToken);
        return produto.Id;
    }
}
