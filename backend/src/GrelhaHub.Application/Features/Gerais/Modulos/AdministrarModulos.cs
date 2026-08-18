using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Modulos;

public sealed record ModuloDaUnidadeDto(
    string Codigo,
    string Nome,
    bool Nucleo,
    bool Habilitado,
    DateTimeOffset? Desde,
    DateTimeOffset? Ate);

/// <summary>
/// <c>SPEC-GER-006</c> — o que a unidade tem habilitado. Consulta liberada ao restaurante: ele
/// precisa saber o que contratou, e a interface monta o menu a partir disto.
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("modulo.visualizar")]
public sealed record ListarModulosDaUnidadeQuery(Guid? UnidadeId, DateTimeOffset? Em)
    : IRequest<IReadOnlyList<ModuloDaUnidadeDto>>;

public sealed class ListarModulosDaUnidadeHandler(
    IAppDbContext db, IContextoDeExecucao contexto, IRelogio relogio)
    : IRequestHandler<ListarModulosDaUnidadeQuery, IReadOnlyList<ModuloDaUnidadeDto>>
{
    public async Task<IReadOnlyList<ModuloDaUnidadeDto>> Handle(
        ListarModulosDaUnidadeQuery request, CancellationToken cancellationToken)
    {
        // RN-GER-014 — só a plataforma consulta outra unidade. O restaurante vê a sua, e o
        // parâmetro é ignorado para ele: aceitar o id de outra unidade aqui seria dar a um
        // cliente uma forma de perguntar o que o vizinho contratou.
        var unidadeId = contexto.EhPlataforma && request.UnidadeId is { } informada
            ? informada
            : contexto.UnidadeId;

        if (unidadeId == Guid.Empty)
        {
            throw new NaoEncontradoException("Unidade não encontrada.");
        }

        var instante = request.Em ?? relogio.Agora;

        var catalogo = await db.Modulos.AsNoTracking().OrderBy(m => m.Ordem).ToListAsync(cancellationToken);

        var habilitacoes = await db.ModulosDaUnidade
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(h => h.UnidadeId == unidadeId)
            .ToListAsync(cancellationToken);

        return catalogo.Select(modulo =>
        {
            var vigente = habilitacoes
                .Where(h => h.ModuloId == modulo.Id)
                .OrderByDescending(h => h.VigenciaInicio)
                .FirstOrDefault(h => h.VigoraEm(instante));

            // RN-GER-019 — o núcleo conta como habilitado mesmo sem linha.
            var habilitado = modulo.Nucleo || vigente is not null;

            return new ModuloDaUnidadeDto(
                Modulo.Sigla(modulo.Codigo),
                modulo.Nome,
                modulo.Nucleo,
                habilitado,
                vigente?.VigenciaInicio,
                vigente?.VigenciaFim);
        }).ToList();
    }
}

/// <summary>
/// <c>SPEC-GER-006</c> — habilita ou desabilita módulo na unidade. **Operação de plataforma**
/// (<c>RN-GER-014</c>): é decisão comercial, não do restaurante.
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("operação de plataforma (RN-GER-014): o usuário de plataforma não tem perfil de unidade, logo não tem permissão de unidade")]
public sealed record AlterarModuloDaUnidadeCommand(
    Guid UnidadeId,
    string Codigo,
    bool Habilitar,
    string Justificativa) : IRequest<IReadOnlyList<ModuloDaUnidadeDto>>;

public sealed class AlterarModuloDaUnidadeValidator : AbstractValidator<AlterarModuloDaUnidadeCommand>
{
    public AlterarModuloDaUnidadeValidator()
    {
        RuleFor(x => x.UnidadeId).NotEmpty();
        RuleFor(x => x.Codigo).NotEmpty().Matches("^[A-Za-z]{2,3}$");

        RuleFor(x => x.Justificativa)
            .NotEmpty()
            .MinimumLength(10).WithMessage("A justificativa precisa de ao menos 10 caracteres.")
            .MaximumLength(500);
    }
}

