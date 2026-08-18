using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// O METADADO de um arquivo, no banco da aplicação — ADR-0018.
/// <para>
/// É esta entidade que participa das regras: pertence a uma unidade (<c>RN-GER-009</c>), tem
/// exclusão lógica (<c>RN-GER-002</c>) e entra na trilha de auditoria. Os bytes ficam no banco de
/// arquivo, com o mesmo <c>Id</c>.
/// </para>
/// <para>
/// ⚠⚠ <b>A ordem de gravação é BINÁRIO PRIMEIRO, METADADO DEPOIS</b>, e não é arbitrária. Não há
/// transação entre os dois bancos, então uma das duas falhas é inevitável — e escolhe-se qual:
/// </para>
/// <list type="bullet">
/// <item>metadado sem bytes ⇒ <b>imagem quebrada na vitrine</b>, visível para o cliente;</item>
/// <item>bytes sem metadado ⇒ <b>invisível para o sistema</b> e removível por varredura, porque o
/// banco de arquivo sabe quais ids nenhum metadado referencia.</item>
/// </list>
/// <para>
/// É a mesma escolha do ADR-0016 para e-mail: quando não há transação, <b>falhe do lado que não
/// mente para o usuário</b>.
/// </para>
/// </summary>
public sealed class Arquivo : Entidade, IPertenceAUnidade, IExcluivelLogicamente, IAuditavelDetalhado
{
    private Arquivo()
    {
    }

    public Arquivo(Guid unidadeId, string pasta, string tipoMime, int tamanhoEmBytes)
    {
        if (unidadeId == Guid.Empty)
        {
            throw new ArgumentException(
                "RN-GER-009 — todo arquivo pertence a uma unidade.", nameof(unidadeId));
        }

        UnidadeId = unidadeId;
        Pasta = pasta;
        TipoMime = tipoMime;
        TamanhoEmBytes = tamanhoEmBytes;
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public Guid UnidadeId { get; private set; }

    /// <summary>`produto` ou `logo`. Escolhida pelo código, nunca pelo usuário.</summary>
    public string Pasta { get; private set; } = null!;

    /// <summary>Da assinatura do arquivo, nunca da extensão (<c>RN-VEN-042</c>).</summary>
    public string TipoMime { get; private set; } = null!;

    public int TamanhoEmBytes { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? ExcluidoEm { get; private set; }

    /// <summary>
    /// <c>RN-GER-002</c> — exclusão LÓGICA.
    /// <para>
    /// ⚠ É isto que o disco não sabia fazer: produto desativado deixava o arquivo lá para sempre, e
    /// produto reativado dependia de o arquivo ainda existir. A exclusão parava na borda do sistema
    /// de arquivos.
    /// </para>
    /// <para>
    /// Os BYTES não são apagados junto: enquanto o metadado existir, mesmo excluído, o arquivo pode
    /// voltar. Quem apaga bytes é uma varredura deliberada, não um clique.
    /// </para>
    /// </summary>
    public void Excluir() => ExcluidoEm ??= DateTimeOffset.UtcNow;

    public bool Excluido => ExcluidoEm is not null;
}
