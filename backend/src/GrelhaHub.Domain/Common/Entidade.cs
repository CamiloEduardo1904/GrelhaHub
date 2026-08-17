namespace GrelhaHub.Domain.Common;

/// <summary>
/// Raiz de toda entidade do domínio.
/// Identidade por <see cref="Guid"/> — ADR-0004: PK <c>uuid</c>, para evitar colisão na
/// carga inicial e em futura sincronização com o app mobile.
/// </summary>
public abstract class Entidade
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public override bool Equals(object? obj) =>
        obj is Entidade outra && GetType() == outra.GetType() && Id.Equals(outra.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>
/// Toda entidade transacional pertence a exatamente uma unidade — <c>RN-GER-009</c>.
/// O recorte é aplicado por filtro global no <c>DbContext</c>: o isolamento não depende de
/// o desenvolvedor lembrar de filtrar (ADR-0005).
/// </summary>
public interface IPertenceAUnidade
{
    Guid UnidadeId { get; }
}

/// <summary>
/// Todo registro transacional guarda a data e hora de criação e o usuário responsável —
/// <c>RN-GER-001</c>. Preenchido por interceptor do EF Core, nunca a mão no handler.
/// </summary>
public interface IAuditavel
{
    DateTimeOffset CriadoEm { get; }
    Guid CriadoPorUsuarioId { get; }
}

/// <summary>
/// Registro que carrega a data do dia operacional — <c>RN-GER-006</c>, ADR-0011.
/// <para>
/// O dia operacional vai das 04h00 (<c>PAR-GER-001</c>) às 03h59 do dia seguinte: uma venda
/// registrada à 01h30 de domingo pertence ao dia operacional de sábado.
/// </para>
/// <para>
/// <b>A propriedade é somente leitura por construção:</b> no banco é coluna
/// <c>GENERATED ALWAYS AS</c>, derivada da data/hora do evento. Nunca atribua valor a ela na
/// aplicação, e nunca agrupe relatório por data de criação.
/// </para>
/// </summary>
public interface IPossuiDiaOperacional
{
    DateOnly DataOperacional { get; }
}
