using System.Reflection;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using MediatR;

namespace GrelhaHub.Application.Common.Behaviors;

/// <summary>
/// <c>RN-GER-010</c> — recusa a operação se o usuário não tiver a permissão exigida,
/// <b>antes</b> de o handler executar. <c>SPEC-GER-004</c> CA-2.
/// <para>
/// O handler não é chamado: nada é lido, nada é gravado. É a diferença entre negar a operação e
/// executá-la e descartar o resultado.
/// </para>
/// </summary>
public sealed class PermissaoBehavior<TRequest, TResponse>(IContextoDeExecucao contexto)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var exigida = typeof(TRequest).GetCustomAttribute<ExigePermissaoAttribute>();

        // Sem [ExigePermissao] só chega aqui quem tem [SemPermissao] — a partida recusa o resto.
        if (exigida is null)
        {
            return await next();
        }

        // RN-GER-014 — o usuário de plataforma não tem perfil de unidade, e portanto não tem
        // permissão de unidade. Ele opera acima delas: é quem dá suporte ao restaurante.
        if (contexto.EhPlataforma)
        {
            return await next();
        }

        if (!contexto.Pode(exigida.Codigo))
        {
            throw new SemPermissaoException(exigida.Codigo);
        }

        return await next();
    }
}
