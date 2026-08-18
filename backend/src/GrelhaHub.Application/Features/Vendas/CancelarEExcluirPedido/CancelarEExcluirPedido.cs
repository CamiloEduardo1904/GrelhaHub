using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.CancelarEExcluirPedido;

/// <summary>
/// Cancela o pedido — <c>RN-GER-002</c>, <c>RN-VEN-019</c>.
/// <para>
/// <b>Comando próprio, separado de avançar</b>, porque exige permissão diferente:
/// <c>pedido.cancelar</c>. Um único comando com um <c>enum</c> de ação não conseguiria declarar
/// duas permissões, e a verificação teria de voltar para dentro do handler — que é o que
/// <c>RN-GER-010</c> manda evitar.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("pedido.cancelar")]
public sealed record CancelarPedidoCommand(Guid PedidoId, string Motivo) : IRequest<string>;

public sealed class CancelarPedidoValidator : AbstractValidator<CancelarPedidoCommand>
{
    public CancelarPedidoValidator()
    {
        RuleFor(x => x.PedidoId).NotEmpty();

        // RN-GER-002 — o desfazimento é registrado com motivo e responsável.
        RuleFor(x => x.Motivo)
            .NotEmpty()
            .MinimumLength(3)
            .WithMessage("Informe o motivo do cancelamento.");
    }
}

public sealed class CancelarPedidoHandler(IAppDbContext db)
    : IRequestHandler<CancelarPedidoCommand, string>
{
    public async Task<string> Handle(CancelarPedidoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await db.Pedidos
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == request.PedidoId, cancellationToken)
            ?? throw new NaoEncontradoException("Pedido não encontrado.");

        try
        {
            pedido.Cancelar(request.Motivo);
        }
        catch (InvalidOperationException excecao)
        {
            // ADR-0009 — a invariante é do domínio, a mensagem ao usuário sai da aplicação.
            throw new RegraDeNegocioException(excecao.Message);
        }

        await db.SaveChangesAsync(cancellationToken);

        return pedido.Status.ToString();
    }
}

/// <summary>
/// <c>RN-VEN-041</c> — exclusão <b>lógica</b> do pedido, pelo administrador da unidade.
/// <para>
/// <b>Não é cancelamento.</b> Cancelar é fato de negócio — o cliente desistiu, e isso é métrica.
/// Excluir é reconhecer que o pedido nunca deveria ter existido: duplicidade, teste, erro de
/// operação. Por isso ele sai da apuração em vez de entrar na taxa de cancelamento.
/// </para>
/// <para>
/// <c>RN-GER-002</c> — nada é apagado. O registro permanece, marcado, com quem excluiu, quando e
/// por quê, e desaparece de toda consulta pelo filtro global.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("pedido.excluir")]
public sealed record ExcluirPedidoCommand(Guid PedidoId, string Motivo) : IRequest;

public sealed class ExcluirPedidoValidator : AbstractValidator<ExcluirPedidoCommand>
{
    public ExcluirPedidoValidator()
    {
        RuleFor(x => x.PedidoId).NotEmpty();

        // Dez caracteres, e não três como no cancelamento: excluir tira o pedido da apuração, e
        // quem audita depois precisa entender o motivo sem perguntar a ninguém.
        RuleFor(x => x.Motivo)
            .NotEmpty()
            .MinimumLength(10)
            .WithMessage("A exclusão exige um motivo de ao menos 10 caracteres.")
            .MaximumLength(500);
    }
}

public sealed class ExcluirPedidoHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ExcluirPedidoCommand>
{
    public async Task Handle(ExcluirPedidoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await db.Pedidos
            .FirstOrDefaultAsync(p => p.Id == request.PedidoId, cancellationToken)
            ?? throw new NaoEncontradoException("Pedido não encontrado.");

        if (contexto.UsuarioId is not { } usuarioId)
        {
            // RN-VEN-041 exige registrar QUEM excluiu. Sem usuário identificado não há exclusão
            // possível — e é por isso que a rota é autenticada.
            throw new NaoAutorizadoException("Sessão inválida.");
        }

        try
        {
            pedido.ExcluirLogicamente(usuarioId, request.Motivo);
        }
        catch (InvalidOperationException excecao)
        {
            throw new RegraDeNegocioException(excecao.Message, "RN-VEN-041");
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
