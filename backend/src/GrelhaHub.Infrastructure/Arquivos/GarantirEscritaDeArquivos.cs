namespace GrelhaHub.Infrastructure.Arquivos;

/// <summary>
/// Recusa a partida se a raiz de arquivos não for gravável — ADR-0015, <c>SPEC-VEN-002</c>.
/// <para>
/// <b>Mesmo desenho de <c>ValidadorDeDeclaracaoDeModulo</c> e <c>GarantirAusenciaDeDecimalCru</c>:</b>
/// empurrar a falha para o instante mais cedo e mais ruidoso possível. Sem isto, a única coisa que
/// descobre um volume sem permissão é <b>o primeiro usuário que tenta enviar uma foto</b> — e o que
/// ele vê é "Erro interno. Tente novamente", que é falso: tentar de novo nunca vai funcionar.
/// </para>
/// <para>
/// ⚠ <b>Aconteceu em produção em 18/08/2026.</b> O container roda como UID 1001 (ADR-0006) e a
/// imagem não criava <c>/app/arquivos</c>; o Docker montou o volume novo como <c>root:root</c>, e a
/// API subiu inteira — healthcheck verde, cardápio no ar — com a gravação quebrada. O deploy
/// passou, o gate passou, e o defeito esperou um usuário.
/// </para>
/// <para>
/// A sonda <b>escreve e apaga</b> um arquivo de verdade. Conferir se o diretório existe não
/// bastaria: era exatamente o caso que passava — o diretório existia, montado, e sem permissão de
/// escrita para quem precisava escrever.
/// </para>
/// </summary>
public static class GarantirEscritaDeArquivos
{
    public static void Validar(string raiz)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raiz);

        var absoluta = Path.GetFullPath(raiz);
        var sonda = Path.Combine(absoluta, $".escrita-{Guid.CreateVersion7()}");

        try
        {
            Directory.CreateDirectory(absoluta);

            // `WriteAllBytes` com conteúdo vazio ainda cria e abre o arquivo — é escrita de
            // verdade, e é o que o upload vai fazer.
            File.WriteAllBytes(sonda, []);
            File.Delete(sonda);
        }
        catch (Exception excecao) when (excecao is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException(
                $"ADR-0015 — a raiz de arquivos ({absoluta}) não é gravável pelo processo. " +
                "As imagens de produto e as logos vivem nela (SPEC-VEN-002), e sem escrita o " +
                "upload falharia com 500 para o usuário, um por vez, com a API no ar.\n" +
                "\n" +
                "Em produção isto é o volume `arquivos` com dono errado: o container roda como " +
                "UID 1001, e um volume criado antes da imagem que prepara a pasta nasceu " +
                "root:root. Corrija uma vez — não precisa parar a pilha:\n" +
                "\n" +
                "  docker run --rm -v grelhahub_arquivos:/d alpine chown -R 1001:1001 /d\n" +
                "  docker compose up -d api\n",
                excecao);
        }
    }
}
