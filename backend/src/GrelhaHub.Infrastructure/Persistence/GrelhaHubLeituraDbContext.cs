using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Leitura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// O contexto de <b>leitura</b> — ADR-0019.
/// <para>
/// Mesmo banco do contexto de escrita, <b>modelo diferente</b>: aqui só existem tipos sem chave
/// mapeados a visões. Não há <c>DbSet</c> de agregado, não há <c>SaveChanges</c> exposto e o
/// rastreamento está desligado no contexto inteiro.
/// </para>
/// <para>
/// ⚠ <b>Tipo sem chave é o que torna a regra imposta pelo framework.</b> O EF Core recusa rastrear
/// ou gravar entidade sem chave — então "não escreva pelo lado da leitura" deixa de depender de
/// alguém lembrar e passa a ser um erro em tempo de execução, na primeira tentativa.
/// </para>
/// <para>
/// ⚠⚠ <b>O filtro de unidade vale aqui como vale na escrita</b> (<c>RN-GER-009</c>). Deixar o
/// recorte por conta do handler seria trocar uma garantia estrutural por disciplina — e é
/// justamente na leitura que o vazamento entre restaurantes seria invisível: ninguém percebe um
/// número inflado por dado de outra casa, porque o número continua plausível. A plataforma
/// (<c>RN-GER-014</c>) atravessa o filtro, como no outro contexto.
/// </para>
/// </summary>
public sealed class GrelhaHubLeituraDbContext(
    DbContextOptions<GrelhaHubLeituraDbContext> options,
    IContextoDeExecucao contexto) : DbContext(options), ILeituraDb
{
    public IQueryable<PedidoResumoLeitura> PedidosResumo => Set<PedidoResumoLeitura>();

    public IQueryable<ItemDePedidoLeitura> ItensDePedido => Set<ItemDePedidoLeitura>();

    public IQueryable<OperacaoDoDiaLeitura> OperacaoPorDia => Set<OperacaoDoDiaLeitura>();

    public IQueryable<ProdutoVendidoLeitura> ProdutosVendidos => Set<ProdutoVendidoLeitura>();

    public IQueryable<AutorLeitura> Autores => Set<AutorLeitura>();

    public IQueryable<UnidadeLeitura> Unidades => Set<UnidadeLeitura>();

    public IQueryable<HorarioLeitura> Horarios => Set<HorarioLeitura>();

    public IQueryable<CategoriaLeitura> Categorias => Set<CategoriaLeitura>();

    public IQueryable<ProdutoDoCardapioLeitura> ProdutosDoCardapio =>
        Set<ProdutoDoCardapioLeitura>();

    public IQueryable<ModuloLeitura> Modulos => Set<ModuloLeitura>();

    public IQueryable<ModuloDependenciaLeitura> DependenciasDeModulo =>
        Set<ModuloDependenciaLeitura>();

    public IQueryable<ModuloDaUnidadeLeitura> ModulosDaUnidade => Set<ModuloDaUnidadeLeitura>();

    public IQueryable<PerfilLeitura> Perfis => Set<PerfilLeitura>();

    public IQueryable<PerfilPermissaoLeitura> PermissoesDePerfil => Set<PerfilPermissaoLeitura>();

    public IQueryable<UsuarioLeitura> Usuarios => Set<UsuarioLeitura>();

    public IQueryable<ParametroLeitura> Parametros => Set<ParametroLeitura>();

    public IQueryable<ParametroValorLeitura> ValoresDeParametro => Set<ParametroValorLeitura>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        // Sem rastreamento no contexto inteiro. Consulta de leitura nunca precisa disso, e o
        // rastreamento cobra memória e tempo por linha materializada.
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        Visao<PedidoResumoLeitura>(modelBuilder, "vw_pedido_resumo");
        Visao<ItemDePedidoLeitura>(modelBuilder, "vw_pedido_item");
        Visao<OperacaoDoDiaLeitura>(modelBuilder, "vw_operacao_dia");
        Visao<ProdutoVendidoLeitura>(modelBuilder, "vw_produto_vendido");
        Visao<AutorLeitura>(modelBuilder, "vw_autor");

        // ADR-0019, segunda leva — o cadastro.
        Visao<UnidadeLeitura>(modelBuilder, "vw_unidade");
        Visao<HorarioLeitura>(modelBuilder, "vw_unidade_horario");
        Visao<CategoriaLeitura>(modelBuilder, "vw_cardapio_categoria");
        Visao<ProdutoDoCardapioLeitura>(modelBuilder, "vw_cardapio_produto");
        Visao<ModuloLeitura>(modelBuilder, "vw_modulo");
        Visao<ModuloDependenciaLeitura>(modelBuilder, "vw_modulo_dependencia");
        Visao<ModuloDaUnidadeLeitura>(modelBuilder, "vw_unidade_modulo");
        Visao<PerfilLeitura>(modelBuilder, "vw_perfil");
        Visao<PerfilPermissaoLeitura>(modelBuilder, "vw_perfil_permissao");
        Visao<UsuarioLeitura>(modelBuilder, "vw_usuario");
        Visao<ParametroLeitura>(modelBuilder, "vw_parametro");
        Visao<ParametroValorLeitura>(modelBuilder, "vw_parametro_valor");
    }

    /// <summary>
    /// Mapeia um modelo de leitura à sua visão, sem chave.
    /// <para>
    /// ⚠⚠ O recorte por unidade só é aplicado a quem declara <see cref="IRecortadoPorUnidade"/>.
    /// A primeira versão filtrava <b>tudo</b>, e isso quebrava as rotas anônimas: o cardápio
    /// público resolve a casa pelo <b>slug</b>, e nesse instante ainda não há unidade no contexto —
    /// o filtro devolveria zero linhas e o cliente leria "restaurante não encontrado" para uma casa
    /// que existe. Mesmo desenho do lado da escrita, onde só <c>IPertenceAUnidade</c> é filtrado.
    /// </para>
    /// </summary>
    private void Visao<T>(ModelBuilder modelBuilder, string visao)
        where T : class
    {
        var entidade = modelBuilder.Entity<T>().HasNoKey().ToView(visao);

        if (typeof(IRecortadoPorUnidade).IsAssignableFrom(typeof(T)))
        {
            entidade.HasQueryFilter(RecorteDeUnidade<T>());
        }
    }

    /// <summary>
    /// <c>RN-GER-009</c> + <c>RN-GER-014</c> — a mesma expressão do contexto de escrita, escrita
    /// aqui de novo porque os dois modelos não compartilham tipo base.
    /// <para>
    /// ⚠ Toda visão <b>precisa</b> expor <c>unidade_id</c> para poder ser filtrada. Uma visão sem
    /// essa coluna não é filtrável, e uma visão não filtrável é um vazamento entre restaurantes
    /// esperando a primeira consulta.
    /// </para>
    /// </summary>
    private System.Linq.Expressions.Expression<Func<T, bool>> RecorteDeUnidade<T>()
        where T : class
    {
        var parametro = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");

        var coluna = typeof(T).GetProperty("UnidadeId")
            ?? throw new InvalidOperationException(
                $"O modelo de leitura {typeof(T).Name} não expõe `UnidadeId`. Toda visão precisa "
                + "dela para ser recortada por unidade (RN-GER-009) — sem a coluna, a consulta "
                + "devolveria o dado de todos os restaurantes.");

        // e => EhPlataforma || e.UnidadeId == UnidadeAtual
        var corpo = System.Linq.Expressions.Expression.OrElse(
            System.Linq.Expressions.Expression.Property(
                System.Linq.Expressions.Expression.Constant(this), nameof(PlataformaAtual)),
            System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Property(parametro, coluna),
                System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Constant(this), nameof(UnidadeAtual))));

        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(corpo, parametro);
    }

    private Guid UnidadeAtual => contexto.UnidadeId;

    /// <summary><c>RN-GER-014</c> — a plataforma enxerga todas as unidades. Ver ADR-0013.</summary>
    private bool PlataformaAtual => contexto.EhPlataforma;
}
