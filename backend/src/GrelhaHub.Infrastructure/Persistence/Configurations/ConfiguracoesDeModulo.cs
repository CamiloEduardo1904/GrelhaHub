using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>SPEC-GER-006</c> — catálogo global de módulos. Como <c>parametro</c>, não pertence a
/// unidade: o que pertence é a habilitação.
/// </summary>
public sealed class ModuloConfiguration : IEntityTypeConfiguration<Modulo>
{
    public void Configure(EntityTypeBuilder<Modulo> builder)
    {
        builder.ToTable("modulo");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Nome).IsRequired().HasMaxLength(60);
        builder.Property(m => m.Ordem).IsRequired();
        builder.Property(m => m.Nucleo).IsRequired().HasDefaultValue(false);

        // Guardado como a sigla do catálogo de regras — GER, VEN, RH… O CHECK está na migration.
        builder.Property(m => m.Codigo)
            .IsRequired()
            .HasMaxLength(3)
            .HasConversion(
                codigo => Modulo.Sigla(codigo),
                sigla => DeSigla(sigla));

        builder.HasIndex(m => m.Codigo).IsUnique().HasDatabaseName("uq_modulo_codigo");

    }

    private static CodigoDeModulo DeSigla(string sigla) =>
        Enum.GetValues<CodigoDeModulo>().First(codigo => Modulo.Sigla(codigo) == sigla);
}

/// <summary><c>RN-GER-020</c> — arestas do grafo de dependências entre módulos.</summary>
public sealed class ModuloDependenciaConfiguration : IEntityTypeConfiguration<ModuloDependencia>
{
    public void Configure(EntityTypeBuilder<ModuloDependencia> builder)
    {
        builder.ToTable("modulo_dependencia");

        // Chave composta: uma aresta é única pelo par.
        builder.HasKey(d => new { d.ModuloId, d.DependeDeId });

        // As duas pontas explícitas — é o que o EF não consegue inferir numa relação
        // auto-referenciada.
        builder.HasOne(d => d.Modulo)
            .WithMany()
            .HasForeignKey(d => d.ModuloId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.DependeDe)
            .WithMany()
            .HasForeignKey(d => d.DependeDeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// <c>SPEC-GER-006</c> — habilitação com vigência. **Terceira aplicação** do padrão de
/// <c>parametro_valor</c>: a `EXCLUDE` vive na migration, em SQL, porque o EF Core não a expressa.
/// </summary>
public sealed class UnidadeModuloConfiguration : IEntityTypeConfiguration<UnidadeModulo>
{
    public void Configure(EntityTypeBuilder<UnidadeModulo> builder)
    {
        builder.ToTable("unidade_modulo");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.VigenciaInicio).IsRequired();
        builder.Property(u => u.Justificativa).HasMaxLength(500);
        builder.Property(u => u.CriadoEm).HasColumnName("dh_criacao");

        // RN-GER-001 — qual usuario de plataforma habilitou ou desabilitou o modulo.
        builder.Property(u => u.CriadoPorUsuarioId).HasColumnName("criado_por");

        builder.HasOne(u => u.Modulo)
            .WithMany()
            .HasForeignKey(u => u.ModuloId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(u => u.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // RN-GER-009 — unidade_id é a primeira coluna. Vigência DESC porque a consulta quase
        // sempre quer a habilitação corrente.
        builder.HasIndex(u => new { u.UnidadeId, u.ModuloId, u.VigenciaInicio })
            .HasDatabaseName("ix_unidade_modulo_busca")
            .IsDescending(false, false, true);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_unidade_modulo_vigencia",
            "vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio"));

        builder.Ignore(u => u.EstaHabilitado);
    }
}
