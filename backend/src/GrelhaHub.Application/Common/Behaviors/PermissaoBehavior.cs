using System.Reflection;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace GrelhaHub.Application.Common.Behaviors;

/// <summary>
/// <c>RN-GER-010</c> — recusa a operação se o usuário não tiver a permissão exigida,
/// <b>antes</b> de o handler executar. <c>SPEC-GER-004</c> CA-2.
/// <para>
/// O handler não é chamado: nada é lido, nada é gravado. É a diferença entre negar a operação e
/// executá-la e descartar o resultado.
/// </para>
/// <para>
/// ADR-0022 §3 — quem <b>executa</b> a decisão é o <see cref="IAuthorizationService"/> do
/// ASP.NET, contra uma política registrada por permissão na partida (a partir do
/// <c>CatalogoDeAcesso</c>). Quem a <b>declara</b> continua sendo o caso de uso, no
/// <c>[ExigePermissao]</c> do request — e é por isso que o atributo NÃO migrou para
/// <c>[Authorize(Policy = ...)]</c> no controller: controller se esquece, request não, porque a
/// partida recusa operação sem declaração (<c>ValidadorDeDeclaracaoDeModulo</c>).
/// </para>
/// </summary>
public sealed class PermissaoBehavior<TRequest, TResponse>(
    IContextoDeExecucao contexto,
    IAuthorizationService autorizacao)
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

        // Sem principal não há de onde ler claim: rota pública tentando operação protegida.
        // Falha fechado, com a mesma recusa de quem não tem a permissão.
        if (contexto.Principal is not { } principal)
        {
            throw new SemPermissaoException(exigida.Codigo);
        }

        // A política tem o MESMO nome do código da permissão, e exige a claim `permissao` com
        // esse valor — claims que vieram do JWT, emitidas no login a partir do papel do usuário.
        var resultado = await autorizacao.AuthorizeAsync(principal, exigida.Codigo);

        if (!resultado.Succeeded)
        {
            throw new SemPermissaoException(exigida.Codigo);
        }

        return await next();
    }
}
