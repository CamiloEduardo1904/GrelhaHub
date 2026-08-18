using System.Reflection;
using MediatR;

namespace GrelhaHub.Application.Common.Modulos;

/// <summary>
/// Recusa a partida se alguma operação não declarar o seu módulo — <c>RN-GER-017</c>, ADR-0014.
/// <para>
/// <b>É o que faz o gate valer.</b> Um behavior que só verifica quem declarou falha <i>aberto</i>:
/// a operação nova, sem atributo, passaria sem gate nenhum — e ninguém descobriria, porque não
/// haveria erro. Aqui a aplicação não sobe, e a mensagem diz qual operação corrigir.
/// </para>
/// <para>
/// Mesmo desenho de <c>GarantirAusenciaDeDecimalCru</c>: empurrar a falha para o instante mais
/// cedo e mais ruidoso possível.
/// </para>
/// </summary>
public static class ValidadorDeDeclaracaoDeModulo
{
    /// <summary>
    /// Percorre todo <see cref="IBaseRequest"/> concreto do assembly e exige
    /// <see cref="ExigeModuloAttribute"/> ou <see cref="SemModuloAttribute"/>.
    /// </summary>
    public static void Validar(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var operacoes = assembly.GetTypes()
            .Where(tipo => tipo is { IsAbstract: false, IsInterface: false })
            .Where(tipo => typeof(IBaseRequest).IsAssignableFrom(tipo))
            .ToList();

        var semModulo = operacoes
            .Where(tipo => tipo.GetCustomAttribute<ExigeModuloAttribute>() is null
                        && tipo.GetCustomAttribute<SemModuloAttribute>() is null)
            .Select(tipo => tipo.Name)
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        if (semModulo.Count > 0)
        {
            throw new InvalidOperationException(
                "RN-GER-017 — operação sem declaração de módulo: " +
                $"{string.Join(", ", semModulo)}. " +
                "Marque com [ExigeModulo(CodigoDeModulo.X)] ou justifique com [SemModulo(\"motivo\")]. " +
                "Sem declaração, a operação passaria sem gate de módulo e ninguém notaria (ADR-0014).");
        }

        // RN-GER-010 — o mesmo desenho para permissão. Uma operação sem declaração passaria sem
        // gate de autorização, e o modo de falha é o pior possível: ninguém percebe até alguém
        // fazer o que não devia.
        var semPermissao = operacoes
            .Where(tipo => tipo.GetCustomAttribute<ExigePermissaoAttribute>() is null
                        && tipo.GetCustomAttribute<SemPermissaoAttribute>() is null)
            .Select(tipo => tipo.Name)
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        // RN-GER-015 — o SessaoDeSuporteBehavior identifica ESCRITA pelo sufixo `Command`. Isso
        // so e confiavel se a convencao for imposta: sem esta checagem, uma operacao chamada
        // `ExcluirTudo` passaria como leitura e escaparia da exigencia de sessao de suporte.
        var nomeForaDaConvencao = operacoes
            .Where(tipo => !tipo.Name.EndsWith("Command", StringComparison.Ordinal)
                        && !tipo.Name.EndsWith("Query", StringComparison.Ordinal))
            .Select(tipo => tipo.Name)
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        if (nomeForaDaConvencao.Count > 0)
        {
            throw new InvalidOperationException(
                "RN-GER-015 — operacao cujo nome nao termina em Command nem Query: " +
                $"{string.Join(", ", nomeForaDaConvencao)}. " +
                "O gate de sessao de suporte identifica ESCRITA por esse sufixo (ADR-0013); " +
                "um nome fora da convencao passaria como leitura.");
        }

        if (semPermissao.Count > 0)
        {
            throw new InvalidOperationException(
                "RN-GER-010 — operação sem declaração de permissão: " +
                $"{string.Join(", ", semPermissao)}. " +
                "Marque com [ExigePermissao(\"recurso.acao\")] ou justifique com [SemPermissao(\"motivo\")].");
        }
    }
}
