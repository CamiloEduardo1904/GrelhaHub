using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// O banco de <b>log</b> — ADR-0017: trilha de auditoria e exceções.
/// <para>
/// Separado de <see cref="IAppDbContext"/> porque é outro banco, com outra retenção e outro motivo
/// para crescer. A consulta da trilha lê daqui; a gravação chega pela outbox.
/// </para>
/// <para>
/// ⚠ <c>GravarAsync</c> e não <c>SaveChangesAsync</c>: o nome diferente é para que uma chamada
/// escrita por engano no lugar errado não compile por coincidência de assinatura.
/// </para>
/// </summary>
public interface ILogDb
{
    DbSet<LogAuditoria> Trilha { get; }

    DbSet<LogDeExcecao> Excecoes { get; }

    Task<int> GravarAsync(CancellationToken cancellationToken = default);
}
