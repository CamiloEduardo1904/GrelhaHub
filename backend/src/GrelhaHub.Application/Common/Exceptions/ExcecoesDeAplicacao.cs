namespace GrelhaHub.Application.Common.Exceptions;

/// <summary>
/// Violação de regra de negócio detectada pela aplicação.
/// <para>
/// ADR-0009 — <b>a mensagem de erro ao usuário vem sempre da aplicação</b>. Erro de
/// constraint que vaza para o usuário é defeito da aplicação, não do banco.
/// </para>
/// </summary>
public sealed class RegraDeNegocioException(string mensagem, string? regra = null)
    : Exception(mensagem)
{
    /// <summary>ID da regra violada, quando houver — ex.: <c>RN-VEN-002</c>.</summary>
    public string? Regra { get; } = regra;
}

public sealed class NaoEncontradoException(string mensagem) : Exception(mensagem);

public sealed class NaoAutorizadoException(string mensagem) : Exception(mensagem);

/// <summary>
/// <c>RN-GER-027</c> — o login foi recusado, e o usuário recebe <b>sempre a mesma mensagem</b>,
/// com um <b>código de suporte</b> que diz o motivo a quem atende.
/// <para>
/// <b>Por que a mensagem é única.</b> Diferenciar "senha errada" de "usuário inativo" de "loja
/// desativada" confirma a existência da conta e conta a situação comercial de um cliente a quem só
/// tem o e-mail dele. As três respostas são idênticas — inclusive o código HTTP.
/// </para>
/// <para>
/// <b>O código é de CORRELAÇÃO, não de motivo</b> — e essa escolha foi imposta por um teste.
/// </para>
/// <para>
/// A primeira versão usava código fixo por causa (<c>GH-LOGIN-01/02/03</c>), para o suporte
/// atender sem abrir log. O <c>CA-5</c> de <c>SPEC-GER-004</c> derrubou: ele compara o <b>corpo
/// inteiro</b> das respostas de "senha errada" e "usuário inativo" e exige que sejam idênticas.
/// Um código por motivo <b>é</b> a distinção que a regra recusa — quem tem só o e-mail de alguém
/// descobriria que a conta existe e está inativa, ou que a loja foi desativada.
/// </para>
/// <para>
/// Agora o código é <b>aleatório por tentativa</b>: não diz nada a quem sonda, e o motivo real vai
/// para o <b>log</b> com o mesmo código. O suporte pede o código ao cliente e acha a tentativa.
/// Custa ao suporte consultar o log — e é o preço de não contar ao atacante o que ele quer saber.
/// </para>
/// </summary>
public sealed class LoginRecusadoException : Exception
{
    /// <summary>Credencial inválida, ou conta que não existe.</summary>
    public const string MotivoCredencial = "Credencial";

    /// <summary><c>RN-RH-006</c> — usuário inativo.</summary>
    public const string MotivoUsuarioInativo = "UsuarioInativo";

    /// <summary><c>RN-GER-027</c> — unidade inativa.</summary>
    public const string MotivoUnidadeInativa = "UnidadeInativa";

    public LoginRecusadoException(string motivo)
        : base("Erro ao realizar login. Entre em contato com o suporte. Código: " + GerarCodigo())
    {
        Motivo = motivo;

        // O código está na mensagem e precisa ser o MESMO aqui, para o log casar com o que o
        // cliente lê na tela.
        Codigo = Message[(Message.LastIndexOf(' ') + 1)..];
    }

    /// <summary>
    /// O motivo real. <b>Vai para o log, nunca para a resposta HTTP</b> — é a distinção que
    /// `RN-GER-027` recusa expor.
    /// </summary>
    public string Motivo { get; }

    /// <summary>O que o cliente lê e dita por telefone.</summary>
    public string Codigo { get; }

    /// <summary>
    /// Oito hexadecimais de um GUID. Curto o suficiente para ser ditado, largo o suficiente para
    /// achar a linha certa no log de um dia de movimento.
    /// </summary>
    private static string GerarCodigo() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}

