namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// Quem está executando a operação e em qual unidade.
/// Implementado na API a partir das claims do JWT (ADR-0003).
/// </summary>
public interface IContextoDeExecucao
{
    /// <summary><c>RN-GER-001</c> — responsável pela operação. <c>null</c> em rota pública.</summary>
    Guid? UsuarioId { get; }

    /// <summary>
    /// <c>RN-GER-009</c> — unidade a que todo registro transacional é vinculado.
    /// <para>
    /// Vem do token no acesso autenticado; nas rotas públicas do cardápio é definida pelo
    /// <c>slug</c> via <see cref="DefinirUnidadePublica"/>.
    /// </para>
    /// </summary>
    Guid UnidadeId { get; }

    bool Autenticado { get; }

    /// <summary><c>RN-GER-012</c> — custo, CMV e margem só para gerência.</summary>
    bool PodeVerCusto { get; }

    bool EhGerencia { get; }

    /// <summary>
    /// Fixa a unidade do escopo em rota pública, depois de resolver o <c>slug</c>.
    /// <para>
    /// É o que permite o filtro global do EF Core continuar valendo no cardápio anônimo: sem
    /// isso, a consulta pública precisaria filtrar a unidade a mão em cada lugar — e o
    /// esquecimento vazaria dados de outro restaurante (<c>RN-VEN-037</c>).
    /// </para>
    /// </summary>
    void DefinirUnidadePublica(Guid unidadeId);
}
