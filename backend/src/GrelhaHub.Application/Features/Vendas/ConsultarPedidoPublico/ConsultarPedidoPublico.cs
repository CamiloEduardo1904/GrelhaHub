using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Domain.Vendas;

namespace GrelhaHub.Application.Features.Vendas.ConsultarPedidoPublico;

/// <summary>Acompanhamento do pedido pelo cliente, sem autenticação.</summary>
[SemModulo("pertence a VEN, mas o gate vive no handler: a unidade só é conhecida depois de resolver o slug, e RN-VEN-037 exige 404 e não 403 para quem está fora")]
[SemPermissao("rota pública anônima: o par número + telefone é o que autoriza a leitura")]
public sealed record ConsultarPedidoPublicoQuery(string Slug, int Numero, string Telefone)
    : IRequest<PedidoPublicoResponse>;

public sealed record PedidoPublicoResponse(
    int Numero,
    string Status,
    decimal Total,
    DateTimeOffset CriadoEm,

    /// <summary>Quem pediu — obrigatório no lançamento (<c>RN-CRM-003</c>).</summary>
    string NomeCliente,

    /// <summary>A observação do pedido. Nula quando não houve.</summary>
    string? Observacao,

    /// <summary><c>RN-GER-026</c> / <c>RN-GER-037</c> — a casa, para a tela ter a mesma capa do cardápio.</summary>
    string NomeFantasia,
    string? UrlLogo,
    string? Endereco,

    IReadOnlyList<ItemPublicoDto> Itens);

public sealed record ItemPublicoDto(string Produto, int Quantidade, decimal PrecoPraticado, decimal Total);

/// <summary>
/// ADR-0019 — convertido ao modelo de leitura em 18/08/2026.
/// <para>
/// ⚠ O que saiu daqui: <c>Include(p =&gt; p.Itens)</c> carregava o agregado inteiro — com todas as
/// invariantes de <c>Pedido</c> montadas em memória — para responder quatro campos e uma lista.
/// O total agora vem somado do banco, pela mesma expressão que o painel usa
/// (<c>vw_pedido_resumo</c>), então painel e acompanhamento não têm como divergir.
/// </para>
/// </summary>
public sealed class ConsultarPedidoPublicoHandler(
    ILeituraDb leitura, IContextoDeExecucao contexto, IModulos modulos)
    : IRequestHandler<ConsultarPedidoPublicoQuery, PedidoPublicoResponse>
{
    public async Task<PedidoPublicoResponse> Handle(ConsultarPedidoPublicoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();

        var unidade = await leitura.Unidades
            .FirstOrDefaultAsync(u => u.Slug == slug && u.Ativa, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        contexto.DefinirUnidadePublica(unidade.Id);

        // RN-GER-017 / SPEC-GER-006 CA-7 — gate de módulo AQUI, e não no pipeline.
        //
        // O behavior roda antes do handler, quando a unidade ainda não está no contexto: ela só
        // é conhecida depois de resolver o slug. E a resposta certa é 404, não 403: para quem
        // está do lado de fora, o restaurante simplesmente não tem cardápio online — 403
        // anunciaria que existe algo ali que não foi contratado.
        if (!await modulos.EstaHabilitadoAsync(unidade.Id, CodigoDeModulo.Vendas, em: null, cancellationToken))
        {
            throw new NaoEncontradoException("Restaurante não encontrado.");
        }

        var telefone = new string((request.Telefone ?? string.Empty).Where(char.IsDigit).ToArray());

        // O número do pedido sozinho é adivinhável. Exigir o telefone junto impede que alguém
        // liste pedidos alheios variando o número — o mínimo para uma rota anônima.
        //
        // ⚠ `TelefoneCliente` vem da visão já ligado ao pedido: o `JOIN` que o handler antigo fazia
        // com uma subconsulta em `cliente` agora está em `vw_pedido_resumo`.
        var pedido = await leitura.PedidosResumo
            .Where(p => p.Numero == request.Numero && p.TelefoneCliente == telefone)
            .OrderByDescending(p => p.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException("Pedido não encontrado.");

        var itens = await leitura.ItensDePedido
            .Where(i => i.PedidoId == pedido.Id)
            .OrderBy(i => i.NomeProduto)
            .ToListAsync(cancellationToken);

        return new PedidoPublicoResponse(
            pedido.Numero,
            ((StatusPedido)pedido.Status).ToString(),
            pedido.Total,
            pedido.CriadoEm,

            // A casa registra o nome no ato do pedido (RN-CRM-003); a coalescência cobre o
            // registro antigo que ficou sem cliente vinculado.
            pedido.NomeCliente ?? "Cliente",
            pedido.Observacao,
            unidade.NomeFantasia,
            unidade.UrlLogo,
            ApresentacaoDaCasa.Resumir(
                unidade.Logradouro, unidade.NumeroDoEndereco, unidade.Bairro,
                unidade.Cidade, unidade.Uf),

            [.. itens.Select(item => new ItemPublicoDto(
                item.NomeProduto, item.Quantidade, item.PrecoPraticado, item.Total))]);
    }
}