/// <summary>
/// <c>RN-GER-017</c> — a unidade não tem o módulo habilitado. ADR-0014.
/// <para>
/// Distinta de <see cref="NaoAutorizadoException"/> de propósito: "você não pode" e "o
/// restaurante não contratou" são coisas diferentes, e o suporte precisa distinguir as duas
/// pela mensagem.
/// </para>
/// <para>
/// Responde <b>erro</b> e não lista vazia: vazio se confunde com "não há dados", e o cliente
/// ligaria para o suporte perguntando onde estão os pedidos.
/// </para>
/// </summary>
public sealed class ModuloNaoHabilitadoException(string sigla)
    : Exception($"O módulo {sigla} não está habilitado para este restaurante.")
{
    public string Sigla { get; } = sigla;
}

/// <summary>
/// <c>RN-GER-010</c> — o usuário não tem a permissão exigida. <c>SPEC-GER-004</c>.
/// <para>
/// Distinta de <see cref="ModuloNaoHabilitadoException"/>: "você não pode" e "o restaurante não
/// contratou" são coisas diferentes, e a segunda leva ao comercial, não ao administrador da
/// unidade.
/// </para>
/// </summary>
public sealed class SemPermissaoException(string permissao)
    : Exception("Você não tem permissão para esta operação.")
{
    /// <summary>Código exigido. Vai para o log, não para a mensagem: dizer ao usuário QUAL
    /// permissão falta ajuda quem está mapeando o sistema de fora.</summary>
    public string Permissao { get; } = permissao;
}

/// <summary>
/// <c>RF-SEG-005</c> — o usuario precisa trocar a senha antes de fazer qualquer outra coisa.
/// </summary>
public sealed class TrocaDeSenhaPendenteException()
    : Exception("Troque a sua senha antes de continuar.");

/// <summary>
/// <c>RN-GER-015</c> — o usuario de plataforma tentou ESCREVER sem sessao de suporte aberta.
/// ADR-0013.
/// </summary>
public sealed class SessaoDeSuporteExigidaException()
    : Exception("Abra uma sessão de suporte, com motivo, antes de alterar o dado de um restaurante.");

/// <summary>
/// <c>RN-GER-024</c> ⚠⚠ — o usuário tem <c>usuario.gerenciar</c>, mas o perfil que ele tentou
/// atribuir não está na delegação do perfil dele.
/// <para>
/// <b>Distinta de <see cref="SemPermissaoException"/> de propósito.</b> "Você não pode gerenciar
/// usuários" e "você pode gerenciar usuários, mas não pode criar um Administrador" levam a ações
/// diferentes: a primeira é pedido de permissão, a segunda é pedido ao administrador. Uma
/// mensagem só para os dois casos manda o gerente pedir o acesso que ele já tem.
/// </para>
/// <para>
/// Aqui o nome do perfil <b>vai</b> na mensagem, ao contrário do código de permissão em
/// <see cref="SemPermissaoException"/>: o perfil já é visível na tela de quem está criando o
/// usuário, então dizê-lo não revela nada — e não dizer transformaria a recusa em adivinhação.
/// </para>
/// </summary>
public sealed class DelegacaoNegadaException(string perfilAlvo)
    : Exception($"RN-GER-024 — o seu perfil não pode atribuir o perfil {perfilAlvo}.")
{
    public string PerfilAlvo { get; } = perfilAlvo;
}

/// <summary>
/// <c>RN-GER-014</c> / ADR-0013 — a operação é privativa do suporte da plataforma, e quem tentou
/// está autenticado como usuário de restaurante.
/// <para>
/// <b>Existe porque <see cref="NaoAutorizadoException"/> conflatava duas coisas diferentes</b> e
/// mapeava as duas para <c>401</c>: falha de autenticação (login errado, sessão inválida, senha
/// atual incorreta) e falta de autoridade (autenticado, mas não é plataforma). Para o primeiro
/// caso 401 está certo; para o segundo, <b>401 é errado</b> — o cliente está autenticado, e um
/// 401 leva a interface a mandá-lo fazer login de novo, o que não resolve nada porque não é login
/// que falta.
/// </para>
/// </summary>
public sealed class ExclusivoDaPlataformaException(string mensagem) : Exception(mensagem);
