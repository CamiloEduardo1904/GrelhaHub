using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Cardapio.AlternarSituacaoDoProduto;

/// <summary>
/// O estado <b>cadastral</b> do produto — <c>RN-VEN-044</c>, <c>RN-VEN-020</c>, <c>RN-VEN-045</c>.
/// <para>
/// <b>Não confundir com <c>AlternarDisponibilidade</c>.</b> Ali é "acabou hoje", decisão de quem
/// está no salão, que volta amanhã. Aqui é "saiu do cardápio", decisão de quem cadastra, que só
/// volta se alguém reativar. O efeito no cliente é o mesmo — o produto não aparece —, e é
/// justamente isso que torna a confusão fácil.
/// </para>
/// <para>
/// <b>São duas rotas, e não uma com enum de estado.</b> Um enum obrigaria o servidor a fazer
/// <c>switch</c>, e qualquer erro de digitação no cliente viraria "mudou o outro estado" em
/// silêncio. Rota separada erra 404, que é ruído honesto.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.gerenciar")]
public sealed record AlternarSituacaoDoProdutoCommand(Guid ProdutoId, bool Ativo) : IRequest;

public sealed class AlternarSituacaoDoProdutoHandler(IAppDbContext db, IParametros parametros)
    : IRequestHandler<AlternarSituacaoDoProdutoCommand>
{
    public async Task Handle(
        AlternarSituacaoDoProdutoCommand request, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos
            .FirstOrDefaultAsync(p => p.Id == request.ProdutoId, cancellationToken)
            ?? throw new NaoEncontradoException("Produto não encontrado.");

        // RN-VEN-045 — não há exclusão. Inativar é o que existe, e `Disponivel` não é tocado.
        if (request.Ativo)
        {
            // RN-VEN-046 — reativar é o SEGUNDO caminho de entrada na categoria, e sem esta linha
            // o teto seria contornável pelo gesto mais natural que existe: inativar 50, criar
            // outros 50, reativar os primeiros. `TetoDeProdutos` é o único lugar onde a regra vive
            // (mesmo desenho de `FechoDeDependencia` para RN-GER-020).
            await TetoDeProdutos.ConferirOuFalharAsync(
                db, parametros, produto.CategoriaId, produto.Id, cancellationToken);

            produto.Reativar();
        }
        else
        {
            produto.Inativar();
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
