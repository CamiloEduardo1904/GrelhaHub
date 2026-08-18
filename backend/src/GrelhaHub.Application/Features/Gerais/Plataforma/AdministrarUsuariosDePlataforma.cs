using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Plataforma;

/// <summary>
/// Um usuário de plataforma, na visão da própria plataforma — <c>RN-GER-030</c>.
/// <para>
/// Não tem unidade, não tem perfil e não tem CPF: são as três ausências declaradas de
/// <c>RN-GER-014</c> e <c>RN-GER-023</c>, e por isso este DTO não parece com o de usuário de
/// restaurante. Fundir os dois exigiria três campos nulos que nunca são preenchidos aqui.
/// </para>
/// </summary>
public sealed record UsuarioDeSuporteDto(
    Guid Id,
    string Nome,
    string Email,
    bool Ativo,

    /// <summary><c>RF-SEG-005</c> — senha inicial ainda não trocada.</summary>
    bool DeveTrocarSenha,
    DateTimeOffset CriadoEm,

    /// <summary>
    /// <c>RN-GER-031</c> — este é o usuário autenticado. Vem do <b>servidor</b> porque é o servidor
    /// que recusa a auto-inativação; a tela apenas não oferece o botão.
    /// </summary>
    bool EhVoce);

/// <summary>
/// <c>RN-GER-030</c> — os usuários de plataforma.
/// <para>
/// <b>Existe porque sem ela a criação seria um beco:</b> <c>RN-GER-016</c> esconde o usuário de
/// plataforma de toda listagem de restaurante, e <c>ListarUsuariosQuery</c> filtra
/// <c>!EhPlataforma</c> — quem criasse um colega aqui não o veria em tela nenhuma, e não teria como
/// saber se a criação funcionou.
/// </para>
/// </summary>
[SemModulo("operação de plataforma: administra quem opera o produto, não o contrato de um restaurante")]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record ListarUsuariosDeSuporteQuery : IRequest<IReadOnlyList<UsuarioDeSuporteDto>>;

public sealed class ListarUsuariosDeSuporteHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ListarUsuariosDeSuporteQuery, IReadOnlyList<UsuarioDeSuporteDto>>
{
    public async Task<IReadOnlyList<UsuarioDeSuporteDto>> Handle(
        ListarUsuariosDeSuporteQuery request, CancellationToken cancellationToken)
    {
        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var eu = contexto.UsuarioId;

        // IgnoreQueryFilters explícito: `usuario` não tem filtro global por unidade, e a consulta
        // não deve passar a depender disso se ele existir um dia.
        var usuarios = await db.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.EhPlataforma)
            .OrderBy(u => u.Nome)
            .Select(u => new { u.Id, u.Nome, u.Email, u.Ativo, u.DeveTrocarSenha, u.CriadoEm })
            .ToListAsync(cancellationToken);

        return usuarios
            .Select(u => new UsuarioDeSuporteDto(
                u.Id, u.Nome, u.Email, u.Ativo, u.DeveTrocarSenha, u.CriadoEm, u.Id == eu))
            .ToList();
    }
}

/// <summary>
/// <c>RN-GER-030</c> — a plataforma cria outro usuário de plataforma.
/// <para>
/// É o caminho que <c>RN-GER-016</c> sempre previu — "criado apenas por outro usuário de plataforma
/// <b>ou pela carga inicial</b>" — e do qual só a carga inicial existia. A falta dele não era
/// teórica: quando esta instalação subiu sem usuário de plataforma nenhum, não havia porta na
/// interface, e a recuperação exigiu acesso ao servidor (<c>RN-GER-031</c>).
/// </para>
/// <para>
/// <b>Sem unidade e sem perfil</b>, e não é simplificação: o usuário de plataforma não pertence a
/// restaurante (<c>RN-GER-014</c>) e não pode pertencer a um perfil de unidade. <b>Sem CPF</b> pela
/// exceção declarada em <c>RN-GER-023</c> — ele não é pessoa física de restaurante nenhum.
/// </para>
/// <para>
/// <b>Não passa pela matriz de delegação</b> (<c>RN-GER-024</c>): a matriz limita perfil de unidade
/// delegando perfil de unidade, e aqui não há perfil dos dois lados. O que limita é a natureza —
/// só plataforma cria plataforma —, que é o enunciado de <c>RN-GER-016</c>.
/// </para>
/// </summary>
[SemModulo("operação de plataforma: administra quem opera o produto, não o contrato de um restaurante")]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record CriarUsuarioDeSuporteCommand(
    string Nome,
    string Email,

    /// <summary><c>RF-SEG-005</c> — opcional: sem ela vale o convite por e-mail.</summary>
    string? SenhaInicial = null) : IRequest<UsuarioDeSuporteDto>;

public sealed class CriarUsuarioDeSuporteValidator : AbstractValidator<CriarUsuarioDeSuporteCommand>
{
    public CriarUsuarioDeSuporteValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);

        // O mesmo mínimo de TrocarSenhaValidator e RedefinirSenhaValidator. Divergir aqui
        // permitiria criar uma senha que o próprio dono não conseguiria repetir na troca.
        RuleFor(x => x.SenhaInicial)
            .MinimumLength(8).WithMessage("A senha inicial deve ter ao menos 8 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.SenhaInicial));
    }
}

