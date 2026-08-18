using System.Collections.Concurrent;
using System.Globalization;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Infrastructure.Services;

/// <summary>
/// Cache compartilhado dos valores de parâmetro — <c>SPEC-GER-003</c>.
/// <para>
/// <b>Guarda todas as vigências de um parâmetro, não o valor corrente.</b> Cachear só o valor de
/// agora faria uma consulta em data passada ser servida pela entrada errada, e um pedido antigo
/// passaria a ser explicado por uma taxa que não vigorava quando ele aconteceu.
/// </para>
/// <para>
/// Singleton: o cache atravessa requisições. A chave inclui a unidade porque o valor é por
/// unidade (<c>RN-GER-009</c>).
/// </para>
/// <para>
/// <b>Limite conhecido:</b> a invalidação é local ao processo. Com uma só instância da API
/// (ADR-0006) isso basta; se um dia houver réplica, a escrita precisará avisar as outras.
/// </para>
/// </summary>
public sealed class CacheDeParametros
{
    private readonly ConcurrentDictionary<(Guid Unidade, string Codigo), List<Vigencia>> _entradas = new();

    public sealed record Vigencia(string Valor, DateTimeOffset Inicio, DateTimeOffset? Fim)
    {
        public bool VigoraEm(DateTimeOffset instante) =>
            instante >= Inicio && (Fim is null || instante < Fim);
    }

    public List<Vigencia>? Obter(Guid unidade, string codigo) =>
        _entradas.TryGetValue((unidade, codigo), out var vigencias) ? vigencias : null;

    public void Guardar(Guid unidade, string codigo, List<Vigencia> vigencias) =>
        _entradas[(unidade, codigo)] = vigencias;

    public void Invalidar(Guid unidade, string codigo) =>
        _entradas.TryRemove((unidade, codigo), out _);
}

/// <inheritdoc cref="IParametros"/>
public sealed class Parametros(
    IAppDbContext db,
    IContextoDeExecucao contexto,
    IRelogio relogio,
    CacheDeParametros cache) : IParametros
{
    public async Task<string> ObterTextoAsync(
        string codigo, DateTimeOffset? vigenteEm = null, CancellationToken cancellationToken = default)
    {
        var instante = vigenteEm ?? relogio.Agora;
        var unidade = contexto.UnidadeId;

        var vigencias = cache.Obter(unidade, codigo);

        if (vigencias is null)
        {
            vigencias = await CarregarAsync(codigo, cancellationToken);
            cache.Guardar(unidade, codigo, vigencias);
        }

        var vigente = vigencias.FirstOrDefault(vigencia => vigencia.VigoraEm(instante));

        if (vigente is null)
        {
            // Falha ruidosa: parâmetro sem valor na data é lacuna de carga inicial, não caso de
            // borda. Devolver zero ou nulo espalharia o defeito para dentro de um cálculo.
            throw new RegraDeNegocioException(
                $"O parâmetro {codigo} não tem valor vigente em " +
                $"{instante.ToString("O", CultureInfo.InvariantCulture)}.");
        }

        return vigente.Valor;
    }

    public async Task<T> ObterAsync<T>(
        string codigo, DateTimeOffset? vigenteEm = null, CancellationToken cancellationToken = default)
    {
        var texto = await ObterTextoAsync(codigo, vigenteEm, cancellationToken);

        return Converter<T>(texto, codigo);
    }

    public void Invalidar(string codigo) => cache.Invalidar(contexto.UnidadeId, codigo);

    private async Task<List<CacheDeParametros.Vigencia>> CarregarAsync(
        string codigo, CancellationToken cancellationToken)
    {
        var existe = await db.Parametros
            .AsNoTracking()
            .AnyAsync(parametro => parametro.Codigo == codigo, cancellationToken);

        if (!existe)
        {
            throw new NaoEncontradoException($"Parâmetro {codigo} não encontrado.");
        }

        // O filtro global recorta por unidade (RN-GER-009) — não há WHERE de unidade aqui.
        return await db.ValoresDeParametro
            .AsNoTracking()
            .Where(valor => valor.Parametro!.Codigo == codigo)
            .OrderByDescending(valor => valor.VigenciaInicio)
            .Select(valor => new CacheDeParametros.Vigencia(
                valor.Valor, valor.VigenciaInicio, valor.VigenciaFim))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Conversão sempre em cultura invariante: o texto foi gravado assim, e ler `10.00` com
    /// cultura pt-BR devolveria mil.
    /// </summary>
    private static T Converter<T>(string texto, string codigo)
    {
        var invariante = CultureInfo.InvariantCulture;
        var tipo = typeof(T);

        object convertido = tipo switch
        {
            _ when tipo == typeof(string) => texto,
            _ when tipo == typeof(decimal) => decimal.Parse(texto, invariante),
            _ when tipo == typeof(int) => int.Parse(texto, invariante),
            _ when tipo == typeof(TimeOnly) => TimeOnly.Parse(texto, invariante),
            _ when tipo == typeof(TimeSpan) => TimeSpan.Parse(texto, invariante),
            _ => throw new InvalidOperationException(
                $"O parâmetro {codigo} não pode ser lido como {tipo.Name}. " +
                "Tipos suportados: string, decimal, int, TimeOnly, TimeSpan."),
        };

        return (T)convertido;
    }
}
