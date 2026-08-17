using FluentValidation;
using MediatR;

namespace GrelhaHub.Application.Common.Behaviors;

/// <summary>
/// Executa todos os <c>IValidator</c> registrados para a requisição antes do handler.
/// <para>
/// ADR-0002 — a validação vive no pipeline, e não repetida em cada handler: assim nenhum
/// command escapa do FluentValidation por esquecimento.
/// </para>
/// <para>
/// ADR-0009 — <b>a mensagem de erro ao usuário sempre vem daqui.</b> Uma violação de
/// constraint que chega ao usuário como erro do PostgreSQL é defeito da aplicação: significa
/// que a validação de fluxo faltou. O banco é a última defesa, não a primeira.
/// </para>
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validadores)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validadores.Any())
        {
            return await next();
        }

        var contexto = new ValidationContext<TRequest>(request);

        var resultados = await Task.WhenAll(
            validadores.Select(v => v.ValidateAsync(contexto, cancellationToken)));

        var falhas = resultados
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (falhas.Count > 0)
        {
            throw new ValidationException(falhas);
        }

        return await next();
    }
}
