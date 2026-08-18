using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Plataforma;

public sealed record EstabelecimentoDto(
    Guid Id,
    string NomeFantasia,
    string RazaoSocial,
    string Cnpj,
    string Slug,
    bool Ativa,
    DateTimeOffset CriadoEm,
    string? UrlLogo,
    int Usuarios,
    IReadOnlyList<string> Modulos);

/// <summary>
/// <c>RN-GER-014</c> / ADR-0013 — os estabelecimentos, na visão da plataforma.
/// <para>
/// <b>Esta é a consulta mais perigosa do sistema:</b> ela atravessa unidade de propósito. A
/// autorização é a primeira linha do handler, e não um atributo — "é plataforma" é natureza do
/// usuário, não permissão granular (<c>RN-GER-010</c>).
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record ListarEstabelecimentosQuery : IRequest<IReadOnlyList<EstabelecimentoDto>>;

public sealed class ListarEstabelecimentosHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ListarEstabelecimentosQuery, IReadOnlyList<EstabelecimentoDto>>
{
    public async Task<IReadOnlyList<EstabelecimentoDto>> Handle(
        ListarEstabelecimentosQuery request, CancellationToken cancellationToken)
    {
        ExigirPlataforma(contexto);

        var agora = DateTimeOffset.UtcNow;

        var unidades = await db.Unidades
            .AsNoTracking()
            .OrderBy(u => u.NomeFantasia)
            .Select(u => new
            {
                u.Id, u.NomeFantasia, u.RazaoSocial, u.Cnpj, u.Slug, u.Ativa, u.CriadoEm, u.UrlLogo,

                // Contagem no banco. RN-GER-016 — o usuário de plataforma não conta como usuário
                // do restaurante: ele não é dele.
                Usuarios = db.Usuarios.Count(x => x.UnidadeId == u.Id && !x.EhPlataforma),

                // RN-GER-021 — vigente AGORA. Módulo que já venceu não é módulo habilitado, e
                // listar o histórico aqui faria a tela mostrar o que o cliente não tem mais.
                Modulos = db.ModulosDaUnidade
                    .Where(m => m.UnidadeId == u.Id
                             && m.VigenciaInicio <= agora
                             && (m.VigenciaFim == null || m.VigenciaFim > agora))
                    .Select(m => m.Modulo!.Codigo)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        // Sigla montada em memória: `Modulo.Sigla` é método do domínio e não tem tradução para
        // SQL (a mesma restrição que impede `p.Preco.Valor` de virar consulta).
        return unidades
            .Select(u => new EstabelecimentoDto(
                u.Id, u.NomeFantasia, u.RazaoSocial, u.Cnpj, u.Slug, u.Ativa, u.CriadoEm, u.UrlLogo,
                u.Usuarios,
                u.Modulos.Select(Modulo.Sigla).OrderBy(s => s, StringComparer.Ordinal).ToList()))
            .ToList();
    }

    internal static void ExigirPlataforma(IContextoDeExecucao contexto)
    {
        if (!contexto.EhPlataforma)
        {
            throw new ExclusivoDaPlataformaException(
                "Esta operação é do suporte da plataforma.");
        }
    }
}

/// <summary>
/// <c>SPEC-GER-007</c> §4 — cadastro assistido de estabelecimento, com os módulos vendidos.
/// <para>
/// <b>Delega ao mesmo handler da porta anônima.</b> Duplicar a semeadura de perfis, parâmetros,
/// módulos e cardápio de exemplo produziria unidades diferentes conforme a porta de entrada — e a
/// diferença apareceria semanas depois, como "no cliente que cadastrei pelo telefone o parâmetro
/// não existe".
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record CadastrarEstabelecimentoCommand(
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string? Telefone,
    string NomeAdministrador,
    string SobrenomeAdministrador,
    string CpfAdministrador,
    string Email,
    string Senha,
    IReadOnlyList<string>? Modulos) : IRequest<CadastrarRestauranteResponse>;

public sealed class CadastrarEstabelecimentoValidator : AbstractValidator<CadastrarEstabelecimentoCommand>
{
    public CadastrarEstabelecimentoValidator()
    {
        // As regras de conteúdo são as mesmas da porta anônima e ficam onde já estão: o handler
        // delega, e o validator de CadastrarRestauranteCommand roda no pipeline do comando
        // interno. Aqui só o que é próprio desta rota.
        RuleFor(x => x.Modulos)
            .Must(siglas => siglas is null || siglas.All(s => !string.IsNullOrWhiteSpace(s)))
            .WithMessage("Sigla de módulo vazia na lista.");
    }
}

public sealed class CadastrarEstabelecimentoHandler(IContextoDeExecucao contexto, ISender sender)
    : IRequestHandler<CadastrarEstabelecimentoCommand, CadastrarRestauranteResponse>
{
    public async Task<CadastrarRestauranteResponse> Handle(
        CadastrarEstabelecimentoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var codigos = TraduzirSiglas(request.Modulos);

        return await sender.Send(
            new CadastrarRestauranteCommand(
                request.RazaoSocial,
                request.NomeFantasia,
                request.Cnpj,
                request.Telefone,
                request.NomeAdministrador,
                request.SobrenomeAdministrador,
                request.CpfAdministrador,
                request.Email,
                request.Senha,
                codigos),
            cancellationToken);
    }

    /// <summary>
    /// Sigla → código do módulo. Sigla desconhecida <b>falha</b> em vez de ser ignorada: a lista
    /// vem de quem está vendendo, e um módulo silenciosamente não habilitado é uma venda que o
    /// cliente não recebe.
    /// </summary>
    private static List<CodigoDeModulo>? TraduzirSiglas(IReadOnlyList<string>? siglas)
    {
        if (siglas is null || siglas.Count == 0)
        {
            return null;
        }

        // Mapa sigla → código, montado do próprio enum: uma lista escrita à mão aqui divergiria
        // do catálogo no primeiro módulo novo.
        var porSigla = Enum.GetValues<CodigoDeModulo>()
            .ToDictionary(Modulo.Sigla, codigo => codigo, StringComparer.Ordinal);

        var resultado = new List<CodigoDeModulo>();

        foreach (var sigla in siglas
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal))
        {
            if (!porSigla.TryGetValue(sigla, out var codigo))
            {
                throw new RegraDeNegocioException($"Módulo desconhecido: {sigla}.");
            }

            resultado.Add(codigo);
        }

        return resultado;
    }
}

public sealed record UsuarioDePlataformaDto(Guid Id, Guid UnidadeId, string Nome, string Email, string Perfil);

/// <summary>
/// <c>SPEC-GER-007</c> §4 — a plataforma cria usuário informando a unidade.
/// <para>
/// Rota própria porque <c>CriarUsuarioCommand</c> tira a unidade de quem está autenticado, e a
/// plataforma não tem unidade (<c>RN-GER-014</c>).
/// </para>
/// <para>
/// <b>Não passa pela matriz de delegação</b> (<c>RN-GER-024</c>): a matriz limita perfil de
/// unidade delegando perfil de unidade, e a plataforma não tem perfil. O que a limita é
/// <c>RN-GER-015</c> — sessão de suporte com motivo, imposta pelo pipeline antes de qualquer
/// escrita de plataforma.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record CriarUsuarioDePlataformaCommand(
    Guid UnidadeId,
    string Nome,
    string Sobrenome,
    string Cpf,
    string Email,
    string PerfilNome,
    string SenhaInicial) : IRequest<UsuarioDePlataformaDto>;

