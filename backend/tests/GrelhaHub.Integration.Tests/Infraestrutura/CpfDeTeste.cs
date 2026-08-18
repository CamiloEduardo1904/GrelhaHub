namespace GrelhaHub.Integration.Tests.Infraestrutura;

/// <summary>
/// Gera CPF <b>válido</b> para teste — <c>RN-GER-023</c>.
/// <para>
/// Existe porque `Cpf` recusa dígito verificador errado: um CPF escrito à mão no teste passaria
/// a falhar na validação, e o teste mediria a validação em vez do comportamento que ele quer
/// medir. Aqui os nove primeiros dígitos vêm da semente e os dois últimos são calculados.
/// </para>
/// <para>
/// <b>Não é o algoritmo da produção reimplementado por acaso.</b> É de propósito: um gerador que
/// chamasse `Cpf.De` para se validar não provaria nada, e um que copiasse a implementação
/// esconderia um erro nela. Este calcula os verificadores de forma independente, e o teste de
/// domínio `CpfTests` compara as duas contas em casos conhecidos.
/// </para>
/// </summary>
internal static class CpfDeTeste
{
    /// <summary>
    /// CPF válido e determinístico para a semente. Sementes diferentes dão CPFs diferentes, o
    /// que importa porque o CPF é único em todo o sistema.
    /// </summary>
    public static string Gerar(int semente)
    {
        // 100_000_000 + resto garante nove dígitos sem zero à esquerda, e evita a faixa de
        // sequências repetidas (que `Cpf` recusa mesmo com verificador correto).
        var base9 = (100_000_000 + Math.Abs(semente) % 800_000_000).ToString(
            System.Globalization.CultureInfo.InvariantCulture);

        var comPrimeiro = base9 + Verificador(base9, pesoInicial: 10);
        return comPrimeiro + Verificador(comPrimeiro, pesoInicial: 11);
    }

    private static char Verificador(string digitos, int pesoInicial)
    {
        var soma = 0;

        for (var i = 0; i < digitos.Length; i++)
        {
            soma += (digitos[i] - '0') * (pesoInicial - i);
        }

        var resto = soma * 10 % 11;

        return (char)('0' + (resto == 10 ? 0 : resto));
    }
}
