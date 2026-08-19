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

    /// <summary>
    /// ADR-0022 §3 — o principal autenticado da requisição, para o
    /// <c>PermissaoBehavior</c> entregar ao <c>IAuthorizationService</c>.
    /// <para>
    /// <c>null</c> em rota pública e fora de HTTP. É tipo do BCL
    /// (<c>System.Security.Claims</c>), não do ASP.NET — a Application continua sem
    /// referenciar runtime.
    /// </para>
    /// </summary>
    System.Security.Claims.ClaimsPrincipal? Principal { get; }

    /// <summary>
    /// <c>RN-GER-010</c> — permissões do usuário, vindas do token.
    /// <para>
    /// Códigos <c>recurso.acao</c>. Nome de perfil <b>não</b> aparece aqui de propósito: era
    /// assim antes, e criar o perfil Operador (<c>RN-GER-022</c>) exigiria caçar condicionais.
    /// </para>
    /// </summary>
    IReadOnlySet<string> Permissoes { get; }

    /// <summary><c>RN-GER-010</c> — tem a permissão? Usuário de plataforma pode tudo.</summary>
    bool Pode(string permissao);

    /// <summary>
    /// <c>RN-GER-012</c> — custo, CMV e margem só saem do servidor com <c>custo.visualizar</c>.
    /// <para>
    /// Atalho para <c>Pode("custo.visualizar")</c>, porque é consultado em projeção e repetir a
    /// string em cada query convida ao erro de digitação silencioso.
    /// </para>
    /// </summary>
    bool PodeVerCusto { get; }

    /// <summary>
    /// <c>RF-SEG-005</c> — senha inicial pendente de troca. Enquanto verdadeiro, so a troca de
    /// senha responde (<c>TrocaDeSenhaBehavior</c>).
    /// </summary>
    bool DeveTrocarSenha { get; }

    /// <summary>
    /// <c>RN-GER-014</c> ⚠⚠ — usuário de plataforma (suporte do GrelhaHub).
    /// <para>
    /// <b>Para ele, e só para ele, o recorte por unidade de <c>RN-GER-009</c> não se aplica:</b>
    /// o filtro global do EF Core é desligado, e ele enxerga todas as unidades. Decisão
    /// registrada em ADR-0013, que revisa a invariante do ADR-0005.
    /// </para>
    /// <para>
    /// <b>Deriva de uma única claim, de propósito.</b> Um defeito que marque isto indevidamente
    /// vaza dado de um cliente para outro — é o risco central do ADR-0013. Concentrar a origem
    /// em um ponto é o que torna esse ponto testável.
    /// </para>
    /// </summary>
    bool EhPlataforma { get; }

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
