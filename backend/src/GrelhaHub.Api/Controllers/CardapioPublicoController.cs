using GrelhaHub.Application.Features.Cardapio.ObterCardapioPublico;
using GrelhaHub.Application.Features.Vendas.ConsultarPedidoPublico;
using GrelhaHub.Application.Features.Vendas.CriarPedidoPublico;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrelhaHub.Api.Controllers;

/// <summary>
/// Cardápio público e pedido online — <c>RN-VEN-036</c>, <c>RN-VEN-037</c>, <c>RN-VEN-038</c>.
/// <para>
/// <b>Todas as rotas são anônimas.</b> O recorte de dados vem do <c>slug</c>, que fixa a
/// unidade no contexto de execução; a partir daí o filtro global do EF Core garante que
/// nada de outro restaurante apareça.
/// </para>
/// <para>
/// Nenhuma resposta daqui carrega custo, CMV ou margem (<c>RN-GER-012</c>) — os DTOs
/// públicos simplesmente não têm esses campos.
/// </para>
/// </summary>
[ApiController]
[Route("api/publico/{slug}")]
[AllowAnonymous]
public sealed class CardapioPublicoController(ISender sender) : ControllerBase
{
    [HttpGet("cardapio")]
    public async Task<ActionResult<CardapioPublicoResponse>> Cardapio(
        string slug, CancellationToken ct) =>
        Ok(await sender.Send(new ObterCardapioPublicoQuery(slug), ct));

    /// <summary>
    /// <c>RN-VEN-038</c> — o pedido entra no canal delivery próprio, no estado
    /// <b>Aberto</b>. Não é confirmado automaticamente: a confirmação é ato do restaurante.
    /// </summary>
    [HttpPost("pedidos")]
    public async Task<ActionResult<PedidoCriadoResponse>> Criar(
        string slug, [FromBody] PedidoPublicoBody body, CancellationToken ct)
    {
        var comando = new CriarPedidoPublicoCommand(
            slug, body.NomeCliente, body.Telefone, body.Observacao, body.Itens);

        return Ok(await sender.Send(comando, ct));
    }

    /// <summary>Acompanhamento pelo cliente. O telefone é exigido junto com o número.</summary>
    [HttpGet("pedidos/{numero:int}")]
    public async Task<ActionResult<PedidoPublicoResponse>> Consultar(
        string slug, int numero, [FromQuery] string telefone, CancellationToken ct) =>
        Ok(await sender.Send(new ConsultarPedidoPublicoQuery(slug, numero, telefone), ct));

    public sealed record PedidoPublicoBody(
        string NomeCliente,
        string Telefone,
        string? Observacao,
        IReadOnlyList<ItemDoPedidoDto> Itens);
}
