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

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            // Aviso e não exceção: um ambiente de desenvolvimento pode legitimamente não ter
            // suporte configurado. Em produção a ausência aparece no log do deploy.
            NaoConfigurado(logger, SecaoEmail, SecaoSenha);

            return;
        }

        // IgnoreQueryFilters: o usuário de plataforma não tem unidade, e o filtro global não se
        // aplica a `usuario` — mas a consulta é explícita de qualquer forma.
        var jaExiste = await db.Usuarios
            .IgnoreQueryFilters()
            .AnyAsync(usuario => usuario.EhPlataforma, cancellationToken);

        if (jaExiste)
        {
            JaExiste(logger);
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
            email,
            hash.Gerar(senha));

        db.Usuarios.Add(plataforma);
        await db.SaveChangesAsync(cancellationToken);

        Criado(logger, email);
    }

    [LoggerMessage(EventId = 1400, Level = LogLevel.Warning,
        Message = "RN-GER-014 — usuário de plataforma não configurado ({Email} e {Senha} ausentes). Nenhum acesso de suporte existirá nesta instalação.")]
    private static partial void NaoConfigurado(ILogger logger, string email, string senha);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Information,
        Message = "RN-GER-014 — usuário de plataforma já existe; nada a fazer.")]
    private static partial void JaExiste(ILogger logger);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Warning,
        Message = "RN-GER-014 — usuário de plataforma criado ({Email}). Ele enxerga TODAS as unidades (ADR-0013). Troque a senha inicial.")]
    private static partial void Criado(ILogger logger, string email);
}
