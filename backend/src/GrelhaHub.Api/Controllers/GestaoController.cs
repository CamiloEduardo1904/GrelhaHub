using GrelhaHub.Application.Features.Cardapio.AlternarDisponibilidade;
using GrelhaHub.Application.Features.Cardapio.AlternarSituacaoDaCategoria;
using GrelhaHub.Application.Features.Cardapio.AlternarSituacaoDoProduto;
using GrelhaHub.Application.Features.Cardapio.ListarCardapioInterno;
using GrelhaHub.Application.Features.Cardapio.SalvarCategoria;
using GrelhaHub.Application.Features.Cardapio.SalvarProduto;
using GrelhaHub.Application.Features.Gerais.Auditoria;
using GrelhaHub.Application.Features.Gerais.Autenticar;
using GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;
using GrelhaHub.Application.Features.Gerais.ObterMeuRestaurante;
using GrelhaHub.Application.Features.Gerais.Modulos;
using GrelhaHub.Application.Features.Gerais.Parametros;
using GrelhaHub.Application.Features.Gerais.Usuarios;
using GrelhaHub.Application.Features.Vendas.AlterarStatusPedido;
using GrelhaHub.Application.Features.Vendas.CancelarEExcluirPedido;
using GrelhaHub.Application.Features.Vendas.FilaDeMonitoramento;
using GrelhaHub.Application.Features.Vendas.ListarPedidos;
using GrelhaHub.Application.Features.Vendas.ResumoDaOperacao;
using GrelhaHub.Application.Features.Gerais.Acesso;
using GrelhaHub.Application.Features.Gerais.Plataforma;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrelhaHub.Api.Controllers;

/// <summary>Autenticação e cadastro do restaurante — rotas anônimas.</summary>
[ApiController]
[Route("api/v1/autenticacao")]
public sealed class AutenticacaoController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AutenticarResponse>> Login(
        AutenticarCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));

    // ⚠ NÃO existe rota anônima de cadastro de restaurante, e a ausência é decisão de produto
    // (17/08/2026): **ninguém ganha uma conta sem contrato assinado**. Um `POST` anônimo criava
    // unidade, perfis, parâmetros e um administrador — um cliente inteiro — sem nada assinado do
    // outro lado.
    //
    // Quem cadastra estabelecimento é o usuário de plataforma, em
    // `POST /api/v1/plataforma/estabelecimentos` (SPEC-GER-007 §4), que é também onde os módulos
    // vendidos são informados.
    //
    // O handler `CadastrarRestauranteCommand` **continua existindo**: ele é o caminho único de
    // criação de unidade, chamado pela rota de plataforma. Reabrir o auto-serviço um dia é
    // acrescentar um controller, não reescrever a criação.

    /// <summary>
    /// <c>RN-GER-034</c> ⚠ — pede o link de redefinição de senha.
    /// <para>
    /// <b>Responde <c>202</c> sempre</b>, exista ou não a conta, esteja ela ativa ou não. É a mesma
    /// escolha de <c>RN-GER-027</c> no login: quem sonda não descobre por aqui quem é cliente. Não é
    /// <c>200</c> porque nada foi concluído — a resposta é "recebido; se houver conta, o e-mail
    /// sai", e o código diz isso melhor do que qualquer corpo de mensagem.
    /// </para>
    /// </summary>
    [HttpPost("esqueci-minha-senha")]
    [AllowAnonymous]
    public async Task<IActionResult> EsqueciMinhaSenha(
        SolicitarRedefinicaoDeSenhaCommand comando, CancellationToken ct)
    {
        await sender.Send(comando, ct);

        return Accepted();
    }

    /// <summary>
    /// <c>RN-GER-033</c> / <c>RN-GER-035</c> — consome o link e define a senha.
    /// <para>
    /// Serve ao convite de primeiro acesso e à redefinição: para o sistema é a mesma operação —
    /// provar posse do e-mail e escolher uma senha —, e o que difere entre os dois já está gravado
    /// no token.
    /// </para>
    /// </summary>
    [HttpPost("definir-senha")]
    [AllowAnonymous]
    public async Task<IActionResult> DefinirSenha(
        DefinirSenhaComTokenCommand comando, CancellationToken ct)
    {
        await sender.Send(comando, ct);

        return NoContent();
    }
}

/// <summary>Dados do restaurante autenticado.</summary>
[ApiController]
[Route("api/v1/restaurante")]
[Authorize]
public sealed class RestauranteController(ISender sender) : ControllerBase
{
    [HttpGet("meu")]
    public async Task<ActionResult<MeuRestauranteResponse>> Meu(CancellationToken ct) =>
        Ok(await sender.Send(new ObterMeuRestauranteQuery(), ct));

