using GrelhaHub.Domain.Gerais;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// ADR-0022 — o esquema do Identity com os nomes do projeto: tabela e coluna em português,
/// <c>snake_case</c>, como todo o resto. <b>Nenhum <c>AspNet*</c> chega ao banco.</b>
/// <para>
/// Estas configurações rodam DEPOIS de <c>base.OnModelCreating</c> do
/// <c>IdentityDbContext</c>, e é essa ordem que permite sobrescrever o que a base decide —
/// inclusive remover o índice global de nome de papel, que é o conflito nº 1 que a ADR-0021
/// apontou e a ADR-0022 §2 resolve.
/// </para>
/// </summary>
public sealed class PerfilConfiguration : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("perfil");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasColumnName("nome").IsRequired().HasMaxLength(60);
        builder.Property(p => p.NormalizedName)
            .HasColumnName("nome_normalizado").IsRequired().HasMaxLength(60);
        builder.Property(p => p.ConcurrencyStamp).HasColumnName("carimbo_de_concorrencia");

        builder.Property(p => p.Descricao).HasMaxLength(200);
        builder.Property(p => p.Sistema).IsRequired().HasDefaultValue(false);

        // ⚠⚠ ADR-0022 §2 — REMOVE o índice único GLOBAL que o Identity põe em NormalizedName
        // ("RoleNameIndex"). Papel do Identity é global por desenho; o perfil do GrelhaHub é por
        // unidade, e dois restaurantes têm, cada um, o seu "Gerente".
        var nomeNormalizado = builder.Metadata.FindProperty(nameof(Perfil.NormalizedName))!;

        if (builder.Metadata.FindIndex([nomeNormalizado]) is { } indiceGlobal)
        {
            builder.Metadata.RemoveIndex(indiceGlobal);
        }

        // RN-GER-009 — unidade_id é a PRIMEIRA coluna da chave única de negócio. No PostgreSQL,
        // NULL não conflita com NULL: eventuais perfis de plataforma (unidade_id nulo) convivem
        // com a restrição — a mesma exceção declarada de usuario.email/cpf.
        builder.HasIndex(p => new { p.UnidadeId, p.NormalizedName })
            .IsUnique().HasDatabaseName("uq_perfil_nome");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(p => p.UnidadeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// <c>perfil_permissao</c> — ADR-0022 §3, <c>RN-GER-010</c>: a permissão concedida é uma claim
/// de papel, tipo <c>permissao</c>, valor <c>recurso.acao</c>.
/// <para>
/// ⚠ A chave é <c>int</c> gerada pelo banco — exceção única à regra "chave nasce na aplicação"
/// (ADR-0004), porque o tipo é do framework e o id da concessão não é referenciado por ninguém.
/// </para>
/// <para>
/// <b>Não existe mais tabela <c>permissao</c>:</b> o <c>CatalogoDeAcesso</c> (código) é a lista
/// do que existe; esta tabela guarda o que foi concedido. Descrição vem do catálogo.
/// </para>
/// </summary>
public sealed class PerfilPermissaoConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("perfil_permissao");

        builder.Property(claim => claim.RoleId).HasColumnName("perfil_id");
        builder.Property(claim => claim.ClaimType).HasColumnName("tipo").HasMaxLength(60);
        builder.Property(claim => claim.ClaimValue).HasColumnName("valor").HasMaxLength(200);

        // A consulta do login: todas as claims de UM papel. O FK da base já indexa perfil_id;
        // o nome é do projeto.
        builder.HasIndex(claim => claim.RoleId).HasDatabaseName("ix_perfil_permissao_perfil");

    }
}

/// <summary>
/// <c>usuario_perfil</c> — o N:N usuário × papel que o Identity traz.
/// <para>
/// ⚠⚠ <b>Existe no esquema e fica VAZIO, de propósito.</b> <c>RN-GER-010</c> fixa UM perfil por
/// usuário, e a fonte desse vínculo é <c>usuario.perfil_id</c> — que carrega a FK, a matriz de
/// delegação (<c>RN-GER-024</c>) e a trilha. Popular os dois seria manter duas fontes do mesmo
/// fato, que divergem em silêncio. A tabela permanece porque o <c>IdentityDbContext</c> a exige
/// no modelo — e porque removê-la fecharia a porta de multi-papel que o framework deixa aberta.
/// </para>
/// </summary>
public sealed class UsuarioPerfilConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("usuario_perfil");

        builder.Property(vinculo => vinculo.UserId).HasColumnName("usuario_id");
        builder.Property(vinculo => vinculo.RoleId).HasColumnName("perfil_id");

        // O índice que a FK de perfil ganhou por convenção, com nome do projeto.
        builder.HasIndex(vinculo => vinculo.RoleId).HasDatabaseName("ix_usuario_perfil_perfil");
    }
}

