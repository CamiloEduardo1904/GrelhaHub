using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GrelhaHub.Api.Seguranca;

/// <summary>
/// Converte exceção em resposta HTTP legível.
/// <para>
/// ADR-0009 — <b>a mensagem de erro ao usuário vem sempre da aplicação</b>. Uma violação de
/// constraint que chega ao usuário como erro do PostgreSQL é defeito da aplicação: significa
/// que a validação de fluxo faltou. Por isso o caso não tratado devolve mensagem genérica e
/// registra o detalhe no log, em vez de expor o texto do banco.
/// </para>
/// </summary>
public sealed partial class TratadorDeExcecoes(ILogger<TratadorDeExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problema = Traduzir(exception);

        if (problema.Status >= StatusCodes.Status500InternalServerError)
        {
            FalhaNaoTratada(logger, httpContext.Request.Path, exception);
        }
        else
        {
            OperacaoRecusada(logger, problema.Title, problema.Detail);
        }

        httpContext.Response.StatusCode =
            problema.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problema, cancellationToken);

        return true;
    }

    private static ProblemDetails Traduzir(Exception excecao) => excecao switch
    {
        ValidationException validacao => new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Dados inválidos",
            Detail = string.Join(" ", validacao.Errors.Select(e => e.ErrorMessage).Distinct()),
        },

        RegraDeNegocioException regra => new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Regra de negócio",
            Detail = regra.Message,
            // O ID da regra volta ao cliente: é o que liga o erro visto na tela ao catálogo,
            // e torna o suporte rastreável até a spec.
            Extensions = { ["regra"] = regra.Regra },
        },

        NaoEncontradoException naoEncontrado => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Não encontrado",
            Detail = naoEncontrado.Message,
        },

        NaoAutorizadoException naoAutorizado => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Não autorizado",
            Detail = naoAutorizado.Message,
        },

        ArgumentException argumento => new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Dados inválidos",
            Detail = argumento.Message,
        },

        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erro interno",
            Detail = "Não foi possível concluir a operação. Tente novamente.",
        },
    };

    [LoggerMessage(EventId = 5000, Level = LogLevel.Error, Message = "Falha não tratada em {Rota}")]
    private static partial void FalhaNaoTratada(ILogger logger, string rota, Exception excecao);

    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning, Message = "{Titulo}: {Detalhe}")]
    private static partial void OperacaoRecusada(ILogger logger, string? titulo, string? detalhe);
}
