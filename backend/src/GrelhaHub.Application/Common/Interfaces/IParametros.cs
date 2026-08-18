namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// Ponto <b>único</b> de leitura de parâmetro — <c>SPEC-GER-003</c>.
/// <para>
/// Nenhum handler consulta <c>parametro_valor</c> direto. Se consultasse, o cache divergiria do
/// banco sem ninguém perceber, e o mesmo parâmetro passaria a valer coisas diferentes em dois
/// lugares do mesmo pedido.
/// </para>
/// <para>
/// <b>A leitura é sempre na data do evento, nunca "o valor de agora".</b> Um pedido de três
/// meses atrás foi calculado com a taxa de serviço daquela época; recalculá-lo com a taxa atual
/// reescreveria o passado — o mesmo erro que <c>RN-VEN-008</c> e <c>RN-VEN-010</c> evitam para
/// preço. Por isso <paramref name="vigenteEm"/> existe em toda assinatura, e omiti-lo é uma
/// decisão consciente de querer o valor corrente.
/// </para>
/// </summary>
public interface IParametros
{
    /// <summary>Texto canônico do valor vigente — invariante, `10.00` e nunca `10,00`.</summary>
    Task<string> ObterTextoAsync(
        string codigo, DateTimeOffset? vigenteEm = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valor convertido. Suporta <c>decimal</c>, <c>int</c>, <c>string</c>, <c>TimeOnly</c>,
    /// <c>TimeSpan</c>.
    /// </summary>
    Task<T> ObterAsync<T>(
        string codigo, DateTimeOffset? vigenteEm = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta o cache do parâmetro na unidade corrente. Chamado pela escrita — e só por ela.
    /// </summary>
    void Invalidar(string codigo);
}
