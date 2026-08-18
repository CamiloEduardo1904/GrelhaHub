namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// A ponte entre a transação do negócio e o banco de log — ADR-0017.
/// <para>
/// ⚠⚠ <b>Existe por uma razão só, e ela não é conveniência.</b> A trilha de auditoria sempre gravou
/// na MESMA transação da operação: ou as duas gravam, ou nenhuma grava. É essa propriedade que a
/// torna confiável — não existe operação sem registro. Bancos diferentes não têm transação em
/// comum, e escrever direto no banco de log significaria: a operação grava, a escrita da trilha
/// falha, e ninguém fica sabendo.
/// </para>
/// <para>
/// Então a linha da trilha é gravada <b>aqui</b>, no banco da aplicação, dentro da transação. Um
/// serviço em segundo plano a copia para o banco de log e a apaga daqui.
/// </para>
/// <para>
/// ⚠ <b>O <c>Id</c> é o mesmo dos dois lados</b>, e é o que torna a cópia idempotente: o worker
/// pode gravar no log e cair antes de apagar da outbox, e a segunda tentativa não pode duplicar a
/// linha.
/// </para>
/// </summary>
public sealed class OutboxDeAuditoria
{
    private OutboxDeAuditoria()
    {
    }

    public OutboxDeAuditoria(LogAuditoria registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        Id = registro.Id;
        EnfileiradoEm = registro.OcorridoEm;

        UnidadeId = registro.UnidadeId;
        Entidade = registro.Entidade;
        RegistroId = registro.RegistroId;
        Operacao = registro.Operacao;
        ValoresAnteriores = registro.ValoresAnteriores;
        ValoresNovos = registro.ValoresNovos;
        Justificativa = registro.Justificativa;
        UsuarioId = registro.UsuarioId;
        OcorridoEm = registro.OcorridoEm;
        DataOperacional = registro.DataOperacional;
    }

    /// <summary>
    /// Reconstrói o registro para gravar no banco de log.
    /// <para>
    /// ⚠ Passa pelo CONSTRUTOR de <see cref="LogAuditoria"/>, e não por atribuição direta: é ele
    /// que recusa registro sem valores anteriores nem novos (<c>RN-GER-011</c>). A outbox transporta
    /// o que já foi validado, e reconstruir pelo mesmo caminho garante que nada inválido atravesse
    /// a ponte entre os bancos.
    /// </para>
    /// </summary>
    public LogAuditoria ParaTrilha() =>
        new LogAuditoria(
            UnidadeId, Entidade, RegistroId, Operacao, ValoresAnteriores, ValoresNovos,
            Justificativa, UsuarioId, OcorridoEm)
            // O mesmo id dos dois lados — é o que torna a cópia idempotente.
            .PreservandoId(Id);

    /// <summary>O mesmo id da linha no banco de log — ver o resumo.</summary>
    public Guid Id { get; private set; }

    // ── O conteúdo do registro, em colunas ───────────────────────────────────────
    //
    // ⚠ Tentei guardar o registro como DOCUMENTO — `OwnsOne(o => o.Registro).ToJson()` — e o EF
    // Core recusou: `LogAuditoria` não pode ser entidade no contexto de log e propriedade de
    // outra entidade no contexto da aplicação dentro do mesmo modelo.
    //
    // As colunas espelhadas são a segunda escolha, e têm um custo real: precisam acompanhar a
    // trilha quando ela ganhar campo. `ParaTrilha()` é onde esse acoplamento fica VISÍVEL — quem
    // acrescentar coluna na trilha quebra a compilação ali, que é o lugar certo para descobrir.
    // Espelhamento silencioso seria pior: a coluna nova simplesmente não atravessaria a ponte.

    public Guid? UnidadeId { get; private set; }

    public string Entidade { get; private set; } = null!;

    public Guid RegistroId { get; private set; }

    public OperacaoDeAuditoria Operacao { get; private set; }

    public string? ValoresAnteriores { get; private set; }

    public string? ValoresNovos { get; private set; }

    public string? Justificativa { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }

    /// <summary>
    /// ⚠ Calculada no banco da APLICAÇÃO e transportada — nunca recalculada. Ver o contexto de log.
    /// </summary>
    public DateOnly DataOperacional { get; private set; }

    public DateTimeOffset EnfileiradoEm { get; private set; }

    /// <summary>
    /// Quantas vezes a cópia falhou.
    /// <para>
    /// ⚠ A linha <b>não</b> é descartada depois de N tentativas: trilha perdida é o que a outbox
    /// existe para impedir. O contador serve para o alerta — uma linha com dez tentativas significa
    /// banco de log fora do ar, e isso precisa de gente, não de descarte automático.
    /// </para>
    /// </summary>
    public int Tentativas { get; private set; }

    public string? UltimoErro { get; private set; }

    public void RegistrarFalha(string erro)
    {
        Tentativas += 1;
        UltimoErro = erro.Length > 500 ? erro[..500] : erro;
    }
}
