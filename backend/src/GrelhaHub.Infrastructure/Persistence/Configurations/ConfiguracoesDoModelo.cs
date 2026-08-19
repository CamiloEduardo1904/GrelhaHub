using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Domain.Crm;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Expressão SQL do dia operacional — <c>RN-GER-006</c> ⚠⚠, <c>RN-GER-036</c>, ADR-0011.
/// <para>
/// <b>A virada e o fuso vêm das COLUNAS DA PRÓPRIA LINHA</b>, copiadas da unidade quando o pedido
/// nasceu. Até 18/08/2026 eram literais — <c>'America/Sao_Paulo'</c> e <c>interval '4 hours'</c> —,
/// e essa fixação era a razão técnica de a virada não poder ser cadastrada por casa.
/// </para>
/// <para>
/// <b>O que a mudança preserva é exatamente o que a versão anterior protegia.</b> O argumento de lá
/// estava certo: alterar a expressão reclassificaria vendas passadas. Continua verdade — e é por
/// isso que a virada é copiada para a linha em vez de lida da unidade. Editar o horário de
/// funcionamento muda os pedidos <b>seguintes</b>; os gravados carregam o que valia.
/// </para>
/// <para>
/// ⚠ <b>Conferido em PostgreSQL 16 antes de escrever:</b> coluna gerada aceita referência a outra
/// coluna da mesma linha, inclusive em <c>AT TIME ZONE</c>. O que ela recusa é referência a outra
/// tabela — que é justamente o desenho que a cópia evita.
/// </para>
/// </summary>
internal static class DiaOperacionalSql
{
    public const string Expressao =
        "(((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date)";
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

        // RN-GER-006 / RN-GER-036 ⚠⚠ — a virada DESTA casa, derivada do horário de funcionamento.
        // Default de 4 horas no banco: quem já existia continua apurando como apurava.
        builder.Property(u => u.HoraDeViradaOperacional)
            .HasColumnName("hora_virada_operacional")
            .HasColumnType("interval")
            .IsRequired()
            .HasDefaultValueSql("interval '4 hours'")

            // Mesmo sentinel do pedido, pela mesma razão: 00h00 é virada válida e não pode ser
            // confundida com "não informado".
            .HasSentinel(TimeSpan.MinValue);

        // RN-GER-037 — o endereço. Tudo nulável: as unidades anteriores a esta regra não têm.
        builder.Property(u => u.Cep).HasMaxLength(8);
        builder.Property(u => u.Logradouro).HasMaxLength(200);
        builder.Property(u => u.NumeroDoEndereco).HasColumnName("numero_endereco").HasMaxLength(20);
        builder.Property(u => u.Complemento).HasMaxLength(100);
        builder.Property(u => u.Bairro).HasMaxLength(100);
        builder.Property(u => u.Cidade).HasMaxLength(100);
        builder.Property(u => u.Uf).HasMaxLength(2).IsFixedLength();

