using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Common.Modulos;

/// <summary>
/// Recusa a partida se algum perfil do catálogo não estiver declarado na matriz de delegação —
/// <c>RN-GER-024</c>, <c>SPEC-GER-007</c> CA-8.
/// <para>
/// <b>Este validador é a contrapartida de uma decisão de produto.</b> O time escolheu lista
/// fixa de delegação em vez da regra geral de contenção de permissões, ciente do custo: perfil
/// novo exige acrescentar linha em <see cref="MatrizDeDelegacao"/>. O problema não é o custo — é
/// que <b>esquecer seria silencioso</b>. O perfil novo simplesmente não seria atribuível por
/// ninguém, ou seria atribuível por engano se alguém copiasse a linha errada, e não haveria erro
/// para ninguém notar.
/// </para>
/// <para>
/// Mesmo desenho de <see cref="ValidadorDeDeclaracaoDeModulo"/> e de
/// <c>GarantirAusenciaDeDecimalCru</c>: empurrar a falha para o instante mais cedo e mais
/// ruidoso possível. Um perfil esquecido derruba o boot com o nome do perfil na mensagem, em vez
/// de virar um pedido de acesso três semanas depois.
/// </para>
/// </summary>
public static class ValidadorDeDelegacao
{
    public static void Validar()
    {
        var doCatalogo = CatalogoDeAcesso.Perfis
            .Select(perfil => perfil.Nome)
            .ToList();

        var naMatriz = MatrizDeDelegacao.PerfisDeclarados;

        // 1. Todo perfil do catálogo precisa dizer o que delega — inclusive "nada".
        var ausentes = doCatalogo
            .Where(nome => !naMatriz.Contains(nome, StringComparer.OrdinalIgnoreCase))
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        if (ausentes.Count > 0)
        {
            throw new InvalidOperationException(
                "RN-GER-024 — perfil sem declaração na matriz de delegação: " +
                $"{string.Join(", ", ausentes)}. " +
                "Acrescente o perfil a MatrizDeDelegacao, com a lista dos perfis que ele pode " +
                "atribuir — use lista vazia para \"não delega ninguém\". " +
                "A omissão é recusada de propósito: um perfil que não declara nada não é um " +
                "perfil sem poder de delegação, é um perfil que alguém esqueceu.");
        }

        // 2. E a matriz não pode delegar perfil que não existe: uma entrada apontando para
        // perfil renomeado nunca casaria, e a recusa apareceria como bug de permissão.
        var alvosDesconhecidos = naMatriz
            .SelectMany(MatrizDeDelegacao.DelegaveisPor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(alvo => !doCatalogo.Contains(alvo, StringComparer.OrdinalIgnoreCase))
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        if (alvosDesconhecidos.Count > 0)
        {
            throw new InvalidOperationException(
                "RN-GER-024 — a matriz de delegação atribui perfil que não existe no catálogo: " +
                $"{string.Join(", ", alvosDesconhecidos)}. " +
                "Provavelmente um perfil foi renomeado em CatalogoDeAcesso e a matriz ficou " +
                "apontando para o nome antigo.");
        }
    }
}
