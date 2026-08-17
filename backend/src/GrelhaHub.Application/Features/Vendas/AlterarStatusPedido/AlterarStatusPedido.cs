using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.AlterarStatusPedido;

public enum AcaoDePedido
{
    Confirmar = 1,
    Avancar = 2,
    Cancelar = 3,
}

/// <summary>
/// Move o pedido no ciclo de vida — <c>RN-VEN-018</c>, <c>RN-VEN-019</c>.
/// <para>
/// A transição em si é decidida no <b>domínio</b> (a entidade <c>Pedido</c>), não aqui: o
/// handler orquestra, a entidade guarda a invariante (ADR-0009).
/// </para>
/// </summary>
public sealed record AlterarStatusPedidoCommand(Guid PedidoId, AcaoDePedido Acao, string? Motivo)
    : IRequest<string>;

public sealed class AlterarStatusPedidoValidator : AbstractValidator<AlterarStatusPedidoCommand>
{
    public AlterarStatusPedidoValidator()
    {
        RuleFor(x => x.PedidoId).NotEmpty();

        // RN-VEN-014 — o cancelamento exige motivo.
        RuleFor(x => x.Motivo)
            .NotEmpty()
            .MinimumLength(3)
            .When(x => x.Acao == AcaoDePedido.Cancelar)
            .WithMessage("Informe o motivo do cancelamento.");
    }
}

public sealed class AlterarStatusPedidoHandler(IAppDbContext db, IContextoDeExecucao contexto)
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

                case AcaoDePedido.Cancelar:
                    // RN-VEN-014 — cancelamento é operação de gerência.
                    if (!contexto.EhGerencia)
                    {
                        throw new NaoAutorizadoException("Apenas a gerência pode cancelar um pedido.");
                    }

                    pedido.Cancelar(request.Motivo!);
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