    /// <summary><c>RN-GER-036</c> / <c>RN-GER-037</c> — endereço e horário de funcionamento.</summary>
    [HttpGet("operacao")]
    public async Task<ActionResult<OperacaoDaUnidadeDto>> Operacao(
        [FromQuery] Guid? unidadeId, CancellationToken ct) =>
        Ok(await sender.Send(new ObterOperacaoDaUnidadeQuery(unidadeId), ct));

    /// <summary>
    /// <c>RN-GER-036</c> ⚠⚠ — grava a semana inteira e <b>rederiva a virada do dia operacional</b>.
    /// <para>
    /// A resposta devolve a virada resultante: ela não é digitada, e a tela precisa mostrar qual
    /// régua passou a valer — senão a consequência mais importante do cadastro fica invisível.
    /// </para>
    /// </summary>
    [HttpPut("operacao")]
    public async Task<ActionResult<OperacaoDaUnidadeDto>> DefinirOperacao(
        DefinirOperacaoDaUnidadeCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));
}

/// <summary>
/// Parâmetros do sistema — <c>SPEC-GER-003</c>.
/// <para>
/// Consulta liberada a qualquer perfil autenticado: o atendente precisa conhecer taxa de serviço
/// e teto de desconto durante a operação. Escrita é de gerência (<c>RN-GER-010</c>).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/parametros")]
[Authorize]
public sealed class ParametrosController(ISender sender) : ControllerBase
{
    /// <summary>
    /// <c>vigenteEm</c> ausente devolve o valor corrente. Informe-o para explicar um cálculo
    /// passado — a leitura é sempre na data do evento.
    /// </summary>
    [HttpGet("{codigo}")]
    public async Task<ActionResult<ParametroResponse>> Obter(
        string codigo, [FromQuery] DateTimeOffset? vigenteEm, CancellationToken ct) =>
        Ok(await sender.Send(new ObterParametroQuery(codigo, vigenteEm), ct));

    /// <summary>Cria uma nova vigência; a anterior é fechada, nunca sobrescrita.</summary>
    [HttpPut("{codigo}")]
    public async Task<ActionResult<ParametroResponse>> Alterar(
        string codigo, [FromBody] AlterarValorBody body, CancellationToken ct) =>
        Ok(await sender.Send(
            new AlterarValorDeParametroCommand(
                codigo, body.NovoValor, body.VigenciaInicio, body.Justificativa),
            ct));

    public sealed record AlterarValorBody(
        string NovoValor, DateTimeOffset VigenciaInicio, string Justificativa);
}

/// <summary>
/// Módulos — <c>SPEC-GER-006</c>. O restaurante consulta o que contratou; quem altera é a
/// plataforma (<c>RN-GER-014</c>).
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class ModulosController(ISender sender) : ControllerBase
{
    /// <summary>Módulos habilitados na própria unidade — a interface monta o menu com isto.</summary>
    [HttpGet("modulos")]
    public async Task<ActionResult<IReadOnlyList<ModuloDaUnidadeDto>>> Meus(
        [FromQuery] DateTimeOffset? em, CancellationToken ct) =>
        Ok(await sender.Send(new ListarModulosDaUnidadeQuery(null, em), ct));

    /// <summary>
    /// O que uma unidade qualquer tem habilitado — <b>só a plataforma</b>. Aceita <c>em</c> para
    /// responder "o que esta unidade tinha contratado em março" (<c>RN-GER-021</c>).
    /// </summary>
    [HttpGet("plataforma/unidades/{unidadeId:guid}/modulos")]
    public async Task<ActionResult<IReadOnlyList<ModuloDaUnidadeDto>>> DaUnidade(
        Guid unidadeId, [FromQuery] DateTimeOffset? em, CancellationToken ct) =>
        Ok(await sender.Send(new ListarModulosDaUnidadeQuery(unidadeId, em), ct));

    /// <summary>Habilita o módulo, abrindo vigência. Decisão comercial da plataforma.</summary>
    [HttpPost("plataforma/unidades/{unidadeId:guid}/modulos/{codigo}/habilitar")]
    public async Task<ActionResult<IReadOnlyList<ModuloDaUnidadeDto>>> Habilitar(
        Guid unidadeId, string codigo, [FromBody] MotivoBody body, CancellationToken ct) =>
        Ok(await sender.Send(
            new AlterarModuloDaUnidadeCommand(unidadeId, codigo, true, body.Justificativa), ct));

    /// <summary>Fecha a vigência corrente. <c>RN-GER-018</c> — não apaga dado.</summary>
    [HttpPost("plataforma/unidades/{unidadeId:guid}/modulos/{codigo}/desabilitar")]
    public async Task<ActionResult<IReadOnlyList<ModuloDaUnidadeDto>>> Desabilitar(
        Guid unidadeId, string codigo, [FromBody] MotivoBody body, CancellationToken ct) =>
        Ok(await sender.Send(
            new AlterarModuloDaUnidadeCommand(unidadeId, codigo, false, body.Justificativa), ct));

    public sealed record MotivoBody(string Justificativa);
}

