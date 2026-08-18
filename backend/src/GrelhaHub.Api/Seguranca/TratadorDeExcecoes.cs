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

        if (exception is LoginRecusadoException login)
        {
            // RN-GER-027 — o motivo real vai para o LOG, com o mesmo código que o cliente lê. É
            // como o suporte responde sem que a resposta HTTP conte nada a quem sonda.
            LoginRecusado(logger, login.Codigo, login.Motivo);
        }

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

        TrocaDeSenhaPendenteException => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Troca de senha pendente",
            Detail = "Troque a sua senha antes de continuar.",
            Extensions = { ["regra"] = "RF-SEG-005", ["trocarSenha"] = true },
        },

        SessaoDeSuporteExigidaException sessao => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Sessão de suporte exigida",
            Detail = sessao.Message,
            Extensions = { ["regra"] = "RN-GER-015" },
        },

        SemPermissaoException permissao => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Sem permissão",
            Detail = permissao.Message,

            // A mensagem NÃO diz qual permissão falta: informar isso ajuda quem está mapeando o
            // sistema de fora. O código vai para o log, onde o suporte o encontra.
            Extensions = { ["regra"] = "RN-GER-010" },
        },

        ExclusivoDaPlataformaException plataforma => new ProblemDetails
        {
            // 403 e NÃO 401: quem chegou aqui está autenticado. Um 401 faria a interface pedir
            // login de novo, e login não é o que falta (RN-GER-014, ADR-0013).
            Status = StatusCodes.Status403Forbidden,
            Title = "Exclusivo da plataforma",
            Detail = plataforma.Message,
            Extensions = { ["regra"] = "RN-GER-014" },
        },

        DelegacaoNegadaException delegacao => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Perfil fora da sua delegação",
            Detail = delegacao.Message,

            // Aqui o perfil VAI na resposta, ao contrário do código de permissão acima: o perfil
            // já está visível na tela de quem cria o usuário, e omiti-lo transformaria a recusa
            // em adivinhação (RN-GER-024).
            Extensions = { ["regra"] = "RN-GER-024", ["perfil"] = delegacao.PerfilAlvo },
        },

        ModuloNaoHabilitadoException modulo => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Módulo não habilitado",
            Detail = modulo.Message,
            // 403 e não 404: o restaurante existe e a rota existe — o que falta é contrato.
            // Quem vê isto é o próprio cliente, e a mensagem tem de levá-lo ao comercial, não
            // ao suporte técnico (RN-GER-017, ADR-0014).
            Extensions = { ["regra"] = "RN-GER-017", ["modulo"] = modulo.Sigla },
        },

        LoginRecusadoException login => new ProblemDetails
        {
            // 401: aqui a autenticação REALMENTE falhou. O código vai na extensão para o suporte
            // e para o log; a mensagem é a mesma nos três motivos (RN-GER-027).
            Status = StatusCodes.Status401Unauthorized,
            Title = "Login recusado",
            Detail = login.Message,
            // SÓ o código. O motivo fica no log: pôr `motivo` aqui reintroduziria exatamente a
            // distinção que RN-GER-027 recusa.
            Extensions = { ["regra"] = "RN-GER-027", ["codigo"] = login.Codigo },
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

    [LoggerMessage(
        EventId = 4010,
        Level = LogLevel.Warning,
        Message = "RN-GER-027 — login recusado. Código {Codigo}, motivo {Motivo}.")]
    private static partial void LoginRecusado(ILogger logger, string codigo, string motivo);

    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning, Message = "{Titulo}: {Detalhe}")]
    private static partial void OperacaoRecusada(ILogger logger, string? titulo, string? detalhe);
}
