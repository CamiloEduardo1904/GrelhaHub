using GrelhaHub.Application.Common.Exceptions;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// O recorte de unidade de uma <b>leitura</b> — <c>RN-GER-009</c>, <c>RN-GER-014</c>, ADR-0013.
/// <para>
/// Existe porque o filtro global do EF Core resolve o caso do restaurante e <b>não</b> o da
/// plataforma. Para o usuário de restaurante o recorte é automático e esquecer o <c>WHERE</c> é
/// inofensivo. Para o usuário de plataforma o filtro <b>passa tudo</b> — e isso é decisão do
/// ADR-0013, não descuido: o suporte precisa enxergar a base instalada.
/// </para>
/// <para>
/// <b>O que faltava era o recorte OPCIONAL.</b> A plataforma conseguia ver tudo e não conseguia
/// ver <i>uma casa</i>: sem parâmetro de unidade, a listagem de pedidos de três clientes vinha num
/// só bolo, sem coluna dizendo de quem era cada linha — inútil para atender um cliente por
/// telefone. E a listagem de usuários era pior: ela lançava "unidade não encontrada" para a
/// plataforma, então o suporte simplesmente não via a equipe do cliente.
/// </para>
/// </summary>
public static class UnidadeDaConsulta
{
    /// <summary>
    /// A unidade a recortar, ou <c>null</c> para "todas".
    /// <list type="bullet">
    /// <item><b>Plataforma</b> — informou a unidade, recorta nela; não informou, vê todas
    /// (ADR-0013). O <c>null</c> é resposta legítima, e é o que <c>AcessoTests</c> CA-9
    /// afirma</item>
    /// <item><b>Restaurante</b> — sempre a própria, e o parâmetro é <b>ignorado</b>
    /// (<c>RN-GER-016</c>). Honrá-lo daria a um cliente uma forma de perguntar o que o vizinho
    /// tem</item>
    /// </list>
    /// </summary>
    public static Guid? Recorte(IContextoDeExecucao contexto, Guid? informada)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.EhPlataforma)
        {
            return informada is { } escolhida && escolhida != Guid.Empty ? escolhida : null;
        }

        var propria = contexto.UnidadeId;

        return propria != Guid.Empty
            ? propria
            : throw new NaoEncontradoException("Unidade não encontrada.");
    }
}
