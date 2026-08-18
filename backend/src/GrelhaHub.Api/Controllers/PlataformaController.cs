using GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;
using GrelhaHub.Application.Features.Gerais.Plataforma;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GrelhaHub.Api.Controllers;

/// <summary>
/// Administração da plataforma — <c>RN-GER-014</c>, ADR-0013, <c>SPEC-GER-007</c>.
/// <para>
/// Toda rota daqui exige usuário de plataforma. A autorização é do <b>handler</b>, não de um
/// atributo de controller: o gate de permissão do pipeline (<c>RN-GER-010</c>) trabalha com
/// permissão granular, e "é plataforma" não é permissão — é natureza do usuário.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/plataforma")]
public sealed class PlataformaController(ISender sender) : ControllerBase
{
    /// <summary>Os estabelecimentos, com os módulos habilitados de cada um.</summary>
    [HttpGet("estabelecimentos")]
    public async Task<ActionResult<IReadOnlyList<EstabelecimentoDto>>> Listar(CancellationToken ct) =>
        Ok(await sender.Send(new ListarEstabelecimentosQuery(), ct));

    /// <summary>
    /// Cadastra estabelecimento pela plataforma — venda assistida.
    /// <para>
    /// A diferença em relação à porta anônima é uma só, e é o que justifica a rota: aqui os
    /// <b>módulos vendidos</b> entram no ato da criação, porque quem cadastra pelo telefone já
    /// sabe o que foi contratado.
    /// </para>
    /// </summary>
    [HttpPost("estabelecimentos")]
    public async Task<ActionResult<CadastrarRestauranteResponse>> Cadastrar(
        CadastrarEstabelecimentoCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));

    /// <summary>
    /// Cria usuário em um estabelecimento, informando a unidade.
    /// <para>
    /// Rota própria porque a de unidade (<c>/api/v1/usuarios</c>) tira a unidade de quem está
    /// autenticado, e a plataforma não tem unidade (<c>RN-GER-014</c>).
    /// </para>
    /// </summary>
    [HttpPost("estabelecimentos/{unidadeId:guid}/usuarios")]
    public async Task<ActionResult<UsuarioDePlataformaDto>> CriarUsuario(
        Guid unidadeId, CriarUsuarioDePlataformaCorpo corpo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corpo);

        return Ok(await sender.Send(corpo.ParaComando(unidadeId), ct));
    }

    /// <summary><c>RN-GER-029</c> — altera os dados cadastrais. Slug e CNPJ não entram.</summary>
    [HttpPut("estabelecimentos/{unidadeId:guid}")]
    public async Task<ActionResult<EstabelecimentoDto>> Alterar(
        Guid unidadeId, AlterarEstabelecimentoCorpo corpo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corpo);

        return Ok(await sender.Send(corpo.ParaComando(unidadeId), ct));
    }

    /// <summary>
    /// <c>RN-GER-029</c> — inativa ou reativa. Inativa, <b>ninguém da casa autentica</b>
    /// (<c>RN-GER-027</c>) e nada é apagado.
    /// </summary>
    [HttpPost("estabelecimentos/{unidadeId:guid}/situacao")]
    public async Task<ActionResult<EstabelecimentoDto>> AlternarSituacao(
        Guid unidadeId, SituacaoCorpo corpo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corpo);

        return Ok(await sender.Send(
            new AlternarSituacaoDaUnidadeCommand(unidadeId, corpo.Ativa, corpo.Justificativa), ct));
    }

    /// <summary>
    /// O catálogo de módulos com o <b>grafo de dependência</b> — <c>RN-GER-020</c>.
    /// <para>
    /// Serve ao formulário de cadastro assistido, que precisa do grafo <b>antes</b> de a unidade
    /// existir (a consulta de módulos de uma unidade exige o id dela). Sem isto, a tela teria de
    /// carregar uma cópia do grafo no JavaScript — e a cópia divergiria do banco.
    /// </para>
    /// </summary>
    [HttpGet("modulos")]
    public async Task<ActionResult<IReadOnlyList<ModuloDoCatalogoDto>>> Catalogo(CancellationToken ct) =>
        Ok(await sender.Send(new ListarCatalogoDeModulosQuery(), ct));
}

// ⚠ NÃO existe rota de alterar módulo aqui, e a ausência é correção de um defeito meu:
// eu havia criado `PUT estabelecimentos/{id}/modulos/{sigla}` sem notar que
// `POST plataforma/unidades/{id}/modulos/{codigo}/habilitar|desabilitar` já existia desde a
// SPEC-GER-006, testada. Duas rotas para o mesmo comando é a mesma armadilha de duas
// implementações da mesma regra: uma ganha correção e a outra não.

/// <summary>O corpo sem a unidade, que vem da rota.</summary>
public sealed record AlterarEstabelecimentoCorpo(
    string RazaoSocial, string NomeFantasia, string? Telefone)
{
    public AlterarEstabelecimentoCommand ParaComando(Guid unidadeId) =>
        new(unidadeId, RazaoSocial, NomeFantasia, Telefone);
}

public sealed record SituacaoCorpo(bool Ativa, string Justificativa);

/// <summary>O corpo sem a unidade, que vem da rota.</summary>
public sealed record CriarUsuarioDePlataformaCorpo(
    string Nome,
    string Sobrenome,
    string Cpf,
    string Email,
    string PerfilNome,
    string SenhaInicial)
{
    public CriarUsuarioDePlataformaCommand ParaComando(Guid unidadeId) =>
        new(unidadeId, Nome, Sobrenome, Cpf, Email, PerfilNome, SenhaInicial);
}