public sealed class CriarUsuarioDePlataformaValidator : AbstractValidator<CriarUsuarioDePlataformaCommand>
{
    public CriarUsuarioDePlataformaValidator()
    {
        RuleFor(x => x.UnidadeId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sobrenome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PerfilNome).NotEmpty();

        RuleFor(x => x.Cpf)
            .NotEmpty()
            .Must(cpf => Domain.Common.Cpf.TentarDe(cpf, out _))
            .WithMessage("O CPF informado não é válido.");

        RuleFor(x => x.SenhaInicial)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A senha inicial deve ter ao menos 8 caracteres.");
    }
}

public sealed class CriarUsuarioDePlataformaHandler(
    IAppDbContext db,
    IContextoDeExecucao contexto,
    IHashDeSenha hash,
    Acesso.EmissorDeLinkDeAcesso convites)
    : IRequestHandler<CriarUsuarioDePlataformaCommand, UsuarioDePlataformaDto>
{
    public async Task<UsuarioDePlataformaDto> Handle(
        CriarUsuarioDePlataformaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var email = request.Email.Trim().ToLowerInvariant();
        var cpf = Cpf.De(request.Cpf);

        if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new RegraDeNegocioException("Já existe um usuário com este e-mail.");
        }

        if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Cpf == cpf, cancellationToken))
        {
            throw new RegraDeNegocioException("Já existe um usuário com este CPF.");
        }

        // `IgnoreQueryFilters` não é necessário para a plataforma (o filtro já a libera), e está
        // escrito para não depender disso: se o desligamento do filtro mudar, esta consulta
        // continua correta.
        var perfil = await db.Perfis
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.UnidadeId == request.UnidadeId && p.Nome == request.PerfilNome,
                cancellationToken)
            ?? throw new NaoEncontradoException(
                $"O perfil {request.PerfilNome} não existe nesta unidade.");

        var usuario = new Usuario(
            request.UnidadeId,
            request.Nome,
            email,
            hash.Gerar(request.SenhaInicial),
            perfil.Id,
            deveTrocarSenha: true,
            sobrenome: request.Sobrenome,
            cpf: cpf);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(cancellationToken);

        // RN-GER-032 / RN-GER-033 — convite de primeiro acesso, enfileirado depois de gravar.
        await convites.EmitirAsync(usuario, TipoDeToken.Convite, cancellationToken);

        return new UsuarioDePlataformaDto(
            usuario.Id, request.UnidadeId, usuario.NomeCompleto, usuario.Email, perfil.Nome);
    }
}

