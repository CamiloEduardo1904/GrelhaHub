using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SessaoDeSuporteEntidade = GrelhaHub.Domain.Gerais.SessaoDeSuporte;

namespace GrelhaHub.Application.Features.Gerais.SessaoDeSuporte;

public sealed record SessaoDeSuporteDto(
    Guid Id,
    Guid UnidadeId,
    string Restaurante,
    string Motivo,
    DateTimeOffset AbertaEm,
    DateTimeOffset ValidaAte,
    DateTimeOffset? EncerradaEm);

/// <summary>
/// <c>RN-GER-015</c> ⚠ — abre a sessão que autoriza o suporte a <b>escrever</b> no dado de uma
/// unidade. ADR-0013.
/// <para>
/// Ler para diagnosticar é rotina; alterar o dado de um cliente sem dizer por quê, não. É a
/// contrapartida de o suporte enxergar todas as unidades.
/// </para>
/// </summary>
[SemModulo("operação de plataforma: antecede e atravessa o contrato comercial do restaurante")]
[SemPermissao("operação de plataforma (RN-GER-014): o usuário de plataforma não tem perfil de unidade")]
public sealed record AbrirSessaoDeSuporteCommand(Guid UnidadeId, string Motivo)
    : IRequest<SessaoDeSuporteDto>;

public sealed class AbrirSessaoDeSuporteValidator : AbstractValidator<AbrirSessaoDeSuporteCommand>
{
    public AbrirSessaoDeSuporteValidator()
    {
        RuleFor(x => x.UnidadeId).NotEmpty();

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .MinimumLength(10).WithMessage("A sessão de suporte exige motivo de ao menos 10 caracteres.")
            .MaximumLength(500);
    }
}

public sealed class AbrirSessaoDeSuporteHandler(IAppDbContext db, IContextoDeExecucao contexto, IRelogio relogio)
    : IRequestHandler<AbrirSessaoDeSuporteCommand, SessaoDeSuporteDto>
{
    public async Task<SessaoDeSuporteDto> Handle(
        AbrirSessaoDeSuporteCommand request, CancellationToken cancellationToken)
    {
        if (!contexto.EhPlataforma || contexto.UsuarioId is not { } usuarioId)
        {
            // RN-GER-016 — só a plataforma abre sessão de suporte. Um administrador de restaurante
            // que pudesse abri-la se autoautorizaria a escrever como suporte.
            throw new ExclusivoDaPlataformaException(
                "Apenas o suporte da plataforma abre sessão de suporte.");
        }

        var unidade = await db.Unidades
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UnidadeId, cancellationToken)
            ?? throw new NaoEncontradoException("Unidade não encontrada.");

        var limite = relogio.Agora - SessaoDeSuporteEntidade.Duracao;

        // Sessão já aberta e válida é reaproveitada: abrir duas para o mesmo atendimento poluiria
        // a auditoria com registros que não correspondem a decisões distintas.
        var existente = await db.SessoesDeSuporte
            .Where(sessao => sessao.UsuarioId == usuarioId
                          && sessao.UnidadeId == request.UnidadeId
                          && sessao.EncerradaEm == null
                          && sessao.AbertaEm > limite)
            .OrderByDescending(sessao => sessao.AbertaEm)
            .FirstOrDefaultAsync(cancellationToken);

        if (existente is not null)
        {
            return Montar(existente, unidade.NomeFantasia);
        }

        var nova = new SessaoDeSuporteEntidade(usuarioId, request.UnidadeId, request.Motivo);

        db.SessoesDeSuporte.Add(nova);
        await db.SaveChangesAsync(cancellationToken);

        return Montar(nova, unidade.NomeFantasia);
    }

    internal static SessaoDeSuporteDto Montar(SessaoDeSuporteEntidade sessao, string restaurante) =>
        new(sessao.Id,
            sessao.UnidadeId,
            restaurante,
            sessao.Motivo,
            sessao.AbertaEm,
            sessao.AbertaEm + SessaoDeSuporteEntidade.Duracao,
            sessao.EncerradaEm);
}

/// <summary>
/// Encerra a sessão. Não é obrigatório — ela <b>expira</b> em duas horas de qualquer forma, porque
/// ninguém encerra sessão de suporte ao terminar o dia, e sessão aberta para sempre equivale a não
/// ter sessão.
/// </summary>
[SemModulo("operação de plataforma: antecede e atravessa o contrato comercial do restaurante")]
[SemPermissao("operação de plataforma (RN-GER-014): encerra a própria sessão")]
public sealed record EncerrarSessaoDeSuporteCommand(Guid SessaoId) : IRequest;

public sealed class EncerrarSessaoDeSuporteHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<EncerrarSessaoDeSuporteCommand>
{
    public async Task Handle(EncerrarSessaoDeSuporteCommand request, CancellationToken cancellationToken)
    {
        if (!contexto.EhPlataforma || contexto.UsuarioId is not { } usuarioId)
        {
            throw new ExclusivoDaPlataformaException("Apenas o suporte da plataforma encerra sessão de suporte.");
        }

        var sessao = await db.SessoesDeSuporte
            .FirstOrDefaultAsync(
                s => s.Id == request.SessaoId && s.UsuarioId == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException("Sessão de suporte não encontrada.");

        sessao.Encerrar();
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Sessões de suporte abertas sobre uma unidade — para o suporte saber o que está aberto, e para
/// a auditoria. <c>RN-GER-015</c>.
/// </summary>
[SemModulo("operação de plataforma: antecede e atravessa o contrato comercial do restaurante")]
[SemPermissao("operação de plataforma (RN-GER-014)")]
public sealed record ListarSessoesDeSuporteQuery(Guid? UnidadeId)
    : IRequest<IReadOnlyList<SessaoDeSuporteDto>>;

public sealed class ListarSessoesDeSuporteHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ListarSessoesDeSuporteQuery, IReadOnlyList<SessaoDeSuporteDto>>
{
    public async Task<IReadOnlyList<SessaoDeSuporteDto>> Handle(
        ListarSessoesDeSuporteQuery request, CancellationToken cancellationToken)
    {
        if (!contexto.EhPlataforma)
        {
            throw new ExclusivoDaPlataformaException("Apenas o suporte da plataforma consulta sessões de suporte.");
        }

        var consulta = db.SessoesDeSuporte.AsNoTracking();

        if (request.UnidadeId is { } unidadeId)
        {
            consulta = consulta.Where(sessao => sessao.UnidadeId == unidadeId);
        }

        var sessoes = await consulta
            .OrderByDescending(sessao => sessao.AbertaEm)
            .ToListAsync(cancellationToken);

        var nomes = await db.Unidades
            .AsNoTracking()
            .ToDictionaryAsync(unidade => unidade.Id, unidade => unidade.NomeFantasia, cancellationToken);

        return sessoes
            .Select(sessao => AbrirSessaoDeSuporteHandler.Montar(
                sessao, nomes.GetValueOrDefault(sessao.UnidadeId, "—")))
            .ToList();
    }
}
