using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Cardapio;

/// <summary>
/// <c>SPEC-VEN-002</c> — imagem do produto e logo da unidade. CA-1 a CA-11.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ImagemTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "ADR-0018 — JPEG válido vira metadado na aplicação e bytes no banco de arquivo (CA-1)")]
    public async Task CA1_JpegValidoEhAceito()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Com foto", 32.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await EnviarAsync(gestao, $"/api/v1/produtos/{produtoId}/imagem", Jpeg(), "foto.jpg");

        resposta.EnsureSuccessStatusCode();

        var url = await UrlDaRespostaAsync(resposta);

        url.Should().StartWith(Prefixo);

        var id = Guid.Parse(url[Prefixo.Length..]);

        await using var db = Contexto(restaurante);

        var produto = await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoId);

        produto.UrlImagem.Should().Be(url, "a coluna aponta para o arquivo");

        // ⚠ O METADADO fica no banco da APLICAÇÃO — é lá que RN-GER-009 e RN-GER-002 alcançam.
        var metadado = await db.Arquivos.AsNoTracking().SingleAsync(a => a.Id == id);

        metadado.UnidadeId.Should().Be(restaurante.UnidadeId);
        metadado.TipoMime.Should().Be("image/jpeg", "o tipo vem da ASSINATURA, não da extensão");
        metadado.Pasta.Should().Be("produto");
        metadado.TamanhoEmBytes.Should().Be(512);
        metadado.ExcluidoEm.Should().BeNull();

        // ⚠ Os BYTES ficam no OUTRO banco, com o MESMO id — e é a única coisa que liga os dois
        // lados: chave estrangeira não atravessa banco.
        await using var arquivo = Ambiente.CriarContextoDeArquivo();

        var binario = await arquivo.Binarios.AsNoTracking().SingleAsync(b => b.Id == id);

        binario.Conteudo.Should().HaveCount(512);
        binario.Conteudo.Take(4).Should().Equal([(byte)0xFF, 0xD8, 0xFF, 0xE0], "os bytes são os enviados");
        binario.TipoMime.Should().Be("image/jpeg");
    }

    [Fact(DisplayName = "RN-VEN-042 — PNG válido é aceito (CA-1)")]
    public async Task CA1_PngValidoEhAceito()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Com png", 28.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await EnviarAsync(gestao, $"/api/v1/produtos/{produtoId}/imagem", Png(), "foto.png");

        resposta.EnsureSuccessStatusCode();

        var url = await UrlDaRespostaAsync(resposta);

        var id = Guid.Parse(url[Prefixo.Length..]);

        await using var db = Contexto(restaurante);

        (await db.Arquivos.AsNoTracking().SingleAsync(a => a.Id == id))
            .TipoMime.Should().Be("image/png");
    }

    [Fact(DisplayName = "RN-VEN-042 — acima de 5 MB é recusado e a coluna não muda (CA-2)")]
    public async Task CA2_AcimaDoLimiteEhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Pesado", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // JPEG legítimo na assinatura, grande no corpo: prova que o limite é o tamanho, e não a
        // validação de formato falhando por acidente.
        var grande = new byte[ArquivoDeImagem.TamanhoMaximoEmBytes + 1024];
        Jpeg().CopyTo(grande, 0);

        var resposta = await EnviarAsync(gestao, $"/api/v1/produtos/{produtoId}/imagem", grande, "grande.jpg");

        resposta.IsSuccessStatusCode.Should().BeFalse();

        await using var db = Contexto(restaurante);

        (await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoId))
            .UrlImagem.Should().BeNull("nada é gravado quando o arquivo é recusado");
    }

    [Theory(DisplayName = "RN-VEN-042 — formato fora de JPEG e PNG é recusado (CA-3)")]
    [InlineData("GIF89a", "animado.gif", "image/gif")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", "vetor.svg", "image/svg+xml")]
    [InlineData("%PDF-1.7", "menu.pdf", "application/pdf")]
    public async Task CA3_FormatoNaoAceitoEhRecusado(string conteudo, string nome, string tipo)
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Formato", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem",
            Encoding.ASCII.GetBytes(conteudo), nome, tipo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// O critério que separa validação real de teatro: extensão e <c>Content-Type</c> dizem
    /// "JPEG", e o conteúdo não é.
    /// </summary>
    [Fact(DisplayName = "RN-VEN-042 ⚠ — extensão e Content-Type mentirosos são recusados pela assinatura (CA-4)")]
    public async Task CA4_AssinaturaDecide()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Disfarce", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var disfarcado = Encoding.ASCII.GetBytes("<?php system($_GET['c']); ?>");

        var resposta = await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem",
            disfarcado, "foto.jpg", "image/jpeg");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a extensão e o Content-Type passaram; a assinatura não");

        (await resposta.Content.ReadAsStringAsync()).Should().Contain("RN-VEN-042");

        await using var db = Contexto(restaurante);

        (await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoId))
            .UrlImagem.Should().BeNull();
    }

    [Fact(DisplayName = "RN-VEN-042 — o nome enviado não sobrevive em lugar nenhum (CA-5)")]
    public async Task CA5_NomeDoUsuarioEhDescartado()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Travessia", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem",
            Jpeg(), "../../../etc/passwd.jpg");

        resposta.EnsureSuccessStatusCode();

        var url = await UrlDaRespostaAsync(resposta);

        url.Should().NotContain("..", "travessia de diretório é o ataque mais antigo contra upload");
        url.Should().NotContain("passwd");

        // ⚠ Com o ADR-0018 a categoria inteira do problema deixou de existir: não há sistema de
        // arquivos do outro lado. O teste continua porque o que ele guarda é a INVARIANTE — o nome
        // recebido não vira identidade —, e ela vale para qualquer armazenamento que venha depois.
        Guid.TryParse(url[Prefixo.Length..], out _)
            .Should().BeTrue("a identidade do arquivo é gerada pelo sistema");
    }

    /// <summary>
    /// ⚠⚠ <b>Este teste mudou de lugar em 18/08/2026, e a mudança é o ponto.</b> No ADR-0015 a
    /// unidade estava no CAMINHO do arquivo — <c>/arquivos/{unidade}/produto/{nome}</c> —, e era
    /// isso que se conferia. Um caminho não é um vínculo: nada impedia que ele apontasse para uma
    /// pasta e a linha do banco dissesse outra coisa.
    /// <para>
    /// Com o ADR-0018 a unidade é <b>coluna do metadado</b>, sob o filtro global. A asserção desceu
    /// para onde o recorte realmente acontece.
    /// </para>
    /// </summary>
    [Fact(DisplayName = "RN-GER-009 — o metadado do arquivo carrega a unidade (CA-6)")]
    public async Task CA6_MetadadoCarregaAUnidade()
    {
        var primeiro = await CadastrarRestauranteAsync("Primeira Casa", "11111111000191");
        var segundo = await CadastrarRestauranteAsync("Segunda Casa", "22222222000191");

        var urlPrimeiro = await EnviarLogoAsync(primeiro);
        var urlSegundo = await EnviarLogoAsync(segundo);

        urlPrimeiro.Should().NotBe(urlSegundo);

        await using (var db = Contexto(primeiro))
        {
            // O filtro global já está ligado: `SingleAsync` sem predicado só acha o DESTA unidade.
            (await db.Arquivos.AsNoTracking().SingleAsync()).UnidadeId
                .Should().Be(primeiro.UnidadeId);
        }

        await using (var db = Contexto(segundo))
        {
            (await db.Arquivos.AsNoTracking().SingleAsync()).UnidadeId
                .Should().Be(segundo.UnidadeId);
        }
    }

    [Fact(DisplayName = "RN-GER-009 — produto de outra unidade não recebe imagem (CA-7)")]
    public async Task CA7_ProdutoDeOutraUnidade_Da404()
    {
        var alvo = await CadastrarRestauranteAsync("Alvo Burger", "11111111000191");
        var intruso = await CadastrarRestauranteAsync("Intruso Burger", "22222222000191");

        var produtoDoAlvo = await CriarProdutoAsync(alvo, "Do alvo", 30.00m);

        using var comoIntruso = Ambiente.CriarCliente(intruso.Token);

        var resposta = await EnviarAsync(
            comoIntruso, $"/api/v1/produtos/{produtoDoAlvo}/imagem", Jpeg(), "foto.jpg");

        // 404 e não 403: dizer "existe, mas não é seu" já conta que existe (RN-VEN-037).
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var db = Contexto(alvo);

        (await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoDoAlvo))
            .UrlImagem.Should().BeNull();
    }

    [Fact(DisplayName = "RN-GER-010 — sem cardapio.gerenciar não envia imagem (CA-8)")]
    public async Task CA8_SemPermissao_Da403()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Protegido", 30.00m);

        var token = await CriarCozinhaAsync(restaurante);

        using var comoCozinha = Ambiente.CriarCliente(token);

        var resposta = await EnviarAsync(
            comoCozinha, $"/api/v1/produtos/{produtoId}/imagem", Jpeg(), "foto.jpg");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "RN-GER-002 — trocar a imagem preserva o arquivo anterior (CA-9)")]
    public async Task CA9_TrocaPreservaOArquivoAnterior()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Trocado", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var primeira = await UrlDaRespostaAsync(await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem", Jpeg(), "primeira.jpg"));

        var segunda = await UrlDaRespostaAsync(await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem", Png(), "segunda.png"));

        segunda.Should().NotBe(primeira);

        await using var db = Contexto(restaurante);

        (await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoId))
            .UrlImagem.Should().Be(segunda);

        using var escopo = Ambiente.CriarEscopo();
        var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeArquivo>();

        armazenamento.Existe(primeira).Should().BeTrue(
            "RN-GER-002 — apagar arquivo é a exclusão física mais irreversível que existe, e a " +
            "trilha guarda a referência antiga");
        armazenamento.Existe(segunda).Should().BeTrue();
    }

    [Fact(DisplayName = "RN-VEN-037 — o cardápio público devolve imagem e logo, e nenhum custo (CA-10)")]
    public async Task CA10_CardapioPublicoDevolveAsImagens()
    {
        var restaurante = await CadastrarRestauranteAsync("Vitrine Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Na vitrine", 34.50m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        await EnviarAsync(gestao, $"/api/v1/produtos/{produtoId}/imagem", Jpeg(), "foto.jpg");
        await EnviarLogoAsync(restaurante);

        using var anonimo = Ambiente.CriarCliente();

        var bruto = await anonimo.GetStringAsync(
            new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        using var documento = JsonDocument.Parse(bruto);
        var raiz = documento.RootElement;

        raiz.GetProperty("urlLogo").GetString().Should().NotBeNullOrEmpty();

        var comImagem = raiz.GetProperty("categorias").EnumerateArray()
            .SelectMany(c => c.GetProperty("produtos").EnumerateArray())
            .Where(p => p.GetProperty("urlImagem").ValueKind is not JsonValueKind.Null)
            .ToList();

        comImagem.Should().NotBeEmpty("o produto com foto aparece com o caminho dela");

        // RN-GER-012 — nenhuma chave de custo entrou junto com as imagens.
        foreach (var chave in new[] { "custo", "cmv", "margem" })
        {
            System.Text.RegularExpressions.Regex.IsMatch(
                bruto, $"\"{chave}[A-Za-z]*\"\\s*:",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Should().BeFalse($"o cardápio público não devolve {chave}");
        }
    }

    [Fact(DisplayName = "RN-GER-002 — remover a imagem limpa a coluna e mantém o arquivo (CA-11)")]
    public async Task CA11_RemoverMantemOArquivo()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Removido", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var url = await UrlDaRespostaAsync(await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem", Jpeg(), "foto.jpg"));

        (await gestao.DeleteAsync(new Uri($"/api/v1/produtos/{produtoId}/imagem", UriKind.Relative)))
            .EnsureSuccessStatusCode();

        await using var db = Contexto(restaurante);

        (await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoId))
            .UrlImagem.Should().BeNull();

        using var escopo = Ambiente.CriarEscopo();

        escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeArquivo>()
            .Existe(url).Should().BeTrue("a referência sai; o arquivo fica (RN-GER-002)");
    }

    [Fact(DisplayName = "RN-VEN-042 — requisição sem arquivo é recusada")]
    public async Task SemArquivo_EhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Foto Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Vazio", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        using var vazio = new MultipartFormDataContent();

        var resposta = await gestao.PostAsync(
            new Uri($"/api/v1/produtos/{produtoId}/imagem", UriKind.Relative), vazio);

        resposta.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── ADR-0018: a leitura ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "ADR-0018 — a rota devolve os BYTES com o tipo da assinatura")]
    public async Task Leitura_DevolveOsBytes()
    {
        var restaurante = await CadastrarRestauranteAsync("Leitura Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Lido", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var url = await UrlDaRespostaAsync(await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem", Png(), "foto.png", "image/png"));

        // ⚠ ANÔNIMO de propósito: é o cliente que abre /c/nome-da-casa sem login (RN-VEN-037).
        // Se esta chamada precisar de token, a foto some da vitrine.
        using var anonimo = Ambiente.CriarCliente();

        var resposta = await anonimo.GetAsync(new Uri(url, UriKind.Relative));

        resposta.EnsureSuccessStatusCode();
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        var bytes = await resposta.Content.ReadAsByteArrayAsync();

        bytes.Should().Equal(Png(), "sai exatamente o que entrou");
    }

    [Fact(DisplayName = "ADR-0018 — a rota /base64 devolve o data URI que o navegador consome")]
    public async Task Leitura_DevolveBase64()
    {
        var restaurante = await CadastrarRestauranteAsync("Base Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Em base64", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var url = await UrlDaRespostaAsync(await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem", Png(), "foto.png", "image/png"));

        using var anonimo = Ambiente.CriarCliente();

        using var documento = await anonimo.GetFromJsonAsync<JsonDocument>(
            new Uri(url + "/base64", UriKind.Relative));

        var raiz = documento!.RootElement;

        raiz.GetProperty("tipoMime").GetString().Should().Be("image/png");
        raiz.GetProperty("tamanhoEmBytes").GetInt32().Should().Be(512);

        var conteudo = raiz.GetProperty("conteudoBase64").GetString()!;

        // O prefixo vem pronto: montar "data:" no frontend é onde se erra o MIME de uma das pontas.
        conteudo.Should().StartWith("data:image/png;base64,");

        Convert.FromBase64String(conteudo["data:image/png;base64,".Length..])
            .Should().Equal(Png());
    }

    [Fact(DisplayName = "ADR-0018 — arquivo inexistente devolve 404, não 500")]
    public async Task Leitura_Inexistente_Da404()
    {
        using var anonimo = Ambiente.CriarCliente();

        var resposta = await anonimo.GetAsync(
            new Uri(Prefixo + Guid.CreateVersion7(), UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// ⚠⚠ <b>A garantia que o disco não sabia dar.</b> No ADR-0015 o arquivo continuava servido
    /// pelo Nginx depois de o registro sumir do sistema — a exclusão parava na borda do sistema de
    /// arquivos. Aqui o metadado é consultado ANTES dos bytes, então excluir o metadado tira a
    /// imagem do ar sem apagar byte nenhum (<c>RN-GER-002</c>).
    /// </summary>
    [Fact(DisplayName = "RN-GER-002 — metadado excluído tira a imagem do ar, e os bytes ficam")]
    public async Task Leitura_MetadadoExcluido_Da404()
    {
        var restaurante = await CadastrarRestauranteAsync("Sumico Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Sumido", 30.00m);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var url = await UrlDaRespostaAsync(await EnviarAsync(
            gestao, $"/api/v1/produtos/{produtoId}/imagem", Jpeg(), "foto.jpg"));

        var id = Guid.Parse(url[Prefixo.Length..]);

        await using (var db = Contexto(restaurante))
        {
            var metadado = await db.Arquivos.SingleAsync(a => a.Id == id);

            metadado.Excluir();

            await db.SaveChangesAsync();
        }

        using var anonimo = Ambiente.CriarCliente();

        (await anonimo.GetAsync(new Uri(url, UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var arquivo = Ambiente.CriarContextoDeArquivo();

        (await arquivo.Binarios.AsNoTracking().AnyAsync(b => b.Id == id))
            .Should().BeTrue("os bytes ficam: quem apaga byte é varredura deliberada, não um clique");
    }

    // ── auxiliares ──────────────────────────────────────────────────────────────────

    /// <summary>O prefixo da referência gravada na coluna — ADR-0018.</summary>
    private const string Prefixo = "/api/v1/arquivos/";


    /// <summary>Assinatura JPEG real (<c>FF D8 FF</c>) seguida de conteúdo qualquer.</summary>
    private static byte[] Jpeg()
    {
        var bytes = new byte[512];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF; bytes[3] = 0xE0;
        return bytes;
    }

    /// <summary>Assinatura PNG real (<c>89 50 4E 47 0D 0A 1A 0A</c>).</summary>
    private static byte[] Png()
    {
        var bytes = new byte[512];
        byte[] assinatura = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        assinatura.CopyTo(bytes, 0);
        return bytes;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente, string rota, byte[] conteudo, string nome, string tipo = "image/jpeg")
    {
        using var forma = new MultipartFormDataContent();
        using var parte = new ByteArrayContent(conteudo);

        parte.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        forma.Add(parte, "arquivo", nome);

        return await cliente.PostAsync(new Uri(rota, UriKind.Relative), forma);
    }

    private static async Task<string> UrlDaRespostaAsync(HttpResponseMessage resposta)
    {
        resposta.EnsureSuccessStatusCode();

        using var documento = await resposta.Content.ReadFromJsonAsync<JsonDocument>();

        return documento!.RootElement.GetProperty("url").GetString()!;
    }

    private async Task<string> EnviarLogoAsync(RestauranteDeTeste restaurante)
    {
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        return await UrlDaRespostaAsync(
            await EnviarAsync(gestao, "/api/v1/unidade/logo", Png(), "logo.png", "image/png"));
    }

    private async Task<string> CriarCozinhaAsync(RestauranteDeTeste restaurante)
    {
        const string senha = "senha-de-cozinha-1234";
        const string email = "cozinha@foto.teste";

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        Guid perfilId;
        await using (var db = Contexto(restaurante))
        {
            perfilId = await db.Perfis.Where(p => p.Name == "Cozinha").Select(p => p.Id).SingleAsync();
        }

        (await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new
            {
                Nome = "Cozinheiro",
                Sobrenome = "Da Chapa",
                Cpf = CpfDeTeste.Gerar(555_111),
                Email = email,
                PerfilId = perfilId,
                SenhaInicial = senha,
            }))
            .EnsureSuccessStatusCode();

        // RF-SEG-005 — troca a senha, senão o gate de senha pendente responde antes do de permissão
        // e o teste mediria o behavior errado.
        var inicial = await AutenticarAsync(email, senha);

        using var primeiroAcesso = Ambiente.CriarCliente(inicial);

        (await primeiroAcesso.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = senha, NovaSenha = senha + "-nova" }))
            .EnsureSuccessStatusCode();

        return await AutenticarAsync(email, senha + "-nova");
    }
}
