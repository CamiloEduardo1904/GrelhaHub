using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Cardapio.AlternarDisponibilidade;

/// <summary>
/// Liga e desliga a exibição do produto no cardápio — o "acabou hoje".
/// <para>
/// Distinto de inativar: <c>RN-VEN-020</c> trata do produto retirado do cardápio, que
/// permanece no histórico. Isto aqui é temporário e do dia a dia.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.gerenciar")]
public sealed record AlternarDisponibilidadeCommand(Guid ProdutoId, bool Disponivel) : IRequest;

public sealed class AlternarDisponibilidadeHandler(IAppDbContext db)
    : IRequestHandler<AlternarDisponibilidadeCommand>
{
    public async Task Handle(AlternarDisponibilidadeCommand request, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == request.ProdutoId, cancellationToken)
            ?? throw new NaoEncontradoException("Produto não encontrado.");

        produto.DefinirDisponibilidade(request.Disponivel);
        await db.SaveChangesAsync(cancellationToken);
    }
}
