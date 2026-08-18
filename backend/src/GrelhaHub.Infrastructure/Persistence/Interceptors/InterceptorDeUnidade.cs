using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrelhaHub.Infrastructure.Persistence.Interceptors;

/// <summary>
/// <c>RN-GER-009</c> / <c>SPEC-GER-001</c> §4 — preenche <c>unidade_id</c> em toda entidade
/// <c>Added</c> que implemente <see cref="IPertenceAUnidade"/>.
/// <para>
/// <b>Por que interceptor e não uma linha em cada handler:</b> o que depende de o
/// desenvolvedor lembrar é o que falta justamente quando importa. E o modo de falha é o pior
/// possível — não dá erro. Um <c>unidade_id</c> esquecido grava <see cref="Guid.Empty"/>, o
/// filtro global deixa de encontrar o registro, e a operação parece ter falhado quando na
/// verdade gravou um dado órfão que ninguém mais vê.
/// </para>
/// <para>
/// <b>Não substitui o valor já atribuído.</b> Há fluxo em que a unidade é conhecida e o
/// contexto ainda não a tem: o cadastro de restaurante roda anônimo e cria usuário, categoria
/// e produto da unidade que acabou de nascer. Ali o handler informa a unidade, e o interceptor
/// não tem o que corrigir.
/// </para>
/// </summary>
public sealed class InterceptorDeUnidade(IContextoDeExecucao contexto) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        PreencherUnidade(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        PreencherUnidade(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void PreencherUnidade(DbContext? contextoDeDados)
    {
        if (contextoDeDados is null)
        {
            return;
        }

        foreach (var entrada in contextoDeDados.ChangeTracker.Entries<IPertenceAUnidade>())
        {
            if (entrada.State != EntityState.Added || entrada.Entity.UnidadeId != Guid.Empty)
            {
                continue;
            }

            if (contexto.UnidadeId == Guid.Empty)
            {
                // Falha ruidosa: gravar Guid.Empty produziria um registro invisível ao filtro
                // global — dado órfão que não aparece em consulta, contagem nem relatório.
                // Melhor recusar a gravação do que criar o fantasma.
                throw new InvalidOperationException(
                    $"RN-GER-009 — {entrada.Entity.GetType().Name} está sendo gravado sem unidade, " +
                    "e não há unidade no contexto de execução para atribuir.");
            }

            // Escrito pela entrada do EF, e não pela propriedade: o setter é privado de
            // propósito — a unidade não é campo que qualquer código altera.
            entrada.Property(nameof(IPertenceAUnidade.UnidadeId)).CurrentValue = contexto.UnidadeId;
        }
    }
}
