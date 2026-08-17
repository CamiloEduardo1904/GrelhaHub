namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// Fonte de tempo do sistema. Existe para que o dia operacional seja calculado em um único
/// lugar e para que os testes possam fixar o instante.
/// </summary>
public interface IRelogio
{
    DateTimeOffset Agora { get; }

    /// <summary>
    /// Dia operacional correspondente a um instante — <c>RN-GER-006</c>, ADR-0011.
    /// <para>
    /// Serve para consulta e exibição na aplicação. A fonte de verdade da agregação é a
    /// coluna gerada <c>data_operacional</c> no banco.
    /// </para>
    /// </summary>
    DateOnly DiaOperacional(DateTimeOffset instante);

    DateOnly DiaOperacionalDeHoje();
}
