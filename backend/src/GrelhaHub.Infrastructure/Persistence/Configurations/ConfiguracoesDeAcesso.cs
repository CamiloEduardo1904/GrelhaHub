using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>SPEC-GER-004</c> — catálogo <b>global</b> de permissões. Como <c>parametro</c> e
/// <c>modulo</c>: a permissão existe no sistema; o que pertence à unidade é o perfil que a concede.
/// </summary>
public sealed class PermissaoConfiguration : IEntityTypeConfiguration<Permissao>
{
    public void Configure(EntityTypeBuilder<Permissao> builder)
    {
        builder.ToTable("permissao");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).IsRequired().HasMaxLength(60);
        builder.Property(p => p.Descricao).IsRequired();

        builder.Property(p => p.Modulo)
            .IsRequired()
            .HasMaxLength(3)
            .HasConversion(codigo => Modulo.Sigla(codigo), sigla => DeSigla(sigla));

        builder.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("uq_permissao_codigo");
    }

    private static CodigoDeModulo DeSigla(string sigla) =>
        Enum.GetValues<CodigoDeModulo>().First(codigo => Modulo.Sigla(codigo) == sigla);
}

/// <summary><c>SPEC-GER-004</c> — perfil da unidade, com as permissões que concede.</summary>
public sealed class PerfilConfiguration : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        builder.ToTable("perfil");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome).IsRequired().HasMaxLength(60);
        builder.Property(p => p.Descricao).HasMaxLength(200);
        builder.Property(p => p.Sistema).IsRequired().HasDefaultValue(false);

        // RN-GER-009 — unidade_id é a PRIMEIRA coluna da chave única de negócio.
        builder.HasIndex(p => new { p.UnidadeId, p.Nome })
            .IsUnique().HasDatabaseName("uq_perfil_nome");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(p => p.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Permissoes)
            .WithOne()
            .HasForeignKey(concedida => concedida.PerfilId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Perfil.Permissoes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// <c>SPEC-GER-004</c> — concessão. Chave composta: a mesma permissão não se concede duas vezes
/// ao mesmo perfil.
/// <para>
/// <b>Única relação com <c>CASCADE</c> no modelo, e justificada:</b> a concessão não é registro
/// transacional — é a composição do perfil. Revogar uma permissão remove a linha, e é isso que se
/// espera; <c>RN-GER-002</c> fala de <i>registro transacional</i>, que a concessão não é.
/// </para>
/// </summary>
public sealed class PerfilPermissaoConfiguration : IEntityTypeConfiguration<PerfilPermissao>
{
    public void Configure(EntityTypeBuilder<PerfilPermissao> builder)
    {
        builder.ToTable("perfil_permissao");
        builder.HasKey(concedida => new { concedida.PerfilId, concedida.PermissaoId });

        builder.HasOne(concedida => concedida.Permissao)
            .WithMany()
            .HasForeignKey(concedida => concedida.PermissaoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// <c>RN-GER-015</c> — sessão de suporte da plataforma sobre uma unidade. ADR-0013.
/// <para>
/// <b>Sem filtro global de unidade</b>, e é deliberado: a sessão é registro <b>da plataforma</b>
/// sobre um cliente, não registro do cliente. Sujeitá-la ao recorte a esconderia justamente de
/// quem precisa auditá-la.
/// </para>
/// </summary>
public sealed class SessaoDeSuporteConfiguration : IEntityTypeConfiguration<SessaoDeSuporte>
{
    public void Configure(EntityTypeBuilder<SessaoDeSuporte> builder)
    {
        builder.ToTable("sessao_suporte");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Motivo).IsRequired().HasMaxLength(500);
        builder.Property(s => s.AbertaEm).IsRequired().HasColumnName("dh_abertura");
        builder.Property(s => s.EncerradaEm).HasColumnName("dh_encerramento");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(s => s.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // O behavior consulta "tenho sessão aberta e recente?" a cada escrita de plataforma.
        builder.HasIndex(s => new { s.UsuarioId, s.EncerradaEm, s.AbertaEm })
            .HasDatabaseName("ix_sessao_suporte_aberta");

        builder.HasIndex(s => new { s.UnidadeId, s.AbertaEm })
            .HasDatabaseName("ix_sessao_suporte_unidade")
            .IsDescending(false, true);

        builder.Ignore(s => s.EstaAberta);
    }
}
