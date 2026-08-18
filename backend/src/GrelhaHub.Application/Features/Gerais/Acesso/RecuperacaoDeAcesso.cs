using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Acesso;

/// <summary>
/// Emite o link e monta o e-mail — <c>RN-GER-033</c>, <c>RN-GER-032</c>, ADR-0016.
/// <para>
/// Serviço e não handler porque **três caminhos precisam dele**: o "esqueci minha senha", a criação
/// de usuário de restaurante e a criação de usuário de plataforma. Repetir a emissão em cada um
/// seria repetir a invalidação dos anteriores — e é justamente a parte que, esquecida em um dos
/// caminhos, deixa dois links válidos ao mesmo tempo sem ninguém notar.
/// </para>
/// </summary>
public sealed class EmissorDeLinkDeAcesso(
    IAppDbContext db, ITokensDeAcesso tokens, IEnviadorDeEmail email, IRelogio relogio)
{
    /// <summary>
    /// <c>PAR-GER-002</c> — 60 minutos para redefinição.
    /// <para>
    /// ⚠ Cópia do parâmetro no código, como <c>PAR-VEN-005</c> no cliente: <c>parametro_valor</c> é
    /// por unidade (<c>SPEC-GER-003</c>) e este fluxo é **anônimo** — no instante da solicitação
    /// ainda não se sabe de que unidade é a pessoa, e para o usuário de plataforma não há unidade
    /// nenhuma. Enquanto não houver parâmetro de escopo global, a constante fica aqui nomeando o
    /// que copia.
    /// </para>
    /// </summary>
    public static readonly TimeSpan ValidadeDaRedefinicao = TimeSpan.FromMinutes(60);

    /// <summary><c>PAR-GER-003</c> — 72 horas para o convite: quem foi cadastrado hoje pode só abrir o e-mail depois do fim de semana.</summary>
    public static readonly TimeSpan ValidadeDoConvite = TimeSpan.FromHours(72);

    public bool CorreioConfigurado => email.Configurado;

    public async Task EmitirAsync(
        Usuario usuario, TipoDeToken tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var agora = relogio.Agora;

        // RN-GER-033 — emitir invalida os anteriores do mesmo tipo. Sem isto, pedir o link duas
        // vezes deixaria dois válidos, e o primeiro e-mail — que pode ter ido para a caixa errada
        // — continuaria abrindo a conta.
        var anteriores = await db.TokensDeAcesso
            .Where(t => t.UsuarioId == usuario.Id
                     && t.Tipo == tipo
                     && t.UsadoEm == null
                     && t.InvalidadoEm == null)
            .ToListAsync(cancellationToken);

        foreach (var anterior in anteriores)
        {
            anterior.Invalidar(agora);
        }

        var validade = tipo == TipoDeToken.Convite ? ValidadeDoConvite : ValidadeDaRedefinicao;
        var (token, hash) = tokens.Gerar();

        db.TokensDeAcesso.Add(new TokenDeAcesso(usuario.Id, tipo, hash, agora + validade));

        await db.SaveChangesAsync(cancellationToken);

        // Só depois de gravar. Enfileirar antes seria prometer um link que uma falha de gravação
        // deixaria sem lastro — e o usuário receberia um e-mail cujo link não funciona.
        email.Enfileirar(Montar(usuario, tipo, token));
    }

    /// <summary>
    /// O que este método decide, e o que ele delega.
    /// <para>
    /// Decide aqui o que depende do <b>domínio</b>: qual tela recebe o token e por quanto tempo ele
    /// vale. A aparência vai inteira para <see cref="ModeloDeEmailDeAcesso"/> — separação que
    /// existe para que a prévia do e-mail possa ser gerada sem banco nem SMTP, com o mesmo código
    /// que sai para o cliente.
    /// </para>
    /// </summary>
    private MensagemDeEmail Montar(Usuario usuario, TipoDeToken tipo, string token)
    {
        var caminho = tipo == TipoDeToken.Convite ? "definir-senha" : "redefinir-senha";
        var link = $"{email.BaseDoSite}/{caminho}?token={token}";

        var validade = tipo == TipoDeToken.Convite
            ? $"{ValidadeDoConvite.TotalHours:0} horas"
            : $"{ValidadeDaRedefinicao.TotalMinutes:0} minutos";

        return ModeloDeEmailDeAcesso.Montar(
            usuario.Email, usuario.Nome, tipo, link, validade, email.Empresa, email.Cnpj);
    }
}

/// <summary>
/// <c>RN-GER-034</c> ⚠ — "esqueci minha senha".
/// <para>
/// <b>A resposta é a mesma exista ou não a conta</b>, e por isso este comando não devolve nada e
/// nunca lança por usuário não encontrado. Dizer "não encontramos este e-mail" entrega a quem sonda
/// a lista de quem é cliente — mesma escolha de <c>RN-GER-027</c> no login.
/// </para>
/// </summary>
[SemModulo("recuperação de acesso: antecede a sessão, e quem a pede não está autenticado")]
[SemPermissao("anônima por natureza — quem esqueceu a senha não tem como provar quem é")]
public sealed record SolicitarRedefinicaoDeSenhaCommand(string Email) : IRequest;