/// <summary>Usuários da unidade — <c>SPEC-GER-004</c>.</summary>
[ApiController]
[Route("api/v1/usuarios")]
[Authorize]
public sealed class UsuariosController(ISender sender) : ControllerBase
{
    /// <summary>
    /// <c>RN-GER-016</c> — o usuário de plataforma não aparece nesta lista.
    /// <para>
    /// <c>unidadeId</c> é honrado <b>somente</b> para a plataforma (<c>RN-GER-014</c>): é como o
    /// suporte vê a equipe de um cliente. Para o restaurante o parâmetro é ignorado — aceitá-lo
    /// daria a um cliente uma forma de perguntar quem trabalha no vizinho.
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar(
        [FromQuery] Guid? unidadeId, CancellationToken ct) =>
        Ok(await sender.Send(new ListarUsuariosQuery(unidadeId), ct));

    /// <summary>
    /// <c>SPEC-GER-007</c> — os perfis da unidade, marcando quais quem pede pode atribuir
    /// (<c>RN-GER-024</c>). A tela precisa disto para montar o seletor sem copiar a matriz.
    /// </summary>
    /// <summary><c>RN-GER-028</c> — altera nome, sobrenome, CPF e e-mail. Perfil não entra.</summary>
    [HttpPut("{usuarioId:guid}")]
    public async Task<ActionResult<UsuarioDto>> Alterar(
        Guid usuarioId, AlterarUsuarioCorpo corpo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corpo);

        return Ok(await sender.Send(corpo.ParaComando(usuarioId), ct));
    }

    /// <summary>
    /// <c>RN-GER-028</c> — redefine a senha de OUTRO usuário. Reativa a troca obrigatória.
    /// </summary>
    [HttpPost("{usuarioId:guid}/senha")]
    public async Task<IActionResult> RedefinirSenha(
        Guid usuarioId, [FromBody] SenhaCorpo corpo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corpo);

        await sender.Send(new RedefinirSenhaCommand(usuarioId, corpo.NovaSenha), ct);

        return NoContent();
    }

    /// <summary><c>RN-GER-028</c> — reativa quem foi inativado.</summary>
    [HttpPost("{usuarioId:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid usuarioId, CancellationToken ct)
    {
        await sender.Send(new ReativarUsuarioCommand(usuarioId), ct);

        return NoContent();
    }

    [HttpGet("perfis")]
    public async Task<ActionResult<IReadOnlyList<PerfilDaUnidadeDto>>> Perfis(
        [FromQuery] Guid? unidadeId, CancellationToken ct) =>
        Ok(await sender.Send(new ListarPerfisQuery(unidadeId), ct));

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Criar(
        CriarUsuarioCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));

    /// <summary><c>RN-GER-002</c> — desativa; não exclui.</summary>
    [HttpPost("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        await sender.Send(new DesativarUsuarioCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// <c>RF-SEG-005</c> — o próprio usuário troca a sua senha. Única rota que responde enquanto
    /// a troca estiver pendente.
    /// </summary>
    [HttpPost("minha-senha")]
    public async Task<IActionResult> TrocarSenha(TrocarSenhaCommand comando, CancellationToken ct)
    {
        await sender.Send(comando, ct);
        return NoContent();
    }
}

/// <summary>
/// Trilha de auditoria — <c>SPEC-GER-005</c>, <c>RN-GER-011</c>.
/// <para>
/// <b>Somente leitura.</b> Não existe rota que altere ou exclua registro de auditoria: uma trilha
/// alterável não é trilha (CA-7).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/auditoria")]
[Authorize]
public sealed class AuditoriaController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Restrita a quem tem <c>auditoria.consultar</c> — a trilha guarda valores anteriores de
    /// campos sensíveis, e abri-la ao atendente contornaria <c>RN-GER-012</c> por outra porta.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<TrilhaResponse>> Consultar(
        [FromQuery] Guid? unidadeId,
        [FromQuery] string? entidade,
        [FromQuery] Guid? registroId,
        [FromQuery] Guid? usuarioId,
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken ct = default) =>
        Ok(await sender.Send(
            new ConsultarTrilhaQuery(
                unidadeId, entidade, registroId, usuarioId, de, ate, pagina, tamanho),
            ct));
}

