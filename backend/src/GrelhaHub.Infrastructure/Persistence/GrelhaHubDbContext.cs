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
    public DbSet<Parametro> Parametros => Set<Parametro>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<SessaoDeSuporte> SessoesDeSuporte => Set<SessaoDeSuporte>();
    public DbSet<LogAuditoria> Trilha => Set<LogAuditoria>();
    public DbSet<Permissao> Permissoes => Set<Permissao>();
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<ModuloDependencia> DependenciasDeModulo => Set<ModuloDependencia>();
    public DbSet<UnidadeModulo> ModulosDaUnidade => Set<UnidadeModulo>();
    public DbSet<ParametroValor> ValoresDeParametro => Set<ParametroValor>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensDePedido => Set<ItemPedido>();

    /// <summary>
    /// Convenções de tipo — <c>SPEC-GER-001</c> §4. Declaradas <b>uma vez</b> aqui para que
    /// nenhuma configuração de entidade precise repeti-las, e para que as 40 tabelas seguintes
    /// as herdem sem redecidir.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        ConvencoesDeTipo.Aplicar(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        AplicarChaveGeradaPelaAplicacao(modelBuilder);
        AplicarFiltroDeUnidade(modelBuilder);
        GarantirAusenciaDeDecimalCru(modelBuilder);
    }

    /// <summary>
    /// <c>RN-GER-003</c> e <c>RN-GER-004</c> — recusa o modelo se alguma propriedade mapeada
    /// for <c>decimal</c> cru.
    /// <para>
    /// <b>Falha na partida, não na gravação.</b> Sem esta guarda, uma quantidade declarada como
    /// <c>decimal</c> não daria erro em lugar nenhum: seria mapeada com a precisão que
    /// sobrasse e <c>0,1563</c> viraria <c>0,16</c> silenciosamente — em uma ficha técnica de
    /// 20 insumos o desvio entra direto no CMV, e o número continua plausível. Este é o modo
    /// de falha que o ADR-0009 manda empurrar para a camada mais externa possível.
    /// </para>
    /// <para>
    /// Dinheiro é <see cref="Dinheiro"/>; quantidade de insumo é <see cref="Quantidade"/>.
    /// Contagem de unidade vendida é <c>int</c>, e não entra aqui.
    /// </para>
    /// </summary>
    private static void GarantirAusenciaDeDecimalCru(ModelBuilder modelBuilder)
    {
        var cruas = modelBuilder.Model.GetEntityTypes()
            .SelectMany(tipo => tipo.GetDeclaredProperties()
                .Where(propriedade => propriedade.ClrType == typeof(decimal)
                                   || propriedade.ClrType == typeof(decimal?))
                .Select(propriedade => $"{tipo.ClrType.Name}.{propriedade.Name}"))
            .ToList();

        if (cruas.Count > 0)
        {
            throw new InvalidOperationException(
                $"SPEC-GER-001 — propriedade mapeada como decimal cru: {string.Join(", ", cruas)}. " +
                "Use Dinheiro (RN-GER-003, duas casas) ou Quantidade (RN-GER-004, quatro casas). " +
                "Um decimal solto não distingue R$ 12,00 de 12 gramas e herda a precisão errada em silêncio.");
        }
    }

    /// <summary>
    /// Declara que a chave primária é gerada pela <b>aplicação</b>, nunca pelo banco.
    /// <para>
    /// É a verdade do modelo: <see cref="Entidade.Id"/> recebe
    /// <c>Guid.CreateVersion7()</c> no inicializador (ADR-0004). Mas a convenção do EF Core
    /// para chave <c>Guid</c> é <c>ValueGeneratedOnAdd</c>, e é daí que vem um defeito
    /// silencioso.
    /// </para>
    /// <para>
    /// <b>O modo de falha, encontrado pelo CA-9 de SPEC-VEN-001:</b> ao acrescentar um filho
    /// novo a um agregado <i>já rastreado</i> — um item a um pedido lido do banco — o EF
    /// decide entre <c>Added</c> e <c>Modified</c> olhando a chave. Com
    /// <c>ValueGeneratedOnAdd</c> e chave já preenchida, ele conclui "isto já existe" e emite
    /// <c>UPDATE ... WHERE id = @id</c> em uma linha que nunca foi inserida. O resultado é
    /// <c>DbUpdateConcurrencyException</c> — "esperava afetar 1 linha, afetou 0" — que parece
    /// conflito de concorrência e não é.
    /// </para>
    /// <para>
    /// Com <c>ValueGeneratedNever</c> o EF passa a confiar no estado do grafo e insere. O
    /// fluxo atual não expunha o defeito porque o único caminho que chama
    /// <c>AdicionarItem</c> cria o pedido junto — grafo inteiro <c>Added</c>. O lançamento de
    /// item da onda 4 bateria nele de frente.
    /// </para>
    /// </summary>
    private static void AplicarChaveGeradaPelaAplicacao(ModelBuilder modelBuilder)
    {
        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(Entidade).IsAssignableFrom(tipo.ClrType))
            {
                continue;
            }

            modelBuilder.Entity(tipo.ClrType)
                .Property(nameof(Entidade.Id))
                .ValueGeneratedNever();
        }
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
            var deUnidade = typeof(IPertenceAUnidade).IsAssignableFrom(tipo.ClrType);
            var excluivel = typeof(IExcluivelLogicamente).IsAssignableFrom(tipo.ClrType);

            if (!deUnidade && !excluivel)
            {
                continue;
            }

            var parametro = Expression.Parameter(tipo.ClrType, "e");

            var daUnidade = deUnidade
                ? Expression.Equal(
                    Expression.Property(parametro, nameof(IPertenceAUnidade.UnidadeId)),
                    Expression.Property(Expression.Constant(this), nameof(UnidadeAtual)))
                : null;

            // RN-GER-014 ⚠⚠ / ADR-0013 — o usuário de plataforma enxerga TODAS as unidades.
            //
            // É a única exceção ao recorte, e o ADR-0013 revisa a invariante do ADR-0005 por
            // causa dela: antes, esquecer um filtro era inofensivo porque este aqui cobria;
            // agora, um defeito que marque EhPlataforma indevidamente vaza dado de um cliente
            // para outro. Por isso EhPlataforma deriva de UMA claim, e há teste de integração
            // provando que usuário comum não a obtém.
            var ehPlataforma = Expression.Property(
                Expression.Constant(this), nameof(PlataformaAtual));

            Expression corpo = deUnidade
                ? Expression.OrElse(daUnidade!, ehPlataforma)
                : Expression.Constant(true);

            // RN-VEN-041 / RN-GER-002 — exclusão é LÓGICA: o registro excluído desaparece de toda
            // consulta sem que nenhum handler precise escrever `Where(p => p.ExcluidoEm == null)`.
            //
            // Mesmo motivo do filtro de unidade: o que depende de o desenvolvedor lembrar é o que
            // falta justamente quando importa — e aqui o esquecimento faria um pedido excluído
            // voltar a compor faturamento.
            if (excluivel)
            {
                var naoExcluido = Expression.Equal(
                    Expression.Property(parametro, nameof(IExcluivelLogicamente.ExcluidoEm)),
                    Expression.Constant(null, typeof(DateTimeOffset?)));

                corpo = deUnidade ? Expression.AndAlso(corpo, naoExcluido) : naoExcluido;
            }

            modelBuilder.Entity(tipo.ClrType)
                .HasQueryFilter(Expression.Lambda(corpo, parametro));
        }
    }

    private Guid UnidadeAtual => contexto.UnidadeId;

    /// <summary><c>RN-GER-014</c> — lido pelo filtro global. Ver ADR-0013.</summary>
    private bool PlataformaAtual => contexto.EhPlataforma;
}
