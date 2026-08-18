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
    DbSet<Parametro> Parametros { get; }
    DbSet<Perfil> Perfis { get; }
    DbSet<SessaoDeSuporte> SessoesDeSuporte { get; }

    /// <summary><c>RN-GER-033</c> — os links de uso único (convite e redefinição de senha).</summary>
    DbSet<TokenDeAcesso> TokensDeAcesso { get; }
    DbSet<LogAuditoria> Trilha { get; }
    DbSet<Permissao> Permissoes { get; }
    DbSet<Modulo> Modulos { get; }
    DbSet<ModuloDependencia> DependenciasDeModulo { get; }
    DbSet<UnidadeModulo> ModulosDaUnidade { get; }
    DbSet<ParametroValor> ValoresDeParametro { get; }
    DbSet<Categoria> Categorias { get; }
    DbSet<Produto> Produtos { get; }
    DbSet<Cliente> Clientes { get; }
    DbSet<Pedido> Pedidos { get; }
    DbSet<ItemPedido> ItensDePedido { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
