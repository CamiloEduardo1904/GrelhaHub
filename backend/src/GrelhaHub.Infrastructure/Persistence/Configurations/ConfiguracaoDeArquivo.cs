using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// O METADADO do arquivo — ADR-0018. Os bytes estão no outro banco.
/// <para>
/// ⚠ Não existe chave estrangeira para <c>arquivo_binario</c>, e não pode existir: chave
/// estrangeira não atravessa banco. O que liga os dois lados é o <b>mesmo id</b>, e a integridade
/// é mantida pela ordem de gravação — bytes primeiro, metadado depois. Ver
/// <see cref="Arquivo"/>.
/// </para>
/// </summary>
public sealed class ArquivoConfiguration : IEntityTypeConfiguration<Arquivo>
{
    public void Configure(EntityTypeBuilder<Arquivo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("arquivo");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Pasta).IsRequired().HasMaxLength(30);
        builder.Property(a => a.TipoMime).IsRequired().HasMaxLength(100);
        builder.Property(a => a.CriadoEm).HasColumnName("dh_criacao");
        builder.Property(a => a.ExcluidoEm).HasColumnName("dh_exclusao");

        // `CHECK` valida DOMÍNIO, nunca política: 5 MB é RN-VEN-042 e vive na validação da
        // aplicação, onde vira mensagem para o usuário. O que o banco recusa aqui é o
        // impossível — arquivo de tamanho zero ou negativo.
        builder.ToTable(tabela => tabela.HasCheckConstraint(
            "ck_arquivo_tamanho", "tamanho_em_bytes > 0"));

        // RN-GER-009 — `unidade_id` PRIMEIRO. A leitura real é sempre "os arquivos desta
        // unidade", e a pasta separa logo de foto de produto.
        builder.HasIndex(a => new { a.UnidadeId, a.Pasta })
            .HasDatabaseName("ix_arquivo_unidade_pasta");
    }
}
