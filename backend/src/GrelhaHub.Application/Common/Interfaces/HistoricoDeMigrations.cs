namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// O guarda que faltava no <c>--migrate</c> — ADR-0022 §6, 20/08/2026.
/// <para>
/// <b>Nasceu de um deploy quebrado, e o valor dele é a MENSAGEM.</b> A ADR-0022 substituiu 18
/// migrations por uma só, e a tabela dela dizia <i>"não há dado a preservar — o harness recria o
/// banco a cada execução"</i>. Isso era verdade do harness de teste e <b>falso da VPS</b>, que tem
/// banco persistente com as 18 antigas no <c>__EFMigrationsHistory</c>.
/// </para>
/// <para>
/// O que o deploy mostrou foi <c>CREATE TABLE arquivo</c> falhando — porque o EF, não achando
/// nenhum ID conhecido no histórico, concluiu que o banco estava vazio e começou do zero. Quem
/// lesse o log entenderia "erro de SQL", e a informação de verdade — <b>este banco tem migrations
/// que este código não conhece</b> — não estava em lugar nenhum.
/// </para>
/// </summary>
public static class HistoricoDeMigrations
{
    /// <summary>
    /// As migrations <b>aplicadas no banco</b> que este código <b>não conhece</b>.
    /// <para>
    /// Lista vazia é o caso normal — inclusive num banco novo, onde nada foi aplicado ainda, e num
    /// banco em dia, onde tudo o que foi aplicado é conhecido.
    /// </para>
    /// <para>
    /// ⚠ <b>A pergunta é deliberadamente esta, e não "faltam migrations".</b> Faltar é o caso
    /// comum: é o que todo deploy resolve. <b>Sobrar</b> é o sintoma de que o banco veio de outro
    /// caminho — histórico reescrito, migration apagada, ou dois códigos diferentes apontando para
    /// o mesmo banco. Nenhum desses se resolve aplicando mais nada.
    /// </para>
    /// <para>
    /// ⚠ Comparação por <b>ordinal</b>: id de migration é identificador técnico, e comparar com a
    /// cultura da máquina faria o deploy depender de onde ele roda.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Orfas(
        IEnumerable<string> aplicadas, IEnumerable<string> conhecidas)
    {
        ArgumentNullException.ThrowIfNull(aplicadas);
        ArgumentNullException.ThrowIfNull(conhecidas);

        var doCodigo = new HashSet<string>(conhecidas, StringComparer.Ordinal);

        return [.. aplicadas.Where(id => !doCodigo.Contains(id)).OrderBy(id => id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// A mensagem que o deploy precisa ler — e o motivo de esta classe existir.
    /// <para>
    /// Ela diz <b>o que aconteceu</b>, <b>o que NÃO fazer</b> e <b>quais são as saídas</b>. Erro de
    /// infraestrutura sem caminho de saída vira tentativa e erro em cima de banco, que é o pior
    /// lugar do sistema para experimentar.
    /// </para>
    /// </summary>
    public static string Explicar(string banco, IReadOnlyList<string> orfas)
    {
        ArgumentNullException.ThrowIfNull(orfas);

        return $"""
            O banco '{banco}' tem {orfas.Count} migration(s) que este código NÃO conhece:

              {string.Join("\n  ", orfas)}

            Isto NÃO é migration pendente — é o contrário. O banco foi criado por um código
            diferente deste, e aplicar a migration atual em cima dele falharia no primeiro
            CREATE TABLE, como se o banco estivesse vazio.

            Causa mais comum: o histórico de migrations foi reescrito (ADR-0022 §6 substituiu 18
            migrations por uma), e este banco é anterior à troca.

            As saídas, e nenhuma delas é "tentar de novo":

              1. Banco SEM dado a preservar — recriar o esquema e deixar a migration criar tudo:
                 DROP SCHEMA public CASCADE; CREATE SCHEMA public;
                 No pipeline: enfileirar com a variável recriarBanco=true.

              2. Banco COM dado a preservar — escrever a migration de transição do esquema antigo
                 para o atual. Não existe atalho aqui: baseline sem ajuste deixa o esquema
                 divergindo do modelo em silêncio, que é pior do que este erro.
            """;
    }
}
