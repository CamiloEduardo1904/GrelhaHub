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
    /// O dia operacional da trilha — e ⚠ <b>ele é o da PLATAFORMA, não o da casa</b>.
    /// <para>
    /// <b>Divergência declarada em 18/08/2026, não descuido.</b> Quando <c>RN-GER-036</c> passou a
    /// derivar a virada do horário de cada unidade, <c>pedido</c> acompanhou copiando a virada para
    /// a linha. A trilha <b>não</b>: ela registra também o que a plataforma faz — e a plataforma não
    /// tem unidade (<c>RN-GER-014</c>), logo não tem virada. Uma trilha com o dia de cada casa
    /// deixaria as linhas de plataforma sem resposta, e "sem resposta" numa trilha de auditoria é
    /// pior do que uma régua declarada.
    /// </para>
    /// <para>
    /// O custo é real e está na §8 da <c>SPEC-GER-009</c>: "o que mudou no dia em que o caixa não
    /// fechou" responde pela régua das 04h00, que pode não ser a da casa. Quando a pergunta
    /// aparecer de verdade, o caminho é o mesmo do pedido — copiar a virada para a linha —, e a
    /// coluna de origem já existirá em <c>unidade</c>.
    /// </para>
    /// </summary>
    internal const string DiaOperacionalDaTrilha =
        "(((dh_ocorrencia AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)";
}
