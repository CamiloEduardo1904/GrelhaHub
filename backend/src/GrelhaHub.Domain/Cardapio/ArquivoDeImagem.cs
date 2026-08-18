namespace GrelhaHub.Domain.Cardapio;

/// <summary>Os formatos aceitos por <c>RN-VEN-042</c> e <c>RN-GER-026</c>.</summary>
public enum FormatoDeImagem
{
    Jpeg = 1,
    Png = 2,
}

/// <summary>
/// <c>RN-VEN-042</c> / <c>RN-GER-026</c> — a validação de um arquivo de imagem enviado pelo
/// usuário.
/// <para>
/// <b>A regra diz "formato conferido pelo conteúdo, não pela extensão", e é aqui que isso
/// acontece.</b> Extensão e <c>Content-Type</c> são <i>declarados por quem envia</i>: um
/// executável renomeado para <c>foto.jpg</c>, servido com <c>Content-Type: image/jpeg</c>, passa
/// nos dois sem esforço. Só os primeiros bytes não mentem.
/// </para>
/// <para>
/// Está no <b>domínio</b> e não na infraestrutura porque o limite de 5 MB e a lista de formatos
/// vêm de regra de negócio catalogada — não de capacidade de disco.
/// </para>
/// </summary>
public static class ArquivoDeImagem
{
    /// <summary>
    /// <c>RN-VEN-042</c> — 5 MB.
    /// <para>
    /// <b>Não é <c>PAR-*</c> de propósito:</b> parâmetro existe para o que o restaurante
    /// configura (taxa, tolerância, política de saldo). Teto de upload é restrição de
    /// infraestrutura, igual em toda unidade, e um restaurante que pudesse elevá-lo elevaria o
    /// custo de disco de quem hospeda.
    /// </para>
    /// </summary>
    public const long TamanhoMaximoEmBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Quantos bytes bastam para decidir. O maior prefixo que conferimos é o do PNG, com 8.
    /// </summary>
    public const int BytesDeAssinatura = 8;

    // FF D8 FF — todo JPEG começa assim (SOI + marcador). O quarto byte varia por variante
    // (JFIF, Exif), e conferi-lo recusaria JPEG legítimo de câmera.
    private static readonly byte[] AssinaturaJpeg = [0xFF, 0xD8, 0xFF];

    // 89 50 4E 47 0D 0A 1A 0A — "\x89PNG\r\n\x1a\n". Os bytes de controle no fim existem
    // justamente para detectar arquivo corrompido por transferência em modo texto.
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// O formato que os bytes realmente são, ou <c>null</c> se não é imagem aceita.
    /// </summary>
    public static FormatoDeImagem? FormatoDe(ReadOnlySpan<byte> inicioDoArquivo)
    {
        if (Comeca(inicioDoArquivo, AssinaturaJpeg))
        {
            return FormatoDeImagem.Jpeg;
        }

        if (Comeca(inicioDoArquivo, AssinaturaPng))
        {
            return FormatoDeImagem.Png;
        }

        return null;
    }

    /// <summary>
    /// A extensão canônica do formato — <b>a que vai para o disco</b>. Não a que o usuário
    /// enviou: <c>.jpeg</c> e <c>.JPG</c> viram o mesmo <c>.jpg</c>, e nome de arquivo do
    /// usuário nunca toca o sistema de arquivos (<c>SPEC-VEN-002</c> §4).
    /// </summary>
    public static string ExtensaoDe(FormatoDeImagem formato) => formato switch
    {
        FormatoDeImagem.Jpeg => ".jpg",
        FormatoDeImagem.Png => ".png",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato desconhecido."),
    };

    public static string TipoDeConteudoDe(FormatoDeImagem formato) => formato switch
    {
        FormatoDeImagem.Jpeg => "image/jpeg",
        FormatoDeImagem.Png => "image/png",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato desconhecido."),
    };

    /// <summary>
    /// As extensões que a tela pode oferecer no seletor de arquivo. <b>Conveniência de
    /// interface</b> — a decisão é sempre <see cref="FormatoDe"/>.
    /// </summary>
    public static readonly string[] ExtensoesAceitas = [".jpg", ".jpeg", ".png"];

    private static bool Comeca(ReadOnlySpan<byte> conteudo, ReadOnlySpan<byte> assinatura) =>
        conteudo.Length >= assinatura.Length
        && conteudo[..assinatura.Length].SequenceEqual(assinatura);
}
