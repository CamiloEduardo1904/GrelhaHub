using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Cardapio;

/// <summary>
/// O armazenamento em si — ADR-0018. As bordas que a rota HTTP nunca alcança.
/// <para>
/// ⚠ Estes casos existem porque a validação que os cobre é <b>defesa em profundidade</b>: o handler
/// já garante unidade e pasta antes de chamar. Se um caso de uso futuro chamar direto, é aqui que a
/// chamada errada falha — e é aqui que está escrito o porquê.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ArmazenamentoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-009 — gravar sem unidade é recusado antes de tocar em qualquer banco")]
    public async Task SemUnidade_EhRecusado()
    {
        using var escopo = Ambiente.CriarEscopo();
        var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeArquivo>();

        using var conteudo = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]);

        var acao = async () => await armazenamento.GravarAsync(
            Guid.Empty, "produto", FormatoDeImagem.Jpeg, conteudo);

        // Arquivo sem unidade pertenceria ao SISTEMA e não a um restaurante — e nenhuma consulta
        // saberia de quem ele é.
        await acao.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*RN-GER-009*");

        await using var arquivo = Ambiente.CriarContextoDeArquivo();

        (await arquivo.Binarios.AsNoTracking().CountAsync())
            .Should().Be(0, "a recusa acontece ANTES da gravação dos bytes");
    }

    [Fact(DisplayName = "SPEC-VEN-002 — pasta fora de `produto` e `logo` é recusada")]
    public async Task PastaDesconhecida_EhRecusada()
    {
        var restaurante = await CadastrarRestauranteAsync("Pasta Burger", "12121212000191");

        using var escopo = Ambiente.CriarEscopo();
        var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeArquivo>();

        using var conteudo = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]);

        // A pasta é escolhida pelo CÓDIGO, nunca pelo usuário. A validação existe para o caso em
        // que um handler futuro a receba de fora sem perceber.
        var acao = async () => await armazenamento.GravarAsync(
            restaurante.UnidadeId, "../etc", FormatoDeImagem.Jpeg, conteudo);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "ADR-0018 — `Existe` responde pelo METADADO, não pelos bytes")]
    public async Task Existe_OlhaOMetadado()
    {
        var restaurante = await CadastrarRestauranteAsync("Existe Burger", "13131313000191");

        using var escopo = Ambiente.CriarEscopo();
        var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeArquivo>();

        using var conteudo = new MemoryStream(Png());

        var referencia = await armazenamento.GravarAsync(
            restaurante.UnidadeId, "logo", FormatoDeImagem.Png, conteudo);

        armazenamento.Existe(referencia).Should().BeTrue();

        // ⚠ Referência que não é do banco de arquivo responde `false` sem explodir: as gravadas
        // antes de 18/08/2026 apontam para `/arquivos/...` (ADR-0015), e continuam sendo servidas
        // pelo Nginx. Quem pergunta aqui está conferindo o armazenamento NOVO.
        armazenamento.Existe("/arquivos/qualquer/coisa.png").Should().BeFalse();
        armazenamento.Existe("/api/v1/arquivos/nao-e-um-guid").Should().BeFalse();
        armazenamento.Existe("").Should().BeFalse();
    }

    [Fact(DisplayName = "ADR-0018 — ler um id que não existe devolve nulo, e não exceção")]
    public async Task Ler_Inexistente_DevolveNulo()
    {
        using var escopo = Ambiente.CriarEscopo();
        var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeArquivo>();

        (await armazenamento.LerAsync(Guid.CreateVersion7())).Should().BeNull();
    }

    [Fact(DisplayName = "ADR-0018 — o data URI carrega o tipo da assinatura e os bytes intactos")]
    public async Task DataUri_CarregaTipoEBytes()
    {
        var restaurante = await CadastrarRestauranteAsync("Uri Burger", "14141414000191");

        using var escopo = Ambiente.CriarEscopo();
        var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeArquivo>();

        using var conteudo = new MemoryStream(Png());

        var referencia = await armazenamento.GravarAsync(
            restaurante.UnidadeId, "produto", FormatoDeImagem.Png, conteudo);

        var id = Guid.Parse(referencia["/api/v1/arquivos/".Length..]);

        var lido = await armazenamento.LerAsync(id);

        lido.Should().NotBeNull();

        var dataUri = lido!.ComoDataUri();

        dataUri.Should().StartWith("data:image/png;base64,");

        Convert.FromBase64String(dataUri["data:image/png;base64,".Length..])
            .Should().Equal(Png());
    }

    /// <summary>Assinatura PNG real (<c>89 50 4E 47 0D 0A 1A 0A</c>).</summary>
    private static byte[] Png()
    {
        var bytes = new byte[64];
        byte[] assinatura = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        assinatura.CopyTo(bytes, 0);
        return bytes;
    }
}
