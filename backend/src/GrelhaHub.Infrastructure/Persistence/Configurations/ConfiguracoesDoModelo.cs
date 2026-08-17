using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Domain.Crm;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Expressão SQL do dia operacional — <c>RN-GER-006</c> ⚠⚠, ADR-0011.
/// <para>
/// A hora de virada (<c>PAR-GER-001</c>, 04h00) é <b>constante</b> aqui de propósito:
/// <c>GENERATED ALWAYS AS</c> exige expressão imutável, e alterá-la reclassificaria vendas
/// passadas — exatamente o que a regra existe para impedir. Mudar a virada é operação de
/// migration, não edição de parâmetro em tela.
/// </para>
/// </summary>
internal static class DiaOperacionalSql
{
    public const string Expressao =
        "(((dh_criacao AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)";
}

public sealed class UnidadeConfiguration : IEntityTypeConfiguration<Unidade>
{
    public void Configure(EntityTypeBuilder<Unidade> builder)
    {
        builder.ToTable("unidade");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.RazaoSocial).IsRequired();
        builder.Property(u => u.NomeFantasia).IsRequired();
        builder.Property(u => u.Cnpj).IsRequired().HasMaxLength(14);
        builder.Property(u => u.Slug).IsRequired().HasMaxLength(60);
        builder.Property(u => u.Telefone).HasMaxLength(11);
        builder.Property(u => u.FusoHorario).IsRequired().HasMaxLength(50);
        builder.Property(u => u.CriadoEm).HasColumnName("dh_criacao");

        builder.HasIndex(u => u.Cnpj).IsUnique().HasDatabaseName("uq_unidade_cnpj");

        // RN-VEN-036 — o slug é único em todo o sistema e compõe o link público.
        builder.HasIndex(u => u.Slug).IsUnique().HasDatabaseName("uq_unidade_slug");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_unidade_cnpj", "cnpj ~ '^[0-9]{14}$'");
            t.HasCheckConstraint("ck_unidade_slug", "slug ~ '^[a-z0-9-]{3,60}$'");
        });
    }
}

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome).IsRequired();
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.SenhaHash).IsRequired().HasMaxLength(500);
        builder.Property(u => u.Perfil).HasConversion<int>().IsRequired();
        builder.Property(u => u.CriadoEm).HasColumnName("dh_criacao");

        // RN-GER-009 — unidade_id é a PRIMEIRA coluna de toda chave única de negócio.
        builder.HasIndex(u => new { u.UnidadeId, u.Email })
            .IsUnique().HasDatabaseName("uq_usuario_email");

        // Login precisa achar o usuário antes de saber a unidade.
        builder.HasIndex(u => u.Email).HasDatabaseName("ix_usuario_email");

        builder.HasOne(u => u.Unidade)
            .WithMany()
            .HasForeignKey(u => u.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categoria");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nome).IsRequired();

        builder.HasIndex(c => new { c.UnidadeId, c.Nome })
            .IsUnique().HasDatabaseName("uq_categoria_nome");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(c => c.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Produtos)
            .WithOne(p => p.Categoria!)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata
            .FindNavigation(nameof(Categoria.Produtos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produto");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(500);
        builder.Property(p => p.Preco).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(p => p.UrlImagem).HasMaxLength(500);

        builder.HasIndex(p => new { p.UnidadeId, p.Nome })
            .IsUnique().HasDatabaseName("uq_produto_nome");

        builder.HasIndex(p => new { p.UnidadeId, p.CategoriaId })
            .HasDatabaseName("ix_produto_categoria");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(p => p.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // RN-VEN-032 — o preço vigente é sempre maior que zero.
        // O CHECK valida o DOMÍNIO. Política (teto de desconto, por exemplo) nunca vira
        // CHECK: vira parâmetro (docs/04-parametros-do-sistema.md).
        builder.ToTable(t => t.HasCheckConstraint("ck_produto_preco", "preco > 0"));
    }
}

public sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("cliente");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nome).IsRequired();
        builder.Property(c => c.Telefone).IsRequired().HasMaxLength(11);
        builder.Property(c => c.CriadoEm).HasColumnName("dh_criacao");

        // RN-CRM-001 — o cliente é identificado unicamente pelo telefone.
        builder.HasIndex(c => new { c.UnidadeId, c.Telefone })
            .IsUnique().HasDatabaseName("uq_cliente_telefone");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(c => c.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedido");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Numero).IsRequired();
        builder.Property(p => p.Canal).HasConversion<int>().IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.Observacao).HasMaxLength(500);
        builder.Property(p => p.MotivoCancelamento).HasMaxLength(500);
        builder.Property(p => p.CriadoEm).HasColumnName("dh_criacao");
        builder.Property(p => p.ConfirmadoEm).HasColumnName("dh_confirmacao");
        builder.Property(p => p.FinalizadoEm).HasColumnName("dh_finalizacao");

        // RN-GER-006 ⚠⚠ — coluna GERADA. A aplicação lê, nunca escreve.
        // Agrupar por dh_criacao faria a venda de sábado à noite cair em domingo.
        builder.Property(p => p.DataOperacional)
            .HasColumnName("data_operacional")
            .HasColumnType("date")
            .HasComputedColumnSql(DiaOperacionalSql.Expressao, stored: true)
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(p => new { p.UnidadeId, p.DataOperacional, p.Numero })
            .IsUnique().HasDatabaseName("uq_pedido_numero");

        builder.HasIndex(p => new { p.UnidadeId, p.DataOperacional, p.Status })
            .HasDatabaseName("ix_pedido_dia_status");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(p => p.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Itens)
            .WithOne(i => i.Pedido!)
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Pedido.Itens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.Total);
        builder.Ignore(p => p.AceitaAlteracaoDeItens);
    }
}

public sealed class ItemPedidoConfiguration : IEntityTypeConfiguration<ItemPedido>
{
    public void Configure(EntityTypeBuilder<ItemPedido> builder)
    {
        builder.ToTable("item_pedido");
        builder.HasKey(i => i.Id);

        // RN-VEN-008 ⚠ — cópia do preço no momento da venda. Alterar o preço de tabela
        // depois NÃO altera este valor.
        builder.Property(i => i.PrecoPraticado).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(i => i.NomeProduto).IsRequired();
        builder.Property(i => i.Quantidade).IsRequired();
        builder.Property(i => i.Observacao).HasMaxLength(300);

        builder.HasIndex(i => new { i.UnidadeId, i.PedidoId }).HasDatabaseName("ix_item_pedido");

        builder.HasOne<Produto>()
            .WithMany()
            .HasForeignKey(i => i.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            // RN-VEN-011 — a quantidade é sempre maior que zero.
            t.HasCheckConstraint("ck_item_quantidade", "quantidade > 0");
            t.HasCheckConstraint("ck_item_preco", "preco_praticado >= 0");
        });

        builder.Ignore(i => i.Total);
    }
}