public sealed record ModuloDoCatalogoDto(
    string Sigla,
    string Nome,
    bool Nucleo,
    int Ordem,

    /// <summary><c>RN-GER-020</c> — as dependências DIRETAS, por sigla.</summary>
    IReadOnlyList<string> DependeDe);

/// <summary>
/// <c>RN-GER-020</c> — o catálogo de módulos com o grafo de dependência.
/// <para>
/// Existe para o formulário de cadastro assistido, que precisa do grafo <b>antes</b> de a unidade
/// existir: <c>ListarModulosDaUnidadeQuery</c> exige um <c>unidadeId</c>, e no cadastro ainda não
/// há um. A alternativa era uma cópia do grafo no JavaScript — e cópia de regra divergindo da
/// fonte é o modo de falha que este projeto persegue.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record ListarCatalogoDeModulosQuery : IRequest<IReadOnlyList<ModuloDoCatalogoDto>>;

public sealed class ListarCatalogoDeModulosHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ListarCatalogoDeModulosQuery, IReadOnlyList<ModuloDoCatalogoDto>>
{
    public async Task<IReadOnlyList<ModuloDoCatalogoDto>> Handle(
        ListarCatalogoDeModulosQuery request, CancellationToken cancellationToken)
    {
        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var catalogo = await db.Modulos.AsNoTracking().OrderBy(m => m.Ordem).ToListAsync(cancellationToken);
        var arestas = await db.DependenciasDeModulo.AsNoTracking().ToListAsync(cancellationToken);

        var porId = catalogo.ToDictionary(m => m.Id);

        // `Modulo.Sigla` é método do domínio e não tem tradução para SQL — a montagem é em memória,
        // como em `ListarEstabelecimentos`.
        return catalogo
            .Select(modulo => new ModuloDoCatalogoDto(
                Modulo.Sigla(modulo.Codigo),
                modulo.Nome,
                modulo.Nucleo,
                modulo.Ordem,
                arestas
                    .Where(a => a.ModuloId == modulo.Id && porId.ContainsKey(a.DependeDeId))
                    .Select(a => Modulo.Sigla(porId[a.DependeDeId].Codigo))
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }
}
