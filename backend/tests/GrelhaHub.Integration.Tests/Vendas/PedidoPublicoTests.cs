using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;
using GrelhaHub.Application.Features.Gerais.Plataforma;
using GrelhaHub.Application.Features.Vendas.ConsultarPedidoPublico;
using GrelhaHub.Application.Features.Vendas.CriarPedidoPublico;
using GrelhaHub.Application.Features.Vendas.ListarPedidos;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Vendas;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Vendas;

/// <summary>
/// <c>SPEC-VEN-001</c> — CA-3, CA-4, CA-5, CA-6, CA-8, CA-10, CA-11 e CA-12: o pedido pelo
/// cardápio público.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class PedidoPublicoTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string TelefoneDoCliente = "11988887777";

    [Fact(DisplayName = "RN-VEN-038 — o pedido do cardápio público nasce Aberto no canal delivery próprio")]
    public async Task PedidoPublico_NasceAbertoEmDeliveryProprio()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Duplo Cheddar", 41.00m);

        using var anonimo = Ambiente.CriarCliente();

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, (produtoId, 1));

        criado.Status.Should().Be(nameof(StatusPedido.Aberto));
        criado.Numero.Should().Be(1, "o número é sequencial por dia operacional");

        await using var db = Contexto(restaurante);

        var pedido = await db.Pedidos.AsNoTracking().SingleAsync();

        pedido.Status.Should().Be(StatusPedido.Aberto);
        pedido.Canal.Should().Be(CanalVenda.DeliveryProprio);
        pedido.ConfirmadoEm.Should().BeNull(
            "a confirmação é ato do restaurante — é ela que dispara baixa de estoque e produção");

        // Estoque e produção entram nas ondas 3 e 7. Enquanto não existirem, o que se pode
        // afirmar é que o pedido não avançou sozinho — e é isso que RN-VEN-038 garante.
    }

    [Fact(DisplayName = "RN-VEN-008 — o preço praticado é congelado no pedido e não muda com o preço de tabela")]
    public async Task PrecoPraticado_NaoAcompanhaOPrecoDeTabela()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var categoriaId = await PrimeiraCategoriaAsync(restaurante);
        var produtoId = await CriarProdutoAsync(restaurante, "X-Tudo", 32.00m, categoriaId);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, (produtoId, 1));

        criado.Total.Should().Be(32.00m);

        // Amanhã o preço sobe.
        var reajuste = await gestao.PostAsJsonAsync(
            new Uri("/api/v1/cardapio/produtos", UriKind.Relative),
            new
            {
                Id = produtoId,
                CategoriaId = categoriaId,
                Nome = "X-Tudo",
                Descricao = "Reajustado",
                Preco = 38.00m,
            });

        reajuste.EnsureSuccessStatusCode();

        var acompanhamento = await anonimo.GetFromJsonAsync<PedidoPublicoResponse>(new Uri(
            $"/api/publico/{restaurante.Slug}/pedidos/{criado.Numero}?telefone={TelefoneDoCliente}",
            UriKind.Relative));

        acompanhamento!.Itens.Should().ContainSingle();
        acompanhamento.Itens[0].PrecoPraticado.Should().Be(
            32.00m, "o item guarda uma cópia do preço, não uma referência ao cadastro");
        acompanhamento.Total.Should().Be(32.00m, "o total de um pedido passado não muda");

        await using var db = Contexto(restaurante);

        var produto = await db.Produtos.AsNoTracking().SingleAsync(p => p.Id == produtoId);
        produto.Preco.Should().Be(
            Dinheiro.De(38.00m), "o preço de tabela realmente mudou — é o item que não acompanhou");
    }

    [Fact(DisplayName = "RN-VEN-039 — o total do pedido é a soma dos itens")]
    public async Task TotalDoPedido_EhASomaDosItens()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");

        var lanche = await CriarProdutoAsync(restaurante, "Smash Duplo", 28.00m);
        var acompanhamento = await CriarProdutoAsync(restaurante, "Batata Rustica", 18.00m);

        using var anonimo = Ambiente.CriarCliente();

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, (lanche, 2), (acompanhamento, 1));

        // 2 × 28,00 + 1 × 18,00 = 74,00. Sem desconto, taxa de serviço nem taxa de entrega:
        // nenhuma delas incide nesta fatia.
        criado.Total.Should().Be(74.00m);

        await using var db = Contexto(restaurante);

        var itens = await db.ItensDePedido.AsNoTracking().ToListAsync();

        Dinheiro.Somar(itens.Select(item => item.Total)).Should().Be(Dinheiro.De(74.00m));
    }

    [Fact(DisplayName = "RN-GER-006 — pedido criado à 01h30 de domingo pertence ao dia operacional de sábado")]
    public async Task PedidoDeMadrugada_PertenceAoDiaOperacionalAnterior()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Madrugao", 33.00m);

        using var anonimo = Ambiente.CriarCliente();

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, (produtoId, 1));

        // O pedido nasce com a hora real. Para exercitar a regra num instante escolhido, o
        // caminho é reescrever dh_criacao no banco — data_operacional é recalculada pelo
        // PostgreSQL, porque a coluna é GENERATED ALWAYS AS ... STORED.
        var umaEMeiaDaManhaDeDomingo = new DateTimeOffset(2026, 8, 16, 1, 30, 0, TimeSpan.FromHours(-3));

        await ReescreverCriacaoAsync(restaurante.UnidadeId, criado.Id, umaEMeiaDaManhaDeDomingo);

        await using var db = Contexto(restaurante);

        var pedido = await db.Pedidos.AsNoTracking().SingleAsync();

        pedido.DataOperacional.Should().Be(
            new DateOnly(2026, 8, 15),
            "sábado 15/08 — a venda da madrugada fecha com o caixa que foi aberto no sábado");

        pedido.CriadoEm.ToOffset(TimeSpan.FromHours(-3)).Day.Should().Be(
            16, "a data de criação continua sendo domingo: é a de apuração que difere");
    }

    [Fact(DisplayName = "RN-VEN-019 — pedido cancelado não avança para nenhum estado")]
    public async Task PedidoCancelado_NaoAvanca()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Frango Crocante", 31.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, (produtoId, 1));

        var cancelamento = await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{criado.Id}/cancelar", UriKind.Relative),
            new { Motivo = "Cliente desistiu antes da produção" });

        cancelamento.EnsureSuccessStatusCode();

        var avanco = await gestao.PostAsync(
            new Uri($"/api/v1/pedidos/{criado.Id}/avancar", UriKind.Relative), content: null);

        avanco.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await avanco.Content.ReadAsStringAsync()).Should().Contain("RN-VEN-019");

        var confirmacao = await gestao.PostAsync(
            new Uri($"/api/v1/pedidos/{criado.Id}/confirmar", UriKind.Relative), content: null);

        confirmacao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // O salto de estado — Aberto direto para Entregue — não tem como ser expresso por esta
        // API: não existe rota que receba o estado de destino. A recusa do salto está provada
        // em PedidoTests, no domínio; aqui prova-se que o estado terminal é terminal de fato.
        await using var db = Contexto(restaurante);
        (await db.Pedidos.AsNoTracking().SingleAsync()).Status.Should().Be(StatusPedido.Cancelado);
    }

    [Fact(DisplayName = "RN-VEN-040 — pedido em produção, relido do banco, recusa novo item")]
    public async Task PedidoEmProducao_RelidoDoBanco_RecusaNovoItem()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Brisket", 54.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, (produtoId, 1));

        await AvancarAsync(gestao, criado.Id, "confirmar");
        await AvancarAsync(gestao, criado.Id, "avancar");

        await using var db = Contexto(restaurante);

        // Relido do PostgreSQL — não construído em memória. O EF materializa o agregado por
        // construtor privado e backing field (_itens, PropertyAccessMode.Field). Se esse
        // mapeamento estivesse errado, o pedido vindo do banco não imporia a invariante, e o
        // teste de domínio — que sempre constrói em memória — nunca veria.
        var pedido = await db.Pedidos.Include(p => p.Itens).SingleAsync(p => p.Id == criado.Id);

        pedido.Status.Should().Be(StatusPedido.EmProducao);
        pedido.Itens.Should().ContainSingle("o item do pedido veio junto na reidratação");
        pedido.AceitaAlteracaoDeItens.Should().BeFalse();

        var acrescentar = () => pedido.AdicionarItem(produtoId, "Brisket", Dinheiro.De(54.00m), 1);

        acrescentar.Should().Throw<InvalidOperationException>().WithMessage("*RN-VEN-040*");

        pedido.Itens.Should().ContainSingle("a recusa não pode ter deixado item pela metade");
    }

    [Fact(DisplayName = "RN-VEN-040 — pedido confirmado, relido do banco, ainda aceita novo item")]
    public async Task PedidoConfirmado_RelidoDoBanco_AindaAceitaNovoItem()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Pernil Desfiado", 44.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, (produtoId, 1));

        await AvancarAsync(gestao, criado.Id, "confirmar");

        // A fronteira é a produção, não a confirmação: entre confirmar e produzir ainda há
        // fila, e nesse intervalo o restaurante consegue acomodar um "esqueci a batata".
        await using (var db = Contexto(restaurante))
        {
            var pedido = await db.Pedidos.Include(p => p.Itens).SingleAsync(p => p.Id == criado.Id);

            pedido.Status.Should().Be(StatusPedido.Confirmado);
            pedido.AceitaAlteracaoDeItens.Should().BeTrue();

            pedido.AdicionarItem(produtoId, "Pernil Desfiado", Dinheiro.De(44.00m), 2);

            await db.SaveChangesAsync();
        }

        await using var conferencia = Contexto(restaurante);

        var persistido = await conferencia.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .SingleAsync(p => p.Id == criado.Id);

        persistido.Itens.Should().HaveCount(2, "o item acrescentado foi gravado de fato");
        persistido.Total.Should().Be(Dinheiro.De(132.00m), "44,00 + 2 × 44,00 — RN-VEN-039");

        persistido.Itens.Should().OnlyContain(
            item => item.UnidadeId == restaurante.UnidadeId,
            "RN-GER-009 — o item herda a unidade do pedido, sem o handler precisar lembrar");
    }

    [Fact(DisplayName = "RN-GER-002 — o cancelamento pela API exige motivo")]
    public async Task Cancelamento_SemMotivo_EhRecusado()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Costela no Bafo", 58.00m);

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var criado = await CriarPedidoAsync(anonimo, restaurante.Slug, (produtoId, 1));

        var semMotivo = await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{criado.Id}/cancelar", UriKind.Relative),
            new { Motivo = "   " });

        semMotivo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using var db = Contexto(restaurante);

        (await db.Pedidos.AsNoTracking().SingleAsync()).Status.Should().Be(
            StatusPedido.Aberto, "operação recusada não pode ter mudado o estado");
    }

    [Fact(DisplayName = "RN-GER-013 — CNPJ e telefone entram com máscara e são gravados só com dígitos")]
    public async Task DocumentoETelefone_SaoGravadosSemMascara()
    {
        // Pela rota de plataforma: a porta anonima de cadastro foi fechada em 17/08/2026.
        var tokenDePlataforma = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        using var suporte = Ambiente.CriarCliente(tokenDePlataforma);

        var cadastro = await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/estabelecimentos", UriKind.Relative),
            new CadastrarEstabelecimentoCommand(
                RazaoSocial: "Brasa e Fogo LTDA",
                NomeFantasia: "Brasa e Fogo",
                Cnpj: "12.345.678/0001-99",
                Telefone: "(11) 98765-4321",
                NomeAdministrador: "Dona do Restaurante",
                SobrenomeAdministrador: "Da Brasa",

                // RN-GER-013 — o CPF entra COM mascara, como o CNPJ e o telefone deste teste.
                CpfAdministrador: "529.982.247-25",

                Email: "admin@brasa.teste",
                Senha: "senha-de-teste-123",
                Modulos: null));

        cadastro.EnsureSuccessStatusCode();

        var criado = await cadastro.Content.ReadFromJsonAsync<CadastrarRestauranteResponse>();

        await using var db = Ambiente.CriarContexto(criado!.UnidadeId);

        var unidade = await db.Unidades.AsNoTracking().SingleAsync(u => u.Id == criado.UnidadeId);

        unidade.Cnpj.Should().Be("12345678000199");
        unidade.Telefone.Should().Be("11987654321");
    }

    [Fact(DisplayName = "RN-CRM-001 — o mesmo telefone com formatação diferente resolve para um único cliente")]
    public async Task MesmoTelefoneFormatadoDeOutroJeito_NaoDuplicaCliente()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Cheddar Melt", 36.00m);

        using var anonimo = Ambiente.CriarCliente();

        await EnviarPedidoAsync(anonimo, restaurante.Slug, produtoId, "11988887777");
        await EnviarPedidoAsync(anonimo, restaurante.Slug, produtoId, "(11) 98888-7777");
        await EnviarPedidoAsync(anonimo, restaurante.Slug, produtoId, "11 98888 7777");

        await using var db = Contexto(restaurante);

        var clientes = await db.Clientes.AsNoTracking().ToListAsync();

        clientes.Should().ContainSingle(
            "RN-GER-013 mantém o telefone sem máscara, e é isso que faz a unicidade de RN-CRM-001 valer");

        clientes[0].Telefone.Should().Be("11988887777");

        (await db.Pedidos.AsNoTracking().CountAsync()).Should().Be(3, "os três pedidos foram aceitos");
    }

    [Fact(DisplayName = "RN-VEN-052 — observação com mais de 80 caracteres é recusada, e com 80 é gravada inteira")]
    public async Task Observacao_RespeitaOTetoDeOitentaCaracteres()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "X-Salada", 26.00m);

        using var anonimo = Ambiente.CriarCliente();

        var excedida = new string('a', Pedido.MaximoDeCaracteresNaObservacao + 1);

        var recusado = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            CorpoDoPedido(produtoId, excedida));

        recusado.StatusCode.Should().Be(
            HttpStatusCode.UnprocessableEntity,
            "o campo da vitrine tem maxLength, mas quem não passa pela vitrine chega aqui");

        // O limite é fechado, não aberto: 80 exatos passam.
        var noLimite = new string('b', Pedido.MaximoDeCaracteresNaObservacao);

        var aceito = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            CorpoDoPedido(produtoId, noLimite));

        aceito.EnsureSuccessStatusCode();

        await using var db = Contexto(restaurante);

        var pedido = await db.Pedidos.AsNoTracking().SingleAsync();

        pedido.Observacao.Should().Be(
            noLimite, "80 caracteres cabem inteiros — é para isso que o teto existe");
    }

    [Fact(DisplayName = "RN-VEN-052 — a observação chega inteira à listagem da loja, sem truncar")]
    public async Task Observacao_ChegaInteiraNaListagemDaLoja()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa e Fogo", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Onion Rings", 22.00m);

        // Oitenta caracteres com espaço e acento — o texto que a cozinha realmente recebe, e não
        // uma sequência de 'a' que qualquer corte deixaria parecida com o original.
        const string pedidoDoCliente =
            "Sem cebola e sem picles, por favor. Capricha na maionese da casa. Obrigado!!!!!!";

        pedidoDoCliente.Length.Should().Be(
            Pedido.MaximoDeCaracteresNaObservacao, "o caso testa o texto NO limite");

        using var anonimo = Ambiente.CriarCliente();
        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var enviado = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            CorpoDoPedido(produtoId, pedidoDoCliente));

        enviado.EnsureSuccessStatusCode();

        var lista = await gestao.GetFromJsonAsync<List<PedidoResumoDto>>(
            new Uri("/api/v1/pedidos", UriKind.Relative));

        lista.Should().NotBeNull();
        lista!.Should().ContainSingle();

        // ⚠ O corte, se existisse, seria daqui — e é isso que este caso recusa. Truncar na API
        // pouparia bytes e apagaria a única frase que o cliente escreveu para a cozinha.
        lista![0].Observacao.Should().Be(pedidoDoCliente);
        lista[0].Observacao.Should().NotContain("…", "instrução de preparo não leva reticências");
    }

    private static object CorpoDoPedido(Guid produtoId, string? observacao) =>
        new
        {
            NomeCliente = "Cliente de Teste",
            Telefone = TelefoneDoCliente,
            Observacao = observacao,
            Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
        };

    private static async Task<PedidoCriadoResponse> CriarPedidoAsync(
        HttpClient anonimo, string slug, params (Guid ProdutoId, int Quantidade)[] itens)
    {
        var resposta = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente de Teste",
                Telefone = TelefoneDoCliente,
                Observacao = (string?)null,
                Itens = itens
                    .Select(item => new ItemDoPedidoDto(item.ProdutoId, item.Quantidade, null))
                    .ToList(),
            });

        resposta.EnsureSuccessStatusCode();

        return await resposta.Content.ReadFromJsonAsync<PedidoCriadoResponse>()
            ?? throw new InvalidOperationException("O pedido não devolveu corpo.");
    }

    private static async Task AvancarAsync(HttpClient gestao, Guid pedidoId, string acao)
    {
        var resposta = await gestao.PostAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/{acao}", UriKind.Relative), content: null);

        resposta.EnsureSuccessStatusCode();
    }

    private static async Task EnviarPedidoAsync(
        HttpClient anonimo, string slug, Guid produtoId, string telefone)
    {
        var resposta = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente de Teste",
                Telefone = telefone,
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        resposta.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Reescreve <c>dh_criacao</c> por SQL. <c>Pedido.CriadoEm</c> não tem setter — e está
    /// certo que não tenha; o teste é que precisa de um instante escolhido.
    /// </summary>
    private async Task ReescreverCriacaoAsync(Guid unidadeId, Guid pedidoId, DateTimeOffset criacao)
    {
        await using var db = Ambiente.CriarContexto(unidadeId);
        await db.Database.OpenConnectionAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        comando.CommandText = "UPDATE pedido SET dh_criacao = @criacao WHERE id = @id";

        Adicionar(comando, "criacao", criacao.ToUniversalTime());
        Adicionar(comando, "id", pedidoId);

        var afetadas = await comando.ExecuteNonQueryAsync();

        afetadas.Should().Be(1);
    }

    private static void Adicionar(DbCommand comando, string nome, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