public sealed class AlterarModuloDaUnidadeHandler(
    IAppDbContext db, IContextoDeExecucao contexto, IRelogio relogio, IModulos modulos, ISender sender)
    : IRequestHandler<AlterarModuloDaUnidadeCommand, IReadOnlyList<ModuloDaUnidadeDto>>
{
    public async Task<IReadOnlyList<ModuloDaUnidadeDto>> Handle(
        AlterarModuloDaUnidadeCommand request, CancellationToken cancellationToken)
    {
        // RN-GER-014 / RN-GER-016 — habilitar módulo é ato comercial da plataforma. Um
        // administrador de restaurante que pudesse fazê-lo se autocontrataria.
        if (!contexto.EhPlataforma)
        {
            throw new ExclusivoDaPlataformaException(
                "Apenas o suporte da plataforma altera os módulos de um restaurante.");
        }

        var sigla = request.Codigo.Trim().ToUpperInvariant();

        var catalogo = await db.Modulos.AsNoTracking().ToListAsync(cancellationToken);

        var modulo = catalogo.FirstOrDefault(m => Modulo.Sigla(m.Codigo) == sigla)
            ?? throw new NaoEncontradoException($"Módulo {sigla} não encontrado.");

        var unidadeExiste = await db.Unidades
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.UnidadeId, cancellationToken);

        if (!unidadeExiste)
        {
            throw new NaoEncontradoException("Unidade não encontrada.");
        }

        var agora = relogio.Agora;

        var habilitacoes = await db.ModulosDaUnidade
            .IgnoreQueryFilters()
            .Where(h => h.UnidadeId == request.UnidadeId)
            .ToListAsync(cancellationToken);

        var arestas = await db.DependenciasDeModulo.AsNoTracking().ToListAsync(cancellationToken);

        bool EstaHabilitado(Guid moduloId) =>
            catalogo.First(m => m.Id == moduloId).Nucleo
            || habilitacoes.Any(h => h.ModuloId == moduloId && h.VigoraEm(agora));

        if (request.Habilitar)
        {
            if (EstaHabilitado(modulo.Id))
            {
                throw new RegraDeNegocioException(
                    $"O módulo {sigla} já está habilitado nesta unidade.", "RN-GER-017");
            }

            // RN-GER-020 — não se habilita módulo sem as suas dependências. Vender VEN sem GER,
            // ou EST sem PRO, produziria um sistema que aceita o pedido e não sabe o que baixar.
            //
            // O cálculo vive em `FechoDeDependencia`, e não aqui, porque o cadastro assistido de
            // estabelecimento (SPEC-GER-007) faz a MESMA pergunta sobre um conjunto inteiro. Duas
            // cópias da regra divergiriam em silêncio.
            //
            // O conjunto avaliado é "o que já está habilitado, mais este": habilitar é o caso
            // particular de "este conjunto é fechado sob dependência?".
            var conjunto = catalogo
                .Where(m => EstaHabilitado(m.Id))
                .Select(m => m.Id)
                .Append(modulo.Id)
                .ToList();

            var faltando = FechoDeDependencia.Faltando(catalogo, arestas, conjunto);

            if (faltando.Count > 0)
            {
                throw new RegraDeNegocioException(
                    $"O módulo {sigla} depende de {string.Join(", ", faltando)}, " +
                    "que não estão habilitados.", "RN-GER-020");
            }

            db.ModulosDaUnidade.Add(new UnidadeModulo(
                request.UnidadeId, modulo.Id, agora, request.Justificativa));
        }
        else
        {
            // RN-GER-019 — o núcleo não se desabilita: não existe unidade operando sem
            // autenticação, parâmetro e auditoria.
            if (modulo.Nucleo)
            {
                throw new RegraDeNegocioException(
                    $"O módulo {sigla} é núcleo e não pode ser desabilitado.", "RN-GER-019");
            }

            var corrente = habilitacoes
                .Where(h => h.ModuloId == modulo.Id && h.VigoraEm(agora))
                .OrderByDescending(h => h.VigenciaInicio)
                .FirstOrDefault()
                ?? throw new RegraDeNegocioException(
                    $"O módulo {sigla} não está habilitado nesta unidade.", "RN-GER-017");

            // RN-GER-020 — na outra direção: quem depende dele ainda está de pé?
            var habilitadosAgora = catalogo
                .Where(m => EstaHabilitado(m.Id))
                .Select(m => m.Id)
                .ToList();

            var dependentes = FechoDeDependencia.DependentesHabilitados(
                catalogo, arestas, habilitadosAgora, modulo.Id);

            if (dependentes.Count > 0)
            {
                throw new RegraDeNegocioException(
                    $"{string.Join(", ", dependentes)} dependem de {sigla} e estão habilitados.",
                    "RN-GER-020");
            }

            // RN-GER-018 — fecha a vigência. NUNCA apaga: o dado transacional do módulo
            // permanece e volta a ser acessível se ele for reabilitado.
            corrente.Desabilitar(agora, request.Justificativa);
        }

        await db.SaveChangesAsync(cancellationToken);

        modulos.Invalidar(request.UnidadeId);

        return await sender.Send(
            new ListarModulosDaUnidadeQuery(request.UnidadeId, null), cancellationToken);
    }
}
