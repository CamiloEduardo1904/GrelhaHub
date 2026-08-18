namespace GrelhaHub.Application.Common.Modulos;

/// <summary>
/// Exige uma permissão granular para a operação — <c>RN-GER-010</c>, <c>SPEC-GER-004</c>.
/// <para>
/// <b>Nenhum handler começa com <c>if (!usuario.Pode(...))</c>.</b> A verificação vive no
/// pipeline: um handler que decide autorização mistura duas responsabilidades e, pior, quem
/// escreve o próximo handler pode simplesmente esquecer a linha.
/// </para>
/// <para>
/// O código é <c>recurso.acao</c>, nunca nome de perfil. Verificar <c>perfil == "Gerente"</c>
/// espalhado pelo código torna impossível criar um perfil novo — e <c>RN-GER-022</c> criou um.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ExigePermissaoAttribute(string codigo) : Attribute
{
    public string Codigo { get; } = string.IsNullOrWhiteSpace(codigo)
        ? throw new ArgumentException("A permissão exigida é obrigatória.", nameof(codigo))
        : codigo.Trim().ToLowerInvariant();
}

/// <summary>
/// Declara que a operação **não** exige permissão, com o motivo.
/// <para>
/// Para rota anônima (cardápio público, login) e para operação que faz a própria verificação por
/// uma razão declarada — como a administração de módulos, restrita ao usuário de plataforma
/// (<c>RN-GER-014</c>), que não tem perfil de unidade e portanto não tem permissão de unidade.
/// </para>
/// <para>
/// Mesma porta de saída de <see cref="SemModuloAttribute"/>, com o mesmo custo: o texto é
/// obrigatório, e o que impede o abuso é a revisão lendo a justificativa.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SemPermissaoAttribute(string motivo) : Attribute
{
    public string Motivo { get; } = string.IsNullOrWhiteSpace(motivo)
        ? throw new ArgumentException(
            "SemPermissao exige motivo: é o que torna a exceção revisável.", nameof(motivo))
        : motivo;
}
