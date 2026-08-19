namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// As permissões que o sistema conhece e os perfis que as agrupam — <c>SPEC-GER-004</c>,
/// <c>RN-GER-010</c>.
/// <para>
/// <b>A permissão é granular (<c>recurso.acao</c>), nunca o nome do perfil.</b> Verificar
/// <c>perfil == "Gerente"</c> espalhado pelo código torna impossível criar um perfil novo sem
/// caçar condicionais — e é exatamente o que `RN-GER-022` (perfil Operador) exigiria se o
/// desenho fosse por nome.
/// </para>
/// <para>
/// Cada permissão declara o **módulo** a que pertence: o gate de módulo
/// (<c>RN-GER-017</c>) e o de permissão (<c>RN-GER-010</c>) respondem perguntas diferentes, e
/// uma permissão de módulo não contratado nunca deveria ser concedida.
/// </para>
/// </summary>
public static class CatalogoDeAcesso
{
    /// <summary>
    /// ADR-0022 §3 — o tipo da claim de papel que carrega uma permissão concedida
    /// (<c>perfil_permissao.tipo</c>). O valor é sempre um código <c>recurso.acao</c> desta
    /// lista. Constante única para emissor, semeador, política e visão não divergirem por
    /// literal repetido.
    /// </summary>
    public const string TipoDeClaimDePermissao = "permissao";

    public sealed record Permitir(string Codigo, string Descricao, CodigoDeModulo Modulo);

    /// <summary>
    /// Permissões existentes. **Só entram aqui as dos módulos já implementados**: `producao.*`,
    /// `entrega.*`, `estoque.*` e `caixa.*` nascem com as suas ondas. Declarar permissão de
    /// funcionalidade que não existe cria a ilusão de que alguém pode fazer algo que o sistema
    /// não faz.
    /// </summary>
    public static IReadOnlyList<Permitir> Permissoes { get; } =
    [
        // ── GER — núcleo ───────────────────────────────────────────────────────────────
        new("parametro.visualizar", "Consultar parâmetros do sistema", CodigoDeModulo.Gerais),
        new("parametro.alterar", "Alterar valor de parâmetro", CodigoDeModulo.Gerais),
        new("usuario.visualizar", "Consultar usuários da unidade", CodigoDeModulo.Gerais),
        new("usuario.gerenciar", "Criar, alterar e desativar usuários", CodigoDeModulo.Gerais),
        new("modulo.visualizar", "Consultar módulos contratados", CodigoDeModulo.Gerais),

        // RN-GER-036 / RN-GER-037 — o cadastro da própria casa: endereço e horário de
        // funcionamento. Duas permissões e não uma, pelo mesmo motivo de `usuario.*`: ler o
        // horário é rotina de quem atende ("a gente abre às 18h"), e mudá-lo move a régua do
        // dia operacional (RN-GER-006) — que é decisão de quem responde pelo faturamento.
        new("unidade.visualizar", "Ver endereço e horário de funcionamento", CodigoDeModulo.Gerais),
        new("unidade.gerenciar", "Alterar endereço e horário de funcionamento", CodigoDeModulo.Gerais),

        // RN-GER-012 — a trilha guarda valores anteriores de campos sensíveis. Abri-la ao
        // atendente contornaria a restrição de custo por outra porta.
        new("auditoria.consultar", "Consultar a trilha de auditoria", CodigoDeModulo.Gerais),

        // ── VEN — cardápio e pedido ────────────────────────────────────────────────────
        new("cardapio.visualizar", "Ver o cardápio pela gestão", CodigoDeModulo.Vendas),
        new("cardapio.gerenciar", "Criar e alterar produto e disponibilidade", CodigoDeModulo.Vendas),
        new("pedido.visualizar", "Ver pedidos", CodigoDeModulo.Vendas),
        new("pedido.avancar", "Confirmar e avançar o pedido no ciclo", CodigoDeModulo.Vendas),
        new("pedido.cancelar", "Cancelar pedido com motivo", CodigoDeModulo.Vendas),

        // RN-VEN-041 — excluir é reconhecer que o pedido nunca deveria ter existido. Separada
        // de `pedido.cancelar` de propósito: cancelar é fato de negócio e entra na métrica;
        // excluir tira da apuração, e por isso é poder de administrador.
        new("pedido.excluir", "Excluir logicamente pedido lançado por engano", CodigoDeModulo.Vendas),

        // RN-GER-012 — a permissão que decide se custo, CMV e margem SAEM do servidor.
        new("custo.visualizar", "Ver custo, CMV e margem", CodigoDeModulo.Vendas),

        // RN-GER-038 — o painel de operação, SEPARADO de `pedido.visualizar`.
        //
        // ⚠ A separação é o ponto. Quem atende o telefone precisa ver o pedido da mesa 4; o
        // faturamento da semana é outra pergunta e responde a quem responde pelo resultado.
        // Junto com `pedido.visualizar`, o agregado de venda — que é o passo anterior a margem —
        // chegaria a todo atendente por uma porta lateral que `RN-GER-012` fecha na porta da
        // frente.
        new("operacao.visualizar", "Ver o painel de operação e o faturamento", CodigoDeModulo.Vendas),
    ];

