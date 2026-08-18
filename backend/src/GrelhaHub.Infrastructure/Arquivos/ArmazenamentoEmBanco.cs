using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GrelhaHub.Infrastructure.Arquivos;

/// <summary>
/// Guarda o arquivo no BANCO DE ARQUIVO — ADR-0018, substituindo o volume de disco do ADR-0015.
/// <para>
/// ⚠⚠ <b>A ordem de gravação é a decisão inteira desta classe.</b> Binário primeiro, metadado
/// depois. Não há transação entre dois bancos, então uma das duas falhas vai acontecer um dia — e
/// escolhe-se qual:
/// </para>
/// <list type="bullet">
/// <item><b>metadado sem bytes</b> ⇒ imagem quebrada na vitrine, que o CLIENTE vê;</item>
/// <item><b>bytes sem metadado</b> ⇒ invisível para o sistema, removível por varredura.</item>
/// </list>
/// <para>
/// É a mesma escolha do ADR-0016 para e-mail: quando não há transação, <b>falhe do lado que não
/// mente para o usuário</b>.
/// </para>
/// </summary>
public sealed partial class ArmazenamentoEmBanco(
    IArquivoDb arquivos,
    IAppDbContext app,
    ILogger<ArmazenamentoEmBanco> log) : IArmazenamentoDeArquivo
{
    /// <summary>
    /// O prefixo da referência gravada na coluna do produto e da unidade.
    /// <para>
    /// ⚠ É uma ROTA, e não um caminho de disco. A diferença importa: o conteúdo passa a sair pela
    /// API, onde o metadado é consultado antes — e é isso que faz a exclusão lógica valer também
    /// para a imagem, coisa que o diretório servido pelo Nginx não sabia fazer.
    /// </para>
    /// </summary>
    public const string Prefixo = "/api/v1/arquivos/";

    public async Task<string> GravarAsync(
        Guid unidadeId,
        string pasta,
        FormatoDeImagem formato,
        Stream conteudo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (unidadeId == Guid.Empty)
        {
            throw new ArgumentException(
                "RN-GER-009 — todo arquivo pertence a uma unidade.", nameof(unidadeId));
        }

        // A pasta é escolhida pelo código, nunca pelo usuário. A validação existe para o caso em
        // que um handler futuro a receba de fora sem perceber.
        if (pasta is not ("produto" or "logo"))
        {
            throw new ArgumentException($"Pasta não permitida: {pasta}.", nameof(pasta));
        }

        using var memoria = new MemoryStream();
        await conteudo.CopyToAsync(memoria, cancellationToken);

        var bytes = memoria.ToArray();
        var tipoMime = ArquivoDeImagem.TipoDeConteudoDe(formato);

        var metadado = new Arquivo(unidadeId, pasta, tipoMime, bytes.Length);

        // 1) OS BYTES. Se falhar aqui, nada foi prometido a ninguém.
        arquivos.Binarios.Add(new ArquivoBinario(metadado.Id, bytes, tipoMime));
        await arquivos.GravarAsync(cancellationToken);

        // 2) O METADADO. Se falhar aqui, sobra um binário órfão — invisível e varrível.
        app.Arquivos.Add(metadado);
        await app.SaveChangesAsync(cancellationToken);

        ArquivoGravado(log, metadado.Id, unidadeId, bytes.Length);

        return Prefixo + metadado.Id;
    }

    public bool Existe(string referencia) =>
        IdDe(referencia) is Guid id
        && app.Arquivos.IgnoreQueryFilters().Any(a => a.Id == id && a.ExcluidoEm == null);

    public async Task<ConteudoDeArquivo?> LerAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // ⚠ `IgnoreQueryFilters` DELIBERADO, e a razão precisa estar escrita porque a linha parece
        // um furo no RN-GER-009: a imagem do cardápio é servida a quem NÃO tem sessão — o cliente
        // que abre /c/nome-da-casa no celular. Com o filtro ligado, `UnidadeId` vale `Guid.Empty`
        // no contexto anônimo e NENHUMA imagem apareceria na vitrine.
        //
        // A exposição é a mesma de antes: no ADR-0015 o diretório era servido pelo Nginx sem
        // autenticação nenhuma. O que mudou para melhor é que agora a exclusão lógica vale — e
        // ela é conferida logo abaixo, que o disco não conferia.
        var metadado = await app.Arquivos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, cancellationToken);

        if (metadado is null)
        {
            return null;
        }

        var binario = await arquivos.Binarios
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        // Binário ausente com metadado presente é o cenário que a ordem de gravação evita. Se
        // acontecer, é 404 e não 500: o dado está incompleto, e a resposta honesta é "não tem".
        if (binario is null)
        {
            BinarioAusente(log, id);

            return null;
        }

        return new ConteudoDeArquivo(id, binario.Conteudo, binario.TipoMime);
    }

    /// <summary>
    /// Extrai o id de uma referência <c>/api/v1/arquivos/{id}</c>.
    /// <para>
    /// Devolve <c>null</c> para qualquer outra coisa — inclusive para os caminhos de disco do
    /// ADR-0015 (<c>/arquivos/...</c>), que existem em base gravada antes de 18/08/2026.
    /// </para>
    /// </summary>
    public static Guid? IdDe(string? referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia)
            || !referencia.StartsWith(Prefixo, StringComparison.Ordinal))
        {
            return null;
        }

        return Guid.TryParse(referencia[Prefixo.Length..], out var id) ? id : null;
    }

    [LoggerMessage(EventId = 4210, Level = LogLevel.Information,
        Message = "ADR-0018 — arquivo {Id} gravado para a unidade {UnidadeId} ({Bytes} bytes).")]
    private static partial void ArquivoGravado(
        ILogger logger, Guid id, Guid unidadeId, int bytes);

    [LoggerMessage(EventId = 4211, Level = LogLevel.Error,
        Message = "ADR-0018 — metadado {Id} existe e o binário NÃO. Ordem de gravação violada.")]
    private static partial void BinarioAusente(ILogger logger, Guid id);
}
