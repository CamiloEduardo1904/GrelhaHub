using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// Leva a trilha da outbox para o banco de log — ADR-0017.
/// <para>
/// A operação e o registro de auditoria gravam juntos, na mesma transação do banco da aplicação
/// (<see cref="OutboxDeAuditoria"/>). Este serviço faz a segunda metade: copia para o banco de log
/// e apaga da outbox.
/// </para>
/// <para>
/// ⚠ <b>Se a API cair entre as duas coisas, nada se perde</b> — a linha continua na outbox e a
/// próxima volta a encontra. É esse o ponto do padrão, e é o que permitiu separar os bancos sem
/// enfraquecer a trilha.
/// </para>
/// </summary>
public sealed partial class ServicoDeDrenagemDaOutbox(
    IServiceScopeFactory escopos,
    ILogger<ServicoDeDrenagemDaOutbox> log) : BackgroundService
{
    /// <summary>
    /// De quanto em quanto tempo a outbox é varrida.
    /// <para>
    /// Cinco segundos porque ninguém consulta a trilha em tempo real — ela é lida por auditoria e
    /// suporte, minutos ou dias depois. Intervalo menor cobraria conexão por nada.
    /// </para>
    /// </summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Quantas linhas por volta.
    /// <para>
    /// ⚠ O lote existe para o caso ruim, não para o normal: banco de log de volta depois de uma
    /// hora fora encontra milhares de linhas acumuladas, e copiar tudo numa transação só seria uma
    /// transação gigante bloqueando a outbox — que é escrita por TODA operação do sistema.
    /// </para>
    /// </summary>
    private const int TamanhoDoLote = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var relogio = new PeriodicTimer(Intervalo);

        while (await relogio.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DrenarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception excecao)
            {
                // ⚠ Nunca deixa a exceção subir: um `BackgroundService` que lança MORRE, e com ele
                // a drenagem — a outbox cresceria em silêncio até alguém notar pelo disco.
                FalhaNaDrenagem(log, excecao);
            }
        }
    }

    /// <summary>
    /// Uma volta: lê o lote, copia, apaga.
    /// <para>
    /// ⚠⚠ <b>A ORDEM É COPIAR E DEPOIS APAGAR</b>, e não o contrário. Invertida, uma falha entre as
    /// duas perderia a trilha — que é justamente o que a outbox existe para impedir. Nesta ordem, a
    /// falha possível é copiar e não apagar: a linha é reprocessada, e a cópia é idempotente porque
    /// o <c>Id</c> é o mesmo dos dois lados.
    /// </para>
    /// </summary>
    public static async Task<int> CopiarAsync(
        IAppDbContext app, ILogDb logDb, int tamanhoDoLote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(logDb);

        var pendentes = await app.OutboxDeAuditoria
            .OrderBy(o => o.EnfileiradoEm)
            .Take(tamanhoDoLote)
            .ToListAsync(cancellationToken);

        if (pendentes.Count == 0)
        {
            return 0;
        }

        var ids = pendentes.Select(o => o.Id).ToList();

        // Idempotência: o que já está lá não entra de novo. É o caso de "copiou e caiu antes de
        // apagar", que acontece de verdade num reinício no instante errado.
        var jaCopiados = await logDb.Trilha
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var novos = pendentes
            .Where(o => !jaCopiados.Contains(o.Id))
            .Select(o => o.ParaTrilha())
            .ToList();

        if (novos.Count > 0)
        {
            logDb.Trilha.AddRange(novos);
            await logDb.GravarAsync(cancellationToken);
        }

        app.OutboxDeAuditoria.RemoveRange(pendentes);
        await app.SaveChangesAsync(cancellationToken);

        return pendentes.Count;
    }

    private async Task DrenarAsync(CancellationToken cancellationToken)
    {
        using var escopo = escopos.CreateScope();

        var app = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
        var logDb = escopo.ServiceProvider.GetRequiredService<ILogDb>();

        var copiados = await CopiarAsync(app, logDb, TamanhoDoLote, cancellationToken);

        if (copiados > 0)
        {
            Drenou(log, copiados);
        }
    }

    [LoggerMessage(EventId = 1700, Level = LogLevel.Debug,
        Message = "ADR-0017 — {Quantidade} registro(s) de auditoria copiados para o banco de log.")]
    private static partial void Drenou(ILogger logger, int quantidade);

    [LoggerMessage(EventId = 1701, Level = LogLevel.Error,
        Message = "ADR-0017 — falha ao drenar a outbox de auditoria. Os registros CONTINUAM na " +
                  "outbox e serão tentados de novo; nada foi perdido. Se isto se repetir, o banco " +
                  "de log está fora do ar.")]
    private static partial void FalhaNaDrenagem(ILogger logger, Exception excecao);
}
