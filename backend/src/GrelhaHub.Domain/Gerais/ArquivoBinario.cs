namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Os BYTES de um arquivo, no banco de arquivo — ADR-0018.
/// <para>
/// ⚠ <b>Esta entidade não tem regra nenhuma, e é de propósito.</b> Ela é um monte de bytes com um
/// identificador. Tudo o que é regra — a que unidade pertence, se foi excluído, quem o enviou —
/// vive no METADADO, no banco da aplicação, onde participa do filtro de unidade, da exclusão lógica
/// e da trilha de auditoria.
/// </para>
/// <para>
/// A separação é o que permite o <c>JOIN</c> que importa (<c>produto → arquivo</c>) continuar sendo
/// uma consulta só, no mesmo banco. Só a busca dos bytes atravessa para o outro, e ela acontece uma
/// vez por imagem — não por listagem.
/// </para>
/// </summary>
public sealed class ArquivoBinario
{
    private ArquivoBinario()
    {
    }

    public ArquivoBinario(Guid id, byte[] conteudo, string tipoMime)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (conteudo.Length == 0)
        {
            throw new ArgumentException("Arquivo vazio não é arquivo.", nameof(conteudo));
        }

        Id = id;
        Conteudo = conteudo;
        TipoMime = tipoMime;
    }

    /// <summary>
    /// ⚠ O MESMO id do metadado, e não um id próprio. É o que liga os dois lados sem chave
    /// estrangeira — que não existe entre bancos — e o que permite a varredura de órfãos: binário
    /// cujo id nenhum metadado referencia.
    /// </summary>
    public Guid Id { get; private set; }

    public byte[] Conteudo { get; private set; } = [];

    /// <summary>
    /// Derivado da ASSINATURA do arquivo, nunca da extensão (<c>RN-VEN-042</c>).
    /// </summary>
    public string TipoMime { get; private set; } = null!;
}
