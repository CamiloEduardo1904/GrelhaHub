using GrelhaHub.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// Convenções de tipo do modelo — <c>SPEC-GER-001</c> §4.
/// <para>
/// Declaradas <b>uma vez</b> e em um ponto próprio, para que as 40 tabelas seguintes as herdem
/// sem redecidir, e para que a convenção possa ser <b>verificada por teste</b> sem depender de
/// já existir uma tabela que a use — o caso do <c>CA-4</c>, cuja coluna de quantidade só nasce
/// na onda 2.
/// </para>
/// </summary>
public static class ConvencoesDeTipo
{
    public static void Aplicar(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // RN-GER-003 — valores monetários em reais, duas casas decimais.
        configurationBuilder.Properties<Dinheiro>()
            .HaveConversion<ConversorDeDinheiro>()
            .HavePrecision(PrecisaoDeDinheiro, Dinheiro.CasasDecimais);

        // RN-GER-004 — quantidade de insumo com QUATRO casas, para gramas e mililitros.
        configurationBuilder.Properties<Quantidade>()
            .HaveConversion<ConversorDeQuantidade>()
            .HavePrecision(PrecisaoDeQuantidade, Quantidade.CasasDecimais);

        // RN-GER-023 — CPF sempre com 11 dígitos, sem máscara. `char(11)` e não `varchar`:
        // o tamanho é fixo por definição, e fixar no tipo evita a coluna aceitar 10.
        configurationBuilder.Properties<Cpf>()
            .HaveConversion<ConversorDeCpf>()
            .HaveColumnType($"char({Cpf.Digitos})");

        // Evita `text` por descuido.
        configurationBuilder.Properties<string>().HaveMaxLength(200);

        // NÃO existe convenção para `decimal` cru — e a ausência é deliberada.
        // Uma regra geral `Properties<decimal>().HavePrecision(12, 2)` parece prudente e é
        // uma armadilha: a primeira quantidade de insumo declarada como decimal herdaria
        // duas casas e 0,1563 seria gravado como 0,16 sem erro nenhum. Quem tenta usar
        // decimal cru é barrado por GrelhaHubDbContext.GarantirAusenciaDeDecimalCru.
    }

    /// <summary>
    /// 12 dígitos para moeda: até 9.999.999.999,99. Um pedido de hamburgueria não chega perto,
    /// mas soma de faturamento anual em relatório chega.
    /// </summary>
    public const int PrecisaoDeDinheiro = 12;

    /// <summary>14 dígitos para quantidade, dos quais 4 são decimais.</summary>
    public const int PrecisaoDeQuantidade = 14;
}
