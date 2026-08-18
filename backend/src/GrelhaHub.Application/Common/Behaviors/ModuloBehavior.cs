using System.Reflection;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;

namespace GrelhaHub.Application.Common.Behaviors;

/// <summary>
/// <c>RN-GER-017</c> — recusa a operação se o módulo não estiver habilitado na unidade,
/// <b>antes</b> de o handler executar. ADR-0014.
/// <para>
/// <b>Módulo não contratado não é questão de permissão.</b> Um usuário com todas as permissões
/// de estoque continua sem operar estoque numa unidade que não contratou EST — são duas
/// perguntas diferentes, e esta vem primeiro.
/// </para>
/// </summary>
public sealed class ModuloBehavior<TRequest, TResponse>(IModulos modulos, IContextoDeExecucao contexto)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var declaracao = typeof(TRequest).GetCustomAttribute<ExigeModuloAttribute>();

        // Sem [ExigeModulo] só chega aqui quem tem [SemModulo] — a partida recusa o resto
        // (ValidadorDeDeclaracaoDeModulo). Não há caminho para operação sem declaração.
        if (declaracao is null)
        {
            return await next();
        }

        // RN-GER-014 — o usuário de plataforma opera acima do contrato comercial: é ele quem
        // habilita e desabilita módulo, e precisa diagnosticar módulo que o cliente desligou.
        if (contexto.EhPlataforma)
        {
            return await next();
        }

        var unidade = contexto.UnidadeId;

        if (unidade == Guid.Empty)
        {
            // Sem unidade no escopo não há contrato a consultar. Falha fechada: o contrário
            // deixaria toda rota anônima passar por cima do gate.
            throw new ModuloNaoHabilitadoException(Modulo.Sigla(declaracao.Modulo));
        }

        var habilitado = await modulos.EstaHabilitadoAsync(
            unidade, declaracao.Modulo, em: null, cancellationToken);

        if (!habilitado)
        {
            throw new ModuloNaoHabilitadoException(Modulo.Sigla(declaracao.Modulo));
        }

        return await next();
    }
}
