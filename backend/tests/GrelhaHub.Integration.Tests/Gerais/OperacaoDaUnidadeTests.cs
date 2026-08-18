using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Features.Gerais.Plataforma;
using GrelhaHub.Integration.Tests.Infraestrutura;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// A CONSULTA de endereço e horário — <c>RN-GER-036</c>, <c>RN-GER-037</c>, <c>RN-GER-006</c>.
/// <para>
/// ⚠⚠ <b>Este arquivo existe porque a medição de cobertura mostrou
/// <c>ObterOperacaoDaUnidadeHandler</c> em ZERO por cento.</b> A escrita tinha teste
/// (<c>HorarioEViradaTests</c> cobre o <c>PUT</c>); a leitura, nenhum — e é a leitura que alimenta
/// a aba "Loja", a tela que o dono do produto abre para conferir o cadastro.
/// </para>
/// <para>
/// O handler tem quatro caminhos e nenhum era exercitado: plataforma sem informar a unidade,
/// plataforma informando, restaurante usando a própria, e unidade inexistente. Três deles são
/// recusas — e recusa sem teste é onde a mensagem errada mora.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class OperacaoDaUnidadeTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private static readonly Uri Rota = new("/api/v1/restaurante/operacao", UriKind.Relative);

    [Fact(DisplayName = "RN-GER-036 — o restaurante lê a PRÓPRIA operação, sem informar unidade")]
    public async Task Restaurante_LeAPropria()
    {
        var restaurante = await CadastrarRestauranteAsync("Brasa Da Leitura", "50505050000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await gestao.GetAsync(Rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        var operacao = await resposta.Content.ReadFromJsonAsync<OperacaoDaUnidadeDto>();

        operacao.Should().NotBeNull();

        // ⚠ RN-GER-006 — a virada vem SEMPRE, mesmo sem horário cadastrado. É a consequência mais
        // importante deste cadastro, e a tela precisa dela para dizer qual régua está valendo.
        operacao!.HoraDeViradaOperacional.Should().NotBe(default);

        // Casa recém-cadastrada não tem horário nem endereço, e isso é estado VÁLIDO
        // (RN-GER-037: tudo opcional). O que não pode é a consulta falhar por causa disso.
        operacao.Horarios.Should().NotBeNull();
        operacao.Endereco.Should().NotBeNull();
    }

    [Fact(DisplayName = "RN-GER-016 — o restaurante informando outra unidade lê a própria, não a do vizinho")]
    public async Task Restaurante_IgnoraUnidadeInformada()
    {
        var minha = await CadastrarRestauranteAsync("Brasa Propria", "51515151000191");
        var vizinha = await CadastrarRestauranteAsync("Brasa Vizinha", "52525252000191");

        using var gestao = Ambiente.CriarCliente(minha.Token);

        // Manda o id do vizinho de propósito.
        var resposta = await gestao.GetAsync(
            new Uri($"/api/v1/restaurante/operacao?unidadeId={vizinha.UnidadeId}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var operacao = await resposta.Content.ReadFromJsonAsync<OperacaoDaUnidadeDto>();

        // ⚠ O parâmetro é IGNORADO, não honrado nem recusado. Honrá-lo daria a um cliente uma
        // forma de perguntar o que o vizinho tem; recusá-lo faria a tela do restaurante quebrar
        // por um parâmetro que ele nem sabe que manda.
        operacao!.UnidadeId.Should().Be(minha.UnidadeId);
        operacao.UnidadeId.Should().NotBe(vizinha.UnidadeId);
    }

    [Fact(DisplayName = "RN-GER-014 — a plataforma lê a operação de QUALQUER casa, informando qual")]
    public async Task Plataforma_LeDeQualquerCasa()
    {
        var casa = await CadastrarRestauranteAsync("Brasa Do Suporte", "53535353000191");

        using var suporte = Ambiente.CriarCliente(await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma));

        var resposta = await suporte.GetAsync(
            new Uri($"/api/v1/restaurante/operacao?unidadeId={casa.UnidadeId}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        var operacao = await resposta.Content.ReadFromJsonAsync<OperacaoDaUnidadeDto>();

        operacao!.UnidadeId.Should().Be(casa.UnidadeId);
    }

    [Fact(DisplayName = "RN-GER-014 — a plataforma SEM informar a unidade é recusada com mensagem, não com erro")]
    public async Task Plataforma_SemUnidade_Recusada()
    {
        using var suporte = Ambiente.CriarCliente(await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma));

        var resposta = await suporte.GetAsync(Rota);

        // ⚠ A plataforma NÃO TEM unidade própria (RN-GER-014). Sem o parâmetro, o handler não tem
        // de qual casa falar — e a resposta precisa dizer isso em português, não estourar.
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await resposta.Content.ReadAsStringAsync())
            .Should().Contain("estabelecimento", "a mensagem diz O QUE FALTA, não que deu errado");
    }

    [Fact(DisplayName = "RN-GER-014 — unidade inexistente devolve 404, e não lista vazia")]
    public async Task UnidadeInexistente_Devolve404()
    {
        using var suporte = Ambiente.CriarCliente(await AutenticarAsync(
            AmbienteDeIntegracao.EmailDePlataforma, AmbienteDeIntegracao.SenhaDePlataforma));

        var resposta = await suporte.GetAsync(
            new Uri($"/api/v1/restaurante/operacao?unidadeId={Guid.CreateVersion7()}", UriKind.Relative));

        // Devolver 200 com endereço vazio faria a tela de cadastro abrir para uma casa que não
        // existe — e o primeiro salvamento criaria dado órfão.
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
