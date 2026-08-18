using GrelhaHub.Domain.Cardapio;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>O conteúdo de um arquivo, do jeito que sai do armazenamento.</summary>
public sealed record ConteudoDeArquivo(Guid Id, byte[] Conteudo, string TipoMime)
{
    /// <summary>
    /// A representação que o navegador consome direto em <c>src</c> — ADR-0018.
    /// <para>
    /// ⚠ base64 <b>cresce o conteúdo em um terço</b>. É por isso que ela é montada aqui, sob
    /// demanda, e nunca entra numa listagem: uma vitrine de vinte produtos que carregasse a
    /// imagem embutida no JSON entregaria megabytes ao celular do cliente antes do primeiro
    /// preço aparecer na tela.
    /// </para>
    /// </summary>
    public string ComoDataUri() => $"data:{TipoMime};base64,{Convert.ToBase64String(Conteudo)}";
}

/// <summary>
/// Onde os arquivos de imagem vivem — <b>ADR-0018</b>, <c>SPEC-VEN-002</c>.
/// <para>
/// <b>A interface existe para que a decisão possa ser revista sem tocar handler</b>, e foi
/// revista: até 18/08/2026 a implementação escrevia em volume de disco (ADR-0015); hoje escreve
/// no <b>banco de arquivo</b>. Nenhum caso de uso mudou — que era exatamente a aposta.
/// </para>
/// </summary>
public interface IArmazenamentoDeArquivo
{
    /// <summary>
    /// Grava e devolve a <b>referência pública</b> — o que vai para a coluna do banco.
    /// </summary>
    /// <param name="unidadeId">
    /// <c>RN-GER-009</c>: o metadado carrega a unidade. Sem isso, um arquivo pertenceria ao
    /// sistema e não a um restaurante — e nenhuma consulta saberia de quem ele é.
    /// </param>
    /// <param name="pasta">Subpasta lógica — <c>produto</c> ou <c>logo</c>.</param>
    /// <param name="formato">
    /// O formato <b>já conferido pela assinatura</b> do arquivo. É ele que decide o tipo MIME
    /// gravado: o nome que o usuário enviou nunca chega até aqui (<c>SPEC-VEN-002</c> §4).
    /// </param>
    Task<string> GravarAsync(
        Guid unidadeId,
        string pasta,
        FormatoDeImagem formato,
        Stream conteudo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O arquivo existe? Serve ao teste e ao diagnóstico. <b>Não existe remoção na interface</b>,
    /// e é deliberado: <c>RN-GER-002</c> não permite apagar, e um método `RemoverAsync` seria um
    /// convite permanente a chamá-lo.
    /// </summary>
    bool Existe(string referencia);

    /// <summary>
    /// Lê o conteúdo, ou <c>null</c> quando não há — ADR-0018.
    /// <para>
    /// ⚠ Passa pelo METADADO antes dos bytes. É lá que estão a exclusão lógica e a unidade dona;
    /// buscar direto no banco de arquivo entregaria imagem de registro excluído.
    /// </para>
    /// </summary>
    Task<ConteudoDeArquivo?> LerAsync(Guid id, CancellationToken cancellationToken = default);
}
