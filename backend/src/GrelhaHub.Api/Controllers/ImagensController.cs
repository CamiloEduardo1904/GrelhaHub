using GrelhaHub.Application.Features.Cardapio.Imagens;
using GrelhaHub.Domain.Cardapio;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GrelhaHub.Api.Controllers;

/// <summary>
/// Envio de imagem — <c>RN-VEN-042</c>, <c>RN-GER-026</c>, <c>SPEC-VEN-002</c>.
/// <para>
/// <b>O limite de tamanho é declarado aqui, na borda, e não só no handler.</b>
/// <c>[RequestSizeLimit]</c> faz o Kestrel recusar a requisição <i>antes</i> de o corpo ser lido:
/// validar depois de ler seria aceitar 500 MB na memória para então reclamar do tamanho. A
/// checagem no handler continua existindo como rede — se este atributo for afrouxado, a regra
/// segue imposta.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class ImagensController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Uma folga sobre os 5 MB de <c>RN-VEN-042</c>: o corpo <c>multipart</c> carrega cabeçalho e
    /// delimitador além do arquivo, e apertar exatamente no limite recusaria uma imagem de 5 MB
    /// exatos — que a regra permite. Quem decide o tamanho do <b>arquivo</b> é o handler.
    /// </summary>
    private const long TetoDoCorpo = ArquivoDeImagem.TamanhoMaximoEmBytes + (64 * 1024);

    [HttpPost("produtos/{produtoId:guid}/imagem")]
    [RequestSizeLimit(TetoDoCorpo)]
    public async Task<ActionResult<ImagemSalvaResponse>> EnviarDoProduto(
        Guid produtoId, IFormFile arquivo, CancellationToken ct)
    {
        var recebido = await LerAsync(arquivo, ct);

        return Ok(await sender.Send(new EnviarImagemDeProdutoCommand(produtoId, recebido), ct));
    }

    [HttpDelete("produtos/{produtoId:guid}/imagem")]
    public async Task<IActionResult> RemoverDoProduto(Guid produtoId, CancellationToken ct)
    {
        await sender.Send(new RemoverImagemDeProdutoCommand(produtoId), ct);

        return NoContent();
    }

    [HttpPost("unidade/logo")]
    [RequestSizeLimit(TetoDoCorpo)]
    public async Task<ActionResult<ImagemSalvaResponse>> EnviarLogo(
        IFormFile arquivo, CancellationToken ct)
    {
        var recebido = await LerAsync(arquivo, ct);

        return Ok(await sender.Send(new EnviarLogoDaUnidadeCommand(recebido), ct));
    }

    /// <summary>
    /// Materializa o arquivo. <b>O nome recebido é levado adiante apenas para log e mensagem</b> —
    /// quem decide o nome no disco é o armazenamento, a partir do formato conferido
    /// (<c>SPEC-VEN-002</c> §4).
    /// </summary>
    private static async Task<ArquivoRecebido> LerAsync(IFormFile? arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            // Deixa o handler decidir a mensagem: ela é da aplicação, não do controller (ADR-0009).
            return new ArquivoRecebido(string.Empty, string.Empty, []);
        }

        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, ct);

        return new ArquivoRecebido(
            arquivo.FileName ?? string.Empty,
            arquivo.ContentType ?? string.Empty,
            memoria.ToArray());
    }
}
