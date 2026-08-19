using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrelhaHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>SPEC-GER-003</c> — o catálogo de parâmetros. Global: a definição não pertence a unidade,
/// só o valor pertence.
/// </summary>
public sealed class ParametroConfiguration : IEntityTypeConfiguration<Parametro>
{
    /// <summary>
    /// Texto gravado para cada <see cref="TipoDeValor"/>. Estável por contrato: entra no
    /// <c>CHECK</c>, então renomear o membro do C# não pode invalidar dado já gravado.
    /// </summary>
    internal static readonly Dictionary<TipoDeValor, string> TextoDoTipo = new()
    {
        [TipoDeValor.Percentual] = "percentual",
        [TipoDeValor.Dinheiro] = "dinheiro",
        [TipoDeValor.Dias] = "dias",
        [TipoDeValor.Tempo] = "tempo",
        [TipoDeValor.Intervalo] = "intervalo",
        [TipoDeValor.Enumerado] = "enum",
        [TipoDeValor.Inteiro] = "inteiro",
        [TipoDeValor.Fator] = "fator",
    };

    internal static string Texto(TipoDeValor tipo) => TextoDoTipo[tipo];

    internal static TipoDeValor Tipo(string texto) =>
        TextoDoTipo.First(par => par.Value == texto).Key;

    public void Configure(EntityTypeBuilder<Parametro> builder)
    {
        builder.ToTable("parametro");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Descricao).IsRequired();
        builder.Property(p => p.Dominio).HasMaxLength(100);

        builder.Property(p => p.TipoValor)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(tipo => Texto(tipo), texto => Tipo(texto));

        builder.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("uq_parametro_codigo");
    }
}

/// <summary>
/// <c>SPEC-GER-003</c> — o valor com vigência. **Padrão reusado por `preco_produto`
/// (<c>RN-VEN-010</c>) e `ficha_tecnica` (<c>RN-PRO-002</c>).**
/// <para>
/// A `EXCLUDE` que impede sobreposição não é expressável pelo modelo do EF Core e vive na
/// migration, em SQL. Está declarada lá com o mesmo nome que aparece na spec.
/// </para>
/// </summary>
public sealed class ParametroValorConfiguration : IEntityTypeConfiguration<ParametroValor>
{
    public void Configure(EntityTypeBuilder<ParametroValor> builder)
    {
        builder.ToTable("parametro_valor");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Valor).IsRequired().HasMaxLength(50);
        builder.Property(v => v.Justificativa).HasMaxLength(500);
        builder.Property(v => v.VigenciaInicio).IsRequired();
        builder.Property(v => v.CriadoEm).HasColumnName("dh_criacao");

        // RN-GER-001 — quem alterou o parametro. SPEC-GER-003 CA-6.
        builder.Property(v => v.CriadoPorUsuarioId).HasColumnName("criado_por");

        // ADR-0022 / RN-GER-001 — `criado_por` é REFERÊNCIA, e não `Guid` solto.
        //
        // ⚠ O commit que resolveu isto no `pedido` deixou estas de fora, e a documentação de
        // arquitetura apontou a incoerência: três colunas com o mesmo nome, o mesmo significado e
        // regras diferentes. Coluna que aponta para gente e não tem chave é coluna que aceita um id
        // inexistente sem ninguém recusar.
        //
        // `Restrict` como as do pedido: usuário não se apaga (RN-GER-002), e apagá-lo jamais pode
        // levar junto o registro que ele criou.
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(v => v.CriadoPorUsuarioId)
            .HasConstraintName("fk_parametro_valor_criado_por")
            .OnDelete(DeleteBehavior.Restrict);


        builder.HasOne(v => v.Parametro)
            .WithMany()
            .HasForeignKey(v => v.ParametroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(v => v.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // RN-GER-009 — unidade_id é a PRIMEIRA coluna do índice de busca.
        // Ordem por vigencia_inicio DESC porque a consulta quase sempre quer a vigência mais
        // recente que não passa da data do evento.
        builder.HasIndex(v => new { v.UnidadeId, v.ParametroId, v.VigenciaInicio })
            .HasDatabaseName("ix_parametro_valor_busca")
            .IsDescending(false, false, true);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_parametro_valor_vigencia",
            "vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio"));

        builder.Ignore(v => v.EstaAberta);
    }
}