public sealed class SolicitarRedefinicaoDeSenhaValidator
    : AbstractValidator<SolicitarRedefinicaoDeSenhaCommand>
{
    public SolicitarRedefinicaoDeSenhaValidator()
    {
        // Formato inválido é o ÚNICO erro que esta rota devolve, e não vaza nada: qualquer um sabe
        // que "abc" não é e-mail.
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class SolicitarRedefinicaoDeSenhaHandler(
    IAppDbContext db, EmissorDeLinkDeAcesso emissor)
    : IRequestHandler<SolicitarRedefinicaoDeSenhaCommand>
{
    public async Task Handle(
        SolicitarRedefinicaoDeSenhaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email.Trim().ToLowerInvariant();

        // IgnoreQueryFilters: não há unidade no contexto — ninguém está autenticado.
        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // RN-GER-034 — as três recusas saem por aqui, em silêncio e com a mesma resposta:
        // conta que não existe, usuário inativo (RN-RH-006) e unidade inativa (RN-GER-027).
        // Emitir link para quem não consegue autenticar seria mandar a pessoa a um beco.
        if (usuario is null || !usuario.Ativo)
        {
            return;
        }

        if (!usuario.EhPlataforma && (usuario.Unidade is null || !usuario.Unidade.Ativa))
        {
            return;
        }

        await emissor.EmitirAsync(usuario, TipoDeToken.RedefinicaoDeSenha, cancellationToken);
    }
}

/// <summary>
/// <c>RN-GER-033</c> / <c>RN-GER-035</c> — consome o link e define a senha.
/// <para>
/// Serve tanto ao convite quanto à redefinição: para o sistema é a mesma operação — provar posse do
/// e-mail e escolher uma senha. O que muda é a validade, e ela já está gravada no token.
/// </para>
/// </summary>
[SemModulo("recuperação de acesso: quem usa o link ainda não tem sessão")]
[SemPermissao("anônima por natureza — a autorização é a posse do link")]
public sealed record DefinirSenhaComTokenCommand(string Token, string NovaSenha) : IRequest;

public sealed class DefinirSenhaComTokenValidator : AbstractValidator<DefinirSenhaComTokenCommand>
{
    public DefinirSenhaComTokenValidator()
    {
        RuleFor(x => x.Token).NotEmpty();

        // O mesmo mínimo de TrocarSenhaValidator e RedefinirSenhaValidator. Três lugares com o
        // mesmo número é dívida conhecida; divergir seria pior — senha aceita aqui e recusada lá.
        RuleFor(x => x.NovaSenha)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A senha deve ter ao menos 8 caracteres.");
    }
}

public sealed class DefinirSenhaComTokenHandler(
    IAppDbContext db, ITokensDeAcesso tokens, IHashDeSenha hash, IRelogio relogio)
    : IRequestHandler<DefinirSenhaComTokenCommand>
{
    public async Task Handle(
        DefinirSenhaComTokenCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agora = relogio.Agora;

        // Procura pelo HASH: o token em claro nunca esteve no banco (RN-GER-033).
        //
        // ⚠ O hash é calculado ANTES da consulta, e não dentro do `Where`: chamada de método não
        // atravessa a árvore de expressão — o EF Core não saberia traduzi-la e a consulta falharia
        // em tempo de execução, não de compilação.
        var procurado = tokens.HashDe(request.Token);

        var registro = await db.TokensDeAcesso
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.Hash == procurado, cancellationToken);

        // Uma mensagem para os quatro casos — inexistente, expirado, já usado, invalidado. Quem
        // recebe o link não ganha nada sabendo qual deles é, e a distinção diria a um estranho que
        // o token existiu.
        if (registro is null || !registro.Vigente(agora))
        {
            throw new RegraDeNegocioException(
                "Este link não é mais válido. Peça um novo.", "RN-GER-033");
        }

        var usuario = registro.Usuario
            ?? throw new RegraDeNegocioException("Este link não é mais válido. Peça um novo.");

        if (!usuario.Ativo)
        {
            throw new RegraDeNegocioException("Este link não é mais válido. Peça um novo.");
        }

        // RN-GER-035 — a senha foi escolhida pelo próprio dono: ninguém mais a conhece, e a troca
        // obrigatória perde a razão de existir. `TrocarSenha` já limpa a pendência; `RedefinirSenha`
        // é que a reativa, e é o caminho de quando um terceiro escolhe (RN-GER-028).
        usuario.TrocarSenha(hash.Gerar(request.NovaSenha));

        registro.Consumir(agora);

        await db.SaveChangesAsync(cancellationToken);
    }
}
