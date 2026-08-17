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
    public bool Ativa { get; private set; } = true;
    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary><c>RN-GER-005</c> — a operação ocorre neste fuso.</summary>
    public string FusoHorario { get; private set; } = "America/Sao_Paulo";

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
