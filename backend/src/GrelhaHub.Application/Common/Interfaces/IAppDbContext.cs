using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Domain.Crm;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// Contrato de persistência visto pela camada Application — ADR-0002: a Application depende
/// da abstração, e o provider (Npgsql) é detalhe da Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<Unidade> Unidades { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<Categoria> Categorias { get; }
    DbSet<Produto> Produtos { get; }
    DbSet<Cliente> Clientes { get; }
    DbSet<Pedido> Pedidos { get; }
    DbSet<ItemPedido> ItensDePedido { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
