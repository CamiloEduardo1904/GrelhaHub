using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Cardapio.AlternarSituacaoDaCategoria;

/// <summary>
/// Esconde e devolve um grupo do cardápio — <c>RN-VEN-043</c>, <c>RN-VEN-045</c>.
/// <para>
/// <b>Inativar a categoria não toca nos produtos.</b> Eles continuam com <c>ativo = true</c> e
/// somem do cardápio público porque a categoria sumiu. Reativar devolve o grupo inteiro
/// exatamente como estava — é o que faz "esconder as sobremesas no inverno" ser um clique.
/// </para>
/// <para>
/// A alternativa, inativar em cascata, é irreversível: ao reativar, o sistema não saberia qual
/// produto estava inativo <i>antes</i> por decisão de alguém. A informação se perde na primeira
/// operação.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.gerenciar")]
public sealed record AlternarSituacaoDaCategoriaCommand(Guid CategoriaId, bool Ativa) : IRequest;

public sealed class AlternarSituacaoDaCategoriaHandler(IAppDbContext db)
    : IRequestHandler<AlternarSituacaoDaCategoriaCommand>
{
    public async Task Handle(
        AlternarSituacaoDaCategoriaCommand request, CancellationToken cancellationToken)
    {
        // Sem `Include(c => c.Produtos)`, e a ausência é deliberada: carregar os produtos aqui não
        // serve a nada além de tentar quem vier depois a percorrê-los.
        var categoria = await db.Categorias
            .FirstOrDefaultAsync(c => c.Id == request.CategoriaId, cancellationToken)
            ?? throw new NaoEncontradoException("Categoria não encontrada.");

        if (request.Ativa)
        {
            categoria.Reativar();
        }
        else
        {
            categoria.Inativar();
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
