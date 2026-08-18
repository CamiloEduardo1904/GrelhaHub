using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Common.Modulos;

/// <summary>
/// <c>RN-GER-020</c> — <b>a única implementação da dependência entre módulos.</b>
/// <para>
/// Existe porque a regra passou a ser necessária em <b>dois</b> caminhos: alterar um módulo de uma
/// unidade existente (<c>SPEC-GER-006</c>) e criar a unidade já com a lista de módulos vendidos
/// (<c>SPEC-GER-007</c> §4). Deixar cada handler com a sua cópia é exatamente o modo de falha que
/// o ADR-0009 nomeia: <b>duas implementações da mesma regra divergem em silêncio</b>, e a
/// divergência aparece como "pelo cadastro assistido passa, pela tela de módulos não".
/// </para>
/// <para>
/// A pergunta que este tipo responde é sempre a mesma: <b>este conjunto de módulos é fechado sob
/// dependência?</b> Habilitar um módulo é o caso particular em que o conjunto é "o que já está
/// habilitado, mais este".
/// </para>
/// </summary>
public static class FechoDeDependencia
{
    /// <summary>
    /// As siglas que <b>faltam</b> para que <paramref name="conjunto"/> seja fechado sob
    /// dependência — vazio quando está tudo lá.
    /// <para>
    /// O resultado é o <b>fecho transitivo</b>, e não apenas as dependências diretas. Pedir
    /// <c>EST</c> sozinho devolve <c>PRO</c> <i>e</i> <c>VEN</c>: parar na primeira camada obrigaria
    /// quem está vendendo a descobrir a cadeia por tentativa e erro, uma recusa por vez.
    /// </para>
    /// <para>
    /// Módulo <c>Nucleo</c> conta como presente sempre (<c>RN-GER-019</c>): ele não é opcional, e
    /// listá-lo como faltante mandaria o vendedor "habilitar" o que já está habilitado.
    /// </para>
    /// </summary>
    /// <param name="catalogo">Todos os módulos — o <c>Nucleo</c> vem daqui.</param>
    /// <param name="arestas">O grafo, como está no banco (<c>modulo_dependencia</c>).</param>
    /// <param name="conjunto">Os ids dos módulos considerados presentes.</param>
    public static IReadOnlyList<string> Faltando(
        IReadOnlyCollection<Modulo> catalogo,
        IReadOnlyCollection<ModuloDependencia> arestas,
        IReadOnlyCollection<Guid> conjunto)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(arestas);
        ArgumentNullException.ThrowIfNull(conjunto);

        var porId = catalogo.ToDictionary(modulo => modulo.Id);

        var presentes = new HashSet<Guid>(conjunto);

        foreach (var nucleo in catalogo.Where(modulo => modulo.Nucleo))
        {
            presentes.Add(nucleo.Id);
        }

        // Fecho transitivo por busca em largura. `Add` devolvendo falso é o que faz a busca
        // TERMINAR mesmo se o grafo tiver um ciclo: `ck_modulo_dependencia_ciclo` impede o
        // autolaço, mas não um ciclo de três arestas, e um laço infinito aqui derrubaria o cadastro
        // em vez de recusá-lo.
        var necessarios = new HashSet<Guid>(presentes);
        var fila = new Queue<Guid>(presentes);

        while (fila.Count > 0)
        {
            var atual = fila.Dequeue();

            foreach (var aresta in arestas.Where(a => a.ModuloId == atual))
            {
                if (necessarios.Add(aresta.DependeDeId))
                {
                    fila.Enqueue(aresta.DependeDeId);
                }
            }
        }

        return necessarios
            .Except(presentes)
            .Where(porId.ContainsKey)
            .Select(id => porId[id])
            .OrderBy(modulo => modulo.Ordem)
            .Select(modulo => Modulo.Sigla(modulo.Codigo))
            .ToList();
    }

    /// <summary>
    /// Quem, dentro de <paramref name="conjunto"/>, depende de <paramref name="moduloId"/> —
    /// <c>RN-GER-020</c> na outra direção. É o que impede desabilitar PRO com EST de pé.
    /// <para>
    /// Direta e não transitiva de propósito: quem impede a saída é o dependente <b>imediato</b>
    /// que ainda está habilitado, e é o nome dele que o operador precisa ver para saber o que
    /// desligar primeiro.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> DependentesHabilitados(
        IReadOnlyCollection<Modulo> catalogo,
        IReadOnlyCollection<ModuloDependencia> arestas,
        IReadOnlyCollection<Guid> conjunto,
        Guid moduloId)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(arestas);
        ArgumentNullException.ThrowIfNull(conjunto);

        var porId = catalogo.ToDictionary(modulo => modulo.Id);
        var presentes = new HashSet<Guid>(conjunto);

        return arestas
            .Where(aresta => aresta.DependeDeId == moduloId)
            .Select(aresta => aresta.ModuloId)
            .Where(presentes.Contains)
            .Where(porId.ContainsKey)
            .Select(id => porId[id])
            .OrderBy(modulo => modulo.Ordem)
            .Select(modulo => Modulo.Sigla(modulo.Codigo))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
