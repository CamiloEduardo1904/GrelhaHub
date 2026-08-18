using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Cardapio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrelhaHub.Infrastructure.Arquivos;

/// <summary>Configuração do armazenamento — ADR-0015.</summary>
public sealed class OpcoesDeArquivo
{
    public const string Secao = "Arquivos";

    /// <summary>
    /// Diretório raiz no disco. Em contêiner é um volume montado; em teste, uma pasta temporária.
    /// </summary>
    public string Raiz { get; set; } = "arquivos";

    /// <summary>
    /// Prefixo da URL pública. O Nginx serve este caminho como estático, sem interpretador atrás
    /// (ADR-0015).
    /// </summary>
    public string PrefixoPublico { get; set; } = "/arquivos";
}

/// <summary>
/// ADR-0015 — grava no volume de disco e devolve o caminho público.
/// <para>
/// <b>Três defesas, e nenhuma delas é opcional:</b>
/// </para>
/// <list type="number">
/// <item>O nome do arquivo é <b>gerado</b>. O nome enviado pelo usuário é descartado antes de
/// chegar aqui — travessia de diretório (<c>../../</c>) é o ataque mais antigo que existe contra
/// upload, e o único jeito de não ser vulnerável é nunca usar o nome recebido</item>
/// <item>A extensão vem do <b>formato conferido pela assinatura</b>, não da que o usuário
/// escreveu</item>
/// <item>O caminho final é <b>verificado contra a raiz</b> antes da escrita. É defesa em
/// profundidade: se algum dia um dos componentes do caminho passar a vir de fora, a escrita
/// falha em vez de sair da raiz</item>
/// </list>
/// </summary>
public sealed partial class ArmazenamentoEmDisco : IArmazenamentoDeArquivo
{
    private readonly OpcoesDeArquivo _opcoes;
    private readonly ILogger<ArmazenamentoEmDisco> _log;
    private readonly string _raizAbsoluta;

    public ArmazenamentoEmDisco(IOptions<OpcoesDeArquivo> opcoes, ILogger<ArmazenamentoEmDisco> log)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _opcoes = opcoes.Value;
        _log = log;
        _raizAbsoluta = Path.GetFullPath(_opcoes.Raiz);
    }

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
                "RN-GER-009 — o caminho do arquivo carrega a unidade, e ela não pode ser vazia.",
                nameof(unidadeId));
        }

        // A pasta é escolhida pelo código, nunca pelo usuário. A validação existe para o caso em
        // que um handler futuro a receba de fora sem perceber.
        if (pasta is not ("produto" or "logo"))
        {
            throw new ArgumentException($"Pasta não permitida: {pasta}.", nameof(pasta));
        }

        var nome = $"{Guid.CreateVersion7()}{ArquivoDeImagem.ExtensaoDe(formato)}";

        var relativo = Path.Combine(unidadeId.ToString(), pasta, nome);
        var absoluto = Path.GetFullPath(Path.Combine(_raizAbsoluta, relativo));

        // Defesa em profundidade: mesmo com nome gerado, confere que o destino está DENTRO da
        // raiz. Custa uma comparação de string e fecha a categoria inteira do problema.
        if (!absoluto.StartsWith(_raizAbsoluta, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "O caminho calculado sai da raiz de arquivos. Escrita recusada.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absoluto)!);

        await using (var destino = new FileStream(
            absoluto, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            // `CreateNew` e não `Create`: se o nome já existir — o que só aconteceria com colisão
            // de UUID v7 —, a escrita falha em vez de sobrescrever silenciosamente o arquivo de
            // outro registro.
            await conteudo.CopyToAsync(destino, cancellationToken);
        }

        var caminhoPublico = $"{_opcoes.PrefixoPublico}/{relativo.Replace('\\', '/')}";

        ArquivoGravado(_log, caminhoPublico, unidadeId);

        return caminhoPublico;
    }

    public bool Existe(string caminhoPublico)
    {
        var absoluto = Absoluto(caminhoPublico);

        return absoluto is not null && File.Exists(absoluto);
    }

    /// <summary>
    /// Traduz caminho público em caminho de disco, recusando qualquer coisa que saia da raiz.
    /// Devolve <c>null</c> em vez de lançar: quem chama está conferindo existência, e "caminho
    /// suspeito" e "arquivo ausente" levam à mesma resposta.
    /// </summary>
    private string? Absoluto(string caminhoPublico)
    {
        if (string.IsNullOrWhiteSpace(caminhoPublico)
            || !caminhoPublico.StartsWith(_opcoes.PrefixoPublico, StringComparison.Ordinal))
        {
            return null;
        }

        var relativo = caminhoPublico[_opcoes.PrefixoPublico.Length..].TrimStart('/');
        var absoluto = Path.GetFullPath(Path.Combine(_raizAbsoluta, relativo));

        return absoluto.StartsWith(_raizAbsoluta, StringComparison.Ordinal) ? absoluto : null;
    }

    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Information,
        Message = "Arquivo gravado em {Caminho} para a unidade {UnidadeId}.")]
    private static partial void ArquivoGravado(ILogger logger, string caminho, Guid unidadeId);
}
