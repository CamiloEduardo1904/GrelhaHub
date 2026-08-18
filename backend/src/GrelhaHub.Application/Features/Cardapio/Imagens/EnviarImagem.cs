using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Cardapio.Imagens;

/// <summary>
/// O arquivo recebido, já materializado em memória pela borda HTTP.
/// <para>
/// <b>É <c>byte[]</c> e não <c>Stream</c> de propósito.</b> A assinatura precisa ser lida
/// <i>antes</i> da gravação e o conteúdo gravado depois — sobre um <c>Stream</c> de requisição,
/// que só avança, isso exigiria rebobinar algo que não rebobina. Com o limite de 5 MB
/// (<c>RN-VEN-042</c>) e o teto do ASP.NET recusando o excesso <b>antes</b> de o corpo ser lido,
/// materializar é seguro e simples.
/// </para>
/// </summary>
public sealed record ArquivoRecebido(string NomeOriginal, string TipoDeConteudo, byte[] Conteudo);

public sealed record ImagemSalvaResponse(string Url);

/// <summary>
/// <c>RN-VEN-042</c> — envia a imagem do produto. <c>SPEC-VEN-002</c> CA-1 a CA-9.
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.gerenciar")]
public sealed record EnviarImagemDeProdutoCommand(Guid ProdutoId, ArquivoRecebido Arquivo)
    : IRequest<ImagemSalvaResponse>;

public sealed class EnviarImagemDeProdutoHandler(
    IAppDbContext db, IArmazenamentoDeArquivo armazenamento, IContextoDeExecucao contexto)
    : IRequestHandler<EnviarImagemDeProdutoCommand, ImagemSalvaResponse>
{
    public async Task<ImagemSalvaResponse> Handle(
        EnviarImagemDeProdutoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var formato = ValidadorDeImagem.ConferirOuFalhar(request.Arquivo);

        // O filtro global de unidade faz o recorte: produto de outro restaurante simplesmente
        // não é encontrado, e a resposta é 404 — não 403. RN-VEN-037: dizer "existe, mas não é
        // seu" já é contar que existe.
        var produto = await db.Produtos
            .FirstOrDefaultAsync(p => p.Id == request.ProdutoId, cancellationToken)
            ?? throw new NaoEncontradoException("Produto não encontrado.");

        var caminho = await GravarAsync(
            armazenamento, contexto, "produto", formato, request.Arquivo, cancellationToken);

        // RN-GER-002 — aponta para a nova. NÃO apaga a anterior.
        produto.DefinirImagem(caminho);

        await db.SaveChangesAsync(cancellationToken);

        return new ImagemSalvaResponse(caminho);
    }

    internal static async Task<string> GravarAsync(
        IArmazenamentoDeArquivo armazenamento,
        IContextoDeExecucao contexto,
        string pasta,
        FormatoDeImagem formato,
        ArquivoRecebido arquivo,
        CancellationToken cancellationToken)
    {
        var unidadeId = contexto.UnidadeId;

        if (unidadeId == Guid.Empty)
        {
            throw new NaoEncontradoException("Unidade não encontrada.");
        }

        using var conteudo = new MemoryStream(arquivo.Conteudo, writable: false);

        return await armazenamento.GravarAsync(
            unidadeId, pasta, formato, conteudo, cancellationToken);
    }
}

/// <summary><c>RN-GER-002</c> — limpa a referência. O arquivo permanece no disco.</summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.gerenciar")]
public sealed record RemoverImagemDeProdutoCommand(Guid ProdutoId) : IRequest;

public sealed class RemoverImagemDeProdutoHandler(IAppDbContext db)
    : IRequestHandler<RemoverImagemDeProdutoCommand>
{
    public async Task Handle(RemoverImagemDeProdutoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var produto = await db.Produtos
            .FirstOrDefaultAsync(p => p.Id == request.ProdutoId, cancellationToken)
            ?? throw new NaoEncontradoException("Produto não encontrado.");

        produto.RemoverImagem();

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// <c>RN-GER-026</c> — a logo da unidade, exibida no cardápio público.
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.gerenciar")]
public sealed record EnviarLogoDaUnidadeCommand(ArquivoRecebido Arquivo)
    : IRequest<ImagemSalvaResponse>;

public sealed class EnviarLogoDaUnidadeHandler(
    IAppDbContext db, IArmazenamentoDeArquivo armazenamento, IContextoDeExecucao contexto)
    : IRequestHandler<EnviarLogoDaUnidadeCommand, ImagemSalvaResponse>
{
    public async Task<ImagemSalvaResponse> Handle(
        EnviarLogoDaUnidadeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var formato = ValidadorDeImagem.ConferirOuFalhar(request.Arquivo);

        var unidadeId = contexto.UnidadeId;

        // `unidade` não está sob o filtro global — ELA é a unidade. O recorte é explícito.
        var unidade = await db.Unidades
            .FirstOrDefaultAsync(u => u.Id == unidadeId, cancellationToken)
            ?? throw new NaoEncontradoException("Unidade não encontrada.");

        var caminho = await EnviarImagemDeProdutoHandler.GravarAsync(
            armazenamento, contexto, "logo", formato, request.Arquivo, cancellationToken);

        unidade.DefinirLogo(caminho);

        await db.SaveChangesAsync(cancellationToken);

        return new ImagemSalvaResponse(caminho);
    }
}

/// <summary>
/// A validação de <c>RN-VEN-042</c>, em um lugar só — as duas operações a compartilham, e um
/// segundo caminho de validação é um segundo caminho para escapar dela.
/// </summary>
internal static class ValidadorDeImagem
{
    public static FormatoDeImagem ConferirOuFalhar(ArquivoRecebido? arquivo)
    {
        if (arquivo is null || arquivo.Conteudo.Length == 0)
        {
            throw new RegraDeNegocioException("Envie um arquivo de imagem.", "RN-VEN-042");
        }

        // O teto do ASP.NET já recusa a requisição maior antes de o corpo ser lido. Esta
        // checagem é a rede: se o limite da borda for afrouxado, a regra continua imposta.
        if (arquivo.Conteudo.Length > ArquivoDeImagem.TamanhoMaximoEmBytes)
        {
            throw new RegraDeNegocioException(
                "A imagem não pode passar de 5 MB.", "RN-VEN-042");
        }

        // ⚠ AQUI está a regra. Extensão e Content-Type são declarados por quem envia: um arquivo
        // qualquer renomeado para .jpg, servido como image/jpeg, passa nos dois sem esforço. Só
        // os primeiros bytes não mentem — e é isso que RN-VEN-042 quer dizer com "conferido pelo
        // conteúdo".
        var formato = ArquivoDeImagem.FormatoDe(arquivo.Conteudo);

        if (formato is null)
        {
            throw new RegraDeNegocioException(
                "O arquivo não é uma imagem JPEG ou PNG válida.", "RN-VEN-042");
        }

        return formato.Value;
    }
}
