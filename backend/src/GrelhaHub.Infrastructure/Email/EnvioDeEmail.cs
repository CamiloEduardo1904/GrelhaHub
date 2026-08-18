using System.Threading.Channels;
using GrelhaHub.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GrelhaHub.Infrastructure.Email;

/// <summary>Configuração do correio — ADR-0016. Vem do <c>.env</c> da VPS (<c>RNF-008</c>).</summary>
public sealed class OpcoesDeEmail
{
    public const string Secao = "Email";

    public string Host { get; set; } = string.Empty;

    /// <summary>587 com STARTTLS. 465 liga o SSL implícito automaticamente.</summary>
    public int Porta { get; set; } = 587;

    public string Usuario { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;

    /// <summary>De onde a mensagem sai. O alias `noreply@` do domínio (ADR-0016).</summary>
    public string Remetente { get; set; } = string.Empty;
    public string NomeDoRemetente { get; set; } = "GrelhaHub";

    /// <summary>
    /// Base pública das telas, para montar o link do e-mail — `https://grelhahub.com`.
    /// <para>
    /// ⚠ Vem de configuração e **nunca** do cabeçalho `Host` da requisição. Montar link com o que
    /// o cliente mandou é o furo clássico de troca de senha: um `Host: servidor-do-atacante`
    /// devolve um e-mail legítimo, do nosso domínio, com um link que entrega o token a outro.
    /// </para>
    /// </summary>
    public string BaseDoSite { get; set; } = string.Empty;

    /// <summary>
    /// <c>RN-GER-032</c> — sem configuração completa não há envio, e isso é estado válido.
    /// <para>
    /// ⚠ <b>A senha entra na conta.</b> Host, usuário e remetente vêm do <c>appsettings</c> e estão
    /// sempre preenchidos; o que falta em desenvolvimento é justamente o segredo. Sem exigi-la
    /// aqui, a fila tentaria enviar em toda máquina de desenvolvimento e falharia na autenticação
    /// — barulho de log no lugar do aviso claro de "não configurado".
    /// </para>
    /// </summary>
    public bool Completo =>
        !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Remetente)
        && !string.IsNullOrWhiteSpace(Senha);
}

/// <summary>
/// A fila em memória — <c>RN-GER-032</c>, ADR-0016.
/// <para>
/// <b>É aqui que "o envio não entra na transação" acontece.</b> `Enfileirar` escreve num canal e
/// volta; quem envia é o <see cref="ServicoDeEnvioDeEmail"/>, em outra thread, depois que a
/// requisição já respondeu.
/// </para>
/// <para>
/// ⚠ <b>A fila é volátil</b>: reinício do contêiner descarta o que não saiu. Aceito e declarado no
/// ADR-0016, porque **todo e-mail nosso é reemitível** — o usuário pede outro link. Quem trouxer
/// para cá um e-mail que não se possa pedir de novo (nota fiscal, recibo) precisa revisar o ADR
/// antes, não depois.
/// </para>
/// </summary>
public sealed class FilaDeEmail
{
    // Ilimitado: a alternativa é descartar mensagem em silêncio quando encher, e o volume previsto
    // é de dezenas por mês (ADR-0016). Um teto que nunca encosta é teto que não protege de nada e
    // ainda esconde o descarte.
    private readonly Channel<MensagemDeEmail> _canal =
        Channel.CreateUnbounded<MensagemDeEmail>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<MensagemDeEmail> Leitor => _canal.Reader;

    public bool Escrever(MensagemDeEmail mensagem) => _canal.Writer.TryWrite(mensagem);
}