public sealed class CriarUsuarioDeSuporteHandler(
    IAppDbContext db,
    IContextoDeExecucao contexto,
    IHashDeSenha hash,
    ITokensDeAcesso tokens,
    Acesso.EmissorDeLinkDeAcesso convites)
    : IRequestHandler<CriarUsuarioDeSuporteCommand, UsuarioDeSuporteDto>
{
    public async Task<UsuarioDeSuporteDto> Handle(
        CriarUsuarioDeSuporteCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var email = request.Email.Trim().ToLowerInvariant();

        // O e-mail é único em TODO o sistema (SPEC-GER-004), não por unidade: sem
        // `IgnoreQueryFilters` a checagem passaria a depender do filtro global estar desligado para
        // a plataforma, e o erro apareceria como violação de constraint crua no lugar de mensagem.
        if (await db.Usuarios.IgnoreQueryFilters()
                .AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new RegraDeNegocioException("Já existe um usuário com este e-mail.");
        }

        // RF-SEG-005 — `deveTrocarSenha: true`. Duas pessoas conhecem esta senha, e enquanto ela
        // valer o dono da conta não é o único que entra. Difere da carga inicial de propósito:
        // ali a senha vem do .env e não foi entregue a ninguém.
        var usuario = Usuario.CriarDePlataforma(
            request.Nome.Trim(),
            email,
            hash.Gerar(request.SenhaInicial ?? tokens.Gerar().Token),
            deveTrocarSenha: true);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(cancellationToken);

        // RN-GER-032 / RN-GER-033 — o colega de suporte recebe o mesmo convite de primeiro acesso.
        await convites.EmitirAsync(usuario, TipoDeToken.Convite, cancellationToken);

        return new UsuarioDeSuporteDto(
            usuario.Id, usuario.Nome, usuario.Email, usuario.Ativo,
            usuario.DeveTrocarSenha, usuario.CriadoEm, EhVoce: false);
    }
}

/// <summary>
/// <c>RN-GER-031</c> ⚠ — inativa ou reativa um usuário de plataforma.
/// <para>
/// <b>Inativa; não exclui</b> (<c>RN-GER-002</c>). E recusa dois casos que deixariam o produto sem
/// quem o atenda:
/// </para>
/// <list type="number">
/// <item><b>Si mesmo.</b> Quem se inativa perde a sessão no próximo request e não tem como
/// desfazer — só outro usuário de plataforma reativaria.</item>
/// <item><b>O último ativo.</b> Aqui não há desfazer por caminho nenhum: o acesso de plataforma não
/// se recria pela interface, e a carga inicial não sobrescreve usuário existente
/// (<c>SemeadorDePlataforma</c>). Sobraria acesso ao servidor.</item>
/// </list>
/// <para>
/// A contagem do último ativo é feita <b>no banco, na mesma transação</b> da escrita: contar antes
/// e decidir depois é uma corrida entre dois usuários de plataforma inativando um ao outro no mesmo
/// instante, e o resultado seria zero acessos de suporte com as duas operações "bem-sucedidas".
/// </para>
/// </summary>
[SemModulo("operação de plataforma: administra quem opera o produto, não o contrato de um restaurante")]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record AlternarSituacaoDoUsuarioDeSuporteCommand(Guid UsuarioId, bool Ativo)
    : IRequest<UsuarioDeSuporteDto>;

public sealed class AlternarSituacaoDoUsuarioDeSuporteHandler(
    IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<AlternarSituacaoDoUsuarioDeSuporteCommand, UsuarioDeSuporteDto>
{
    public async Task<UsuarioDeSuporteDto> Handle(
        AlternarSituacaoDoUsuarioDeSuporteCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => u.Id == request.UsuarioId && u.EhPlataforma, cancellationToken)
            ?? throw new NaoEncontradoException("Usuário de plataforma não encontrado.");

        if (request.Ativo)
        {
            usuario.Reativar();
        }
        else
        {
            // RN-GER-031 — si mesmo. Checado antes da contagem porque a mensagem é diferente: aqui
            // existe saída (outro usuário reativa), e dizer "é o último" seria falso.
            if (usuario.Id == contexto.UsuarioId)
            {
                throw new RegraDeNegocioException(
                    "Você não pode inativar o seu próprio acesso de plataforma. " +
                    "Peça a outro usuário de plataforma.",
                    "RN-GER-031");
            }

            var outrosAtivos = await db.Usuarios
                .IgnoreQueryFilters()
                .CountAsync(
                    u => u.EhPlataforma && u.Ativo && u.Id != request.UsuarioId,
                    cancellationToken);

            if (outrosAtivos == 0)
            {
                throw new RegraDeNegocioException(
                    "Este é o último acesso de plataforma ativo. Inativá-lo deixaria a " +
                    "instalação sem suporte e sem caminho de volta pela interface: crie outro " +
                    "usuário de plataforma antes.",
                    "RN-GER-031");
            }

            usuario.Inativar();
        }

        await db.SaveChangesAsync(cancellationToken);

        return new UsuarioDeSuporteDto(
            usuario.Id, usuario.Nome, usuario.Email, usuario.Ativo,
            usuario.DeveTrocarSenha, usuario.CriadoEm,
            EhVoce: usuario.Id == contexto.UsuarioId);
    }
}
