using System.Collections.Concurrent;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Infrastructure.Services;

/// <summary>
/// Cache das habilitações por unidade — <c>SPEC-GER-006</c>.
/// <para>
/// Guarda <b>todas as vigências</b>, não a lista do que está habilitado agora: uma consulta de
/// cobrança em data passada não pode ser servida pela entrada do presente. Mesmo desenho de
/// <see cref="CacheDeParametros"/>.
/// </para>
/// <para>
/// <b>Limite conhecido:</b> invalidação local ao processo. Com uma instância da API (ADR-0006)
/// basta; com réplica, a habilitação precisaria avisar as outras.
/// </para>
/// </summary>
public sealed class CacheDeModulos
{
    private readonly ConcurrentDictionary<Guid, List<Vigencia>> _porUnidade = new();

    public sealed record Vigencia(CodigoDeModulo Modulo, DateTimeOffset Inicio, DateTimeOffset? Fim)
    {
        public bool VigoraEm(DateTimeOffset instante) =>
            instante >= Inicio && (Fim is null || instante < Fim);
    }

    public List<Vigencia>? Obter(Guid unidade) =>
        _porUnidade.TryGetValue(unidade, out var vigencias) ? vigencias : null;

    public void Guardar(Guid unidade, List<Vigencia> vigencias) => _porUnidade[unidade] = vigencias;

    public void Invalidar(Guid unidade) => _porUnidade.TryRemove(unidade, out _);
}

/// <inheritdoc cref="IModulos"/>
public sealed class Modulos(IAppDbContext db, IRelogio relogio, CacheDeModulos cache) : IModulos
{
    public async Task<bool> EstaHabilitadoAsync(
        Guid unidadeId,
        CodigoDeModulo modulo,
        DateTimeOffset? em = null,
        CancellationToken cancellationToken = default)
    {
        // RN-GER-019 — o núcleo está sempre habilitado. Não vai ao banco: não existe unidade
        // operando sem autenticação, parâmetro e auditoria, e uma consulta que pudesse
        // responder "não" abriria a porta para uma unidade inacessível a si mesma.
        if (modulo == CodigoDeModulo.Gerais)
        {
            return true;
        }

        var vigencias = await CarregarAsync(unidadeId, cancellationToken);
        var instante = em ?? relogio.Agora;

        return vigencias.Any(vigencia => vigencia.Modulo == modulo && vigencia.VigoraEm(instante));
    }

    public async Task<IReadOnlyList<CodigoDeModulo>> HabilitadosAsync(
        Guid unidadeId, DateTimeOffset? em = null, CancellationToken cancellationToken = default)
    {
        var vigencias = await CarregarAsync(unidadeId, cancellationToken);
        var instante = em ?? relogio.Agora;

        var habilitados = vigencias
            .Where(vigencia => vigencia.VigoraEm(instante))
            .Select(vigencia => vigencia.Modulo)
            .ToHashSet();

        // O núcleo entra sempre, mesmo sem linha em unidade_modulo (RN-GER-019).
        habilitados.Add(CodigoDeModulo.Gerais);

        return habilitados.OrderBy(modulo => (int)modulo).ToList();
    }

    public void Invalidar(Guid unidadeId) => cache.Invalidar(unidadeId);

    private async Task<List<CacheDeModulos.Vigencia>> CarregarAsync(
        Guid unidadeId, CancellationToken cancellationToken)
    {
        var doCache = cache.Obter(unidadeId);

        if (doCache is not null)
        {
            return doCache;
        }

        // IgnoreQueryFilters: o gate de módulo roda para a unidade do token, mas também para a
        // unidade resolvida por slug numa rota pública — e nesse instante o escopo pode ainda
        // não estar fixado. O recorte aqui é explícito, pelo parâmetro.
        var vigencias = await db.ModulosDaUnidade
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(habilitacao => habilitacao.UnidadeId == unidadeId)
            .Select(habilitacao => new CacheDeModulos.Vigencia(
                habilitacao.Modulo!.Codigo, habilitacao.VigenciaInicio, habilitacao.VigenciaFim))
            .ToListAsync(cancellationToken);

        cache.Guardar(unidadeId, vigencias);

        return vigencias;
    }
}
