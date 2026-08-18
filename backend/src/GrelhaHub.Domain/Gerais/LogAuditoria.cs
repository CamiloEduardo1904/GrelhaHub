using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// O que aconteceu com um registro — <c>RN-GER-011</c>.
/// <para>
/// <b><c>Exclusao</c> significa exclusão LÓGICA</b> (<c>RN-VEN-041</c>), nunca física. A versão
/// original de <c>SPEC-GER-005</c> omitia este valor de propósito, argumentando que
/// <c>RN-GER-002</c> torna a exclusão impossível — e admiti-la sugeriria que ela pode acontecer.
/// O argumento continua valendo para exclusão física; a exclusão lógica nasceu depois, com
/// <c>RN-VEN-041</c>, e precisa ser distinguível de um cancelamento na trilha.
/// </para>
/// </summary>
public enum OperacaoDeAuditoria
{
    Criacao = 1,
    Alteracao = 2,
    Cancelamento = 3,
    Exclusao = 4,
}

/// <summary>
/// Registro imutável da trilha de auditoria — <c>RN-GER-011</c>, <c>SPEC-GER-005</c>.
/// <para>
/// <b>É livro-razão:</b> só admite <c>INSERT</c> e <c>SELECT</c>. Não há rota que altere ou
/// exclua, e o interceptor recusa qualquer entrada <c>Modified</c> ou <c>Deleted</c> sobre esta
/// tabela. <b>Uma trilha alterável não é trilha.</b>
/// </para>
/// <para>
/// Gravado <b>na mesma transação</b> da operação auditada: auditoria que grava depois, fora da
/// transação, perde o registro justamente quando a operação foi anômala.
/// </para>
/// </summary>
public sealed class LogAuditoria : Entidade, IPossuiDiaOperacional
{
    private LogAuditoria() { }

    public LogAuditoria(
        Guid? unidadeId,
        string entidade,
        Guid registroId,
        OperacaoDeAuditoria operacao,
        string? valoresAnteriores,
        string? valoresNovos,
        string? justificativa,
        Guid? usuarioId,
        DateTimeOffset ocorridoEm)
    {
        if (string.IsNullOrWhiteSpace(entidade))
        {
            throw new ArgumentException("A entidade auditada é obrigatória.", nameof(entidade));
        }

        if (valoresAnteriores is null && valoresNovos is null)
        {
            // Registro sem antes nem depois não diz nada. Se chegou aqui, o interceptor
            // calculou um conjunto vazio de mudanças e não deveria ter gravado.
            throw new ArgumentException(
                "RN-GER-011 — a trilha exige valores anteriores, novos, ou ambos.", nameof(valoresNovos));
        }

        UnidadeId = unidadeId;
        Entidade = entidade;
        RegistroId = registroId;
        Operacao = operacao;
        ValoresAnteriores = valoresAnteriores;
        ValoresNovos = valoresNovos;
        Justificativa = justificativa;
        UsuarioId = usuarioId;
        OcorridoEm = ocorridoEm;
    }

    /// <summary>
    /// <c>RN-GER-009</c> — a unidade do registro auditado.
    /// <para>
    /// <b>Nulo quando o registro não pertence a unidade alguma</b> — o próprio usuário de
    /// plataforma (<c>RN-GER-014</c>) e os catálogos globais. A §6 original desta spec previa
    /// <c>NOT NULL</c>; `RN-GER-014` nasceu depois e criou registros sem unidade.
    /// </para>
    /// </summary>
    public Guid? UnidadeId { get; private set; }

    /// <summary>Nome da tabela — <c>pedido</c>, <c>parametro_valor</c>.</summary>
    public string Entidade { get; private set; } = null!;

    public Guid RegistroId { get; private set; }

    public OperacaoDeAuditoria Operacao { get; private set; }

    /// <summary>
    /// <c>jsonb</c> com <b>apenas as propriedades que mudaram</b>. Auditar 40 colunas para
    /// registrar a alteração de uma torna a trilha ilegível — e a trilha existe para ser lida.
    /// </summary>
    public string? ValoresAnteriores { get; private set; }

    public string? ValoresNovos { get; private set; }

    /// <summary>
    /// Por que a operação aconteceu, quando a própria entidade guarda essa explicação — motivo do
    /// cancelamento, justificativa da alteração de parâmetro.
    /// </summary>
    public string? Justificativa { get; private set; }

    /// <summary>
    /// Quem fez.
    /// <para>
    /// <b>Nulo quando a operação é anônima</b>: o pedido do cardápio público (<c>RN-VEN-038</c>) é
    /// criado por um cliente que não é usuário do sistema, e o cadastro de restaurante roda antes
    /// de existir qualquer usuário. A §6 original previa <c>NOT NULL</c>, o que tornaria essas
    /// duas operações impossíveis de auditar — pior que auditá-las sem autor.
    /// </para>
    /// </summary>
    public Guid? UsuarioId { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }

    /// <summary><c>RN-GER-006</c> ⚠⚠ — coluna gerada. A aplicação lê, nunca escreve.</summary>
    public DateOnly DataOperacional { get; private set; }
}

/// <summary>
/// Tipo da mudança, como o domínio a vê — sem depender do <c>EntityState</c> do EF Core, que é
/// detalhe de plataforma e não pode aparecer em <c>GrelhaHub.Domain</c>.
/// </summary>
public enum TipoDeMudanca
{
    Insercao = 1,
    Atualizacao = 2,
}

/// <summary>
/// Entidade cujas mudanças entram na trilha — <c>RN-GER-011</c>.
/// <para>
/// <b>Marcar a entidade é item da definição de pronto de quem a cria.</b> A lista cresce a cada
/// spec: cancelamento de pedido, alteração de parâmetro, ajuste de estoque, fechamento de caixa.
/// </para>
/// </summary>
public interface IAuditavelDetalhado
{
    /// <summary>
    /// Explicação que a própria entidade guarda — motivo do cancelamento, justificativa da
    /// alteração. Nulo quando a operação não pede uma.
    /// </summary>
    string? JustificativaDaAuditoria => null;

    /// <summary>
    /// Classifica a operação a partir do que mudou.
    /// <para>
    /// <b>Quem classifica é a entidade</b>, e não o interceptor: distinguir "cancelou" de
    /// "alterou" é conhecimento de negócio. Um interceptor que soubesse ler
    /// <c>Status == Cancelado</c> passaria a conhecer o ciclo de vida do pedido, e a próxima
    /// entidade auditável exigiria mais um <c>if</c> lá dentro.
    /// </para>
    /// </summary>
    OperacaoDeAuditoria ClassificarAuditoria(
        TipoDeMudanca mudanca, IReadOnlySet<string> propriedadesAlteradas) =>
        mudanca == TipoDeMudanca.Insercao ? OperacaoDeAuditoria.Criacao : OperacaoDeAuditoria.Alteracao;
}

/// <summary>
/// Propriedade que <b>nunca</b> entra na trilha, nem como valor anterior — hash de senha, token,
/// segredo. O interceptor grava <c>"***"</c> no lugar.
/// <para>
/// A trilha é lida por gerência, e um hash antigo na trilha é um hash a mais em circulação.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class NaoAuditarAttribute : Attribute;
