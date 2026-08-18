using System.Reflection;
using FluentAssertions;
using GrelhaHub.Infrastructure.Email;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// ADR-0016 — a configuração do correio, lida da API de verdade.
/// <para>
/// <b>⚠ Este teste existe por causa de um defeito que passou por toda a suíte.</b> A seção
/// <c>Email</c> é lida por <b>indexador, propriedade a propriedade</b>, numa lista escrita à mão em
/// <c>DependencyInjection</c>. Acrescentei <c>Cnpj</c> à classe de opções e ao <c>appsettings</c>,
/// esqueci a linha da lista, e o campo foi para produção vazio: o rodapé do e-mail saiu com
/// "· CNPJ" e nada depois.
/// </para>
/// <para>
/// <b>Por que nenhum teste caiu:</b> o dublê da suíte — <see cref="CaixaDeSaidaDeTeste"/> — tem os
/// valores escritos dentro dele. Ele existe para dar ao teste o conteúdo do e-mail sem SMTP, e o
/// efeito colateral é que ele responde certo mesmo quando a configuração real está vazia. Dublê que
/// substitui a fonte do dado nunca prova que a fonte funciona.
/// </para>
/// <para>
/// A verificação é por <b>reflexão</b> e não uma lista de campos: uma lista aqui teria o mesmo
/// defeito da lista de lá — quem acrescentar propriedade nova esqueceria as duas.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class OpcoesDeEmailTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    /// <summary>
    /// <c>Senha</c> é legitimamente vazia fora de produção — é o segredo que vem do <c>.env</c> da
    /// VPS, e <c>RN-GER-032</c> trata ambiente sem SMTP como estado válido.
    /// </summary>
    private static readonly string[] PodemFicarVazias = ["Senha"];

    [Fact(DisplayName = "ADR-0016 — toda opção de e-mail chega preenchida da configuração")]
    public void TodaOpcao_ChegaPreenchida()
    {
        using var escopo = Ambiente.CriarEscopo();

        var opcoes = escopo.ServiceProvider.GetRequiredService<IOptions<OpcoesDeEmail>>().Value;

        var vazias = typeof(OpcoesDeEmail)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanWrite)
            .Where(p => !PodemFicarVazias.Contains(p.Name))
            .Where(p => string.IsNullOrWhiteSpace((string?)p.GetValue(opcoes)))
            .Select(p => p.Name)
            .ToArray();

        vazias.Should().BeEmpty(
            "propriedade de `OpcoesDeEmail` só chega preenchida se alguém escrever a linha dela na "
            + "leitura manual de `DependencyInjection` — e o esquecimento é silencioso: nenhum erro, "
            + "nenhum aviso, só um campo vazio no e-mail que o cliente recebe");
    }
}
