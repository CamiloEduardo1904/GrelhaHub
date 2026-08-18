using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Cardapio;
using GrelhaHub.Application.Features.Cardapio.ListarCardapioInterno;
using GrelhaHub.Application.Features.Cardapio.ObterCardapioPublico;
using GrelhaHub.Application.Features.Gerais.Parametros;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Cardapio;

/// <summary>
/// <c>SPEC-VEN-003</c> — cadastro do cardápio. CA-1 a CA-21.
/// <para>
/// O cadastro do restaurante semeia três categorias ("Lanches", "Acompanhamentos", "Bebidas") e
/// sete produtos. Os testes daqui criam os seus próprios nomes para não depender do exemplo — e
/// os dois testes de duplicidade usam justamente um nome semeado, porque é o duplicado mais
/// provável na vida real.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class CadastroDoCardapioTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string Cnpj = "22222222000191";

    [Fact(DisplayName = "RN-VEN-043 — categoria é criada e aparece na gestão como ativa (CA-1)")]
    public async Task CA1_CategoriaEhCriada()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var id = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);

        var categorias = await ListarAsync(gestao);

        var criada = categorias.Should().ContainSingle(c => c.Id == id).Subject;

        criada.Nome.Should().Be("Sobremesas");
        criada.Ordem.Should().Be(4);
        criada.Ativa.Should().BeTrue();
        criada.Produtos.Should().BeEmpty("categoria nova nasce vazia");
    }

    [Fact(DisplayName = "RN-VEN-043 — nome de categoria repetido na unidade é recusado com 422, não 500 (CA-2)")]
    public async Task CA2_NomeDeCategoriaRepetidoEhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/categorias", UriKind.Relative),
            new { Id = (Guid?)null, Nome = "Lanches", Ordem = 9 });

        // 422 e não 500: `uq_categoria_nome` já recusava o duplicado, e sem a conferência no
        // handler o usuário recebia o texto de violação de índice do PostgreSQL. Pelo ADR-0009,
        // erro de constraint que vaza para o usuário é defeito da APLICAÇÃO.
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await resposta.Content.ReadAsStringAsync())
            .Should().Contain("Já existe uma categoria com este nome");
    }

    [Fact(DisplayName = "RN-GER-009 — o mesmo nome de categoria em outra unidade é aceito (CA-3)")]
    public async Task CA3_MesmoNomeEmOutraUnidadeEhAceito()
    {
        var primeiro = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);
        var segundo = await CadastrarRestauranteAsync("Outro Burger", "33333333000191");

        using var gestaoDoSegundo = Ambiente.CriarCliente(segundo.Token);

        // "Sobremesas" no primeiro, "Sobremesas" no segundo: a chave única começa por unidade_id,
        // e é isso que faz dois restaurantes conviverem no mesmo banco.
        using (var gestaoDoPrimeiro = Ambiente.CriarCliente(primeiro.Token))
        {
            await SalvarCategoriaAsync(gestaoDoPrimeiro, "Sobremesas", ordem: 4);
        }

        var resposta = await gestaoDoSegundo.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/categorias", UriKind.Relative),
            new { Id = (Guid?)null, Nome = "Sobremesas", Ordem = 4 });

        resposta.EnsureSuccessStatusCode();
    }

    [Fact(DisplayName = "RN-VEN-043 — categoria é renomeada e reordenada sem trocar de id (CA-4)")]
    public async Task CA4_CategoriaEhRenomeadaEReordenada()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var id = await SalvarCategoriaAsync(gestao, "Sobremesa", ordem: 5);

        var mesmoId = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 1, id: id);

        mesmoId.Should().Be(id, "atualizar não cria outra categoria");

        var atualizada = (await ListarAsync(gestao)).Single(c => c.Id == id);

        atualizada.Nome.Should().Be("Sobremesas");
        atualizada.Ordem.Should().Be(1);
    }

    [Fact(DisplayName = "RN-VEN-043 — categoria inativa esconde os produtos do público SEM inativá-los (CA-5)")]
    public async Task CA5_CategoriaInativaEscondeSemInativar()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);
        using var anonimo = Ambiente.CriarCliente();

        var categoriaId = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);

        await CriarProdutoAsync(restaurante, "Pudim", 14.00m, categoriaId);
        await CriarProdutoAsync(restaurante, "Brownie", 16.00m, categoriaId);

        await AlternarCategoriaAsync(gestao, categoriaId, ativa: false);

        var publico = await PublicoAsync(anonimo, restaurante.Slug);

        publico.Categorias.Should().NotContain(c => c.Id == categoriaId);
        publico.Categorias.SelectMany(c => c.Produtos)
            .Should().NotContain(p => p.Nome == "Pudim" || p.Nome == "Brownie");

        // O coração da regra: o cadastro dos produtos NÃO foi tocado. Propagar a inativação seria
        // irreversível — ao reativar, ninguém saberia qual produto já estava inativo antes.
        await using var db = Contexto(restaurante);

        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.CategoriaId == categoriaId)
            .ToListAsync();

        produtos.Should().HaveCount(2).And.OnlyContain(p => p.Ativo);
    }

    [Fact(DisplayName = "RN-VEN-043 — reativar a categoria devolve os produtos ao cardápio (CA-6)")]
    public async Task CA6_ReativarDevolveOsProdutos()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);
        using var anonimo = Ambiente.CriarCliente();

        var categoriaId = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);

        await CriarProdutoAsync(restaurante, "Pudim", 14.00m, categoriaId);
        await CriarProdutoAsync(restaurante, "Brownie", 16.00m, categoriaId);

        await AlternarCategoriaAsync(gestao, categoriaId, ativa: false);
        await AlternarCategoriaAsync(gestao, categoriaId, ativa: true);

        var publico = await PublicoAsync(anonimo, restaurante.Slug);

        var grupo = publico.Categorias.Should().ContainSingle(c => c.Id == categoriaId).Subject;

        // Os DOIS voltaram, sem ninguém reativar produto por produto: é o que "esconder" ganha de
        // "propagar", e é o que torna "esconder as sobremesas no inverno" um clique.
        grupo.Produtos.Select(p => p.Nome).Should().BeEquivalentTo(["Brownie", "Pudim"]);
    }

    [Fact(DisplayName = "RN-VEN-043 — categoria inativa continua visível para a gestão (CA-7)")]
    public async Task CA7_CategoriaInativaFicaVisivelParaAGestao()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var categoriaId = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);

        await CriarProdutoAsync(restaurante, "Pudim", 14.00m, categoriaId);
        await AlternarCategoriaAsync(gestao, categoriaId, ativa: false);

        var categorias = await ListarAsync(gestao);

        // Havia um `.Where(c => c.Ativa)` nesta consulta, e ele era um beco: a categoria
        // desaparecia da tela e NÃO HAVIA COMO REATIVÁ-LA — os produtos ficavam presos num grupo
        // invisível, com UPDATE na mão como única saída.
        var escondida = categorias.Should().ContainSingle(c => c.Id == categoriaId).Subject;

        escondida.Ativa.Should().BeFalse("é este campo que permite a tela oferecer 'Reativar'");
        escondida.Produtos.Should().ContainSingle(p => p.Nome == "Pudim");
    }

    [Fact(DisplayName = "RN-VEN-044 — nome de produto repetido na unidade é recusado com 422 (CA-8)")]
    public async Task CA8_NomeDeProdutoRepetidoEhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/produtos", UriKind.Relative),
            new
            {
                Id = (Guid?)null,
                CategoriaId = await PrimeiraCategoriaAsync(restaurante),
                Nome = "X-Burger",
                Descricao = "Cópia",
                Preco = 30.00m,
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await resposta.Content.ReadAsStringAsync())
            .Should().Contain("Já existe um produto com este nome");
    }

    [Fact(DisplayName = "RN-VEN-032 — preço zero é recusado e nada é gravado (CA-9)")]
    public async Task CA9_PrecoZeroEhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/produtos", UriKind.Relative),
            new
            {
                Id = (Guid?)null,
                CategoriaId = await PrimeiraCategoriaAsync(restaurante),
                Nome = "De graça",
                Descricao = (string?)null,
                Preco = 0m,
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using var db = Contexto(restaurante);

        (await db.Produtos.AsNoTracking().AnyAsync(p => p.Nome == "De graça"))
            .Should().BeFalse();
    }

    [Fact(DisplayName = "RN-VEN-020 — produto inativo sai do público e fica na gestão (CA-10)")]
    public async Task CA10_ProdutoInativoSaiDoPublico()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);
        using var anonimo = Ambiente.CriarCliente();

        var produtoId = await CriarProdutoAsync(restaurante, "Sazonal", 25.00m);

        await AlternarSituacaoAsync(gestao, produtoId, ativo: false);

        var publico = await PublicoAsync(anonimo, restaurante.Slug);

        publico.Categorias.SelectMany(c => c.Produtos)
            .Should().NotContain(p => p.Id == produtoId);

        var naGestao = (await ListarAsync(gestao))
            .SelectMany(c => c.Produtos)
            .Should().ContainSingle(p => p.Id == produtoId).Subject;

        naGestao.Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "RN-VEN-044 — inativar e indisponibilizar são estados independentes (CA-11)")]
    public async Task CA11_OsDoisEstadosSaoIndependentes()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var produtoId = await CriarProdutoAsync(restaurante, "Dois estados", 25.00m);

        // "Acabou hoje" — decisão de quem está no salão, e volta amanhã.
        await AlternarDisponibilidadeAsync(gestao, produtoId, disponivel: false);

        var depoisDoTurno = await ProdutoAsync(gestao, produtoId);

        depoisDoTurno.Ativo.Should().BeTrue("indisponibilizar não retira do cardápio");
        depoisDoTurno.Disponivel.Should().BeFalse();

        // De volta ao normal, para provar que o próximo passo mexe só no outro campo.
        await AlternarDisponibilidadeAsync(gestao, produtoId, disponivel: true);

        // "Saiu do cardápio" — decisão de quem cadastra, e não volta sozinho.
        await AlternarSituacaoAsync(gestao, produtoId, ativo: false);

        var depoisDaTemporada = await ProdutoAsync(gestao, produtoId);

        depoisDaTemporada.Ativo.Should().BeFalse();
        depoisDaTemporada.Disponivel.Should().BeTrue(
            "inativar NÃO mexe na disponibilidade — fundir os dois é o erro que RN-VEN-044 impede");
    }

    [Fact(DisplayName = "RN-VEN-020 — produto inativo não entra em pedido novo (CA-12)")]
    public async Task CA12_ProdutoInativoNaoEntraEmPedido()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);
        using var anonimo = Ambiente.CriarCliente();

        var produtoId = await CriarProdutoAsync(restaurante, "Fora de linha", 25.00m);

        await AlternarSituacaoAsync(gestao, produtoId, ativo: false);

        var pedido = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente de Teste",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        pedido.IsSuccessStatusCode.Should().BeFalse(
            "sair do cardápio e continuar vendável seria a metade inútil da regra");
    }

    [Fact(DisplayName = "RN-VEN-045 — inativar o produto preserva o item do pedido histórico (CA-13)")]
    public async Task CA13_PedidoHistoricoSobreviveAInativacao()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);
        using var anonimo = Ambiente.CriarCliente();

        var produtoId = await CriarProdutoAsync(restaurante, "Vendido antes", 27.50m);

        var pedido = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente de Teste",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 2, Observacao = (string?)null } },
            });

        pedido.EnsureSuccessStatusCode();

        await AlternarSituacaoAsync(gestao, produtoId, ativo: false);

        await using var db = Contexto(restaurante);

        var item = await db.ItensDePedido.AsNoTracking().SingleAsync();

        // RN-VEN-008 — o item guarda cópia do nome e do preço praticados. Se houvesse exclusão,
        // este pedido perderia o que foi vendido; é por isso que RN-VEN-045 existe.
        item.NomeProduto.Should().Be("Vendido antes");
        item.PrecoPraticado.Valor.Should().Be(27.50m);
        item.Quantidade.Should().Be(2);
    }

    [Fact(DisplayName = "RN-GER-009 — produto de outra unidade não é alterado (CA-14)")]
    public async Task CA14_ProdutoDeOutraUnidadeNaoEhAlterado()
    {
        var alheio = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);
        var invasor = await CadastrarRestauranteAsync("Outro Burger", "33333333000191");

        var produtoId = await CriarProdutoAsync(alheio, "Do vizinho", 25.00m);

        using var comoInvasor = Ambiente.CriarCliente(invasor.Token);

        var situacao = await comoInvasor.PatchAsJsonAsync(
            new Uri($"/api/v1/cardapio/produtos/{produtoId}/situacao", UriKind.Relative),
            new { Ativo = false });

        situacao.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var salvamento = await comoInvasor.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/produtos", UriKind.Relative),
            new
            {
                Id = produtoId,
                CategoriaId = await PrimeiraCategoriaAsync(invasor),
                Nome = "Sequestrado",
                Descricao = (string?)null,
                Preco = 1.00m,
            });

        salvamento.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var db = Contexto(alheio);

        var produto = await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoId);

        produto.Nome.Should().Be("Do vizinho");
        produto.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "RN-GER-010 — sem cardapio.gerenciar não cria categoria nem produto (CA-15)")]
    public async Task CA15_SemPermissaoNaoEscreve()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        var token = await CriarCozinhaAsync(restaurante);

        using var comoCozinha = Ambiente.CriarCliente(token);

        var categoria = await comoCozinha.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/categorias", UriKind.Relative),
            new { Id = (Guid?)null, Nome = "Sobremesas", Ordem = 4 });

        categoria.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var produto = await comoCozinha.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/produtos", UriKind.Relative),
            new
            {
                Id = (Guid?)null,
                CategoriaId = await PrimeiraCategoriaAsync(restaurante),
                Nome = "Da cozinha",
                Descricao = (string?)null,
                Preco = 20.00m,
            });

        produto.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "RN-GER-012 — o cardápio da gestão não devolve custo, CMV nem margem (CA-16)")]
    public async Task CA16_NenhumCampoDeCusto()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var corpo = await gestao.GetStringAsync(new Uri("/api/v1/cardapio", UriKind.Relative));

        // A restrição é de DADO, não de tela: devolver e esconder no front continuaria visível em
        // qualquer inspeção de rede.
        corpo.Should().NotContainEquivalentOf("custo")
            .And.NotContainEquivalentOf("cmv")
            .And.NotContainEquivalentOf("margem");
    }

    // ════════════════════════════════════════════════════════════════════════════════
    // RN-VEN-046 — teto de produtos ativos por categoria (`PAR-VEN-004`)
    // ════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "RN-VEN-046 — o teto padrão é 50 produtos ativos por categoria (CA-17)")]
    public async Task CA17_TetoPadraoEhCinquenta()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Lê pela rota real, e não a constante do código: `PAR-VEN-004` semeado errado no banco
        // só apareceria em produção.
        var parametro = await gestao.GetFromJsonAsync<ParametroResponse>(
            new Uri($"/api/v1/parametros/{TetoDeProdutos.Parametro}", UriKind.Relative));

        parametro!.Valor.Should().Be("50");
    }

    [Fact(DisplayName = "RN-VEN-046 — produto além do teto da categoria é recusado (CA-18)")]
    public async Task CA18_AlemDoTetoEhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var categoriaId = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);

        // Baixa o teto em vez de criar 51 produtos: 51 requisições provariam a mesma coisa e
        // amarrariam o teste ao valor padrão — trocar 50 por 40 amanhã o quebraria. De passagem,
        // isto exercita o "ajustável" da regra, que é metade dela.
        await AjustarTetoAsync(gestao, 2);

        // Prova que o ajuste pegou antes de medir a recusa: sem isto, um teto que não mudou
        // apareceria como "a regra não recusou", que é o diagnóstico errado.
        var conferencia = await gestao.GetFromJsonAsync<ParametroResponse>(
            new Uri($"/api/v1/parametros/{TetoDeProdutos.Parametro}", UriKind.Relative));

        conferencia!.Valor.Should().Be("2", "o ajuste do teto tem de estar valendo agora");

        await CriarProdutoAsync(restaurante, "Pudim", 14.00m, categoriaId);
        await CriarProdutoAsync(restaurante, "Brownie", 16.00m, categoriaId);

        var terceiro = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/produtos", UriKind.Relative),
            new
            {
                Id = (Guid?)null,
                CategoriaId = categoriaId,
                Nome = "Petit gateau",
                Descricao = (string?)null,
                Preco = 22.00m,
            });

        terceiro.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // O número na mensagem: o teto é por unidade e não há tela que o exiba, então sem ele o
        // usuário lê "chegou ao limite" sem saber qual é o limite.
        (await terceiro.Content.ReadAsStringAsync()).Should().Contain("2 produtos");

        await using var db = Contexto(restaurante);

        (await db.Produtos.AsNoTracking().AnyAsync(p => p.Nome == "Petit gateau"))
            .Should().BeFalse();
    }

    [Fact(DisplayName = "RN-VEN-046 — o teto é por categoria, não por unidade (CA-18)")]
    public async Task CA18_OTetoEhPorCategoria()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var cheia = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);
        var vazia = await SalvarCategoriaAsync(gestao, "Porções", ordem: 5);

        await AjustarTetoAsync(gestao, 1);

        await CriarProdutoAsync(restaurante, "Pudim", 14.00m, cheia);

        // Mesmo teto, outra categoria: o limite é do grupo, não do restaurante.
        var noutra = await CriarProdutoAsync(restaurante, "Mandioca frita", 19.00m, vazia);

        noutra.Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "RN-VEN-046 — o teto conta só os ativos: tirar do cardápio abre vaga (CA-19)")]
    public async Task CA19_OTetoContaSoOsAtivos()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var categoriaId = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);

        await AjustarTetoAsync(gestao, 1);

        var primeiro = await CriarProdutoAsync(restaurante, "Pudim", 14.00m, categoriaId);

        // RN-VEN-045 proíbe exclusão: o produto retirado FICA na tabela. Se o teto contasse o
        // histórico, esta categoria nunca aceitaria nada de novo — e o usuário não teria como
        // resolver, porque excluir não existe.
        await AlternarSituacaoAsync(gestao, primeiro, ativo: false);

        var vaga = await CriarProdutoAsync(restaurante, "Brownie", 16.00m, categoriaId);

        vaga.Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "RN-VEN-046 — reativar produto além do teto é recusado (CA-20)")]
    public async Task CA20_ReativarNaoContornaOTeto()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var categoriaId = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);

        await AjustarTetoAsync(gestao, 1);

        var primeiro = await CriarProdutoAsync(restaurante, "Pudim", 14.00m, categoriaId);

        await AlternarSituacaoAsync(gestao, primeiro, ativo: false);
        await CriarProdutoAsync(restaurante, "Brownie", 16.00m, categoriaId);

        // ⚠ O contorno óbvio, e ele nem parece trapaça: dois cliques de gente resolvendo o dia.
        // Sem o teto imposto TAMBÉM na reativação, a categoria fica com o dobro do limite.
        var volta = await gestao.PatchAsJsonAsync(
            new Uri($"/api/v1/cardapio/produtos/{primeiro}/situacao", UriKind.Relative),
            new { Ativo = true });

        volta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using var db = Contexto(restaurante);

        var pudim = await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == primeiro);

        pudim.Ativo.Should().BeFalse("a recusa não pode ter gravado nada");
    }

    [Fact(DisplayName = "RN-VEN-046 — editar produto de categoria ACIMA do teto continua permitido (CA-21)")]
    public async Task CA21_EditarNaCategoriaAcimaDoTetoEhPermitido()
    {
        var restaurante = await CadastrarRestauranteAsync("Cadastro Burger", Cnpj);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var categoriaId = await SalvarCategoriaAsync(gestao, "Sobremesas", ordem: 4);

        // Três produtos com o teto padrão de 50, e SÓ DEPOIS o teto cai para 1. É o cenário real:
        // ninguém cadastra acima do limite, mas o limite pode ser reduzido em cima do que já
        // existe — e aí a categoria fica acima dele.
        var pudim = await CriarProdutoAsync(restaurante, "Pudim", 14.00m, categoriaId);
        await CriarProdutoAsync(restaurante, "Brownie", 16.00m, categoriaId);
        await CriarProdutoAsync(restaurante, "Petit gateau", 22.00m, categoriaId);

        await AjustarTetoAsync(gestao, 1);

        // Corrigir o preço de um produto que JÁ ESTÁ na categoria não é uma entrada nova.
        // Conferir o teto em toda edição travaria justamente a correção do dado que precisa ser
        // corrigido: com a categoria em 3 e o teto em 1, nada mais poderia ser editado ali.
        var reajuste = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/produtos", UriKind.Relative),
            new
            {
                Id = pudim,
                CategoriaId = categoriaId,
                Nome = "Pudim de leite",
                Descricao = "Calda de caramelo",
                Preco = 16.50m,
            });

        reajuste.EnsureSuccessStatusCode();

        await using var db = Contexto(restaurante);

        var salvo = await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == pudim);

        salvo.Nome.Should().Be("Pudim de leite");
        salvo.Preco.Valor.Should().Be(16.50m);
    }

    /// <summary>
    /// Baixa o teto pela rota real de parâmetro — o "ajustável" da regra passando pelo caminho que
    /// o sistema tem (`SPEC-GER-003`).
    /// </summary>
    private static async Task AjustarTetoAsync(HttpClient gestao, int teto)
    {
        var resposta = await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/parametros/{TetoDeProdutos.Parametro}", UriKind.Relative),
            new
            {
                NovoValor = teto.ToString(CultureInfo.InvariantCulture),

                // Ontem, e não "agora": a vigência precisa começar antes do instante em que o
                // handler lê o valor, senão uma diferença de milissegundos entre a escrita e a
                // leitura deixaria o teste intermitente.
                VigenciaInicio = DateTimeOffset.UtcNow.AddDays(-1),
                Justificativa = "Teste de integração do teto por categoria",
            });

        resposta.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> SalvarCategoriaAsync(
        HttpClient gestao, string nome, int ordem, Guid? id = null)
    {
        var resposta = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/categorias", UriKind.Relative),
            new { Id = id, Nome = nome, Ordem = ordem });

        resposta.EnsureSuccessStatusCode();

        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task AlternarCategoriaAsync(HttpClient gestao, Guid categoriaId, bool ativa)
    {
        var resposta = await gestao.PatchAsJsonAsync(
            new Uri($"/api/v1/cardapio/categorias/{categoriaId}/situacao", UriKind.Relative),
            new { Ativa = ativa });

        resposta.EnsureSuccessStatusCode();
    }

    private static async Task AlternarSituacaoAsync(HttpClient gestao, Guid produtoId, bool ativo)
    {
        var resposta = await gestao.PatchAsJsonAsync(
            new Uri($"/api/v1/cardapio/produtos/{produtoId}/situacao", UriKind.Relative),
            new { Ativo = ativo });

        resposta.EnsureSuccessStatusCode();
    }

    private static async Task AlternarDisponibilidadeAsync(
        HttpClient gestao, Guid produtoId, bool disponivel)
    {
        var resposta = await gestao.PatchAsJsonAsync(
            new Uri($"/api/v1/cardapio/produtos/{produtoId}/disponibilidade", UriKind.Relative),
            new { Disponivel = disponivel });

        resposta.EnsureSuccessStatusCode();
    }

    private static async Task<IReadOnlyList<CategoriaInternaDto>> ListarAsync(HttpClient gestao) =>
        await gestao.GetFromJsonAsync<List<CategoriaInternaDto>>(
            new Uri("/api/v1/cardapio", UriKind.Relative))
        ?? throw new InvalidOperationException("O cardápio interno não devolveu corpo.");

    private static async Task<ProdutoInternoDto> ProdutoAsync(HttpClient gestao, Guid produtoId) =>
        (await ListarAsync(gestao)).SelectMany(c => c.Produtos).Single(p => p.Id == produtoId);

    private static async Task<CardapioPublicoResponse> PublicoAsync(HttpClient anonimo, string slug) =>
        await anonimo.GetFromJsonAsync<CardapioPublicoResponse>(
            new Uri($"/api/publico/{slug}/cardapio", UriKind.Relative))
        ?? throw new InvalidOperationException("O cardápio público não devolveu corpo.");

    /// <summary>
    /// Usuário de Cozinha autenticado, para o teste de permissão. A troca de senha no primeiro
    /// acesso é obrigatória: sem ela, o gate de senha pendente responde <b>antes</b> do de
    /// permissão e o teste mediria o behavior errado.
    /// </summary>
    private async Task<string> CriarCozinhaAsync(RestauranteDeTeste restaurante)
    {
        const string senha = "senha-de-cozinha-1234";
        const string email = "cozinha@cadastro.teste";

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        Guid perfilId;
        await using (var db = Contexto(restaurante))
        {
            perfilId = await db.Perfis.Where(p => p.Nome == "Cozinha").Select(p => p.Id).SingleAsync();
        }

        (await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new
            {
                Nome = "Cozinheiro",
                Sobrenome = "Do Cardápio",
                Cpf = CpfDeTeste.Gerar(555_222),
                Email = email,
                PerfilId = perfilId,
                SenhaInicial = senha,
            }))
            .EnsureSuccessStatusCode();

        var inicial = await AutenticarAsync(email, senha);

        using var primeiroAcesso = Ambiente.CriarCliente(inicial);

        (await primeiroAcesso.PostAsJsonAsync(
            new Uri("/api/v1/usuarios/minha-senha", UriKind.Relative),
            new { SenhaAtual = senha, NovaSenha = senha + "-nova" }))
            .EnsureSuccessStatusCode();

        return await AutenticarAsync(email, senha + "-nova");
    }
}
