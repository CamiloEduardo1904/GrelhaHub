using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Plataforma;

/// <summary>
/// <c>RN-GER-029</c> — inativa ou reativa a unidade. <b>Ato da plataforma</b>, com justificativa.
/// <para>
/// A propriedade <c>Unidade.Ativa</c> existiu por três specs sem nenhuma operação que a mudasse, e
/// <c>Autenticar</c> já a consultava (<c>RN-GER-027</c>): o bloqueio de acesso estava pronto e era
/// <b>inalcançável</b>. Este comando é o caminho que faltava.
/// </para>
/// <para>
/// <b>Não apaga nada</b> (<c>RN-GER-002</c>): o cardápio público para de responder, os usuários
/// param de autenticar, e todo o histórico permanece. Reativar devolve o acesso sem restaurar
/// coisa alguma, porque nada foi removido.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record AlternarSituacaoDaUnidadeCommand(Guid UnidadeId, bool Ativa, string Justificativa)
    : IRequest<EstabelecimentoDto>;

public sealed class AlternarSituacaoDaUnidadeValidator
    : AbstractValidator<AlternarSituacaoDaUnidadeCommand>
{
    public AlternarSituacaoDaUnidadeValidator()
    {
        RuleFor(x => x.UnidadeId).NotEmpty();

        // Mesmo mínimo de `AlterarModuloDaUnidadeCommand`: parar a operação de um cliente é ato
        // que alguém vai precisar explicar depois.
        RuleFor(x => x.Justificativa)
            .NotEmpty()
            .MinimumLength(10).WithMessage("A justificativa precisa de ao menos 10 caracteres.")
            .MaximumLength(500);
    }
}

public sealed class AlternarSituacaoDaUnidadeHandler(
    IAppDbContext db, IContextoDeExecucao contexto, ISender sender)
    : IRequestHandler<AlternarSituacaoDaUnidadeCommand, EstabelecimentoDto>
{
    public async Task<EstabelecimentoDto> Handle(
        AlternarSituacaoDaUnidadeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var unidade = await db.Unidades
            .FirstOrDefaultAsync(u => u.Id == request.UnidadeId, cancellationToken)
            ?? throw new NaoEncontradoException("Estabelecimento não encontrado.");

        if (unidade.Ativa == request.Ativa)
        {
            throw new RegraDeNegocioException(
                request.Ativa
                    ? "Este estabelecimento já está ativo."
                    : "Este estabelecimento já está inativo.",
                "RN-GER-029");
        }

        if (request.Ativa)
        {
            unidade.Reativar();
        }
        else
        {
            unidade.Inativar();
        }

        await db.SaveChangesAsync(cancellationToken);

        // Devolve a lista atualizada da própria casa, para a tela não precisar recarregar tudo.
        var todos = await sender.Send(new ListarEstabelecimentosQuery(), cancellationToken);

        return todos.Single(e => e.Id == request.UnidadeId);
    }
}

/// <summary>
/// Altera os dados cadastrais do estabelecimento — <b>ato da plataforma</b>.
/// <para>
/// <b>O <c>slug</c> não entra</b>, e não é esquecimento: <c>RN-VEN-036</c> o torna imutável porque
/// ele circula em rede social e material impresso. O CNPJ também não: ele identifica a pessoa
/// jurídica do contrato, e trocá-lo é cadastrar outro cliente.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record AlterarEstabelecimentoCommand(
    Guid UnidadeId,
    string RazaoSocial,
    string NomeFantasia,
    string? Telefone) : IRequest<EstabelecimentoDto>;

public sealed class AlterarEstabelecimentoValidator : AbstractValidator<AlterarEstabelecimentoCommand>
{
    public AlterarEstabelecimentoValidator()
    {
        RuleFor(x => x.UnidadeId).NotEmpty();
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NomeFantasia).NotEmpty().MinimumLength(3).MaximumLength(200);
    }
}

public sealed class AlterarEstabelecimentoHandler(
    IAppDbContext db, IContextoDeExecucao contexto, ISender sender)
    : IRequestHandler<AlterarEstabelecimentoCommand, EstabelecimentoDto>
{
    public async Task<EstabelecimentoDto> Handle(
        AlterarEstabelecimentoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var unidade = await db.Unidades
            .FirstOrDefaultAsync(u => u.Id == request.UnidadeId, cancellationToken)
            ?? throw new NaoEncontradoException("Estabelecimento não encontrado.");

        unidade.Renomear(request.RazaoSocial, request.NomeFantasia, request.Telefone);

        await db.SaveChangesAsync(cancellationToken);

        var todos = await sender.Send(new ListarEstabelecimentosQuery(), cancellationToken);

        return todos.Single(e => e.Id == request.UnidadeId);
    }
}
