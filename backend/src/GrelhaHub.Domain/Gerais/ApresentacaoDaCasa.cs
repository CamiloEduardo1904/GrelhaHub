namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Duas perguntas sobre a casa que o <b>lado da leitura</b> também precisa responder —
/// <c>RN-GER-037</c> e <c>RN-VEN-048</c>.
/// <para>
/// ⚠⚠ <b>Por que isto saiu de dentro de <see cref="Unidade"/>:</b> o ADR-0019 mandou as consultas
/// para um modelo de leitura que <b>não carrega agregado</b> — e o cardápio público precisa
/// exatamente destas duas respostas. As opções eram reescrever a regra em SQL (duas
/// implementações divergindo em silêncio, que é o defeito que <c>DiaOperacionalParidadeTests</c>
/// existe para pegar) ou <b>extrair a função pura</b> e chamá-la dos dois lados.
/// </para>
/// <para>
/// A segunda opção não tem paridade a manter: é o mesmo código. <see cref="Unidade"/> delega para
/// cá, e o handler de leitura chama daqui — não há como divergirem.
/// </para>
/// </summary>
public static class ApresentacaoDaCasa
{
    /// <summary>
    /// <c>RN-GER-037</c> — o endereço em uma linha, ou <c>null</c> quando não há logradouro.
    /// <para>
    /// Sem logradouro não há endereço: "Centro · Curitiba/PR" não leva ninguém a lugar nenhum, e
    /// exibir meia informação num cardápio é pior do que não exibir nada.
    /// </para>
    /// </summary>
    public static string? Resumir(
        string? logradouro, string? numero, string? bairro, string? cidade, string? uf)
    {
        if (string.IsNullOrWhiteSpace(logradouro))
        {
            return null;
        }

        var partes = new List<string>
        {
            string.IsNullOrWhiteSpace(numero) ? logradouro : $"{logradouro}, {numero}",
        };

        if (!string.IsNullOrWhiteSpace(bairro))
        {
            partes.Add(bairro);
        }

        if (!string.IsNullOrWhiteSpace(cidade))
        {
            partes.Add(string.IsNullOrWhiteSpace(uf) ? cidade : $"{cidade}/{uf}");
        }
        else if (!string.IsNullOrWhiteSpace(uf))
        {
            partes.Add(uf);
        }

        return string.Join(" · ", partes);
    }

    /// <summary>Um dia da semana da casa, do jeito que as duas pontas conseguem montar.</summary>
    public readonly record struct DiaDaCasa(DayOfWeek DiaDaSemana, TimeOnly? Abre, TimeOnly? Fecha)
    {
        /// <summary>Dia sem horário é dia fechado (<c>RN-GER-036</c>).</summary>
        public bool Fechado => Abre is null || Fecha is null;

        /// <summary>
        /// <c>RN-GER-036</c> — a casa que fecha depois da meia-noite. <c>18:00–02:00</c> é um turno
        /// só, e comparar <c>hora &lt; fecha</c> nele fecharia a loja às 18h01.
        /// </summary>
        public bool AtravessaMeiaNoite => !Fechado && Fecha!.Value <= Abre!.Value;
    }

    /// <summary>
    /// <c>RN-VEN-048</c> — a casa está aberta neste instante?
    /// <para>
    /// ⚠ A conversão para o fuso DA CASA acontece <b>aqui</b>, e não em quem chama. O contêiner
    /// roda em UTC: um chamador que esquecesse de converter perguntaria com a hora de Londres e
    /// fecharia a loja três horas antes — sem erro nenhum, só uma vitrine fechada no meio da noite
    /// de sábado.
    /// </para>
    /// <para>
    /// <b>Semana vazia responde ABERTA.</b> A casa que ainda não cadastrou horário não deve ficar
    /// invisível na vitrine por causa de um cadastro que ninguém pediu para ela fazer.
    /// </para>
    /// </summary>
    public static bool AbertaEm(
        IReadOnlyCollection<DiaDaCasa> semana, string fusoHorario, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(semana);

        if (semana.Count == 0)
        {
            return true;
        }

        var local = TimeZoneInfo.ConvertTime(
            instante, TimeZoneInfo.FindSystemTimeZoneById(fusoHorario));

        var hora = TimeOnly.FromTimeSpan(local.TimeOfDay);

        var hoje = semana.FirstOrDefault(d => d.DiaDaSemana == local.DayOfWeek);

        if (!hoje.Fechado)
        {
            var dentro = hoje.AtravessaMeiaNoite
                ? hora >= hoje.Abre!.Value
                : hora >= hoje.Abre!.Value && hora < hoje.Fecha!.Value;

            if (dentro)
            {
                return true;
            }
        }

        // O turno de ONTEM que ainda não terminou: à 01h de domingo a casa aberta é a de sábado.
        var ontem = semana.FirstOrDefault(d => d.DiaDaSemana == local.AddDays(-1).DayOfWeek);

        return !ontem.Fechado && ontem.AtravessaMeiaNoite && hora < ontem.Fecha!.Value;
    }
}
