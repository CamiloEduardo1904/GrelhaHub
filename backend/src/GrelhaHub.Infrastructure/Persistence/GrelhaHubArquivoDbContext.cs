using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>O banco de arquivo — ADR-0018. Uma tabela, sem regra nenhuma.</summary>
public sealed class GrelhaHubArquivoDbContext(DbContextOptions<GrelhaHubArquivoDbContext> options)
    : DbContext(options), IArquivoDb
{
    public DbSet<ArquivoBinario> Binarios => Set<ArquivoBinario>();

    public Task<int> GravarAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ArquivoBinario>(binario =>
        {
            binario.ToTable("arquivo_binario");
            binario.HasKey(b => b.Id);

            // `bytea` — o tipo de binário do PostgreSQL. Sem teto de tamanho aqui: o limite de
            // upload é regra de negócio (RN-VEN-042) e vive na validação, não no esquema.
            binario.Property(b => b.Conteudo).HasColumnType("bytea").IsRequired();
            binario.Property(b => b.TipoMime).IsRequired().HasMaxLength(100);
        });
    }
}
