using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// A unidade de negócio — o restaurante. Raiz de isolamento dos dados: todo registro
/// transacional pertence a exatamente uma unidade (<c>RN-GER-009</c>, ADR-0005).
/// </summary>
public sealed class Unidade : Entidade
{
    private Unidade() { }

    public Unidade(string razaoSocial, string nomeFantasia, string cnpj, string slug, string? telefone)
    {
        RazaoSocial = Exigir(razaoSocial, nameof(razaoSocial));
        NomeFantasia = Exigir(nomeFantasia, nameof(nomeFantasia));
        Cnpj = SomenteDigitos(cnpj);
        Slug = ValidarSlug(slug);
        Telefone = telefone is null ? null : SomenteDigitos(telefone);
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public string RazaoSocial { get; private set; } = null!;
    public string NomeFantasia { get; private set; } = null!;
    public string Cnpj { get; private set; } = null!;

    /// <summary>
    /// Identificador público do cardápio online — <c>RN-VEN-036</c>.
    /// <para>
    /// <b>Imutável após a criação:</b> o link circula em rede social e material impresso, e
    /// alterá-lo quebraria o que já foi distribuído. Não existe setter, nem público nem
    /// interno — mudar o slug exige decisão de produto e uma operação explícita.
    /// </para>
    /// </summary>
    public string Slug { get; private set; } = null!;

    public string? Telefone { get; private set; }
    /// <summary>
    /// <c>RN-GER-029</c> — a unidade está operando.
    /// <para>
    /// ⚠ <b>Esta propriedade existiu com <c>private set</c> e sem nenhum método por três specs.</b>
    /// <c>Autenticar</c> já a consultava (<c>RN-GER-027</c>), então o bloqueio de acesso estava
    /// pronto e era <b>inalcançável</b>: não havia como inativar. Comportamento correto e sem
    /// caminho é indistinguível de comportamento ausente.
    /// </para>
    /// </summary>
    public bool Ativa { get; private set; } = true;

    /// <summary>
    /// <c>RN-GER-029</c> — para a operação da unidade. <b>Não apaga nada</b>
    /// (<c>RN-GER-002</c>): o cardápio público para de responder, os usuários param de autenticar
    /// (<c>RN-GER-027</c>) e todo o histórico permanece.
    /// </summary>
    public void Inativar() => Ativa = false;

    /// <summary>
    /// <c>RN-GER-029</c> — devolve o acesso. Não restaura nada, porque nada foi removido.
    /// </summary>
    public void Reativar() => Ativa = true;
    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary><c>RN-GER-005</c> — a operação ocorre neste fuso.</summary>
    public string FusoHorario { get; private set; } = "America/Sao_Paulo";

    /// <summary>
    /// <c>RN-GER-026</c> — o caminho da logomarca, exibida no cardápio público
    /// (<c>RN-VEN-037</c>).
    /// <para>
    /// <b>Nula é estado válido:</b> unidade sem logo exibe o nome fantasia. A ausência não é erro
    /// a ser corrigido com imagem genérica — placeholder de "sem logo" numa vitrine de
    /// restaurante é pior do que o nome escrito.
    /// </para>
    /// </summary>
    public string? UrlLogo { get; private set; }

    /// <summary>
    /// <c>RN-GER-026</c> — aponta para a logo nova. <b>Não apaga a anterior</b>
    /// (<c>RN-GER-002</c>), pelo mesmo motivo de <c>Produto.DefinirImagem</c>.
    /// </summary>
    public void DefinirLogo(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            throw new ArgumentException("O caminho da logo é obrigatório.", nameof(caminho));
        }

        UrlLogo = caminho;
    }

    /// <summary>Limpa a referência. O arquivo permanece, por <c>RN-GER-002</c>.</summary>
    public void RemoverLogo() => UrlLogo = null;

    public void Renomear(string razaoSocial, string nomeFantasia, string? telefone)
    {
        RazaoSocial = Exigir(razaoSocial, nameof(razaoSocial));
        NomeFantasia = Exigir(nomeFantasia, nameof(nomeFantasia));
        Telefone = telefone is null ? null : SomenteDigitos(telefone);
    }

    private static string Exigir(string valor, string campo) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException($"{campo} é obrigatório.", campo)
            : valor.Trim();

    /// <summary><c>RN-GER-013</c> — documento e telefone sem máscara. A máscara é apresentação.</summary>
    private static string SomenteDigitos(string valor) =>
        new(valor.Where(char.IsDigit).ToArray());

    private static string ValidarSlug(string slug)
    {
        var limpo = (slug ?? string.Empty).Trim().ToLowerInvariant();

        if (!System.Text.RegularExpressions.Regex.IsMatch(limpo, "^[a-z0-9-]{3,60}$"))
        {
            throw new ArgumentException(
                "RN-VEN-036 — o identificador público aceita apenas letras minúsculas, " +
                "números e hífen, com 3 a 60 caracteres.", nameof(slug));
        }

        return limpo;
    }
}
