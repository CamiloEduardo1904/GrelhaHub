using GrelhaHub.Application.Features.Cardapio.AlternarDisponibilidade;
using GrelhaHub.Application.Features.Cardapio.ListarCardapioInterno;
using GrelhaHub.Application.Features.Cardapio.SalvarProduto;
using GrelhaHub.Application.Features.Gerais.Autenticar;
using GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;
using GrelhaHub.Application.Features.Gerais.ObterMeuRestaurante;
using GrelhaHub.Application.Features.Vendas.AlterarStatusPedido;
using GrelhaHub.Application.Features.Vendas.ListarPedidos;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrelhaHub.Api.Controllers;

/// <summary>Autenticação e cadastro do restaurante — rotas anônimas.</summary>
[ApiController]
[Route("api/v1/autenticacao")]
public sealed class AutenticacaoController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AutenticarResponse>> Login(
        AutenticarCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));

    /// <summary>Cadastra o restaurante e o seu administrador — <c>SPEC-VEN-001</c>.</summary>
    [HttpPost("cadastrar-restaurante")]
    [AllowAnonymous]
    public async Task<ActionResult<CadastrarRestauranteResponse>> Cadastrar(
        CadastrarRestauranteCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));
}

/// <summary>Dados do restaurante autenticado.</summary>
[ApiController]
[Route("api/v1/restaurante")]
[Authorize]
public sealed class RestauranteController(ISender sender) : ControllerBase
{
    [HttpGet("meu")]
    public async Task<ActionResult<MeuRestauranteResponse>> Meu(CancellationToken ct) =>
        Ok(await sender.Send(new ObterMeuRestauranteQuery(), ct));
}

/// <summary>Cardápio visto pela gestão.</summary>
[ApiController]
[Route("api/v1/cardapio")]
[Authorize]
public sealed class CardapioController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoriaInternaDto>>> Listar(CancellationToken ct) =>
        Ok(await sender.Send(new ListarCardapioInternoQuery(), ct));

    /// <summary>
    /// Cria ou atualiza um produto.
    /// <para>
    /// <c>RN-GER-010</c> — escrita no cardápio é operação de gerência.
    /// <b>Simplificação declarada</b> (SPEC-VEN-001 §8): a autorização usa o nome do perfil.
    /// O modelo definitivo usa permissão granular (<c>SPEC-GER-004</c>).
    /// </para>
    /// </summary>
    [HttpPost("produtos")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<ActionResult<Guid>> SalvarProduto(
        SalvarProdutoCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));

    [HttpPatch("produtos/{id:guid}/disponibilidade")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<IActionResult> AlterarDisponibilidade(
        Guid id, [FromBody] AlterarDisponibilidadeBody body, CancellationToken ct)
    {
        await sender.Send(new AlternarDisponibilidadeCommand(id, body.Disponivel), ct);
        return NoContent();
    }

    public sealed record AlterarDisponibilidadeBody(bool Disponivel);
}

/// <summary>Pedidos recebidos pelo restaurante.</summary>
[ApiController]
[Route("api/v1/pedidos")]
[Authorize]
public sealed class PedidosController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Sem filtro de data, traz o <b>dia operacional</b> corrente — <c>RN-GER-006</c>.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PedidoResumoDto>>> Listar(
        [FromQuery] StatusPedido? status,
        [FromQuery] DateOnly? dataOperacional,
        CancellationToken ct) =>
        Ok(await sender.Send(new ListarPedidosQuery(status, dataOperacional), ct));

    /// <summary>
    /// <c>RN-VEN-018</c> — a confirmação é o gatilho de baixa de estoque e ordem de produção.
    /// Nesta fatia ela apenas muda o estado; estoque e produção entram nas ondas 3 e 7.
    /// </summary>
    [HttpPost("{id:guid}/confirmar")]
    public async Task<ActionResult<string>> Confirmar(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new AlterarStatusPedidoCommand(id, AcaoDePedido.Confirmar, null), ct));

    [HttpPost("{id:guid}/avancar")]
    public async Task<ActionResult<string>> Avancar(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new AlterarStatusPedidoCommand(id, AcaoDePedido.Avancar, null), ct));

    /// <summary><c>RN-VEN-014</c> — cancelamento exige motivo e perfil de gerência.</summary>
    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<ActionResult<string>> Cancelar(
        Guid id, [FromBody] CancelarBody body, CancellationToken ct) =>
        Ok(await sender.Send(new AlterarStatusPedidoCommand(id, AcaoDePedido.Cancelar, body.Motivo), ct));

    public sealed record CancelarBody(string Motivo);
}
