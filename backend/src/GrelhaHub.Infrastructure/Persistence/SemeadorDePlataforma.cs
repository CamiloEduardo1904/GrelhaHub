using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// Cria o **usuário de suporte da plataforma** — <c>RN-GER-014</c>, <c>RN-GER-016</c>, ADR-0013.
/// <para>
/// <b>Não é seed de migration, de propósito.</b> Migration é história versionada e vai para o
/// repositório; a senha do usuário que enxerga <b>todos os clientes</b> não pode estar lá
/// (<c>RNF-008</c>). Ela vem de configuração — no deploy, do <c>.env</c> da VPS com permissão 600.
/// </para>
/// <para>
/// <b>Idempotente.</b> Roda a cada deploy e não faz nada se o usuário já existir. Não atualiza
/// senha de usuário existente: um deploy que sobrescrevesse a senha do suporte seria uma porta
/// para quem tivesse acesso ao arquivo de configuração antigo.
/// </para>
/// <para>
/// ⚠ <b>Todo caminho de saída daqui é um log.</b> A semeadura roda sem ninguém olhando, e o
/// sintoma de qualquer falha é o mesmo — "não consigo entrar" numa tela de login que, por
/// <c>RN-GER-027</c>, não pode dizer o motivo. Se este método sair em silêncio, o operador fica
/// sem nada para ler. Foi o que aconteceu em 17/08/2026: o aviso existia, mas nomeava as duas
/// chaves de configuração sem dizer qual delas faltava, nem de onde elas deveriam ter vindo.
/// </para>
/// </summary>
public static partial class SemeadorDePlataforma
{
    public const string SecaoEmail = "Plataforma:Email";
    public const string SecaoSenha = "Plataforma:SenhaInicial";
    public const string SecaoNome = "Plataforma:Nome";

    public static async Task SemearAsync(
        GrelhaHubDbContext db,
        IHashDeSenha hash,
        IConfiguration configuracao,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(logger);

        var email = configuracao[SecaoEmail]?.Trim().ToLowerInvariant();
        var senha = configuracao[SecaoSenha];

        var faltando = new List<string>(2);

        if (string.IsNullOrWhiteSpace(email))
        {
            faltando.Add(SecaoEmail);
        }

        if (string.IsNullOrWhiteSpace(senha))
        {
            faltando.Add(SecaoSenha);
        }

        if (faltando.Count > 0)
        {
            // Aviso e não exceção: um ambiente de desenvolvimento pode legitimamente não ter
            // suporte configurado. Em produção a ausência aparece no log do deploy.
            //
            // O aviso nomeia QUAL chave faltou e a cadeia inteira de onde ela vem. Sem a cadeia,
            // quem lê confere o .env, encontra a variável lá, e não tem como suspeitar do elo do
            // meio — o repasse no docker-compose.yml, que é onde ela se perdeu.
            NaoConfigurado(logger, string.Join(" e ", faltando));

            return;
        }

        // A senha é usada COMO ESTÁ, sem Trim: recortar caractere de uma senha em silêncio é
        // gravar um hash que ninguém consegue reproduzir digitando o que está no arquivo. Mas
        // borda em branco quase nunca é intencional — é CRLF de .env editado no Windows —, e o
        // sintoma seria "a senha certa não entra". Então avisa, e não corrige.
        if (senha! != senha!.Trim())
        {
            SenhaComBordaEmBranco(logger, senha!.Length, senha!.Trim().Length);
        }

        // IgnoreQueryFilters: o usuário de plataforma não tem unidade, e o filtro global não se
        // aplica a `usuario` — mas a consulta é explícita de qualquer forma.
        var jaExistente = await db.Usuarios
            .IgnoreQueryFilters()
            .Where(usuario => usuario.EhPlataforma)
            .Select(usuario => usuario.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (jaExistente is not null)
        {
            // O e-mail existente vai no log porque a confusão previsível é configurar um e-mail
            // novo e tentar entrar com ele: o semeador não cria o segundo, e a senha configurada
            // não vale para o primeiro.
            JaExiste(logger, jaExistente);
            return;
        }

        // O e-mail é único globalmente (ver UsuarioConfiguration): se já houver um usuário de
        // restaurante com este e-mail, criar o de plataforma violaria a unicidade — e a falha
        // precisa ser legível, não uma violação de constraint crua.
        var emailEmUso = await db.Usuarios
            .IgnoreQueryFilters()
            .AnyAsync(usuario => usuario.Email == email, cancellationToken);

        if (emailEmUso)
        {
            throw new InvalidOperationException(
                $"RN-GER-014 — o e-mail {email}, configurado para o usuário de plataforma, já " +
                "pertence a um usuário de restaurante. Escolha outro em Plataforma:Email.");
        }

        var nome = configuracao[SecaoNome]?.Trim();

        var plataforma = Usuario.CriarDePlataforma(
            string.IsNullOrWhiteSpace(nome) ? "Suporte GrelhaHub" : nome,
            email!,
            hash.Gerar(senha!));

        db.Usuarios.Add(plataforma);
        await db.SaveChangesAsync(cancellationToken);

        Criado(logger, email!);
    }

    [LoggerMessage(EventId = 1400, Level = LogLevel.Warning,
        Message = "RN-GER-014 — usuário de plataforma não configurado: {Chaves} sem valor. " +
                  "Nenhum acesso de suporte existirá nesta instalação. No deploy estas chaves " +
                  "vêm de PLATAFORMA_EMAIL e PLATAFORMA_SENHA_INICIAL no .env da VPS, " +
                  "REPASSADAS pelo bloco environment do serviço api em docker-compose.yml — " +
                  "confira os dois elos, não só o .env.")]
    private static partial void NaoConfigurado(ILogger logger, string chaves);

    [LoggerMessage(EventId = 1403, Level = LogLevel.Warning,
        Message = "RN-GER-014 — a senha configurada tem espaço ou quebra de linha nas bordas " +
                  "({Bruto} caracteres, {Limpo} sem as bordas). Ela foi usada COMO ESTÁ: o " +
                  "login vai exigir os mesmos caracteres. Quase sempre é CRLF de um .env " +
                  "editado no Windows.")]
    private static partial void SenhaComBordaEmBranco(ILogger logger, int bruto, int limpo);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Information,
        Message = "RN-GER-014 — usuário de plataforma já existe ({Email}); nada a fazer. A " +
                  "senha configurada foi IGNORADA: este semeador nunca sobrescreve senha de " +
                  "usuário existente.")]
    private static partial void JaExiste(ILogger logger, string email);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Warning,
        Message = "RN-GER-014 — usuário de plataforma criado ({Email}). Ele enxerga TODAS as unidades (ADR-0013). Troque a senha inicial.")]
    private static partial void Criado(ILogger logger, string email);
}
