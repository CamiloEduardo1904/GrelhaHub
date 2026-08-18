using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.ResumoDaOperacao;

/// <summary>
/// O painel de operação — <c>RN-VEN-049</c> ⚠⚠, <c>RN-VEN-050</c>, <c>RN-VEN-051</c>,
/// <c>RN-GER-038</c>.
/// <para>
/// <b>Permissão própria</b>, e não <c>pedido.visualizar</c>: quem atende o telefone precisa ver o
/// pedido da mesa 4; o faturamento da semana é outra pergunta. Agregado de venda é o passo anterior
/// a margem, e conceder junto abriria por uma porta lateral o que <c>RN-GER-012</c> fecha na porta
/// da frente.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("operacao.visualizar")]
public sealed record ResumoDaOperacaoQuery(
    /// <summary>
    /// <c>RN-VEN-050</c> / <c>RN-GER-006</c> ⚠⚠ — o período é de <b>dia operacional</b>, e o padrão
    /// é o dia corrente. Nunca data de criação: o pedido da 01h30 de domingo é faturamento de
    /// sábado, e é assim que a casa conta a noite dela.
    /// </summary>
    DateOnly? De,
    DateOnly? Ate,

    /// <summary><c>RN-GER-014</c> / <c>RN-GER-016</c> — só a plataforma informa; para o restaurante é ignorado.</summary>
    Guid? UnidadeId = null) : IRequest<ResumoDaOperacaoDto>;

/// <summary>Um dia da série. <c>RN-VEN-050</c> — dia sem pedido vem com zero, não some.</summary>
public sealed record DiaDaOperacaoDto(DateOnly Dia, decimal Faturamento, int Pedidos);

public sealed record ProdutoVendidoDto(string Produto, int Quantidade, decimal Total);

public sealed record ResumoDaOperacaoDto(
    DateOnly De,
    DateOnly Ate,

    /// <summary><c>RN-VEN-049</c> — só pedidos FINALIZADOS.</summary>
    decimal Faturamento,
    int PedidosFinalizados,

    /// <summary>
    /// <c>RN-VEN-049</c> ⚠⚠ — o que ainda está no salão: confirmado, em produção, pronto ou
    /// entregue. <b>Fora</b> do faturamento e <b>dentro</b> da tela.
    /// </summary>
    int PedidosEmAndamento,
    decimal ValorEmAndamento,

    int PedidosCancelados,

    /// <summary><c>RN-VEN-051</c> — <c>null</c>, e não zero, quando não houve venda.</summary>
    decimal? TicketMedio,

    IReadOnlyList<DiaDaOperacaoDto> Serie,
    IReadOnlyList<ProdutoVendidoDto> MaisVendidos);

