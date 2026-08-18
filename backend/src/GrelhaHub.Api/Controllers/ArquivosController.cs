using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Features.Gerais.Arquivos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrelhaHub.Api.Controllers;

/// <summary>
/// As imagens, servidas do BANCO DE ARQUIVO — ADR-0018.
/// <para>
/// <b>Anônimo</b>, como o diretório estático que ele substitui (ADR-0015): a foto do produto é
/// vista pelo cliente que abre <c>/c/nome-da-casa</c> sem login algum (<c>RN-VEN-037</c>). O
/// controle está em quem <b>grava</b>, nunca em quem lê.
/// </para>
/// <para>
/// ⚠⚠ <b>Duas representações do mesmo arquivo, e a diferença entre elas importa:</b>
/// </para>
/// <list type="bullet">
/// <item><c>GET /api/v1/arquivos/{id}</c> devolve os BYTES com o tipo certo. É esta que
/// <c>&lt;img src&gt;</c> usa, e é a que o navegador consegue <b>cachear</b>;</item>
/// <item><c>GET /api/v1/arquivos/{id}/base64</c> devolve o <b>data URI</b>, para quando o
/// conteúdo precisa viajar embutido — pré-visualização, exportação, e-mail.</item>
/// </list>
/// <para>
/// Base64 <b>infla o conteúdo em um terço</b> e não passa pelo cache de imagem do navegador. Numa
/// vitrine de vinte produtos abertos no celular do cliente, essa diferença é a primeira coisa que
/// aparece — por isso a rota padrão é a binária, e a base64 é escolha explícita de quem chama.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/arquivos")]
[AllowAnonymous]
public sealed class ArquivosController(ISender sender) : ControllerBase
{
    /// <summary>Os bytes, com o tipo declarado pela assinatura do arquivo.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var arquivo = await sender.Send(new ObterArquivoQuery(id), ct);

        // O nome no disco era gerado (SPEC-VEN-002 §4) e agora nem existe: a identidade do arquivo
        // é o id. Trocar a imagem gera id novo, então o conteúdo de uma URL nunca muda — e dá para
        // cachear longe sem risco de servir foto velha.
        Response.Headers.CacheControl = "public,max-age=604800,immutable";

        return File(arquivo.Conteudo, arquivo.TipoMime);
    }

    /// <summary>O mesmo arquivo como <c>data:</c> URI — ADR-0018.</summary>
    [HttpGet("{id:guid}/base64")]
    public async Task<ActionResult<ArquivoResponse>> Base64(Guid id, CancellationToken ct)
    {
        var arquivo = await sender.Send(new ObterArquivoQuery(id), ct);

        Response.Headers.CacheControl = "public,max-age=604800,immutable";

        return Ok(ObterArquivoHandler.ComoBase64(arquivo));
    }
}
