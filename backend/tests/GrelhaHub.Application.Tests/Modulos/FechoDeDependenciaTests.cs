using FluentAssertions;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Tests.Modulos;

/// <summary>
/// <c>RN-GER-020</c> — o fecho de dependência, testado <b>pelo contrato</b>.
/// <para>
/// <b>Estes testes existem porque uma mutação sobreviveu.</b> Ao remover o tratamento de
/// <c>Nucleo</c> de <see cref="FechoDeDependencia"/>, nenhum teste de integração caiu — os dois
/// chamadores já colocam GER no conjunto por conta própria, então o ramo nunca era exercitado por
/// eles. Testar pelo caminho do chamador media o que o chamador faz, não o que a função promete.
/// </para>
/// </summary>
public sealed class FechoDeDependenciaTests
{
    // O grafo real, como a migration o semeia. Escrito aqui de propósito: se o grafo do banco
    // mudar, este teste continua verde e o de integração `Catalogo_ExpoeOGrafo` é que cai — e é
    // ele o responsável por afirmar o conteúdo do grafo.
    private static readonly (CodigoDeModulo De, CodigoDeModulo Para)[] Arestas =
    [
        (CodigoDeModulo.Vendas, CodigoDeModulo.Gerais),
        (CodigoDeModulo.Producao, CodigoDeModulo.Vendas),
        (CodigoDeModulo.Estoque, CodigoDeModulo.Producao),
        (CodigoDeModulo.Compras, CodigoDeModulo.Estoque),
        (CodigoDeModulo.Financeiro, CodigoDeModulo.Vendas),
        (CodigoDeModulo.Crm, CodigoDeModulo.Vendas),
        (CodigoDeModulo.Logistica, CodigoDeModulo.Vendas),
        (CodigoDeModulo.Logistica, CodigoDeModulo.RecursosHumanos),
        (CodigoDeModulo.Fiscal, CodigoDeModulo.Vendas),
    ];