        builder.HasMany(u => u.Horarios)
            .WithOne()
            .HasForeignKey(h => h.UnidadeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Unidade.Horarios))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);


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
        // RN-VEN-053 — mesma razão de `pedido.observacao`: o teto de 200 é do que se digita agora
        // e vive no validator. A coluna continua em 500 porque cadastro feito antes da regra está
        // lá, e a tela é que pede o corte na próxima edição.
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
        // RN-VEN-052 ⚠ — 500, e NÃO os 80 da regra. O teto de 80 é do que se digita agora, e vive
        // no validator; a coluna guarda o que já foi gravado. Encurtar o varchar recusaria pedido
        // antigo na primeira leitura — reclassificar o passado é exatamente o que RN-GER-036
        // decidiu não fazer. Quem for "corrigir" esta divergência, leia a regra antes.
        builder.Property(p => p.Observacao).HasMaxLength(500);
        builder.Property(p => p.MotivoCancelamento).HasMaxLength(500);
        builder.Property(p => p.CriadoEm).HasColumnName("dh_criacao");
        // RN-GER-001 — quem criou. NULO no pedido do cardapio publico: quem o criou foi o
        // cliente, que nao e usuario do sistema (RN-VEN-038).
        builder.Property(p => p.CriadoPorUsuarioId).HasColumnName("criado_por");
        builder.Property(p => p.ConfirmadoEm).HasColumnName("dh_confirmacao");
        builder.Property(p => p.FinalizadoEm).HasColumnName("dh_finalizacao");

        // RN-VEN-058 — o cancelamento tem instante e responsável PRÓPRIOS. Ver o comentário em
        // `Pedido.CanceladoEm`: até 19/08/2026 o instante ia para `dh_finalizacao`.
        builder.Property(p => p.CanceladoEm).HasColumnName("dh_cancelamento");
        builder.Property(p => p.CanceladoPorUsuarioId).HasColumnName("cancelado_por_usuario_id");

        // ⚠⚠ OS TRÊS RESPONSÁVEIS SÃO REFERÊNCIA, e não `Guid` solto — 19/08/2026.
        //
        // Até aqui, `criado_por`, `cancelado_por_usuario_id` e `excluido_por_usuario_id` eram
        // colunas `uuid` sem chave nenhuma: nada impedia gravar um id que não existe, e o modelo
        // não sabia que aquelas três colunas falavam de gente. O banco é a primeira camada da
        // ordem de ADR-0009 (`MOD → constraints → ...`), e integridade referencial é literalmente
        // o que ela existe para dar.
        //
        // ⚠ `HasOne<Usuario>()` SEM navegação: `Pedido` não ganha `Usuario CanceladoPor`, porque o
        // domínio não deve poder navegar de um pedido para o agregado de acesso — quem precisa do
        // nome é a LEITURA, e ela o resolve na visão (`vw_pedido_resumo`). A chave existe no banco
        // sem abrir uma porta no domínio.
        //
        // ⚠ `Restrict`, nunca cascata: `RN-GER-002` proíbe apagar registro transacional, e apagar
        // um usuário jamais pode levar pedidos junto. Se um dia alguém tentar remover usuário com
        // pedido cancelado por ele, o banco recusa — e recusar é a resposta certa.
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(p => p.CriadoPorUsuarioId)
            .HasConstraintName("fk_pedido_criado_por")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(p => p.CanceladoPorUsuarioId)
            .HasConstraintName("fk_pedido_cancelado_por")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(p => p.ExcluidoPorUsuarioId)
            .HasConstraintName("fk_pedido_excluido_por")
            .OnDelete(DeleteBehavior.Restrict);

        // RN-VEN-041 — exclusão lógica. O índice inclui a coluna porque TODA consulta de pedido
        // passa pelo filtro global `dh_exclusao IS NULL`.
        builder.Property(p => p.ExcluidoEm).HasColumnName("dh_exclusao");
        builder.Property(p => p.MotivoExclusao).HasMaxLength(500);
        builder.Ignore(p => p.Excluido);

        // RN-GER-036 — a virada e o fuso APLICADOS, copiados da unidade na inserção. São eles
        // que a coluna gerada abaixo lê: sem estarem NA LINHA, a expressão precisaria de outra
        // tabela, que `GENERATED ALWAYS AS` não permite.
        builder.Property(p => p.HoraDeViradaOperacional)
            .HasColumnName("hora_virada_operacional")
            .HasColumnType("interval")
            .IsRequired()
            .HasDefaultValueSql("interval '4 hours'")

            // ⚠⚠ SENTINEL, e sem ele a regra falha em silêncio no caso mais importante.
            //
            // Com `HasDefaultValueSql`, o EF Core omite do INSERT a propriedade cujo valor é igual
            // ao *sentinel* — que por padrão é o default do CLR. Para `TimeSpan` isso é
            // `TimeSpan.Zero`, que é EXATAMENTE a virada de quem fecha antes da meia-noite
            // (RN-GER-036). O resultado: a casa cadastra 00h00, o pedido nasce sem a coluna, o
            // banco aplica `4 hours`, e o dia operacional sai errado sem erro nenhum.
            //
            // `TimeSpan.MinValue` nunca é uma virada válida — o construtor de `Pedido` recebe
            // sempre um valor entre 00h00 e 23h59 —, então usá-lo como sentinel faz toda virada
            // real ser escrita explicitamente.
            .HasSentinel(TimeSpan.MinValue);

        builder.Property(p => p.FusoOperacional)
            .HasColumnName("fuso_operacional")
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("America/Sao_Paulo");

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

/// <summary>
/// <c>RN-GER-036</c> ⚠⚠ — o horário de funcionamento, um registro por dia da semana.
/// <para>
/// Tabela e não coluna JSON: a primeira pergunta de logística que precisar de "quem abre domingo"
/// vira um <c>WHERE</c>, e não um documento a desmontar. Mesmo argumento do endereço em campos.
/// </para>
/// </summary>
public sealed class HorarioDeFuncionamentoConfiguration
    : IEntityTypeConfiguration<HorarioDeFuncionamento>
{
    public void Configure(EntityTypeBuilder<HorarioDeFuncionamento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("unidade_horario", t =>
        {
            // O CHECK valida DOMÍNIO, nunca política (ADR-0009): os dois juntos ou os dois nulos.
            // "Abre e nunca fecha" seria uma casa aberta para sempre — e a virada derivada dela
            // não teria resposta.
            t.HasCheckConstraint(
                "ck_unidade_horario_par",
                "(abre IS NULL AND fecha IS NULL) OR (abre IS NOT NULL AND fecha IS NOT NULL)");
        });

        builder.HasKey(h => h.Id);

        builder.Property(h => h.DiaDaSemana)
            .HasColumnName("dia_semana")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(h => h.Abre).HasColumnType("time");
        builder.Property(h => h.Fecha).HasColumnType("time");

        builder.Ignore(h => h.Fechado);
        builder.Ignore(h => h.AtravessaMeiaNoite);

        // RN-GER-009 — unidade_id como primeira coluna da chave única. Um dia da semana aparece
        // uma vez por casa: duas linhas para terça-feira dariam duas respostas para "que horas
        // abre", e a virada derivada dependeria de qual delas o `MAX` pegasse.
        builder.HasIndex(h => new { h.UnidadeId, h.DiaDaSemana })
            .IsUnique()
            .HasDatabaseName("uq_unidade_horario_dia");
    }
}
