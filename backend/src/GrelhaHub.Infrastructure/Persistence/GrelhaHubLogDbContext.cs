using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// O banco de <b>log</b> — ADR-0017. Trilha de auditoria e exceções.
/// <para>
/// Banco separado porque o que mora aqui cresce por motivo diferente do dado operacional e tem
/// retenção própria: a trilha cresce a cada gravação e nunca é apagada (<c>RN-GER-002</c>), e o
/// <c>dump</c> do banco de negócio não deveria carregá-la toda vez.
/// </para>
/// <para>
/// ⚠⚠ <b>Não há filtro global de unidade aqui, e é deliberado.</b> Este banco é o registro de
/// <b>quem fez o quê</b>, inclusive da plataforma sobre o dado do cliente — sujeitá-lo ao recorte
/// esconderia justamente o que precisa ser auditado. O recorte, quando a consulta precisa dele, é
/// explícito no handler.
/// </para>
/// <para>
/// ⚠ <b>A trilha não é escrita por aqui pelo caminho normal.</b> Ela chega pela outbox
/// (<see cref="OutboxDeAuditoria"/>): a operação e o registro precisam ser atômicos, e não há
/// transação entre dois bancos. Ver ADR-0017.
/// </para>
/// </summary>
public sealed class GrelhaHubLogDbContext(DbContextOptions<GrelhaHubLogDbContext> options)
    : DbContext(options), ILogDb
{
    public DbSet<LogAuditoria> Trilha => Set<LogAuditoria>();

    public DbSet<LogDeExcecao> Excecoes => Set<LogDeExcecao>();

    public Task<int> GravarAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<LogAuditoria>(trilha =>
        {
            trilha.ToTable("log_auditoria");
            trilha.HasKey(t => t.Id);

            trilha.Property(t => t.Entidade).IsRequired().HasMaxLength(100);
            trilha.Property(t => t.Operacao).IsRequired().HasMaxLength(20)
                .HasConversion<string>();
            trilha.Property(t => t.ValoresAnteriores).HasColumnType("jsonb");
            trilha.Property(t => t.ValoresNovos).HasColumnType("jsonb");
            trilha.Property(t => t.Justificativa).HasMaxLength(500);
            trilha.Property(t => t.OcorridoEm).HasColumnName("dh_ocorrencia");

            // ⚠ Aqui `data_operacional` é COLUNA COMUM, não gerada.
            //
            // No banco da aplicação ela é calculada pelo PostgreSQL a partir do fuso e da virada da
            // própria linha (RN-GER-006). Neste banco não existem `unidade` nem `pedido` para
            // consultar — a linha chega pronta, com o valor que foi calculado LÁ. Recalcular aqui
            // seria uma segunda definição da regra, e das piores: divergiria em silêncio.
            trilha.Property(t => t.DataOperacional).HasColumnName("data_operacional");

            trilha.HasIndex(t => new { t.UnidadeId, t.Entidade, t.RegistroId, t.OcorridoEm })
                .HasDatabaseName("ix_log_auditoria_registro");

            trilha.HasIndex(t => new { t.UnidadeId, t.UsuarioId, t.DataOperacional })
                .HasDatabaseName("ix_log_auditoria_autor");
        });

        modelBuilder.Entity<LogDeExcecao>(excecao =>
        {
            excecao.ToTable("log_excecao");
            excecao.HasKey(e => e.Id);

            excecao.Property(e => e.Tipo).IsRequired().HasMaxLength(200);
            excecao.Property(e => e.Mensagem).IsRequired().HasMaxLength(2000);
            excecao.Property(e => e.Rota).HasMaxLength(500);
            excecao.Property(e => e.Metodo).HasMaxLength(10);
            excecao.Property(e => e.Correlacao).HasMaxLength(100);
            excecao.Property(e => e.OcorridoEm).HasColumnName("dh_ocorrencia");

            // A pilha não tem teto: truncá-la corta justamente o quadro mais interno, que é o
            // que diz onde quebrou.
            excecao.Property(e => e.Pilha).HasColumnType("text");

            // "O que mais quebrou esta semana" — a consulta que justifica a tabela existir.
            excecao.HasIndex(e => new { e.Tipo, e.OcorridoEm })
                .HasDatabaseName("ix_log_excecao_tipo");

            excecao.HasIndex(e => e.Correlacao)
                .HasDatabaseName("ix_log_excecao_correlacao");
        });
    }
}