public sealed class ResumoDaOperacaoHandler(
    IAppDbContext db, IRelogio relogio, IContextoDeExecucao contexto)
    : IRequestHandler<ResumoDaOperacaoQuery, ResumoDaOperacaoDto>
{
    /// <summary>
    /// O teto da série. Não é limite de negócio — é o que impede uma consulta de dez anos de
    /// montar uma série que nenhuma tela desenha e nenhum olho lê.
    /// </summary>
    private const int MaximoDeDias = 366;

    /// <summary>Quantos produtos entram no "mais vendidos". Lista longa vira relatório, e relatório é outra tela.</summary>
    private const int TetoDeProdutos = 8;

    public async Task<ResumoDaOperacaoDto> Handle(
        ResumoDaOperacaoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var de = request.De ?? relogio.DiaOperacionalDeHoje();
        var ate = request.Ate ?? de;

        if (ate < de)
        {
            throw new RegraDeNegocioException(
                "A data final do período não pode ser anterior à inicial.", "RN-GER-006");
        }

        if (ate.DayNumber - de.DayNumber + 1 > MaximoDeDias)
        {
            throw new RegraDeNegocioException(
                $"O período do painel não pode passar de {MaximoDeDias} dias.", "RN-VEN-050");
        }

        var recorte = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        // RN-GER-006 ⚠⚠ — o recorte é por `data_operacional`, a coluna GERADA. Agrupar por
        // `dh_criacao` daria outro número, plausível e errado: a noite de sábado viraria
        // faturamento de domingo.
        var pedidos = await db.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .Where(p => recorte == null || p.UnidadeId == recorte)
            .Where(p => p.DataOperacional >= de && p.DataOperacional <= ate)
            .ToListAsync(cancellationToken);

        // ⚠ O EF Core não traduz acesso a membro de propriedade com conversor de valor
        // (`SPEC-VEN-003` §11): `Sum(p => p.Total.Valor)` não vira SQL. Filtro e recorte vão ao
        // banco; a soma é feita aqui, em memória, sobre o conjunto já reduzido ao período.
        var finalizados = pedidos.Where(p => p.Status == StatusPedido.Finalizado).ToList();

        // RN-VEN-049 ⚠⚠ — o que está no salão fica FORA do faturamento. Contar o que ainda pode
        // ser cancelado dá um número que encolhe sozinho depois, e número que anda para trás sem
        // ninguém mexer destrói a confiança no painel inteiro.
        var emAndamento = pedidos
            .Where(p => p.Status is StatusPedido.Confirmado
                                 or StatusPedido.EmProducao
                                 or StatusPedido.Pronto
                                 or StatusPedido.Entregue)
            .ToList();

        var faturamento = finalizados.Sum(p => p.Total.Valor);

        return new ResumoDaOperacaoDto(
            de,
            ate,
            faturamento,
            finalizados.Count,
            emAndamento.Count,
            emAndamento.Sum(p => p.Total.Valor),
            pedidos.Count(p => p.Status == StatusPedido.Cancelado),

            // RN-VEN-051 — `null` afirma "não houve venda"; zero afirmaria "o ticket foi zero",
            // e são leituras diferentes para quem compara semanas.
            finalizados.Count == 0
                ? null
                : decimal.Round(faturamento / finalizados.Count, 2, MidpointRounding.AwayFromZero),

            MontarSerie(de, ate, finalizados),
            MaisVendidos(finalizados));
    }

    /// <summary>
    /// <c>RN-VEN-050</c> — a faixa inteira, dia a dia, <b>com os zeros</b>.
    /// <para>
    /// ⚠ Devolver só os dias que têm pedido faria a tela desenhar uma série com buracos, e o olho
    /// lê buraco como continuidade: segunda com R$ 0 desaparecida entre domingo e terça vira uma
    /// linha que sobe, quando a casa não vendeu nada. O zero é informação.
    /// </para>
    /// </summary>
    private static List<DiaDaOperacaoDto> MontarSerie(
        DateOnly de, DateOnly ate, List<Pedido> finalizados)
    {
        var porDia = finalizados
            .GroupBy(p => p.DataOperacional)
            .ToDictionary(g => g.Key, g => (Total: g.Sum(p => p.Total.Valor), Quantidade: g.Count()));

        var serie = new List<DiaDaOperacaoDto>();

        for (var dia = de; dia <= ate; dia = dia.AddDays(1))
        {
            var achou = porDia.TryGetValue(dia, out var valores);

            serie.Add(new DiaDaOperacaoDto(
                dia,
                achou ? valores.Total : 0m,
                achou ? valores.Quantidade : 0));
        }

        return serie;
    }

    /// <summary>
    /// O que a casa mais vendeu no período — pelo <b>nome praticado no pedido</b>.
    /// <para>
    /// ⚠ Agrupa por nome e não por produto do cardápio de propósito: <c>RN-VEN-008</c> copia o
    /// nome e o preço na venda, e é essa cópia que o relatório deve refletir. Produto renomeado
    /// depois não reescreve o que foi vendido — pela mesma razão de <c>RN-GER-036</c> não
    /// reclassificar o passado.
    /// </para>
    /// </summary>
    private static List<ProdutoVendidoDto> MaisVendidos(List<Pedido> finalizados) =>
        [.. finalizados
            .SelectMany(p => p.Itens)
            .GroupBy(i => i.NomeProduto)
            .Select(g => new ProdutoVendidoDto(
                g.Key,
                g.Sum(i => i.Quantidade),
                g.Sum(i => i.PrecoPraticado.Valor * i.Quantidade)))
            .OrderByDescending(p => p.Quantidade)
            .ThenBy(p => p.Produto, StringComparer.OrdinalIgnoreCase)
            .Take(TetoDeProdutos)];
}
