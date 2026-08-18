using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-009</c> — horário de funcionamento, endereço e a virada por unidade
/// (<c>RN-GER-006</c> ⚠⚠, <c>RN-GER-036</c>, <c>RN-GER-037</c>, <c>RN-VEN-048</c>).
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class HorarioEViradaTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private async Task<HttpClient> PlataformaAsync()
    {
        var token = await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma);

        return Ambiente.CriarCliente(token);
    }

    private static object Semana(string? abre, string? fecha) =>
        new
        {
            Endereco = new
            {
                Cep = "01310-100",
                Logradouro = "Avenida Paulista",
                Numero = "1000",
                Bairro = "Bela Vista",
                Cidade = "São Paulo",
                Uf = "SP",
            },
            Horarios = Enumerable.Range(0, 7)
                .Select(dia => new { DiaDaSemana = dia, Abre = abre, Fecha = fecha })
                .ToList(),
        };

    /// <summary>
    /// <c>RN-GER-015</c> — a escrita de plataforma sobre dado do cliente exige sessão de suporte
    /// aberta com motivo. Endereço e horário são dado do cliente, e por isso NÃO estão na lista de
    /// isenções do <c>SessaoDeSuporteBehavior</c>: a primeira versão deste teste tomou 403 aqui, e
    /// o 403 estava certo.
    /// </summary>
    private static async Task AbrirSessaoAsync(HttpClient suporte, Guid unidadeId) =>
        (await suporte.PostAsJsonAsync(
            new Uri("/api/v1/plataforma/sessoes-de-suporte", UriKind.Relative),
            new { UnidadeId = unidadeId, Motivo = "Cadastro de horario de funcionamento a pedido do cliente" }))
            .EnsureSuccessStatusCode();

    private static async Task<HttpResponseMessage> DefinirAsync(
        HttpClient plataforma, Guid unidadeId, string? abre, string? fecha)
    {
        await AbrirSessaoAsync(plataforma, unidadeId);

        var corpo = Semana(abre, fecha);

        return await plataforma.PutAsJsonAsync(
            new Uri("/api/v1/restaurante/operacao", UriKind.Relative),
            new
            {
                UnidadeId = unidadeId,
                ((dynamic)corpo).Endereco,
                ((dynamic)corpo).Horarios,
            });
    }

    [Fact(DisplayName = "RN-GER-036 — a virada sai do fechamento que atravessa a meia-noite (CA-1)")]
    public async Task CA1_ViradaDerivadaDoHorario()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Madrugada", "21212121000191");

        using var plataforma = await PlataformaAsync();

        var resposta = await DefinirAsync(plataforma, restaurante.UnidadeId, "18:00", "03:00");

        var corpoDaResposta = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpoDaResposta);

        await using var db = Ambiente.CriarContexto(restaurante.UnidadeId);

        var unidade = await db.Unidades.AsNoTracking()
            .FirstAsync(u => u.Id == restaurante.UnidadeId);

        unidade.HoraDeViradaOperacional.Should().Be(TimeSpan.FromHours(3));

        // RN-GER-037 — o endereço, com o CEP só em dígitos (RN-GER-013).
        unidade.Cep.Should().Be("01310100");
        unidade.EnderecoResumido().Should().Contain("Avenida Paulista, 1000");
    }

    [Fact(DisplayName = "RN-GER-036 ⚠⚠ — o pedido carrega a virada aplicada, e mudar o horário não reclassifica o passado (CA-2)")]
    public async Task CA2_PedidoCarregaAViradaAplicada()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Congelada", "23232323000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Duplo", 30.00m);

        using var plataforma = await PlataformaAsync();

        // Casa que fecha 03h00: vira às 03h00.
        await DefinirAsync(plataforma, restaurante.UnidadeId, "18:00", "03:00");

        using var anonimo = Ambiente.CriarCliente();

        var pedido = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente da Madrugada",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        pedido.EnsureSuccessStatusCode();

        await using var db = Ambiente.CriarContexto(restaurante.UnidadeId);

        var gravado = await db.Pedidos.AsNoTracking().FirstAsync();
        gravado.HoraDeViradaOperacional.Should().Be(TimeSpan.FromHours(3));

        var diaAntes = gravado.DataOperacional;

        // ⚠ O ponto da regra: a casa muda o horário, e o pedido de ontem continua no mesmo dia.
        await DefinirAsync(plataforma, restaurante.UnidadeId, "11:00", "23:00");

        await using var depois = Ambiente.CriarContexto(restaurante.UnidadeId);
        var mesmoPedido = await depois.Pedidos.AsNoTracking().FirstAsync();

        mesmoPedido.HoraDeViradaOperacional.Should().Be(TimeSpan.FromHours(3));
        mesmoPedido.DataOperacional.Should().Be(diaAntes);
    }

    [Fact(DisplayName = "RN-VEN-048 — fora do horário, a vitrine não aceita pedido (CA-3)")]
    public async Task CA3_ForaDoHorarioRecusa()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Fechada", "24242424000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Duplo", 30.00m);

        using var plataforma = await PlataformaAsync();

        // Fechada todos os dias: `abre` nulo é dia fechado (RN-GER-036).
        await DefinirAsync(plataforma, restaurante.UnidadeId, null, null);

        using var anonimo = Ambiente.CriarCliente();

        var cardapio = await anonimo.GetAsync(
            new Uri($"/api/publico/{restaurante.Slug}/cardapio", UriKind.Relative));

        cardapio.EnsureSuccessStatusCode();

        var corpo = await cardapio.Content.ReadAsStringAsync();
        corpo.Should().Contain("\"aberta\":false");

        // A recusa é do SERVIDOR: esconder o botão não impede um POST.
        var pedido = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente Insistente",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        pedido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "RN-VEN-048 — casa sem horário cadastrado continua vendendo (CA-4)")]
    public async Task CA4_SemHorarioContinuaAberta()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Sem Horario", "25252525000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Duplo", 30.00m);

        using var anonimo = Ambiente.CriarCliente();

        // Nenhum horário definido: a regra nova não pode fechar a loja de quem já vendia.
        var pedido = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{restaurante.Slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente de Sempre",
                Telefone = "11977776666",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        pedido.EnsureSuccessStatusCode();
    }
}