/// <summary>
/// Renomeia as FKs que o <c>IdentityDbContext</c> declara com nome herdado do esquema
/// <c>AspNet*</c> ("fk_usuario_claim_asp_net_users_user_id"): a tabela já foi renomeada, e a
/// constraint acompanha. Por metadado, e não por redeclarar <c>HasOne</c> — redeclarar criaria
/// uma SEGUNDA FK ao lado da primeira.
/// <para>
/// O nome sai de <c>fk_&lt;tabela&gt;_&lt;tabela principal&gt;</c>, derivado do PRÓPRIO
/// metadado — nada posicional: a ordem em que a base declara as FKs pode mudar entre versões
/// do Identity, e um nome dado por posição trocaria de dono em silêncio.
/// </para>
/// <para>
/// ⚠ Chamado pelo <c>GrelhaHubDbContext</c> DEPOIS de todas as configurações, e não de dentro
/// de cada uma: a ordem de aplicação das configurações não é garantida, e renomear a FK antes
/// de a tabela PRINCIPAL ter sido renomeada gravaria "asp_net_users" no nome de novo.
/// </para>
/// </summary>
internal static class RenomearFks
{
    public static void Aplicar(Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entidade)
    {
        foreach (var fk in entidade.GetForeignKeys())
        {
            var tabela = entidade.GetTableName();
            var principal = fk.PrincipalEntityType.GetTableName();

            fk.SetConstraintName($"fk_{tabela}_{principal}");
        }
    }
}

/// <summary><c>usuario_claim</c> — claim direta de usuário. Sem uso hoje; esquema do Identity.</summary>
public sealed class UsuarioClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("usuario_claim");

        builder.Property(claim => claim.UserId).HasColumnName("usuario_id");
        builder.Property(claim => claim.ClaimType).HasColumnName("tipo").HasMaxLength(60);
        builder.Property(claim => claim.ClaimValue).HasColumnName("valor").HasMaxLength(200);

        builder.HasIndex(claim => claim.UserId).HasDatabaseName("ix_usuario_claim_usuario");
    }
}

/// <summary>
/// <c>usuario_login</c> — login externo (Google, etc.). Sem uso hoje: a ADR-0022 o lista como
/// "deixa de ser reescrita e passa a ser configuração", e o esquema pronto é essa configuração.
/// </summary>
public sealed class UsuarioLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("usuario_login");

        builder.Property(login => login.LoginProvider).HasColumnName("provedor").HasMaxLength(128);
        builder.Property(login => login.ProviderKey)
            .HasColumnName("chave_no_provedor").HasMaxLength(128);
        builder.Property(login => login.ProviderDisplayName).HasColumnName("nome_do_provedor");
        builder.Property(login => login.UserId).HasColumnName("usuario_id");

        builder.HasIndex(login => login.UserId).HasDatabaseName("ix_usuario_login_usuario");
    }
}

/// <summary>
/// <c>usuario_token</c> — token que o PRÓPRIO Identity guarda (2FA, provedores).
/// <para>
/// ⚠ Não confundir com <c>token_acesso</c> (<c>RN-GER-032</c> a <c>035</c>), que continua
/// nosso: convite e redefinição são token opaco, de uso único, guardado só como hash — o
/// provedor do Identity não guarda token, e a regra decidiu guardar (ADR-0022, "o que NÃO
/// muda").
/// </para>
/// </summary>
public sealed class UsuarioTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("usuario_token");

        builder.Property(token => token.UserId).HasColumnName("usuario_id");
        builder.Property(token => token.LoginProvider).HasColumnName("provedor").HasMaxLength(128);
        builder.Property(token => token.Name).HasColumnName("nome").HasMaxLength(128);
        builder.Property(token => token.Value).HasColumnName("valor");
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
            .HasConstraintName("fk_token_acesso_usuario")
            .OnDelete(DeleteBehavior.Cascade);

        // Emitir token novo invalida os anteriores do mesmo tipo (RN-GER-033): é esta consulta.
        builder.HasIndex(t => new { t.UsuarioId, t.Tipo, t.UsadoEm, t.InvalidadoEm })
            .HasDatabaseName("ix_token_acesso_vigente");
    }
}
