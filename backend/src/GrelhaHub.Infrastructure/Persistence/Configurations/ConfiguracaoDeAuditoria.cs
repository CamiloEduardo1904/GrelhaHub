using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// A OUTBOX de auditoria no banco da aplicação — ADR-0017.
/// <para>
/// ⚠⚠ <b>Este arquivo configurava <c>log_auditoria</c> até 18/08/2026.</b> A trilha mudou de
/// banco; o que ficou aqui é a ponte. A tabela é escrita por TODA operação do sistema e esvaziada
/// pelo serviço de drenagem — ela é fila, não histórico, e cresce só quando o banco de log está
/// fora do ar.
/// </para>
/// <para>
/// ⚠ As colunas espelham a trilha, e o espelho é assumido: <c>OutboxDeAuditoria.ParaTrilha()</c> é
/// onde ele fica visível, e quem acrescentar campo na trilha quebra a compilação lá.
/// </para>
/// </summary>
public sealed class OutboxDeAuditoriaConfiguration : IEntityTypeConfiguration<OutboxDeAuditoria>
{
    public void Configure(EntityTypeBuilder<OutboxDeAuditoria> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_auditoria");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.EnfileiradoEm).HasColumnName("dh_enfileiramento");
        builder.Property(o => o.Tentativas).HasDefaultValue(0);
        builder.Property(o => o.UltimoErro).HasMaxLength(500);

        builder.Property(o => o.Entidade).IsRequired().HasMaxLength(100);
        builder.Property(o => o.Operacao).IsRequired().HasMaxLength(20).HasConversion<string>();
        builder.Property(o => o.ValoresAnteriores).HasColumnType("jsonb");
        builder.Property(o => o.ValoresNovos).HasColumnType("jsonb");
        builder.Property(o => o.Justificativa).HasMaxLength(500);
        builder.Property(o => o.OcorridoEm).HasColumnName("dh_ocorrencia");
        builder.Property(o => o.DataOperacional).HasColumnName("data_operacional");

        // A leitura é sempre "os mais antigos primeiro", e é a única que existe.
        builder.HasIndex(o => o.EnfileiradoEm).HasDatabaseName("ix_outbox_auditoria_fila");
    }
}
