using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using MediatR;

namespace GrelhaHub.Application.Features.Gerais.Arquivos;

/// <summary>
/// A imagem entregue como <b>data URI base64</b> — ADR-0018.
/// <para>
/// ⚠ <c>ConteudoBase64</c> já vem com o prefixo <c>data:image/png;base64,</c>: é o que o
/// navegador consome direto em <c>src</c>, sem o frontend precisar montar a string — e montar
/// string de MIME em duas camadas é como se erra o tipo de uma delas.
/// </para>
/// </summary>
public sealed record ArquivoResponse(
    Guid Id, string TipoMime, int TamanhoEmBytes, string ConteudoBase64);

/// <summary>
/// ADR-0018 — lê o arquivo. <b>Anônima</b>, e por decisão antiga: a imagem do cardápio é vista
/// pelo cliente que não tem login (<c>RN-VEN-037</c>), e no ADR-0015 ela era servida por um
/// diretório do Nginx sem autenticação nenhuma. A exposição não mudou; o que mudou é que agora a
/// exclusão lógica vale, porque o metadado é consultado antes dos bytes.
/// <para>
/// ⚠ Não depende de <c>IAppDbContext</c> nem de <c>ILeituraDb</c>: quem sabe atravessar os dois
/// bancos é <see cref="IArmazenamentoDeArquivo"/>, e a ordem "metadado antes dos bytes" é regra
/// dele. Duplicar a travessia aqui seria um segundo caminho para escapar da checagem de exclusão.
/// </para>
/// </summary>
[SemPermissao("a imagem do cardápio é pública (RN-VEN-037), como era o diretório do Nginx que ela substitui: quem lê não tem sessão. A exclusão lógica do metadado é o que decide se ela existe")]
[SemModulo("a imagem é servida a quem não tem sessão nem unidade no contexto: exigir contrato de módulo aqui apagaria a foto do cardápio do cliente, e o controle de RN-VEN-042 está em quem GRAVA")]
public sealed record ObterArquivoQuery(Guid Id) : IRequest<ConteudoDeArquivo>;

public sealed class ObterArquivoHandler(IArmazenamentoDeArquivo armazenamento)
    : IRequestHandler<ObterArquivoQuery, ConteudoDeArquivo>
{
    public async Task<ConteudoDeArquivo> Handle(
        ObterArquivoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ⚠ Devolve os BYTES, e não a base64. Quem escolhe a representação é a borda: converter
        // aqui obrigaria a rota binária a desfazer a conversão que a consulta acabou de fazer —
        // um terço de memória a mais, ida e volta, em toda foto de cardápio aberta no celular.
        return await armazenamento.LerAsync(request.Id, cancellationToken)
            ?? throw new NaoEncontradoException("Arquivo não encontrado.");
    }

    /// <summary>A resposta da rota base64, montada a partir do conteúdo lido.</summary>
    public static ArquivoResponse ComoBase64(ConteudoDeArquivo conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        return new ArquivoResponse(
            conteudo.Id, conteudo.TipoMime, conteudo.Conteudo.Length, conteudo.ComoDataUri());
    }
}
