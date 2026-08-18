using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Os dez módulos do GrelhaHub, vendidos separadamente — <c>RN-GER-017</c>, ADR-0014.
/// <para>
/// O código de três letras é o mesmo do catálogo de regras e das specs: <c>RN-VEN-018</c>
/// pertence a <see cref="Vendas"/>. Manter um vocabulário só evita a pergunta "o módulo de
/// vendas é VEN ou VENDAS?".
/// </para>
/// </summary>
public enum CodigoDeModulo
{
    /// <summary>Núcleo — <c>RN-GER-019</c>. Autenticação, parâmetro, auditoria, unidade.</summary>
    Gerais = 1,

    Vendas = 2,
    Producao = 3,
    Estoque = 4,
    Compras = 5,
    Financeiro = 6,
    Crm = 7,
    Logistica = 8,
    RecursosHumanos = 9,
    Fiscal = 10,
}

/// <summary>
/// Módulo do sistema — catálogo global, como <see cref="Parametro"/>. Não pertence a unidade:
/// o que pertence a unidade é a <see cref="UnidadeModulo">habilitação</see>.
/// </summary>
public sealed class Modulo : Entidade
{
    private Modulo() { }

    public Modulo(CodigoDeModulo codigo, string nome, bool nucleo, short ordem)
    {
        Codigo = codigo;
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome do módulo é obrigatório.", nameof(nome))
            : nome.Trim();
        Nucleo = nucleo;
        Ordem = ordem;
    }

    public CodigoDeModulo Codigo { get; private set; }

    public string Nome { get; private set; } = null!;

    /// <summary><c>RN-GER-019</c> — módulo núcleo está sempre habilitado e não se desabilita.</summary>
    public bool Nucleo { get; private set; }

    public short Ordem { get; private set; }

    /// <summary>
    /// Sigla usada no catálogo de regras e nas specs. Três letras, exceto <c>RH</c> — que tem
    /// duas no catálogo desde a v0.1, e renomear a sigla para uniformizar quebraria a busca por
    /// ID em 6 regras já escritas.
    /// </summary>
    public static string Sigla(CodigoDeModulo codigo) => codigo switch
    {
        CodigoDeModulo.Gerais => "GER",
        CodigoDeModulo.Vendas => "VEN",
        CodigoDeModulo.Producao => "PRO",
        CodigoDeModulo.Estoque => "EST",
        CodigoDeModulo.Compras => "COM",
        CodigoDeModulo.Financeiro => "FIN",
        CodigoDeModulo.Crm => "CRM",
        CodigoDeModulo.Logistica => "LOG",
        CodigoDeModulo.RecursosHumanos => "RH",
        CodigoDeModulo.Fiscal => "FIS",
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, "Módulo desconhecido."),
    };
}

/// <summary>
/// Aresta do grafo de dependências entre módulos — <c>RN-GER-020</c>.
/// <para>
/// <b>Entidade própria e não navegação muitos-para-muitos</b>, de propósito: a relação é
/// auto-referenciada, e o EF Core não resolve sozinho qual chave é qual em
/// <c>Modulo ↔ Modulo</c> — pede configuração explícita. Uma entidade de junção nomeada deixa
/// as duas pontas visíveis no modelo e no SQL, em vez de escondidas em convenção.
/// </para>
/// <para>
/// O grafo é <b>dado de referência</b>, semeado por migration: dez módulos e poucas arestas,
/// escritas à mão e revisáveis.
/// </para>
/// </summary>
public sealed class ModuloDependencia
{
    private ModuloDependencia() { }

    public ModuloDependencia(Guid moduloId, Guid dependeDeId)
    {
        if (moduloId == dependeDeId)
        {
            throw new ArgumentException(
                "RN-GER-020 — um módulo não depende de si mesmo.", nameof(dependeDeId));
        }

        ModuloId = moduloId;
        DependeDeId = dependeDeId;
    }

    public Guid ModuloId { get; private set; }

    public Guid DependeDeId { get; private set; }

    public Modulo? Modulo { get; private set; }

    public Modulo? DependeDe { get; private set; }
}
