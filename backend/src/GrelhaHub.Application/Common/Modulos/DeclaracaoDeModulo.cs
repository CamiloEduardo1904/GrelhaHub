using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Common.Modulos;

/// <summary>
/// Declara a qual módulo a operação pertence — <c>RN-GER-017</c>, ADR-0014.
/// <para>
/// O gate vive no pipeline do MediatR, e não no controller, porque vale para <b>qualquer
/// origem</b>. A onda 4 tem o caso que decide isso: a confirmação de um pedido dispara baixa de
/// estoque e ordem de produção por handler interno. Um gate no controller protegeria a rota de
/// confirmação e deixaria a baixa passar — no módulo que o cliente pode não ter contratado.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ExigeModuloAttribute(CodigoDeModulo modulo) : Attribute
{
    public CodigoDeModulo Modulo { get; } = modulo;
}

/// <summary>
/// Declara que a operação <b>não</b> pertence a módulo algum, com o motivo.
/// <para>
/// Existe para autenticação, cadastro de restaurante e administração de plataforma —
/// operações que antecedem ou transcendem o conceito de módulo contratado.
/// </para>
/// <para>
/// <b>É porta de saída, e o texto é obrigatório de propósito.</b> Nada impede alguém de escrever
/// qualquer coisa nele; o que impede o uso descuidado é a revisão de código lendo a justificativa.
/// Está declarado como risco assumido no ADR-0014.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SemModuloAttribute(string motivo) : Attribute
{
    public string Motivo { get; } = string.IsNullOrWhiteSpace(motivo)
        ? throw new ArgumentException(
            "SemModulo exige motivo: é o que torna a exceção revisável.", nameof(motivo))
        : motivo;
}
