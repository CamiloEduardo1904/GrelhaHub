using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.Acesso;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Tests.Acesso;

/// <summary>
/// O modelo do e-mail de acesso — ADR-0016, ADR-0012.
/// <para>
/// <b>Por que testar um template, que é "só aparência":</b> a caixa de entrada é a única
/// superfície do produto que ninguém abre durante o desenvolvimento. Um rodapé legal que
/// desaparece num refactor não quebra tela nenhuma, não derruba teste de integração — os que
/// existem só extraem o token do corpo — e só é descoberto quando alguém confere uma mensagem
/// recebida. As asserções abaixo são exatamente o que não se percebe faltando.
/// </para>
/// </summary>
public sealed class ModeloDeEmailDeAcessoTests
{
    private const string Empresa = "CDP TECH SOLUTIONS";
    private const string Cnpj = "63.533.872/0001-35";
    private const string Link = "https://grelhahub.com/definir-senha?token=abc-123_XYZ";

    private static Application.Common.Interfaces.MensagemDeEmail Montar(TipoDeToken tipo) =>
        ModeloDeEmailDeAcesso.Montar(
            "alguem@casa.teste", "Camilo", tipo, Link, "72 horas", Empresa, Cnpj);

    [Theory(DisplayName = "ADR-0016 — o rodapé legal vai nos DOIS corpos, HTML e texto")]
    [InlineData(TipoDeToken.Convite)]
    [InlineData(TipoDeToken.RedefinicaoDeSenha)]
    public void RodapeLegal_NosDoisCorpos(TipoDeToken tipo)
    {
        var mensagem = Montar(tipo);

        // O corpo em texto não é rascunho do HTML: é o que o leitor de tela e o cliente sem HTML
        // recebem, e some do radar com facilidade justamente por não ser o que se olha.
        foreach (var corpo in new[] { mensagem.CorpoHtml, mensagem.CorpoTexto })
        {
            corpo.Should().Contain(Empresa);
            corpo.Should().Contain(Cnpj);
            corpo.Should().Contain("Todos os direitos reservados");
            corpo.Should().Contain("automática", "mensagem transacional precisa se declarar automática");
            corpo.Should().Contain("Não responda a este endereço");
            corpo.Should().Contain(Link, "sem o endereço em texto, quem tem imagem ou botão bloqueado fica sem caminho");
        }
    }

    [Theory(DisplayName = "CHAPA-02 — a listra tem exatamente UMA barra âmbar")]
    [InlineData(TipoDeToken.Convite)]
    [InlineData(TipoDeToken.RedefinicaoDeSenha)]
    public void Listra_UmaBarraAmbar(TipoDeToken tipo)
    {
        var html = Montar(tipo).CorpoHtml;

        // `#ffb020` é `--ambar-chama`. Duas ocorrências significam duas barras ativas, que é
        // exatamente o que CHAPA-02 proíbe — e o tipo de coisa que passa despercebida ao copiar
        // uma célula de tabela.
        System.Text.RegularExpressions.Regex.Matches(html, "#ffb020")
            .Should().HaveCount(2, "a barra ativa declara a cor no `bgcolor` E no `style`, uma vez cada");
    }

    [Fact(DisplayName = "ADR-0016 — nenhuma imagem e nenhuma folha de estilo no corpo do e-mail")]
    public void SemImagemESemFolhaDeEstilo()
    {
        var html = Montar(TipoDeToken.Convite).CorpoHtml;

        // Cliente de e-mail bloqueia imagem por padrão e descarta `<style>`. Marca desenhada em
        // imagem chega como moldura vazia; estilo em folha chega como texto sem formatação.
        html.Should().NotContain("<img", "a listra e a wordmark são tabela e texto, não imagem");
        html.Should().NotContain("<style", "Gmail e Outlook descartam a folha; o estilo vai no atributo");
        html.Should().NotContain("<link", "nada de recurso externo");
    }

    [Fact(DisplayName = "ADR-0016 — o assunto e o rótulo distinguem convite de redefinição")]
    public void ConviteERedefinicao_NaoSaoAMesmaMensagem()
    {
        var convite = Montar(TipoDeToken.Convite);
        var redefinicao = Montar(TipoDeToken.RedefinicaoDeSenha);

        // Quem recebe os dois na mesma semana precisa saber qual é qual pela linha do assunto —
        // e quem clica no antigo por engano perde o link novo (RN-GER-033: emitir invalida o
        // anterior).
        convite.Assunto.Should().NotBe(redefinicao.Assunto);
        convite.CorpoHtml.Should().Contain("Primeiro acesso");
        redefinicao.CorpoHtml.Should().Contain("Redefinir senha");
    }
}
