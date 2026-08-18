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
        // Defaults no banco, e não só no inicializador do C#: um INSERT feito por script de
        // carga ou por manutenção direta também precisa nascer válido.
        builder.Property(u => u.FusoHorario)
            .IsRequired().HasMaxLength(50).HasDefaultValue("America/Sao_Paulo");

        builder.Property(u => u.Ativa).HasDefaultValue(true);
        builder.Property(u => u.CriadoEm).HasColumnName("dh_criacao");

        // RN-GER-026 — caminho da logo exibida no cardápio público. 500, como `produto.url_imagem`.
        builder.Property(u => u.UrlLogo).HasMaxLength(500);

        builder.HasIndex(u => u.Cnpj).IsUnique().HasDatabaseName("uq_unidade_cnpj");

        // RN-VEN-036 — o slug é único em todo o sistema e compõe o link público.
        builder.HasIndex(u => u.Slug).IsUnique().HasDatabaseName("uq_unidade_slug");

        builder.ToTable(t =>
        {
            // RN-GER-013 — documento sem máscara: 14 dígitos, nada mais.
            t.HasCheckConstraint("ck_unidade_cnpj", "cnpj ~ '^[0-9]{14}$'");

            // RN-VEN-036 — formato do identificador público.
            t.HasCheckConstraint("ck_unidade_slug", "slug ~ '^[a-z0-9-]{3,60}$'");

            // SPEC-GER-001 §6 — NOT NULL não impede string em branco, e razão social vazia
            // quebra qualquer documento emitido a partir dela.
            t.HasCheckConstraint("ck_unidade_razao_social", "length(trim(razao_social)) > 0");
        });
    }
}

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario", t =>
        {
            // RN-GER-013 / RN-GER-023 — CHECK valida DOMÍNIO (onze dígitos, sem máscara), nunca
            // POLÍTICA. O dígito verificador é regra de negócio e vive no value object `Cpf`,
            // onde a mensagem de erro é legível pelo usuário: constraint que vaza para a tela é
            // defeito da aplicação (ADR-0009).
            t.HasCheckConstraint("ck_usuario_cpf", "cpf IS NULL OR cpf ~ '^[0-9]{11}$'");
        });

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome).IsRequired();

        // RN-GER-023 — nulável: o usuário de plataforma tem um nome só, e os usuários criados
        // antes desta regra não têm sobrenome gravado. NOT NULL obrigaria a inventar dado.
        builder.Property(u => u.Sobrenome);

        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.SenhaHash).IsRequired().HasMaxLength(500);

        // RN-GER-023 — o CPF. O tipo `char(11)` e o conversor vêm da convenção
        // (ConvencoesDeTipo); aqui fica só a nulidade e a unicidade.
        builder.Property(u => u.Cpf);
        // RN-GER-010 — o perfil deixou de ser enum: virou FK. Nulo apenas para o usuário de
        // plataforma (RN-GER-014), que não pertence a unidade e portanto não pertence a perfil dela.
        builder.HasOne(u => u.Perfil)
            .WithMany()
            .HasForeignKey(u => u.PerfilId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // RF-SEG-005 — senha inicial escolhida por outra pessoa não é senha.
        builder.Property(u => u.DeveTrocarSenha).IsRequired().HasDefaultValue(true);
        builder.Property(u => u.CriadoEm).HasColumnName("dh_criacao");
        // RN-GER-001 — quem criou este usuario. Nulo no administrador fundador, criado junto
        // com a propria unidade.
        builder.Property(u => u.CriadoPorUsuarioId).HasColumnName("criado_por");

        // RN-GER-014 — nulo apenas para o usuário de plataforma, que não tem unidade.
        builder.Property(u => u.EhPlataforma).IsRequired().HasDefaultValue(false);

        // ⚠ EXCEÇÃO DECLARADA à convenção de RN-GER-009 (unidade_id como primeira coluna de
        // toda chave única).
        //
        // O e-mail é único GLOBALMENTE, e não por unidade, porque o login recebe apenas e-mail
        // e senha (contrato de SPEC-VEN-001): ele precisa resolver o usuário ANTES de saber a
        // unidade. Com unicidade por unidade, dois restaurantes poderiam registrar o mesmo
        // e-mail e o login escolheria um deles de forma arbitrária — defeito silencioso, que
        // apareceria como "entrei no restaurante errado".
        //
        // Um índice composto (unidade_id, email) seria redundante: a unicidade global já o
        // implica.
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("uq_usuario_email");

        // ⚠ SEGUNDA EXCEÇÃO DECLARADA à convenção de RN-GER-009, pela mesma razão do e-mail e
        // com um argumento próprio: RN-GER-023 diz que o CPF é único em TODO o sistema porque
        // a pessoa é a mesma pessoa em qualquer restaurante. Dois cadastros do mesmo CPF são a
        // mesma pessoa duas vezes, não duas pessoas — e é isso que o índice impede.
        //
        // No PostgreSQL, NULL não conflita com NULL num índice único: o usuário de plataforma
        // (RN-GER-014) e os usuários anteriores a esta regra convivem com a restrição sem
        // precisar de índice parcial.
        builder.HasIndex(u => u.Cpf).IsUnique().HasDatabaseName("uq_usuario_cpf");

        builder.HasOne(u => u.Unidade)
            .WithMany()
            .HasForeignKey(u => u.UnidadeId)
            .IsRequired(false)
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
        // RN-GER-001 — quem criou. NULO no pedido do cardapio publico: quem o criou foi o
        // cliente, que nao e usuario do sistema (RN-VEN-038).
        builder.Property(p => p.CriadoPorUsuarioId).HasColumnName("criado_por");
        builder.Property(p => p.ConfirmadoEm).HasColumnName("dh_confirmacao");
        builder.Property(p => p.FinalizadoEm).HasColumnName("dh_finalizacao");

        // RN-VEN-041 — exclusão lógica. O índice inclui a coluna porque TODA consulta de pedido
        // passa pelo filtro global `dh_exclusao IS NULL`.
        builder.Property(p => p.ExcluidoEm).HasColumnName("dh_exclusao");
        builder.Property(p => p.MotivoExclusao).HasMaxLength(500);
        builder.Ignore(p => p.Excluido);

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

        // RN-GER-002 — RESTRICT e não CASCADE.
        //
        // CASCADE só serviria para habilitar algo que a regra proíbe: apagar um pedido levando
        // os itens embora. E com a exclusão LÓGICA de pedido (RN-VEN-041) o cascade fica não só
        // inútil como perigoso — seria o único caminho por onde um pedido e seus itens poderiam
        // desaparecer de verdade, contornando a trilha.
        //
        // Com RESTRICT, o modelo AFIRMA a regra em vez de abrir uma exceção morta.
        builder.HasMany(p => p.Itens)
            .WithOne(i => i.Pedido!)
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

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
