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

public sealed class TokenDeAcessoConfiguration : IEntityTypeConfiguration<TokenDeAcesso>
{
    public void Configure(EntityTypeBuilder<TokenDeAcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("token_acesso");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Tipo).IsRequired().HasConversion<string>().HasMaxLength(30);

        // ÚNICO: o hash é o que a consulta de resgate procura, e dois registros com o mesmo hash
        // seriam o mesmo link valendo duas vezes — o oposto de "uso único".
        builder.Property(t => t.Hash).IsRequired().HasMaxLength(128).HasColumnName("hash_token");
        builder.HasIndex(t => t.Hash).IsUnique().HasDatabaseName("ux_token_acesso_hash");

        builder.Property(t => t.ExpiraEm).IsRequired().HasColumnName("dh_expiracao");
        builder.Property(t => t.CriadoEm).IsRequired().HasColumnName("dh_criacao");
        builder.Property(t => t.UsadoEm).HasColumnName("dh_uso");
        builder.Property(t => t.InvalidadoEm).HasColumnName("dh_invalidacao");

        builder.HasOne(t => t.Usuario)
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Emitir token novo invalida os anteriores do mesmo tipo (RN-GER-033): é esta consulta.
        builder.HasIndex(t => new { t.UsuarioId, t.Tipo, t.UsadoEm, t.InvalidadoEm })
            .HasDatabaseName("ix_token_acesso_vigente");

    }
}
