using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProdutoDomain = GrelhaHub.Domain.Cardapio.Produto;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Cardapio.SalvarProduto;

/// <summary>Cria ou atualiza um produto. <c>Id</c> nulo cria.</summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.gerenciar")]
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
        // RN-VEN-053 — a descrição é texto de vitrine, não catálogo. ⚠ Produto cadastrado antes
        // desta regra pode trazer mais que isso: a tela mostra quanto excedeu e trava o salvar,
        // para que o corte seja decisão de quem edita e não 422 sobre um campo que ele nem tocou.
        RuleFor(x => x.Descricao)
            .MaximumLength(ProdutoDomain.MaximoDeCaracteresNaDescricao)
            .WithMessage(
                $"A descrição deve ter no máximo {ProdutoDomain.MaximoDeCaracteresNaDescricao} caracteres.");

        // RN-VEN-032 — o preço vigente é sempre maior que zero.
        // O CHECK no banco valida o DOMÍNIO; a mensagem legível vem daqui (ADR-0009).
        RuleFor(x => x.Preco)
            .GreaterThan(0).WithMessage("O preço deve ser maior que zero.")
            .LessThan(100_000).WithMessage("Preço acima do limite aceito.");
    }
}

public sealed class SalvarProdutoHandler(
    IAppDbContext db, IContextoDeExecucao contexto, IParametros parametros)
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

        var nome = request.Nome.Trim();

        // RN-VEN-044 — nome único NA UNIDADE. `uq_produto_nome (unidade_id, nome)` existe desde a
        // migration `Fundacao` e já recusava o duplicado; sem esta conferência o usuário recebia
        // 500 com o texto de violação de índice, que pelo ADR-0009 é defeito da aplicação.
        //
        // ⚠ Não substitui o índice — há corrida entre este SELECT e o INSERT. Ele é a mensagem
        // legível; o índice é a garantia.
        //
        // Sensível a caixa, igual ao índice: as duas camadas dizem a mesma coisa sobre a mesma
        // regra. A limitação está na SPEC-VEN-003 §8.
        var duplicado = await db.Produtos
            .AsNoTracking()
            .AnyAsync(p => p.Id != request.Id && p.Nome == nome, cancellationToken);

        if (duplicado)
        {
            throw new RegraDeNegocioException("Já existe um produto com este nome.");
        }

        // RN-VEN-046 — teto de produtos ativos na categoria, `PAR-VEN-004`.
        //
        // Confere quando o produto ENTRA na categoria: na criação, e na edição que o move de uma
        // categoria para outra. Editar nome ou preço sem trocar de categoria não conta — se
        // contasse, uma categoria que já passou do teto (porque o parâmetro foi reduzido depois)
        // travaria a correção do próprio dado que precisa ser corrigido.
        var categoriaAtual = await CategoriaAtualAsync(request, cancellationToken);

        if (categoriaAtual != request.CategoriaId)
        {
            await TetoDeProdutos.ConferirOuFalharAsync(
                db, parametros, request.CategoriaId, request.Id, cancellationToken);
        }

        if (request.Id is null)
        {
            var novo = new ProdutoDomain(
                contexto.UnidadeId, request.CategoriaId, nome, request.Descricao,
                Dinheiro.De(request.Preco));

            db.Produtos.Add(novo);
            await db.SaveChangesAsync(cancellationToken);
            return novo.Id;
        }

        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NaoEncontradoException("Produto não encontrado.");

        // RN-VEN-008 — alterar o preço aqui NÃO altera pedidos já emitidos: o item guarda
        // uma cópia do preço praticado.
        produto.Atualizar(
            nome, request.Descricao, Dinheiro.De(request.Preco), request.CategoriaId);

        await db.SaveChangesAsync(cancellationToken);
        return produto.Id;
    }

    /// <summary>
    /// A categoria em que o produto está <b>hoje</b>, ou <c>null</c> quando ele está sendo criado.
    /// <para>
    /// Serve a uma pergunta só: o produto está <b>entrando</b> nesta categoria? É o que separa
    /// "criar o 51º produto" de "corrigir o preço do 30º".
    /// </para>
    /// </summary>
    private async Task<Guid?> CategoriaAtualAsync(
        SalvarProdutoCommand request, CancellationToken cancellationToken)
    {
        if (request.Id is null)
        {
            return null;
        }

        return await db.Produtos
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => (Guid?)p.CategoriaId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
