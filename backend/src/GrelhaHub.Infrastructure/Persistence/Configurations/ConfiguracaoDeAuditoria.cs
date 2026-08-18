using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>SPEC-GER-005</c> — a trilha. **Livro-razão: só `INSERT` e `SELECT`.**
/// <para>
/// <b>Sem filtro global de unidade</b>, e é deliberado: a trilha registra também o que não pertence
/// a unidade alguma (o usuário de plataforma, <c>RN-GER-014</c>), e sujeitá-la ao recorte a
/// esconderia de quem audita. O recorte da <i>consulta</i> é explícito no handler.
/// </para>
/// </summary>
public sealed class LogAuditoriaConfiguration : IEntityTypeConfiguration<LogAuditoria>
{
    public void Configure(EntityTypeBuilder<LogAuditoria> builder)
    {
        builder.ToTable("log_auditoria");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Entidade).IsRequired().HasMaxLength(100);
        builder.Property(l => l.RegistroId).IsRequired();
        builder.Property(l => l.Justificativa).HasMaxLength(500);
        builder.Property(l => l.OcorridoEm).IsRequired().HasColumnName("dh_ocorrencia");

        builder.Property(l => l.Operacao)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        // jsonb e não text: o PostgreSQL indexa e consulta dentro do documento, e a trilha vai ser
        // consultada por "quem mudou o preço deste produto" na Sprint 7 & 8.
        builder.Property(l => l.ValoresAnteriores).HasColumnType("jsonb");
        builder.Property(l => l.ValoresNovos).HasColumnType("jsonb");

        // RN-GER-006 ⚠⚠ — coluna gerada, como em `pedido`. A trilha também é apurada por dia
        // operacional: "o que mudou no dia em que o caixa não fechou".
        builder.Property(l => l.DataOperacional)
            .HasColumnName("data_operacional")
            .HasColumnType("date")
            .HasComputedColumnSql(DiaOperacionalDaTrilha, stored: true)
            .ValueGeneratedOnAddOrUpdate();

        // FK sem navegação: a trilha aponta para o registro, mas não o carrega — carregar
        // convidaria a alterar o auditado a partir da trilha.
        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(l => l.UnidadeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(l => l.UsuarioId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.UnidadeId, l.Entidade, l.RegistroId, l.OcorridoEm })
            .HasDatabaseName("ix_log_registro")
            .IsDescending(false, false, false, true);

        builder.HasIndex(l => new { l.UnidadeId, l.UsuarioId, l.DataOperacional })
            .HasDatabaseName("ix_log_usuario");
    }

    /// <summary>
    /// Mesma expressão de <c>pedido.data_operacional</c>, sobre a coluna desta tabela.
    /// <para>
    /// A hora de virada é constante aqui pelo mesmo motivo de <c>RN-GER-006</c>: coluna gerada
    /// exige expressão imutável, e alterá-la reclassificaria o passado.
    /// </para>
    /// </summary>
    internal const string DiaOperacionalDaTrilha =
        "(((dh_ocorrencia AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)";
}