    [Fact(DisplayName = "RN-GER-020 — conjunto fechado não tem nada faltando")]
    public void ConjuntoFechado_NadaFalta()
    {
        var (catalogo, arestas, id) = Montar();

        FechoDeDependencia
            .Faltando(catalogo, arestas, [id(CodigoDeModulo.Vendas)])
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "RN-GER-020 — o fecho é TRANSITIVO: EST sozinho exige PRO e VEN")]
    public void Transitivo_ListaACadeiaInteira()
    {
        var (catalogo, arestas, id) = Montar();

        FechoDeDependencia
            .Faltando(catalogo, arestas, [id(CodigoDeModulo.Estoque)])
            .Should().BeEquivalentTo(["VEN", "PRO"],
                "parar na dependência direta obrigaria a descobrir a cadeia uma recusa por vez");
    }

    /// <summary>
    /// O teste que a mutação sobrevivente pediu: <c>conjunto</c> <b>sem</b> GER, e GER
    /// <b>não</b> é listado como faltante porque é núcleo.
    /// </summary>
    [Fact(DisplayName = "RN-GER-019 — o núcleo conta como presente mesmo fora do conjunto")]
    public void Nucleo_NuncaEhListadoComoFaltante()
    {
        var (catalogo, arestas, id) = Montar();

        // VEN depende de GER, e GER NÃO está no conjunto informado.
        var faltando = FechoDeDependencia.Faltando(
            catalogo, arestas, [id(CodigoDeModulo.Vendas)]);

        faltando.Should().NotContain("GER",
            "o núcleo não é opcional: listá-lo mandaria habilitar o que já está habilitado");
        faltando.Should().BeEmpty();
    }

    [Fact(DisplayName = "RN-GER-020 — LOG exige VEN e RH, e RH não vem de VEN")]
    public void DuasDependencias_AmbasSaoExigidas()
    {
        var (catalogo, arestas, id) = Montar();

        FechoDeDependencia
            .Faltando(catalogo, arestas, [id(CodigoDeModulo.Logistica)])
            .Should().BeEquivalentTo(["VEN", "RH"]);
    }

    [Fact(DisplayName = "RN-GER-020 — o resultado sai na ordem do catálogo, não na da busca")]
    public void Ordem_SegueOCatalogo()
    {
        var (catalogo, arestas, id) = Montar();

        // COM → EST → PRO → VEN. A busca em largura os encontra de trás para frente; quem lê a
        // mensagem espera a ordem de contratação.
        FechoDeDependencia
            .Faltando(catalogo, arestas, [id(CodigoDeModulo.Compras)])
            .Should().Equal("VEN", "PRO", "EST");
    }

    [Fact(DisplayName = "RN-GER-020 — grafo com ciclo termina em vez de travar")]
    public void Ciclo_NaoTravaABusca()
    {
        // `ck_modulo_dependencia_ciclo` impede o autolaço, mas não um ciclo de três arestas. Um
        // laço infinito aqui derrubaria o cadastro em vez de recusá-lo.
        var (catalogo, _, id) = Montar();

        ModuloDependencia[] emCiclo =
        [
            new(id(CodigoDeModulo.Vendas), id(CodigoDeModulo.Producao)),
            new(id(CodigoDeModulo.Producao), id(CodigoDeModulo.Estoque)),
            new(id(CodigoDeModulo.Estoque), id(CodigoDeModulo.Vendas)),
        ];

        var calcular = () => FechoDeDependencia.Faltando(
            catalogo, emCiclo, [id(CodigoDeModulo.Vendas)]);

        calcular.Should().NotThrow();
        calcular().Should().BeEquivalentTo(["PRO", "EST"]);
    }

    [Fact(DisplayName = "RN-GER-020 — dependente habilitado impede a saída; desabilitado, não")]
    public void DependentesHabilitados_OlhamApenasOConjunto()
    {
        var (catalogo, arestas, id) = Montar();

        // EST habilitado impede PRO de sair.
        FechoDeDependencia
            .DependentesHabilitados(
                catalogo, arestas,
                [id(CodigoDeModulo.Vendas), id(CodigoDeModulo.Producao), id(CodigoDeModulo.Estoque)],
                id(CodigoDeModulo.Producao))
            .Should().BeEquivalentTo(["EST"]);

        // Sem EST habilitado, PRO sai.
        FechoDeDependencia
            .DependentesHabilitados(
                catalogo, arestas,
                [id(CodigoDeModulo.Vendas), id(CodigoDeModulo.Producao)],
                id(CodigoDeModulo.Producao))
            .Should().BeEmpty();
    }

    private static (List<Modulo> Catalogo, List<ModuloDependencia> Arestas, Func<CodigoDeModulo, Guid> Id)
        Montar()
    {
        // Ordem igual à da migration: GER=1 … FIS=10. A ordenação do resultado depende dela.
        var ordem = new[]
        {
            CodigoDeModulo.Gerais, CodigoDeModulo.Vendas, CodigoDeModulo.Producao,
            CodigoDeModulo.Estoque, CodigoDeModulo.Compras, CodigoDeModulo.Financeiro,
            CodigoDeModulo.Crm, CodigoDeModulo.Logistica, CodigoDeModulo.RecursosHumanos,
            CodigoDeModulo.Fiscal,
        };

        var catalogo = ordem
            .Select((codigo, i) => new Modulo(
                codigo,
                codigo.ToString(),
                nucleo: codigo == CodigoDeModulo.Gerais,
                ordem: (short)(i + 1)))
            .ToList();

        var porCodigo = catalogo.ToDictionary(m => m.Codigo, m => m.Id);

        var arestas = Arestas
            .Select(a => new ModuloDependencia(porCodigo[a.De], porCodigo[a.Para]))
            .ToList();

        return (catalogo, arestas, codigo => porCodigo[codigo]);
    }
}
