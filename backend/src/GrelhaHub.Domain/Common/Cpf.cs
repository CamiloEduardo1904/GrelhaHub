namespace GrelhaHub.Domain.Common;

/// <summary>
/// O CPF da pessoa — <c>RN-GER-023</c>, <c>RN-GER-013</c>.
/// <para>
/// Value object pelo mesmo motivo de <see cref="Dinheiro"/> e <see cref="Quantidade"/>
/// (<c>SPEC-GER-001</c> §4): <b>uma <c>string</c> crua não distingue CPF de CNPJ, de telefone
/// nem de slug</b>, e o compilador não impede passar um no lugar do outro. Os quatro são texto
/// de dígitos, e trocar dois deles numa chamada de construtor não gera erro — gera dado errado
/// que parece certo.
/// </para>
/// <para>
/// Guarda <b>11 dígitos</b>, sem máscara (<c>RN-GER-013</c>). A máscara é da tela: gravar
/// <c>529.982.247-25</c> torna toda busca dependente de o usuário digitar o ponto no mesmo
/// lugar.
/// </para>
/// </summary>
public readonly record struct Cpf
{
    /// <summary>Quantidade de dígitos — <c>char(11)</c> no banco.</summary>
    public const int Digitos = 11;

    private Cpf(string valor) => Valor = valor;

    /// <summary>Os 11 dígitos, sem pontuação.</summary>
    public string Valor { get; }

    /// <summary>
    /// Único caminho de construção. Aceita com ou sem máscara e <b>recusa</b> o que não é CPF.
    /// </summary>
    /// <exception cref="ArgumentException">Quando não é um CPF válido.</exception>
    public static Cpf De(string? texto)
    {
        if (!TentarDe(texto, out var cpf))
        {
            throw new ArgumentException(
                "RN-GER-023 — o CPF informado não é válido.", nameof(texto));
        }

        return cpf;
    }

    /// <summary>
    /// Versão que não lança — para o validator, que precisa da mensagem amigável e não da
    /// exceção.
    /// </summary>
    public static bool TentarDe(string? texto, out Cpf cpf)
    {
        cpf = default;

        var digitos = SomenteDigitos(texto);

        if (digitos.Length != Digitos)
        {
            return false;
        }

        // Sequência repetida passa no cálculo dos verificadores e não é CPF de ninguém.
        // Validar só a conta deixaria entrar onze CPFs falsos, e 111.111.111-11 é o primeiro
        // que alguém digita para testar um formulário.
        if (EhSequenciaRepetida(digitos))
        {
            return false;
        }

        if (!VerificadoresConferem(digitos))
        {
            return false;
        }

        cpf = new Cpf(digitos);
        return true;
    }

    /// <summary>Formata para exibição. <b>Só apresentação</b> — nunca volta para o banco.</summary>
    public string Formatado() =>
        $"{Valor[..3]}.{Valor[3..6]}.{Valor[6..9]}-{Valor[9..]}";

    public override string ToString() => Valor;

    private static string SomenteDigitos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        return new string(texto.Where(char.IsAsciiDigit).ToArray());
    }

    private static bool EhSequenciaRepetida(string digitos) =>
        digitos.All(digito => digito == digitos[0]);

    /// <summary>
    /// Os dois dígitos verificadores, pelo algoritmo de módulo 11 da Receita Federal.
    /// <para>
    /// O primeiro verificador é calculado sobre os 9 primeiros dígitos com pesos de 10 a 2; o
    /// segundo, sobre os 10 primeiros com pesos de 11 a 2. Resto 0 ou 1 produz dígito 0.
    /// </para>
    /// </summary>
    private static bool VerificadoresConferem(string digitos)
    {
        var primeiro = DigitoVerificador(digitos, ate: 9, pesoInicial: 10);
        var segundo = DigitoVerificador(digitos, ate: 10, pesoInicial: 11);

        return digitos[9] == primeiro && digitos[10] == segundo;
    }

    private static char DigitoVerificador(string digitos, int ate, int pesoInicial)
    {
        var soma = 0;

        for (var i = 0; i < ate; i++)
        {
            soma += (digitos[i] - '0') * (pesoInicial - i);
        }

        var resto = soma * 10 % 11;

        return (char)('0' + (resto == 10 ? 0 : resto));
    }
}

/// <summary>
/// Conversão explícita para o modelo do EF Core, que precisa de <c>string</c>.
/// </summary>
public static class CpfExtensoes
{
    /// <summary>Nulo quando o usuário não tem CPF — plataforma (<c>RN-GER-014</c>).</summary>
    public static string? ValorOuNulo(this Cpf? cpf) => cpf?.Valor;
}
