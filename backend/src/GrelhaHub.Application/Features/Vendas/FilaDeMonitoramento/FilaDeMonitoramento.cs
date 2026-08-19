using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.FilaDeMonitoramento;

/// <summary>
/// O painel de senha — <c>RN-VEN-054</c> ⚠⚠, <c>RN-VEN-056</c>, <c>SPEC-VEN-004</c>.
/// <para>
/// <b>É a pergunta do meio do serviço, e ela não tinha tela:</b> o painel de operação
/// (<c>RN-VEN-049</c>) responde <b>quanto</b> a casa vendeu, e a lista de pedidos responde <b>o que
/// entrou no dia</b>. Nenhuma das duas responde <b>o que ainda tenho para entregar</b> — e a lista
/// responde pior conforme a noite avança, porque ela cresce.
/// </para>
/// <para>
/// ⚠ <b>Permissão é <c>pedido.visualizar</c>, e não uma permissão nova.</b> <c>RN-GER-038</c>
/// separou <c>operacao.visualizar</c> porque o painel <b>agrega</b> venda, e agregado é o passo
/// anterior a margem (<c>RN-GER-012</c>). A fila não agrega nada: é um recorte da mesma lista, com
/// <b>muito menos</b> campos. Permissão nova aqui protegeria algo que a antiga já mostra por
/// inteiro — e permissão que não protege nada é pior que permissão nenhuma, porque alguém confia
/// nela.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("pedido.visualizar")]
public sealed record FilaDeMonitoramentoQuery(
    /// <summary>
    /// <c>RN-GER-014</c> / <c>RN-GER-016</c> — só a plataforma informa; para o restaurante é
    /// <b>ignorado</b> por <see cref="UnidadeDaConsulta.Recorte"/>.
    /// </summary>
    Guid? UnidadeId = null) : IRequest<IReadOnlyList<PedidoNaFilaDto>>;

/// <summary>
/// O pedido como o painel de senha o recebe — <c>RN-VEN-054</c> ⚠⚠.
/// <para>
/// <b>Cinco campos, e a magreza é o cumprimento da regra.</b> A fila exibe o <b>número</b> e nada
/// mais, então nome, telefone, observação, itens, canal e valor <b>não existem neste contrato</b> —
/// não são nulos, não são mascarados, não estão lá. É <c>CHAPA-05</c> aplicada ao servidor: não há
/// cadeado na tela porque não há campo na resposta.
/// </para>
/// <para>
/// ⚠⚠ <b>A versão anterior devolvia a comanda inteira, e o comentário dela dizia que o nome do
/// cliente ficava "porque é como se chama quem vem buscar".</b> A revisão de 19/08/2026 inverteu
/// isso pelo mesmo argumento visto de outro lado: quem vem buscar é chamado pelo <b>número</b>, que
/// o cliente recebe no fim do pedido e vê no acompanhamento. O nome era a segunda forma de dizer a
/// mesma coisa — e custava seis telas de rolagem com sessenta pedidos.
/// </para>
/// <para>
/// O efeito colateral é o que a fila mais precisava: <b>60 pedidos saem de ~26 KB para ~8 KB</b>, e
/// a segunda consulta — a dos itens — deixou de existir. Uma tela que se atualiza a cada 15 s no
/// tablet da cozinha ganha mais com um campo a menos do que com qualquer otimização depois.
/// </para>
/// </summary>
public sealed record PedidoNaFilaDto(
    /// <summary>
    /// Identidade, e é o que o React usa de chave.
    /// <para>
    /// ⚠ <b>Nunca <c>Numero</c>:</b> ele é identificador HUMANO, reinicia a cada virada e repete
    /// entre unidades. Numa fila que atravessa a virada, dois pedidos diferentes teriam a mesma
    /// chave.
    /// </para>
    /// </summary>
    Guid Id,

    /// <summary><c>RN-GER-009</c> — de qual unidade. A plataforma vê várias casas na mesma resposta.</summary>
    Guid UnidadeId,

    /// <summary>O que o painel exibe. Único por (unidade, dia operacional), e <b>reinicia</b>.</summary>
    int Numero,

    /// <summary>
    /// Um dos quatro estados vivos. A tela usa isto <b>só</b> para escolher a coluna — preparando
    /// ou pronto —, e nunca para escrever texto: o painel não tem rótulo de estado por pedido.
    /// </summary>
    string Status,

    /// <summary>
    /// <c>RN-GER-006</c> ⚠⚠ — o dia operacional do pedido.
    /// <para>
    /// <b>É o campo que impede a leitura errada, e ele só ficou mais necessário com a revisão.</b> A
    /// fila não tem recorte de período: o pedido que atravessou a virada continua nela. Com a
    /// comanda inteira havia nome e itens distinguindo dois pedidos; num painel de senha, o #1 de
    /// ontem e o #1 de hoje são <b>o mesmo algarismo na parede</b>. A tela carrega a data no número
    /// de dia anterior — enquanto houver mais de um dia na fila.
    /// </para>
    /// </summary>
    DateOnly DataOperacional);

