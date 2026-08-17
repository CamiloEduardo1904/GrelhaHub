using FluentAssertions;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Domain.Tests.Gerais;

public class UnidadeTests
{
    private static Unidade Criar(string slug = "brasa-e-fogo") =>
        new("Brasa e Fogo LTDA", "Brasa & Fogo Burger", "12345678000199", slug, "11987654321");

    [Fact(DisplayName = "RN-VEN-036 — a unidade tem identificador público que compõe o link do cardápio")]
    public void Unidade_TemSlugPublico()
    {
        var unidade = Criar();

        unidade.Slug.Should().Be("brasa-e-fogo");
    }

    [Theory(DisplayName = "RN-VEN-036 — o identificador público recusa formato inválido")]
    [InlineData("ab")]                    // curto demais
    [InlineData("com espaço")]
    [InlineData("Maiúsculo")]
    [InlineData("acentuação")]
    [InlineData("barra/invalida")]
    [InlineData("")]
    public void Slug_RecusaFormatoInvalido(string slug)
    {
        var acao = () => Criar(slug);

        acao.Should().Throw<ArgumentException>().WithMessage("*RN-VEN-036*");
    }

    [Fact(DisplayName = "RN-VEN-036 — o identificador público não tem setter: é imutável após a criação")]
    public void Slug_EhImutavel()
    {
        // O link circula em rede social e material impresso; alterá-lo quebraria o que já
        // foi distribuído. A garantia é estrutural — não existe caminho para escrever.
        var propriedade = typeof(Unidade).GetProperty(nameof(Unidade.Slug))!;

        propriedade.SetMethod.Should().Match(m => m == null || !m!.IsPublic);
    }

    [Fact(DisplayName = "RN-GER-005 — a operação ocorre em America/Sao_Paulo")]
    public void Unidade_TemFusoDaOperacao()
    {
        Criar().FusoHorario.Should().Be("America/Sao_Paulo");
    }

    [Fact(DisplayName = "RN-GER-003 — o CNPJ é armazenado apenas com dígitos")]
    public void Cnpj_GuardaSomenteDigitos()
    {
        var unidade = new Unidade(
            "Brasa e Fogo LTDA", "Brasa & Fogo", "12.345.678/0001-99", "brasa-e-fogo", "(11) 98765-4321");

        unidade.Cnpj.Should().Be("12345678000199");
        unidade.Telefone.Should().Be("11987654321");
    }
}
