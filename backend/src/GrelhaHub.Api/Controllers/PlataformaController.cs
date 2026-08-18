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

    /// <summary>
    /// <c>RN-GER-030</c> — os usuários de plataforma, os colegas de suporte.
    /// <para>
    /// Rota separada de <c>/api/v1/usuarios</c>, que lista <b>usuário de restaurante</b> e filtra
    /// <c>EhPlataforma</c> fora (<c>RN-GER-016</c>). São duas populações sem interseção: uma tem
    /// unidade, perfil e CPF; a outra não tem nenhum dos três.
    /// </para>
    /// </summary>
    [HttpGet("usuarios")]
    public async Task<ActionResult<IReadOnlyList<UsuarioDeSuporteDto>>> ListarUsuariosDeSuporte(
        CancellationToken ct) =>
        Ok(await sender.Send(new ListarUsuariosDeSuporteQuery(), ct));

    /// <summary>
    /// <c>RN-GER-030</c> — cria outro usuário de plataforma.
    /// <para>
    /// Sem unidade e sem perfil no corpo, e a ausência é o ponto: quem opera o produto não pertence
    /// a restaurante nenhum (<c>RN-GER-014</c>). Nasce com troca de senha obrigatória
    /// (<c>RF-SEG-005</c>) e <b>não</b> exige sessão de suporte aberta (<c>RN-GER-015</c>) — não há
    /// dado de cliente sendo tocado, nem unidade para a qual abrir a sessão.
    /// </para>
    /// </summary>
    [HttpPost("usuarios")]
    public async Task<ActionResult<UsuarioDeSuporteDto>> CriarUsuarioDeSuporte(
        CriarUsuarioDeSuporteCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));

    /// <summary>
    /// <c>RN-GER-031</c> ⚠ — inativa ou reativa um usuário de plataforma. Nunca exclui
    /// (<c>RN-GER-002</c>), nunca a si mesmo, e nunca o último ativo.
    /// </summary>
    [HttpPost("usuarios/{usuarioId:guid}/situacao")]
    public async Task<ActionResult<UsuarioDeSuporteDto>> AlternarSituacaoDoUsuarioDeSuporte(
        Guid usuarioId, SituacaoDoUsuarioCorpo corpo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corpo);

        return Ok(await sender.Send(
            new AlternarSituacaoDoUsuarioDeSuporteCommand(usuarioId, corpo.Ativo), ct));
    }
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

/// <summary>
/// O corpo sem o usuário, que vem da rota — <c>RN-GER-031</c>.
/// <para>
/// <b>Sem justificativa</b>, ao contrário de <see cref="SituacaoCorpo"/>: ali se encerra o contrato
/// de um cliente, e o motivo é o que a trilha precisa guardar. Aqui é a equipe da plataforma
/// administrando a si mesma — a autoria na trilha (<c>SPEC-GER-005</c>) já responde quem desligou
/// quem. Pedir justificativa sem uso seria campo de formulário fingindo controle.
/// </para>
/// </summary>
public sealed record SituacaoDoUsuarioCorpo(bool Ativo);

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