/// <summary>
/// ADR-0019 — lê do modelo de <b>leitura</b>, sobre a mesma visão que a lista de pedidos usa.
/// <para>
/// <b>Nenhuma visão nova, e a ausência é decisão:</b> visão se justifica por agregação
/// (<c>vw_operacao_dia</c> existe porque <c>RN-VEN-049</c> mora nela), e a fila não agrega — ela
/// filtra e projeta cinco colunas.
/// </para>
/// </summary>
public sealed class FilaDeMonitoramentoHandler(ILeituraDb leitura, IContextoDeExecucao contexto)
    : IRequestHandler<FilaDeMonitoramentoQuery, IReadOnlyList<PedidoNaFilaDto>>
{
    /// <summary>
    /// <c>RN-VEN-054</c> / <c>RN-VEN-018</c> — os quatro estados de onde o pedido <b>ainda sai</b>.
    /// <para>
    /// ⚠ A lista é escrita pelo <b>enum</b>, e não pelos números 1 a 4: renumerar
    /// <see cref="StatusPedido"/> um dia quebraria a constante literal em silêncio, devolvendo uma
    /// fila plausível e errada. <c>Entregue</c>, <c>Finalizado</c> e <c>Cancelado</c> ficam de fora
    /// — e <c>Entregue</c> fica de fora <b>de propósito</b>: dali ainda se vai para Finalizado, mas
    /// o que falta é fechamento administrativo, não preparo, e a cozinha não tem o que fazer com
    /// ele. É a mesma distinção de <c>pedidoEncerrado</c> em <c>ciclo.ts</c>, com outro corte.
    /// </para>
    /// </summary>
    private static readonly int[] EstadosNaFila =
    [
        (int)StatusPedido.Aberto,
        (int)StatusPedido.Confirmado,
        (int)StatusPedido.EmProducao,
        (int)StatusPedido.Pronto,
    ];

    public async Task<IReadOnlyList<PedidoNaFilaDto>> Handle(
        FilaDeMonitoramentoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ⚠⚠ RN-GER-016 — o recorte passa pelo helper, e não pelo `request.UnidadeId` cru: um
        // restaurante que informasse a unidade do vizinho intersectaria o filtro do handler com o
        // filtro global do contexto e receberia a PRÓPRIA fila vazia. Ver `ListarPedidos`.
        var unidade = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        // ⚠ SEM filtro de período, e a ausência é RN-VEN-054: o que está vivo está na fila. Um
        // pedido aberto às 23h50 que atravessou a virada (RN-GER-006) continua para ser entregue, e
        // sumiria se o recorte fosse o dia operacional corrente.
        //
        // A ordem é FIFO — quem espera há mais tempo primeiro. É a única ordem que a fila pode ter:
        // ordenar pelo número mais recente poria o pedido que acabou de entrar na frente de quem
        // está esperando há vinte minutos. ⚠ E é FIFO por `CriadoEm`, não por `Numero`: o número
        // reinicia a cada virada, então numa fila que atravessa a madrugada ele ordenaria ao
        // contrário — o #1 de hoje viria antes do #58 de ontem, que está esperando desde a véspera.
        //
        // ⚠⚠ A projeção é de CINCO colunas, e isso importa mais do que parece nesta rota: ela é
        // consultada a cada 15 s por cada painel aberto (RN-VEN-055). Trazer item, valor e cliente
        // para desenhar um número seria pagar a noite inteira por um dado que ninguém lê.
        // ⚠ A projeção do BANCO é anônima, e o DTO é montado depois, em memória. Não é preciosismo:
        // `((StatusPedido)p.Status).ToString()` **não tem tradução para SQL** — o EF Core não
        // converte enum para texto dentro de árvore de expressão, e a consulta estoura em execução,
        // não em compilação. O `Select` anônimo mantém as cinco colunas no fio; o nome do estado é
        // problema de quem lê o resultado.
        var linhas = await leitura.PedidosResumo
            .Where(p => unidade == null || p.UnidadeId == unidade)
            .Where(p => EstadosNaFila.Contains(p.Status))
            .OrderBy(p => p.CriadoEm)
            .Select(p => new { p.Id, p.UnidadeId, p.Numero, p.Status, p.DataOperacional })
            .ToListAsync(cancellationToken);

        return
        [
            .. linhas.Select(l => new PedidoNaFilaDto(
                l.Id,
                l.UnidadeId,
                l.Numero,
                ((StatusPedido)l.Status).ToString(),
                l.DataOperacional))
        ];
    }
}
