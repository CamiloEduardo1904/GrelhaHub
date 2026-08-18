using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Vendas.AlterarStatusPedido;

/// <summary>
/// As duas acoes que avancam o pedido no ciclo.
/// <para>
/// <b>Cancelar saiu daqui</b> e virou <c>CancelarPedidoCommand</c>: exige a permissao
/// <c>pedido.cancelar</c>, e um comando com enum de acao nao consegue declarar duas permissoes
/// diferentes — a verificacao teria de voltar para dentro do handler, que e o que RN-GER-010
/// manda evitar.
/// </para>
/// </summary>
public enum AcaoDePedido
{
    Confirmar = 1,
    Avancar = 2,
}

/// <summary>
/// Move o pedido no ciclo de vida — <c>RN-VEN-018</c>, <c>RN-VEN-019</c>.
/// <para>
/// A transição em si é decidida no <b>domínio</b> (a entidade <c>Pedido</c>), não aqui: o
/// handler orquestra, a entidade guarda a invariante (ADR-0009).
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("pedido.avancar")]
public sealed record AlterarStatusPedidoCommand(Guid PedidoId, AcaoDePedido Acao)
    : IRequest<string>;

public sealed class AlterarStatusPedidoValidator : AbstractValidator<AlterarStatusPedidoCommand>
{
    public AlterarStatusPedidoValidator()
    {
        RuleFor(x => x.PedidoId).NotEmpty();

        // O motivo saiu junto com o cancelar: avancar no ciclo nao pede justificativa. A regra do
        // motivo obrigatorio vive em CancelarPedidoValidator, onde ela se aplica.
    }
}

public sealed class AlterarStatusPedidoHandler(IAppDbContext db)
    : IRequestHandler<AlterarStatusPedidoCommand, string>
{
    public async Task<string> Handle(AlterarStatusPedidoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await db.Pedidos
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == request.PedidoId, cancellationToken)
            ?? throw new NaoEncontradoException("Pedido não encontrado.");

        try
        {
            switch (request.Acao)
            {
                case AcaoDePedido.Confirmar:
                    pedido.Confirmar();
                    break;

                case AcaoDePedido.Avancar:
                    pedido.AvancarStatus();
                    break;

                default:
                    throw new RegraDeNegocioException("Ação desconhecida.");
            }
        }
        catch (InvalidOperationException ex)
        {
            // ADR-0009 — a invariante é do domínio, mas a mensagem ao usuário sai da
            // aplicação. Deixar a exceção do domínio subir crua entregaria stack trace.
            throw new RegraDeNegocioException(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return pedido.Status.ToString();
    }
}