/// <summary>Cardápio visto pela gestão.</summary>
[ApiController]
[Route("api/v1/cardapio")]
[Authorize]
public sealed class CardapioController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoriaInternaDto>>> Listar(
        [FromQuery] Guid? unidadeId, CancellationToken ct) =>
        Ok(await sender.Send(new ListarCardapioInternoQuery(unidadeId), ct));

    /// <summary>
    /// Cria ou atualiza um produto.
    /// <para>
    /// <c>RN-GER-010</c> — escrita no cardápio é operação de gerência.
    /// <b>Simplificação declarada</b> (SPEC-VEN-001 §8): a autorização usa o nome do perfil.
    /// O modelo definitivo usa permissão granular (<c>SPEC-GER-004</c>).
    /// </para>
    /// </summary>
    [HttpPost("produtos")]
    public async Task<ActionResult<Guid>> SalvarProduto(
        SalvarProdutoCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));

    /// <summary>
    /// Cria ou atualiza uma categoria — <c>RN-VEN-043</c>, <c>SPEC-VEN-003</c>.
    /// </summary>
    [HttpPost("categorias")]
    public async Task<ActionResult<Guid>> SalvarCategoria(
        SalvarCategoriaCommand comando, CancellationToken ct) =>
        Ok(await sender.Send(comando, ct));

    /// <summary>
    /// Esconde ou devolve o grupo — <c>RN-VEN-043</c>. <b>Não apaga</b> (<c>RN-VEN-045</c>) e
    /// <b>não toca nos produtos</b>.
    /// </summary>
    [HttpPatch("categorias/{id:guid}/situacao")]
    public async Task<IActionResult> AlterarSituacaoDaCategoria(
        Guid id, [FromBody] AlterarSituacaoDaCategoriaBody body, CancellationToken ct)
    {
        await sender.Send(new AlternarSituacaoDaCategoriaCommand(id, body.Ativa), ct);
        return NoContent();
    }

    /// <summary>
    /// O estado <b>cadastral</b> do produto — <c>RN-VEN-044</c>. "Saiu do cardápio."
    /// <para>
    /// Rota distinta de <c>disponibilidade</c> de propósito: são dois estados independentes, e um
    /// enum único faria erro de digitação virar "mudou o outro estado" em silêncio.
    /// </para>
    /// </summary>
    [HttpPatch("produtos/{id:guid}/situacao")]
    public async Task<IActionResult> AlterarSituacaoDoProduto(
        Guid id, [FromBody] AlterarSituacaoDoProdutoBody body, CancellationToken ct)
    {
        await sender.Send(new AlternarSituacaoDoProdutoCommand(id, body.Ativo), ct);
        return NoContent();
    }

    /// <summary>O estado <b>do turno</b> — <c>RN-VEN-044</c>. "Acabou hoje."</summary>
    [HttpPatch("produtos/{id:guid}/disponibilidade")]
    public async Task<IActionResult> AlterarDisponibilidade(
        Guid id, [FromBody] AlterarDisponibilidadeBody body, CancellationToken ct)
    {
        await sender.Send(new AlternarDisponibilidadeCommand(id, body.Disponivel), ct);
        return NoContent();
    }

    // RN-VEN-045 — NÃO existe DELETE para categoria nem para produto, e a ausência é a
    // implementação da regra: produto excluído levaria consigo o item de pedidos históricos,
    // categoria excluída deixaria produto órfão.

    public sealed record AlterarDisponibilidadeBody(bool Disponivel);

    public sealed record AlterarSituacaoDaCategoriaBody(bool Ativa);

    public sealed record AlterarSituacaoDoProdutoBody(bool Ativo);
}