    /// <summary>
    /// Perfis semeados em toda unidade nova, com as suas permissões.
    /// <para>
    /// <c>RN-GER-010</c> não fixa os perfis — vêm do levantamento §4, mais o Operador de
    /// <c>RN-GER-022</c>.
    /// </para>
    /// </summary>
    public sealed record PerfilPadrao(string Nome, string Descricao, string[] Permissoes);

    public static IReadOnlyList<PerfilPadrao> Perfis { get; } =
    [
        // Todas as permissões — inclusive as que ainda não existem, por Todas().
        new("Administrador", "Acesso total à unidade", Todas()),

        new("Gerente", "Operação completa, custo e parâmetros",
        [
            "parametro.visualizar", "parametro.alterar",
            "usuario.visualizar",

            // RN-GER-025 — o gerente administra os usuários da própria unidade.
            //
            // ⚠ Esta permissão SÓ é segura por causa de `RN-GER-024`: sem a matriz de
            // delegação, `usuario.gerenciar` permitiria ao gerente criar um Administrador e
            // passar a ter o acesso que ninguém lhe deu. Se algum dia a matriz for removida,
            // esta linha tem de sair com ela.
            "usuario.gerenciar",
            "modulo.visualizar",

            // RN-GER-036 — o gerente cadastra o horário: é ele que responde pelo faturamento do
            // dia, e a virada do dia operacional sai desse cadastro.
            "unidade.visualizar", "unidade.gerenciar",
            "auditoria.consultar",
            "cardapio.visualizar", "cardapio.gerenciar",
            "pedido.visualizar", "pedido.avancar", "pedido.cancelar",
            "custo.visualizar",

            // RN-GER-038 — o gerente responde pelo resultado do dia; é dele a pergunta que o
            // painel de operação responde.
            "operacao.visualizar",
        ]),

        // Sem cancelar, sem excluir, sem custo: RN-GER-012 é restrição de dado, e o atendente
        // não pode saber a margem.
        new("Atendente", "Atendimento e lançamento de pedido",
        [
            "parametro.visualizar",
            "modulo.visualizar",

            // Só LER o horário: quem atende o telefone precisa responder "a gente abre às 18h".
            "unidade.visualizar",
            "cardapio.visualizar",
            "pedido.visualizar", "pedido.avancar",
        ]),

        // A cozinha move o pedido no ciclo e não vê preço nem custo.
        new("Cozinha", "Produção — avança o pedido no ciclo",
        [
            "modulo.visualizar",
            "pedido.visualizar", "pedido.avancar",
        ]),

        // RN-GER-022 — perfil de visualização simplista. Conjunto PROVISÓRIO: o time decidiu
        // que o perfil existe, não ainda o que exatamente ele vê. Semeado com o mínimo
        // defensável — errar para menos gera pedido de acesso; errar para mais entrega
        // capacidade que ninguém autorizou.
        new("Operador", "Visualização simplista — provisório, ver RN-GER-022",
        [
            "modulo.visualizar",
            "cardapio.visualizar",
            "pedido.visualizar",
        ]),

        // O entregador nasce SEM permissão: as dele são `entrega.*`, do módulo LOG, que não
        // existe. Perfil vazio é honesto; perfil com permissão inventada, não.
        new("Entregador", "Entrega — permissões entram com o módulo LOG", []),
    ];

    public static string[] Todas() =>
        Permissoes.Select(permissao => permissao.Codigo).ToArray();

    /// <summary>Perfis que a unidade não pode renomear nem excluir: são do sistema.</summary>
    public static bool EhPerfilDeSistema(string nome) =>
        Perfis.Any(perfil => string.Equals(perfil.Nome, nome, StringComparison.OrdinalIgnoreCase));
}
