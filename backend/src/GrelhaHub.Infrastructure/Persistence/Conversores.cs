using GrelhaHub.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// <c>RN-GER-003</c> — <see cref="Dinheiro"/> ↔ <c>numeric(12,2)</c>.
/// <para>
/// A precisão não vem daqui: vem da convenção em <c>GrelhaHubDbContext</c>. O conversor só
/// atravessa a fronteira do tipo.
/// </para>
/// </summary>
internal sealed class ConversorDeDinheiro : ValueConverter<Dinheiro, decimal>
{
    public ConversorDeDinheiro()
        : base(dinheiro => dinheiro.Valor, valor => Dinheiro.De(valor))
    {
    }
}

/// <summary><c>RN-GER-004</c> — <see cref="Quantidade"/> ↔ <c>numeric(14,4)</c>.</summary>
internal sealed class ConversorDeQuantidade : ValueConverter<Quantidade, decimal>
{
    public ConversorDeQuantidade()
        : base(quantidade => quantidade.Valor, valor => Quantidade.De(valor))
    {
    }
}

/// <summary>
/// <c>RN-GER-023</c> — <see cref="Cpf"/> ↔ <c>char(11)</c>.
/// <para>
/// Grava os 11 dígitos sem máscara (<c>RN-GER-013</c>). A volta usa <c>Cpf.De</c>, que valida:
/// se algum dia um CPF inválido entrar por fora da aplicação, a leitura falha alto em vez de
/// materializar um documento que o sistema afirma ter validado.
/// </para>
/// </summary>
internal sealed class ConversorDeCpf : ValueConverter<Cpf, string>
{
    public ConversorDeCpf()
        : base(cpf => cpf.Valor, valor => Cpf.De(valor))
    {
    }
}