/// <summary>Pedidos recebidos pelo restaurante.</summary>
[ApiController]
[Route("api/v1/pedidos")]
[Authorize]
public sealed class PedidosController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Sem filtro de data, traz o <b>dia operacional</b> corrente — <c>RN-GER-006</c>.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PedidoResumoDto>>> Listar(
        [FromQuery] StatusPedido? status,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] Guid? unidadeId,
        CancellationToken ct) =>
        Ok(await sender.Send(new ListarPedidosQuery(status, de, ate, unidadeId), ct));

    /// <summary>
    /// <c>RN-VEN-054</c> ⚠ — a <b>fila de monitoramento</b>: o que ainda está na casa.
    /// <para>
    /// <b>Sem <c>de</c> e sem <c>ate</c>, e a ausência é a regra.</b> A fila não é recortada por
    /// período: o pedido aberto às 23h50 que atravessou a virada (<c>RN-GER-006</c>) continua para
    /// ser entregue, e sumiria se o recorte fosse o dia operacional corrente. O que sai da fila sai
    /// por <b>estado</b> — <c>Entregue</c>, <c>Finalizado</c> ou <c>Cancelado</c> —, nunca por data.
    /// </para>
    /// <para>
    /// ⚠ A resposta <b>não traz telefone nem observação</b> (<c>SPEC-VEN-004</c> §3). É contrato, e
    /// não filtro de tela.
    /// </para>
    /// </summary>
    [HttpGet("fila")]
    public async Task<ActionResult<IReadOnlyList<PedidoNaFilaDto>>> Fila(
        [FromQuery] Guid? unidadeId, CancellationToken ct) =>
        Ok(await sender.Send(new FilaDeMonitoramentoQuery(unidadeId), ct));

    /// <summary>
    /// <c>RN-GER-038</c> — o painel de operação. <b>Permissão própria</b>, não
    /// <c>pedido.visualizar</c>: o faturamento da semana é outra pergunta que a do pedido da mesa 4.
    /// <para>
    /// Rota sob <c>/pedidos</c> porque é do pedido que ela responde, e o recorte é o mesmo — mesmo
    /// período de <b>dia operacional</b> (<c>RN-VEN-050</c>), mesma unidade opcional para a
    /// plataforma (<c>RN-GER-014</c>).
    /// </para>
    /// </summary>
    [HttpGet("resumo")]
    public async Task<ActionResult<ResumoDaOperacaoDto>> Resumo(
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] Guid? unidadeId,
        CancellationToken ct) =>
        Ok(await sender.Send(new ResumoDaOperacaoQuery(de, ate, unidadeId), ct));

    /// <summary>
    /// <c>RN-VEN-018</c> — a confirmação é o gatilho de baixa de estoque e ordem de produção.
    /// Nesta fatia ela apenas muda o estado; estoque e produção entram nas ondas 3 e 7.
    /// </summary>
    [HttpPost("{id:guid}/confirmar")]
    public async Task<ActionResult<string>> Confirmar(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new AlterarStatusPedidoCommand(id, AcaoDePedido.Confirmar), ct));

    [HttpPost("{id:guid}/avancar")]
    public async Task<ActionResult<string>> Avancar(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new AlterarStatusPedidoCommand(id, AcaoDePedido.Avancar), ct));

    /// <summary>
    /// <c>RN-GER-002</c> — cancelamento exige motivo. A autorizacao esta em
    /// <c>[ExigePermissao("pedido.cancelar")]</c> no command, e nao mais em <c>Roles</c>: o gate
    /// por nome de perfil impedia criar o perfil Operador (<c>RN-GER-022</c>).
    /// </summary>
    [HttpPost("{id:guid}/cancelar")]
    public async Task<ActionResult<string>> Cancelar(
        Guid id, [FromBody] MotivoDoPedidoBody body, CancellationToken ct) =>
        Ok(await sender.Send(new CancelarPedidoCommand(id, body.Motivo), ct));

    /// <summary>
    /// <c>RN-VEN-041</c> — exclusao LOGICA, poder de administrador (<c>pedido.excluir</c>).
    /// <para>
    /// Nao e cancelamento: excluir e reconhecer que o pedido nunca deveria ter existido, e por
    /// isso ele sai da apuracao em vez de entrar na taxa de cancelamento. Nada e apagado
    /// (<c>RN-GER-002</c>).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/excluir")]
    public async Task<IActionResult> Excluir(
        Guid id, [FromBody] MotivoDoPedidoBody body, CancellationToken ct)
    {
        await sender.Send(new ExcluirPedidoCommand(id, body.Motivo), ct);
        return NoContent();
    }

    public sealed record MotivoDoPedidoBody(string Motivo);
}

/// <summary>O corpo de <c>PUT /usuarios/{id}</c> — o id vem da rota.</summary>
public sealed record AlterarUsuarioCorpo(string Nome, string Sobrenome, string Cpf, string Email)
{
    public AlterarUsuarioCommand ParaComando(Guid usuarioId) =>
        new(usuarioId, Nome, Sobrenome, Cpf, Email);
}

public sealed record SenhaCorpo(string NovaSenha);
