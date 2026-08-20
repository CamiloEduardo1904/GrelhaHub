using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;

namespace GrelhaHub.Application.Tests.Arquitetura;

/// <summary>
/// O guarda do histórico de migrations — ADR-0022 §6.
/// <para>
/// ⚠⚠ <b>Ele existe por causa de um deploy quebrado em 20/08/2026, e o defeito não era o erro de
/// SQL.</b> A ADR-0022 trocou 18 migrations por uma, e a VPS tinha as 18 antigas no histórico. O EF,
/// não achando nenhum ID conhecido, concluiu que o banco estava vazio e começou do zero: o log
/// mostrou <c>CREATE TABLE arquivo</c> falhando, e a informação de verdade — <i>este banco veio de
/// outro código</i> — não estava em lugar nenhum.
/// </para>
/// <para>
/// A lógica é função pura de propósito. O caminho até o banco é do <c>Program</c>; o que se testa
/// aqui é a <b>pergunta</b>, que é a parte fácil de errar: "sobra" e "falta" são casos opostos, e
/// confundi-los faria o guarda barrar o primeiro deploy de uma instalação nova.
/// </para>
/// </summary>
public sealed class HistoricoDeMigrationsTests
{
    [Fact(DisplayName = "ADR-0022 §6 — banco em dia não tem órfã")]
    public void BancoEmDia_SemOrfa()
    {
        var conhecidas = new[] { "20260101_A", "20260102_B" };

        HistoricoDeMigrations.Orfas(conhecidas, conhecidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "ADR-0022 §6 — migration FALTANDO não é órfã: é o caso de todo deploy")]
    public void MigrationPendente_NaoEhOrfa()
    {
        var conhecidas = new[] { "20260101_A", "20260102_B", "20260103_C" };
        var aplicadas = new[] { "20260101_A" };

        // ⚠ É a distinção que o guarda existe para fazer. FALTAR é o caso comum — é o que todo
        // deploy resolve. Se isto devolvesse órfã, o guarda impediria exatamente a operação que
        // deveria permitir, e o primeiro deploy de uma instalação nova nunca aconteceria.
        HistoricoDeMigrations.Orfas(aplicadas, conhecidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "ADR-0022 §6 — banco novo, sem nada aplicado, não tem órfã")]
    public void BancoNovo_SemOrfa()
    {
        HistoricoDeMigrations.Orfas([], ["20260101_A"]).Should().BeEmpty();
    }

    [Fact(DisplayName = "ADR-0022 §6 — migration aplicada que o código não conhece É órfã")]
    public void MigrationDesconhecida_EhOrfa()
    {
        // O caso real: a VPS tinha as 18 antigas, o código passou a conhecer só a `Fundacao`.
        var aplicadas = new[] { "20260817051948_Fundacao", "20260818190547_VisoesDeLeitura" };
        var conhecidas = new[] { "20260819222202_Fundacao" };

        var orfas = HistoricoDeMigrations.Orfas(aplicadas, conhecidas);

        orfas.Should().Equal("20260817051948_Fundacao", "20260818190547_VisoesDeLeitura");
    }

    [Fact(DisplayName = "ADR-0022 §6 — a mensagem diz o que fazer, e não só o que houve")]
    public void Mensagem_DizOCaminho()
    {
        var texto = HistoricoDeMigrations.Explicar("grelhahub", ["20260817051948_Fundacao"]);

        // ⚠⚠ O valor do guarda É a mensagem. Erro de infraestrutura sem caminho de saída vira
        // tentativa e erro em cima de banco, que é o pior lugar do sistema para experimentar.
        texto.Should().Contain("20260817051948_Fundacao");
        texto.Should().Contain("grelhahub");
        texto.Should().Contain("NÃO conhece");

        // As duas saídas, e a advertência de que "tentar de novo" não é uma delas.
        texto.Should().Contain("DROP SCHEMA");
        texto.Should().Contain("recriarBanco=true");
        texto.Should().Contain("migration de transição");
        texto.Should().Contain("tentar de novo");

        // E o motivo, para quem cair aqui sem ter lido a ADR.
        texto.Should().Contain("ADR-0022");
    }

    [Fact(DisplayName = "ADR-0022 §6 — a ordem das órfãs é estável, para o log ser comparável")]
    public void Orfas_SaemOrdenadas()
    {
        var orfas = HistoricoDeMigrations.Orfas(["20260103_C", "20260101_A", "20260102_B"], []);

        // Log de deploy é lido lado a lado com o da execução anterior. Ordem que varia entre
        // execuções transforma "a mesma falha" em "uma falha diferente".
        orfas.Should().Equal("20260101_A", "20260102_B", "20260103_C");
    }
}
