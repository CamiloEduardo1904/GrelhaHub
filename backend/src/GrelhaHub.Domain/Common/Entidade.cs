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

    /// <summary>
    /// Quem criou.
    /// <para>
    /// <b>Nulo em operação anônima</b>, e a nulidade é obrigatória: o pedido do cardápio público
    /// (<c>RN-VEN-038</c>) é criado por um cliente que não é usuário do sistema, e o cadastro de
    /// restaurante roda antes de existir qualquer usuário. A versão original de <c>RN-GER-001</c>
    /// dizia "todo registro guarda o usuário responsável" — com <c>NOT NULL</c>, essas duas
    /// operações seriam impossíveis. A regra vale onde há responsável.
    /// </para>
    /// </summary>
    Guid? CriadoPorUsuarioId { get; }
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

/// <summary>
/// Registro que admite <b>exclusão lógica</b> — <c>RN-GER-002</c>, <c>RN-VEN-041</c>.
/// <para>
/// Exclusão no GrelhaHub é sempre lógica: nenhum registro transacional é apagado. Quem implementa
/// esta interface recebe filtro global no <c>DbContext</c> e desaparece de toda consulta, sem
/// que nenhum handler precise lembrar de escrever <c>Where(!p.Excluido)</c> — mesmo motivo do
/// filtro de unidade.
/// </para>
/// </summary>
public interface IExcluivelLogicamente
{
    /// <summary>
    /// Instante da exclusão, ou nulo. <b>Propriedade mapeada</b>, e não um <c>bool</c> calculado:
    /// o filtro global precisa traduzir para SQL, e <c>Excluido</c> derivado não tem coluna.
    /// </summary>
    DateTimeOffset? ExcluidoEm { get; }
}
