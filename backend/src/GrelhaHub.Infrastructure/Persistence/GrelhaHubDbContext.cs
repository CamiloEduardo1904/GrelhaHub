using System.Linq.Expressions;
using System.Reflection;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Crm;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistência do GrelhaHub — PostgreSQL (ADR-0004).
/// <para>
/// Convenções: <c>snake_case</c> no banco (EFCore.NamingConventions), PK <c>uuid</c>,
/// <c>timestamptz</c> em UTC, <c>numeric(12,2)</c> para moeda (<c>RN-GER-003</c>) e
/// <c>numeric(14,4)</c> para quantidade de insumo (<c>RN-GER-004</c>).
/// </para>
/// </summary>
public sealed class GrelhaHubDbContext(
    DbContextOptions<GrelhaHubDbContext> options,
    IContextoDeExecucao contexto)
    : DbContext(options), IAppDbContext
{
    public DbSet<Unidade> Unidades => Set<Unidade>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensDePedido => Set<ItemPedido>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // RN-GER-003 — valores monetários em reais, com duas casas decimais.
        // Declarado uma vez aqui para que nenhuma configuração de entidade precise repetir,
        // e para que ninguém mapeie dinheiro como float por descuido.
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);

        configurationBuilder.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        AplicarFiltroDeUnidade(modelBuilder);
    }

    /// <summary>
    /// <c>RN-GER-009</c> / ADR-0005 — recorte por unidade aplicado automaticamente a toda
    /// entidade que implemente <see cref="IPertenceAUnidade"/>.
    /// <para>
    /// <b>Por que filtro global e não filtro por consulta:</b> uma consulta que escapasse do
    /// recorte exporia dados de outra unidade. O isolamento não pode depender de o
    /// desenvolvedor lembrar de filtrar — é o risco declarado no ADR-0005.
    /// </para>
    /// </summary>
    private void AplicarFiltroDeUnidade(ModelBuilder modelBuilder)
    {
        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IPertenceAUnidade).IsAssignableFrom(tipo.ClrType))
            {
                continue;
            }

            var parametro = Expression.Parameter(tipo.ClrType, "e");

            var corpo = Expression.Equal(
                Expression.Property(parametro, nameof(IPertenceAUnidade.UnidadeId)),
                Expression.Property(Expression.Constant(this), nameof(UnidadeAtual)));

            modelBuilder.Entity(tipo.ClrType)
                .HasQueryFilter(Expression.Lambda(corpo, parametro));
        }
    }

    private Guid UnidadeAtual => contexto.UnidadeId;
}
