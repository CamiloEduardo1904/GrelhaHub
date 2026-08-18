using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;

namespace GrelhaHub.Application.Common.Behaviors;

/// <summary>
/// <c>RF-SEG-005</c> — usuário com senha pendente de troca só executa a troca.
/// <para>
/// <b>Senha inicial escolhida por outra pessoa não é senha.</b> Quem criou o usuário a conhece, e
/// enquanto ela vale o dono da conta não é o único que pode entrar. Bloquear no pipeline, e não em
/// cada rota, é o que garante que a rota nova também esteja coberta.
/// </para>
/// </summary>
public sealed class TrocaDeSenhaBehavior<TRequest, TResponse>(IContextoDeExecucao contexto)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// A única operação que passa com senha pendente.
    /// <para>
    /// Comparado por <b>nome do tipo</b> e não por interface marcadora: uma interface seria mais
    /// bonita e teria o mesmo problema — alguém pode implementá-la em outro comando. O nome único
    /// é o que mantém a exceção em um só lugar, visível neste arquivo.
    /// </para>
    /// </summary>
    public const string OperacaoLiberada = "TrocarSenhaCommand";

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (!contexto.DeveTrocarSenha || typeof(TRequest).Name == OperacaoLiberada)
        {
            return await next();
        }

        throw new TrocaDeSenhaPendenteException();
    }
}
