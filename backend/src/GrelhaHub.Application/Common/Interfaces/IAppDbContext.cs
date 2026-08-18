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

    /// <summary><c>RN-GER-033</c> — os links de uso único (convite e redefinição de senha).</summary>
    DbSet<TokenDeAcesso> TokensDeAcesso { get; }

    /// <summary>
    /// <c>RN-GER-036</c> — o horário de funcionamento.
    /// <para>
    /// ⚠ Exposto como <c>DbSet</c> porque o horário novo precisa ser <b>inserido explicitamente</b>.
    /// Filho com <c>Id</c> já preenchido, descoberto pela navegação de um pai já rastreado, é
    /// classificado como <c>Modified</c> pelo EF Core — e o `UPDATE` de uma linha que não existe
    /// falha como conflito de concorrência, mandando investigar o lugar errado. É o mesmo defeito
    /// que o log de SQL do harness pegou no CA-9 da SPEC-VEN-001.
    /// </para>
    /// </summary>
    DbSet<HorarioDeFuncionamento> HorariosDeFuncionamento { get; }
    /// <summary>
    /// ADR-0017 — a ponte para o banco de log. A trilha NÃO mora mais aqui.
    /// <para>
    /// ⚠ O interceptador de auditoria grava nesta tabela, dentro da transação da operação, e um
    /// serviço em segundo plano a copia para o banco de log. É o que preserva "ou os dois gravam,
    /// ou nenhum grava" entre dois bancos que não compartilham transação.
    /// </para>
    /// </summary>
    DbSet<OutboxDeAuditoria> OutboxDeAuditoria { get; }
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
