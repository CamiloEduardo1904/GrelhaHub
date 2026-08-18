using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;

namespace GrelhaHub.Application.Features.Gerais.Parametros;

/// <summary>
/// <c>SPEC-GER-003</c> §3 — altera o valor de um parâmetro criando uma nova vigência.
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("parametro.alterar")]
public sealed record AlterarValorDeParametroCommand(
    string Codigo,
    string NovoValor,
    DateTimeOffset VigenciaInicio,
    string Justificativa) : IRequest<ParametroResponse>;

public sealed class AlterarValorDeParametroValidator : AbstractValidator<AlterarValorDeParametroCommand>
{
    public AlterarValorDeParametroValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .Matches("^PAR-[A-Za-z]{3}-[0-9]{3}$")
            .WithMessage("O código do parâmetro segue o formato PAR-MOD-nnn.");

        RuleFor(x => x.NovoValor).NotEmpty().MaximumLength(50);

        // RN-GER-011 — operação sensível registra o motivo. Dez caracteres não impedem uma
        // justificativa ruim, mas impedem o "." que alguém digita para passar do campo.
        RuleFor(x => x.Justificativa)
            .NotEmpty()
            .MinimumLength(10).WithMessage("A justificativa precisa de ao menos 10 caracteres.")
            .MaximumLength(500);
    }
}

public sealed class AlterarValorDeParametroHandler(
    IAppDbContext db, IContextoDeExecucao contexto, IParametros parametros)
    : IRequestHandler<AlterarValorDeParametroCommand, ParametroResponse>
{
    public async Task<ParametroResponse> Handle(
        AlterarValorDeParametroCommand request, CancellationToken cancellationToken)
    {
        // RN-GER-010 — a autorizacao vive no pipeline, em [ExigePermissao("parametro.alterar")].
        // Um if aqui misturaria autorizacao com regra de negocio, e quem escrevesse o proximo
        // handler poderia esquecer a linha — que e o modo de falha que o behavior elimina.

        var codigo = request.Codigo.Trim().ToUpperInvariant();

        var parametro = await db.Parametros
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Codigo == codigo, cancellationToken)
            ?? throw new NaoEncontradoException("Parâmetro não encontrado.");

        if (!parametro.ValorEhValido(request.NovoValor))
        {
            throw new RegraDeNegocioException(
                $"O valor informado não é válido para este parâmetro: {parametro.Dominio ?? parametro.TipoValor.ToString()}.");
        }

        var corrente = await db.ValoresDeParametro
            .Where(valor => valor.ParametroId == parametro.Id && valor.VigenciaFim == null)
            .OrderByDescending(valor => valor.VigenciaInicio)
            .FirstOrDefaultAsync(cancellationToken);

        if (corrente is not null && request.VigenciaInicio <= corrente.VigenciaInicio)
        {
            // Efeito retroativo reescreveria cálculos já explicados — o oposto do que a
            // vigência existe para garantir.
            throw new RegraDeNegocioException(
                "Não é possível alterar um parâmetro com efeito retroativo.");
        }

        // Fecha a corrente e abre a nova na MESMA transação. A EXCLUDE no banco recusaria a
        // sobreposição se a ordem fosse trocada — e é o que garante que nem carga inicial nem
        // correção manual criem duas vigências ao mesmo tempo.
        corrente?.Fechar(request.VigenciaInicio);

        var novo = new ParametroValor(
            parametro.Id,
            contexto.UnidadeId,
            request.NovoValor.Trim(),
            request.VigenciaInicio,
            request.Justificativa);

        db.ValoresDeParametro.Add(novo);

        await db.SaveChangesAsync(cancellationToken);

        // A escrita é o único lugar que invalida o cache (SPEC-GER-003 §4).
        parametros.Invalidar(codigo);

        return new ParametroResponse(
            parametro.Codigo,
            parametro.Descricao,
            parametro.TipoValor.ToString(),
            parametro.Dominio,
            novo.Valor,
            novo.VigenciaInicio,
            novo.VigenciaFim);
    }
}