/// <summary>
/// <c>RN-GER-032</c> — entrega ao <see cref="FilaDeEmail"/> e volta. Nunca lança.
/// <para>
/// Não lançar é a metade prática da regra: exceção aqui subiria pelo handler e desfaria a operação
/// que já tinha dado certo — usuário criado deixaria de existir porque o correio estava fora do ar.
/// </para>
/// </summary>
public sealed partial class EnviadorDeEmail(
    FilaDeEmail fila, IOptions<OpcoesDeEmail> opcoes, ILogger<EnviadorDeEmail> log) : IEnviadorDeEmail
{
    private readonly OpcoesDeEmail _opcoes = opcoes.Value;

    public bool Configurado => _opcoes.Completo;

    public string BaseDoSite => _opcoes.BaseDoSite.TrimEnd('/');

    public void Enfileirar(MensagemDeEmail mensagem)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        if (!Configurado)
        {
            // Aviso, não exceção: ambiente sem SMTP é legítimo (RN-GER-032). O que não pode é
            // sumir — daí o log com o destinatário e o assunto, que é o que permite saber depois
            // que este e-mail deveria ter saído.
            SemConfiguracao(log, mensagem.Para, mensagem.Assunto);
            return;
        }

        if (!fila.Escrever(mensagem))
        {
            NaoEnfileirado(log, mensagem.Para, mensagem.Assunto);
        }
    }

    [LoggerMessage(EventId = 1600, Level = LogLevel.Warning,
        Message = "RN-GER-032 — sem SMTP configurado; e-mail para {Para} ({Assunto}) NÃO foi enviado. " +
                  "Em produção as chaves Email__* vêm do .env da VPS, repassadas pelo docker-compose.yml.")]
    private static partial void SemConfiguracao(ILogger logger, string para, string assunto);

    [LoggerMessage(EventId = 1601, Level = LogLevel.Error,
        Message = "RN-GER-032 — a fila recusou o e-mail para {Para} ({Assunto}). A operação que o originou NÃO foi desfeita.")]
    private static partial void NaoEnfileirado(ILogger logger, string para, string assunto);
}

/// <summary>
/// Consome a fila e fala SMTP — ADR-0016.
/// <para>
/// Uma conexão por mensagem, e não uma sessão mantida aberta: o volume é de dezenas por mês, e
/// conexão SMTP ociosa é derrubada pelo servidor sem aviso — o custo de reconectar é menor que o
/// de descobrir que a sessão morreu na hora de enviar.
/// </para>
/// </summary>
public sealed partial class ServicoDeEnvioDeEmail(
    FilaDeEmail fila,
    IOptions<OpcoesDeEmail> opcoes,
    ILogger<ServicoDeEnvioDeEmail> log) : BackgroundService
{
    private readonly OpcoesDeEmail _opcoes = opcoes.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var mensagem in fila.Leitor.ReadAllAsync(stoppingToken))
        {
            try
            {
                await EnviarAsync(mensagem, stoppingToken);
                Enviado(log, mensagem.Para, mensagem.Assunto);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Desligando. A mensagem se perde, e é a volatilidade declarada no ADR-0016.
                throw;
            }
            catch (Exception excecao)
            {
                // Sem retentativa, por decisão do ADR-0016: todo e-mail nosso é reemitível, e uma
                // fila com retentativa persistente é uma tabela e um job que este volume não paga.
                // O que não pode faltar é o registro — é por ele que o suporte responde "não
                // chegou".
                FalhaNoEnvio(log, mensagem.Para, mensagem.Assunto, excecao);
            }
        }
    }

    private async Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken)
    {
        using var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(_opcoes.NomeDoRemetente, _opcoes.Remetente));
        mime.To.Add(MailboxAddress.Parse(mensagem.Para));
        mime.Subject = mensagem.Assunto;

        mime.Body = new BodyBuilder
        {
            HtmlBody = mensagem.CorpoHtml,
            TextBody = mensagem.CorpoTexto,
        }.ToMessageBody();

        using var cliente = new SmtpClient();

        // 465 é SSL implícito; qualquer outra porta negocia STARTTLS. `SslOnConnect` numa porta de
        // STARTTLS trava a conexão sem erro legível, e é o engano mais comum de configuração SMTP.
        var seguranca = _opcoes.Porta == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        await cliente.ConnectAsync(_opcoes.Host, _opcoes.Porta, seguranca, cancellationToken);
        await cliente.AuthenticateAsync(_opcoes.Usuario, _opcoes.Senha, cancellationToken);
        await cliente.SendAsync(mime, cancellationToken);
        await cliente.DisconnectAsync(quit: true, cancellationToken);
    }

    [LoggerMessage(EventId = 1602, Level = LogLevel.Information,
        Message = "E-mail enviado para {Para} ({Assunto}).")]
    private static partial void Enviado(ILogger logger, string para, string assunto);

    [LoggerMessage(EventId = 1603, Level = LogLevel.Error,
        Message = "RN-GER-032 — falha ao enviar e-mail para {Para} ({Assunto}). Sem retentativa (ADR-0016): " +
                  "o usuário pede outro link. A operação que originou o e-mail continua válida.")]
    private static partial void FalhaNoEnvio(ILogger logger, string para, string assunto, Exception excecao);
}
