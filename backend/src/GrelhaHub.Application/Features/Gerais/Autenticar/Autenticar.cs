using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;

namespace GrelhaHub.Application.Features.Gerais.Autenticar;

[SemModulo("autenticação antecede qualquer módulo: sem token não há unidade cujo contrato consultar")]
[SemPermissao("login antecede autorização: é ele que emite as permissões")]
public sealed record AutenticarCommand(string Email, string Senha) : IRequest<AutenticarResponse>;

public sealed record AutenticarResponse(
    string Token,
    DateTimeOffset ExpiraEm,
    Guid UsuarioId,
    string Nome,

    /// <summary>
    /// <c>RF-SEG-005</c> — o e-mail de quem entrou.
    /// <para>
    /// A tela de troca de senha no primeiro acesso precisa dele: trocar a senha <b>não</b> derruba
    /// a pendência do token que já está na mão (o claim <c>trocar_senha</c> viaja nele), então a
    /// interface reautentica com a senha nova para receber um token limpo. Sem o e-mail na sessão,
    /// ela teria de pedir ao usuário que o digitasse de novo, dois campos depois de ele já ter
    /// entrado.
    /// </para>
    /// </summary>
    string Email,

    string Perfil,
    Guid? UnidadeId,
    string Restaurante,
    string Slug,

    /// <summary><c>RN-GER-014</c> — verdadeiro para o suporte da plataforma, nunca para o restaurante.</summary>
    bool EhPlataforma,

    /// <summary>
    /// <c>RN-GER-010</c> — as permissoes do perfil, para a interface esconder o que o usuario nao
    /// pode fazer. <b>Esconder na tela e conveniencia</b>; o gate real esta no pipeline.
    /// </summary>
    IReadOnlyList<string> Permissoes,

    /// <summary><c>RF-SEG-005</c> — se verdadeiro, so a rota de troca de senha responde.</summary>
    bool DeveTrocarSenha);

public sealed class AutenticarValidator : AbstractValidator<AutenticarCommand>
{
    public AutenticarValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Informe um e-mail válido.");
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}

public sealed class AutenticarHandler(
    IAppDbContext db, IVerificadorDeCredenciais credenciais, IGeradorDeToken tokens)
    : IRequestHandler<AutenticarCommand, AutenticarResponse>
{
    public async Task<AutenticarResponse> Handle(AutenticarCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // IgnoreQueryFilters: no login ainda não há unidade no contexto — é justamente o
        // token que vai defini-la. O e-mail é único por unidade, então o filtro global
        // recusaria tudo antes de haver o que filtrar.
        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .Include(u => u.Unidade)
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // RN-GER-027 — a MENSAGEM é a mesma nos três casos abaixo, e só o CÓDIGO muda.
        //
        // Diferenciar o texto confirmaria a existência da conta para quem tenta adivinhar, e
        // "a sua loja está desativada" contaria a situação comercial de um cliente a quem só tem
        // o e-mail dele. O código serve a quem ATENDE: sem ele, o cliente liga dizendo "não
        // consigo entrar" e o suporte adivinha entre três causas.
        if (usuario is null)
        {
            throw new LoginRecusadoException(LoginRecusadoException.MotivoCredencial);
        }

        // RN-GER-040 ⚠⚠ — a conferência CONTA as falhas e bloqueia na quinta (PAR-GER-004), pelo
        // SignInManager do Identity. Substituiu o `hash.Confere` daqui: conferir sem contar era a
        // lacuna que a ADR-0021 encontrou — força bruta de graça.
        var verificacao = await credenciais.VerificarAsync(usuario, request.Senha);

        if (verificacao.Bloqueada)
        {
            // A ÚNICA recusa de login com mensagem própria — a fresta em RN-GER-027 é aceita e
            // declarada (ADR-0022 §4): o silêncio cobraria o preço do operador legítimo.
            throw new ContaBloqueadaException(
                verificacao.BloqueadaPor ?? Domain.Gerais.PoliticaDeBloqueio.DuracaoDoBloqueio);
        }

        if (!verificacao.Valida)
        {
            throw new LoginRecusadoException(LoginRecusadoException.MotivoCredencial);
        }

        // RN-RH-006 — usuário inativo não registra operação no sistema.
        if (!usuario.Ativo)
        {
            throw new LoginRecusadoException(LoginRecusadoException.MotivoUsuarioInativo);
        }

        // RN-GER-027 — unidade inativa não autentica ninguém, qualquer que seja o perfil.
        // RN-GER-014 — a plataforma não tem unidade, então não há unidade a conferir.
        if (!usuario.EhPlataforma && (usuario.Unidade is null || !usuario.Unidade.Ativa))
        {
            throw new LoginRecusadoException(LoginRecusadoException.MotivoUnidadeInativa);
        }

        // RN-GER-010 / ADR-0022 §3 — as permissoes vem do PAPEL, como claims de perfil
        // (perfil_permissao), e viajam no token. O usuario de plataforma nao tem perfil de
        // unidade: as permissoes dele nao sao de unidade (RN-GER-014).
        //
        // ⚠ `PerfilId` extraido para um Guid LOCAL antes do Where: Guid? dentro da arvore de
        // expressao compila `perfil_id <> NULL` na consulta cacheada — SPEC-VEN-003 §11.7.
        List<string> permissoes = [];

        if (usuario.PerfilId is Guid perfilId)
        {
            permissoes = await db.PermissoesDePerfil
                .Where(claim => claim.RoleId == perfilId
                             && claim.ClaimType == Domain.Gerais.CatalogoDeAcesso.TipoDeClaimDePermissao
                             && claim.ClaimValue != null)
                .Select(claim => claim.ClaimValue!)
                .OrderBy(codigo => codigo)
                .ToListAsync(cancellationToken);
        }

        var (token, expiraEm) = tokens.Gerar(usuario, permissoes);

        return new AutenticarResponse(
            token,
            expiraEm,
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            usuario.EhPlataforma ? "Plataforma" : usuario.Perfil?.Name ?? "Sem perfil",
            usuario.UnidadeId,
            usuario.Unidade?.NomeFantasia ?? "GrelhaHub — suporte",
            usuario.Unidade?.Slug ?? string.Empty,
            usuario.EhPlataforma,
            permissoes,
            usuario.DeveTrocarSenha);
    }
}
