using GrelhaHub.Domain.Cardapio;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// Onde os arquivos de imagem vivem — ADR-0015, <c>SPEC-VEN-002</c>.
/// <para>
/// <b>A interface existe para que a decisão do ADR-0015 possa ser revista sem tocar handler.</b>
/// Hoje a implementação escreve em volume de disco, porque há uma VPS só (ADR-0006). Quando
/// houver mais de uma máquina, objeto remoto entra implementando isto — e nenhum caso de uso
/// muda.
/// </para>
/// </summary>
public interface IArmazenamentoDeArquivo
{
    /// <summary>
    /// Grava e devolve o <b>caminho público</b> — o que vai para a coluna do banco.
    /// </summary>
    /// <param name="unidadeId">
    /// <c>RN-GER-009</c> chegando ao disco: o caminho carrega a unidade. Sem isso, o caminho de
    /// um arquivo vazado serviria arquivo de qualquer restaurante, e o diretório é público por
    /// desenho.
    /// </param>
    /// <param name="pasta">Subpasta lógica — <c>produto</c> ou <c>logo</c>.</param>
    /// <param name="formato">
    /// O formato <b>já conferido pela assinatura</b> do arquivo. É ele que decide a extensão
    /// gravada: o nome que o usuário enviou nunca toca o sistema de arquivos
    /// (<c>SPEC-VEN-002</c> §4).
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
    bool Existe(string caminhoPublico);
}
