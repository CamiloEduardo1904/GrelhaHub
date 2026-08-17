using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Crm;

/// <summary><c>RN-CRM-001</c> — o cliente é identificado unicamente pelo telefone.</summary>
public sealed class Cliente : Entidade, IPertenceAUnidade
{
    private Cliente() { }

    public Cliente(Guid unidadeId, string nome, string telefone)
    {
        UnidadeId = unidadeId;
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome do cliente é obrigatório.", nameof(nome))
            : nome.Trim();
        Telefone = NormalizarTelefone(telefone);
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public Guid UnidadeId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Telefone { get; private set; } = null!;
    public DateTimeOffset CriadoEm { get; private set; }

    public void AtualizarNome(string nome)
    {
        if (!string.IsNullOrWhiteSpace(nome))
        {
            Nome = nome.Trim();
        }
    }

    /// <summary>
    /// Guarda apenas dígitos: o mesmo telefone digitado como "(11) 98765-4321" e
    /// "11987654321" precisa resolver para o mesmo cliente, senão <c>RN-CRM-001</c> não vale.
    /// </summary>
    public static string NormalizarTelefone(string telefone)
    {
        var digitos = new string((telefone ?? string.Empty).Where(char.IsDigit).ToArray());

        return digitos.Length is < 10 or > 11
            ? throw new ArgumentException("Telefone inválido — informe DDD e número.", nameof(telefone))
            : digitos;
    }
}
