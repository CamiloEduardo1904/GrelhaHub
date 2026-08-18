using System.Reflection;
using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;

namespace GrelhaHub.Application.Tests.Arquitetura;

/// <summary>
/// ADR-0019 — a separação leitura/escrita, <b>verificada</b>.
/// <para>
/// <b>Por que um teste e não uma convenção escrita no guia:</b> o projeto declarou "CQRS" no
/// ADR-0002 e entregou CQS por dois meses sem ninguém notar. A divergência entre o documento e o
/// código não apareceu porque nada a media. Separação combinada em reunião volta em três meses;
/// separação verificada, não.
/// </para>
/// <para>
/// É o mesmo desenho de <c>GarantirAusenciaDeDecimalCru</c>, que recusa o modelo na partida quando
/// alguém guarda dinheiro num tipo solto — e da mesma família de
/// <c>OpcoesDeEmailTests</c>. Guarda por reflexão, e não lista escrita à mão: lista envelhece com o
/// primeiro caso de uso novo, e quem o acrescenta não sabe que devia atualizá-la.
/// </para>
/// </summary>
public sealed class SeparacaoDeCqrsTests
{
    private static readonly Assembly Aplicacao = typeof(ILeituraDb).Assembly;

    /// <summary>Todo handler de request do MediatR, com o tipo do request que ele atende.</summary>
    private static IEnumerable<(Type Handler, Type Request)> Handlers()
    {
        foreach (var tipo in Aplicacao.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var contrato in tipo.GetInterfaces().Where(EhHandlerDeRequest))
            {
                yield return (tipo, contrato.GetGenericArguments()[0]);
            }
        }
    }

    private static bool EhHandlerDeRequest(Type contrato) =>
        contrato.IsGenericType
        && (contrato.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
            || contrato.GetGenericTypeDefinition() == typeof(IRequestHandler<>));

    /// <summary>
    /// O que o handler recebe pelo construtor primário — é por aí que a dependência entra.
    /// </summary>
    private static IEnumerable<Type> Dependencias(Type handler) =>
        handler.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType);

    /// <summary>
    /// ⚠⚠ <b>A CATRACA.</b> As consultas ainda não convertidas ao modelo de leitura, nomeadas uma a
    /// uma. A conversão do ADR-0019 é grande — 16 consultas, cada uma com a sua visão — e foi feita
    /// por partes; esta lista é o que impede "por partes" de virar "pela metade, para sempre".
    /// <para>
    /// Ela só anda para <b>baixo</b>, e os dois testes abaixo garantem isso: um recusa infrator que
    /// <b>não</b> esteja na lista (a dívida não cresce), o outro recusa nome na lista que <b>já</b>
    /// foi convertido (a lista não apodrece). Acrescentar um nome aqui para calar o teste exige
    /// escrever o nome — e alguém vai perguntar na revisão.
    /// </para>
    /// <para>
    /// <b>Inventário aberto em 18/08/2026 com 15 nomes.</b> Quando chegar a zero, os dois testes
    /// viram um só e esta lista sai.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> PendentesDeConversao =
    [
        "ConsultarPedidoPublicoHandler",
        "ConsultarTrilhaHandler",
        "ListarCardapioInternoHandler",
        "ListarCatalogoDeModulosHandler",
        "ListarEstabelecimentosHandler",
        "ListarModulosDaUnidadeHandler",
        "ListarPedidosHandler",
        "ListarPerfisHandler",
        "ListarSessoesDeSuporteHandler",
        "ListarUsuariosDeSuporteHandler",
        "ListarUsuariosHandler",
        "ObterCardapioPublicoHandler",
        "ObterMeuRestauranteHandler",
        "ObterOperacaoDaUnidadeHandler",
        "ObterParametroHandler",
    ];

    [Fact(DisplayName = "ADR-0019 — nenhuma Query NOVA alcança o contexto de escrita")]
    public void Query_NaoAlcancaEscrita()
    {
        var infratores = QueriesQueAbremEscrita();

        var novos = infratores.Except(PendentesDeConversao).OrderBy(n => n).ToArray();

        novos.Should().BeEmpty(
            "consulta que abre o contexto de escrita carrega agregado que existe para garantir "
            + "regra na gravação — e volta a somar em memória o que a visão deveria somar no banco "
            + "(ADR-0009: a aplicação é o ÚLTIMO recurso). Use `ILeituraDb`. Infratores fora do "
            + $"inventário: {string.Join(", ", novos)}");
    }

    [Fact(DisplayName = "ADR-0019 — o inventário de conversão não apodrece")]
    public void Inventario_SoAndaParaBaixo()
    {
        var infratores = QueriesQueAbremEscrita();

        var jaConvertidos = PendentesDeConversao.Except(infratores).OrderBy(n => n).ToArray();

        // ⚠ Sem este teste a lista viraria papel de parede: nomes convertidos ficariam nela para
        // sempre, e a próxima pessoa leria "15 pendentes" quando restassem 3. Converteu, tira o
        // nome — e o teste é quem cobra.
        jaConvertidos.Should().BeEmpty(
            "estes handlers já usam o modelo de leitura e continuam listados como pendentes. "
            + $"Remova-os de `PendentesDeConversao`: {string.Join(", ", jaConvertidos)}");
    }

    private static string[] QueriesQueAbremEscrita() =>
        [.. Handlers()
            .Where(h => h.Request.Name.EndsWith("Query", StringComparison.Ordinal))
            .Where(h => Dependencias(h.Handler).Contains(typeof(IAppDbContext)))
            .Select(h => h.Handler.Name)];

    [Fact(DisplayName = "ADR-0019 — handler de Command NÃO alcança o contexto de leitura")]
    public void Command_NaoAlcancaLeitura()
    {
        var infratores = Handlers()
            .Where(h => h.Request.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(h => Dependencias(h.Handler).Contains(typeof(ILeituraDb)))
            .Select(h => h.Handler.Name)
            .OrderBy(n => n)
            .ToArray();

        // ⚠ O sentido inverso importa tanto quanto o outro, e é menos óbvio: escrita que decide
        // regra lendo de uma visão está decidindo sobre uma projeção — que pode não refletir o que
        // a própria transação acabou de mudar. Regra se decide sobre o agregado.
        infratores.Should().BeEmpty(
            "comando decide regra sobre o AGREGADO, nunca sobre uma visão de leitura");
    }

    [Fact(DisplayName = "ADR-0019 — nenhuma Query devolve entidade de domínio")]
    public void Query_NaoDevolveEntidadeDeDominio()
    {
        var dominio = typeof(Domain.Vendas.Pedido).Assembly;

        var infratores = Handlers()
            .Where(h => h.Request.Name.EndsWith("Query", StringComparison.Ordinal))
            .Select(h => new
            {
                Handler = h.Handler.Name,
                Retorno = h.Handler.GetInterfaces()
                    .First(EhHandlerDeRequest)
                    .GetGenericArguments()
                    .Skip(1)
                    .FirstOrDefault(),
            })
            .Where(h => h.Retorno is not null && VazaDominio(h.Retorno!, dominio))
            .Select(h => h.Handler)
            .OrderBy(n => n)
            .ToArray();

        // Devolver a entidade do domínio para fora da aplicação entrega o modelo de escrita à
        // tela: qualquer renomeação de propriedade do agregado passa a ser mudança de contrato de
        // API, e RN-GER-012 (custo não sai da API sem permissão) perde o ponto único de controle.
        infratores.Should().BeEmpty(
            "consulta devolve DTO ou modelo de leitura — nunca entidade de domínio");
    }

    /// <summary>Olha o tipo e, se for coleção, o que ele carrega dentro.</summary>
    private static bool VazaDominio(Type tipo, Assembly dominio)
    {
        if (tipo.Assembly == dominio)
        {
            return true;
        }

        return tipo.IsGenericType
            && tipo.GetGenericArguments().Any(a => VazaDominio(a, dominio));
    }

    [Fact(DisplayName = "ADR-0019 — ILeituraDb não expõe SaveChanges")]
    public void Leitura_NaoExpoeSaveChanges()
    {
        // A ausência é a garantia. Se alguém acrescentar o método "por conveniência", este teste é
        // o que explica por que ele não deveria existir.
        typeof(ILeituraDb).GetMethods()
            .Should().NotContain(m => m.Name.StartsWith("SaveChanges", StringComparison.Ordinal),
                "o lado da leitura não grava, e a interface é onde essa regra é imposta — "
                + "disciplina não sobrevive a uma sexta-feira");
    }
}
