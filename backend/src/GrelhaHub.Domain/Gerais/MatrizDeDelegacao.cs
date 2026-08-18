namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// <c>RN-GER-024</c> ⚠⚠ — <b>ninguém concede acesso que não possui.</b>
/// <para>
/// Esta classe existe por causa de um defeito de segurança encontrado ao especificar "o gerente
/// cadastra operadores". Conceder <c>usuario.gerenciar</c> ao Gerente atende o pedido e abre
/// <b>escalação de privilégio</b>: sem limite de qual perfil ele pode atribuir, o gerente cria
/// um usuário Administrador, entra com ele, e passa a ter o acesso que ninguém lhe deu.
/// </para>
/// <para>
/// <b>Lista fixa, e não contenção de permissões.</b> O time escolheu declarar explicitamente
/// quem delega o quê, em vez da regra geral
/// <c>permissões(alvo) ⊆ permissões(criador)</c>. É mais simples de explicar e de defender em
/// banca. O custo é que <b>perfil novo exige acrescentar linha aqui</b> — e esquecer disso seria
/// silencioso, por isso existe <c>ValidadorDeDelegacao</c>, que recusa a partida da aplicação
/// quando um perfil do catálogo não aparece nesta matriz.
/// </para>
/// </summary>
public static class MatrizDeDelegacao
{
    /// <summary>
    /// Quem pode atribuir o quê. A chave é o perfil de quem cria; o valor, os perfis que ele
    /// pode atribuir.
    /// <para>
    /// <b>O Gerente não delega Gerente.</b> Decisão do time, 17/08/2026: um gerente que cria
    /// outro gerente multiplica quem pode alterar parâmetro e ver custo sem passar pelo
    /// administrador — e essa multiplicação é exatamente o que a hierarquia existe para conter.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string[]> Delegacoes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Administrador"] =
        [
            "Administrador", "Gerente", "Atendente", "Cozinha", "Operador", "Entregador",
        ],

        ["Gerente"] =
        [
            "Atendente", "Cozinha", "Operador", "Entregador",
        ],

        // Não delegam. Declarados de propósito: a ausência de um perfil nesta matriz é erro de
        // partida, então "não delega ninguém" precisa ser dito, e não omitido.
        ["Atendente"] = [],
        ["Cozinha"] = [],
        ["Operador"] = [],
        ["Entregador"] = [],
    };

    /// <summary>
    /// O perfil <paramref name="perfilDeQuemCria"/> pode atribuir
    /// <paramref name="perfilAlvo"/>?
    /// <para>
    /// Perfil desconhecido devolve <c>false</c>: negar o que não se conhece é a única resposta
    /// segura, e a validação na partida garante que "desconhecido" não aconteça em produção.
    /// </para>
    /// </summary>
    public static bool PodeAtribuir(string? perfilDeQuemCria, string? perfilAlvo)
    {
        if (string.IsNullOrWhiteSpace(perfilDeQuemCria) || string.IsNullOrWhiteSpace(perfilAlvo))
        {
            return false;
        }

        return Delegacoes.TryGetValue(perfilDeQuemCria, out var permitidos)
            && permitidos.Contains(perfilAlvo, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Os perfis que este perfil pode atribuir. Serve à tela, que só oferece o que o servidor
    /// aceitaria — <b>esconder não é impedir</b>, e a checagem de verdade é
    /// <see cref="PodeAtribuir"/>.
    /// </summary>
    public static IReadOnlyList<string> DelegaveisPor(string? perfil) =>
        perfil is not null && Delegacoes.TryGetValue(perfil, out var permitidos)
            ? permitidos
            : [];

    /// <summary>Perfis declarados na matriz — usado pela validação na partida.</summary>
    public static IReadOnlyCollection<string> PerfisDeclarados => Delegacoes.Keys;

    /// <summary>
    /// O usuário de plataforma (<c>RN-GER-014</c>) não tem perfil de unidade e não passa por
    /// esta matriz: ele não cria usuário de restaurante pelas rotas de unidade. Se um dia
    /// criar, será por rota de plataforma, com a sua própria regra.
    /// </summary>
    public const string MotivoDaRecusa =
        "RN-GER-024 — o seu perfil não pode atribuir o perfil {0}.";
}
