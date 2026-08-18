using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using GrelhaHub.Application.Common.Interfaces;

namespace GrelhaHub.Integration.Tests.Infraestrutura;

/// <summary>
/// O correio, na suíte — ADR-0016, <c>RN-GER-032</c>.
/// <para>
/// <b>É o único dublê da suíte, e a exceção é declarada.</b> A alternativa seria subir um servidor
/// SMTP no contêiner para provar regras que não são sobre SMTP. O que os testes precisam do e-mail
/// é o <b>conteúdo</b>: o link com o token, que só existe na mensagem — o banco guarda apenas o
/// hash (<c>RN-GER-033</c>), e é justamente essa propriedade que impede o teste de espiar pelo
/// banco.
/// </para>
/// <para>
/// <c>Configurado</c> devolve <c>true</c> de propósito: com <c>false</c>, o enviador de produção
/// descartaria a mensagem com um aviso, e o teste passaria a exercitar o caminho de "sem SMTP" sem
/// perceber.
/// </para>
/// </summary>
internal sealed partial class CaixaDeSaidaDeTeste : IEnviadorDeEmail
{
    private readonly ConcurrentQueue<MensagemDeEmail> _mensagens = new();

    public bool Configurado => true;

    public string BaseDoSite => "https://testes.grelhahub.local";

    public string Cnpj => "63.533.872/0001-35";

    public void Enfileirar(MensagemDeEmail mensagem) => _mensagens.Enqueue(mensagem);

    /// <summary>Limpa entre testes — o `LimparAsync` do ambiente chama isto.</summary>
    public void Limpar() => _mensagens.Clear();

    public IReadOnlyList<MensagemDeEmail> Mensagens => [.. _mensagens];

    public IReadOnlyList<MensagemDeEmail> Para(string email) =>
        [.. _mensagens.Where(m => string.Equals(m.Para, email, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// O token do último e-mail enviado a alguém, extraído do <b>corpo em texto</b>.
    /// <para>
    /// Do texto e não do HTML porque é o corpo que um leitor de tela e um cliente sem imagens veem
    /// — se o token só estivesse no HTML, este teste passaria e o e-mail estaria quebrado para
    /// parte dos destinatários.
    /// </para>
    /// </summary>
    public string? UltimoToken(string email)
    {
        var lista = Para(email);
        var mensagem = lista.Count == 0 ? null : lista[^1];

        if (mensagem is null)
        {
            return null;
        }

        var achado = TokenNoLink().Match(mensagem.CorpoTexto);

        return achado.Success ? achado.Groups[1].Value : null;
    }

    [GeneratedRegex(@"[?&]token=([A-Za-z0-9_\-]+)")]
    private static partial Regex TokenNoLink();
}
