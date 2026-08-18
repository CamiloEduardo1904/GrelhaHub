using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Usuarios;

public sealed record UsuarioDto(
    Guid Id,

    /// <summary>
    /// <c>RN-GER-009</c> — de qual unidade este usuário é. Nulo só no de plataforma, que não
    /// aparece nesta lista (<c>RN-GER-016</c>). Sem ele, a listagem sem recorte da plataforma não
    /// diz de quem é cada pessoa.
    /// </summary>
    Guid? UnidadeId,

    string Nome,
    string? Sobrenome,
    /// <summary><c>RN-GER-023</c> — formatado para exibição. O banco guarda 11 dígitos.</summary>
    string? Cpf,
    string Email,
    string Perfil,
    Guid PerfilId,
    bool Ativo,
    bool DeveTrocarSenha);

/// <summary>
/// <c>SPEC-GER-004</c> — usuários da unidade.
/// <para>
/// <c>RN-GER-016</c> — o usuário de plataforma <b>não aparece</b>. Um usuário que enxerga todos os
/// clientes não pode ser listado, alterado nem sequer visto por um cliente.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("usuario.visualizar")]
public sealed record ListarUsuariosQuery(Guid? UnidadeId = null)
    : IRequest<IReadOnlyList<UsuarioDto>>;

/// <summary>
/// ADR-0019 — convertida ao modelo de leitura em 18/08/2026.
/// <para>
/// ⚠⚠ <b>O recorte deixou de ser responsabilidade deste handler.</b> No lado da escrita
/// <c>Usuario</c> não implementa <c>IPertenceAUnidade</c> — o usuário de plataforma não tem
/// unidade —, e a consequência era que TODO handler precisava lembrar de escrever o
/// <c>WHERE</c>. <c>vw_usuario</c> é recortada pelo filtro global, e o <c>Recorte</c> abaixo passa
/// a servir só ao caso da plataforma escolhendo uma casa.
/// </para>
/// </summary>
public sealed class ListarUsuariosHandler(ILeituraDb leitura, IContextoDeExecucao contexto)
    : IRequestHandler<ListarUsuariosQuery, IReadOnlyList<UsuarioDto>>
{
    public async Task<IReadOnlyList<UsuarioDto>> Handle(
        ListarUsuariosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // RN-GER-014 / ADR-0013 — a plataforma informa QUAL casa quer ver, ou vê todas. O
        // restaurante sempre a própria: `Recorte` IGNORA o parâmetro para ele, e usar o valor cru
        // faria o restaurante que mandasse o id do vizinho ver a própria lista vazia.
        var recorte = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        var linhas = await leitura.Usuarios
            .Where(u => recorte == null || u.UnidadeId == recorte)

            // RN-GER-016 — invisível ao restaurante. Redundante com o recorte de unidade, e
            // escrito de propósito: a redundância aqui custa nada e a ausência custaria o
            // vazamento no dia em que o recorte mudasse.
            .Where(u => !u.EhPlataforma)
            .OrderBy(u => u.Nome)
            .ToListAsync(cancellationToken);

        // ⚠ A formatação do CPF acontece em MEMÓRIA e continua sendo do domínio: `Cpf.Formatado`
        // é value object, e a visão devolve o dígito cru. Formatar em SQL seria a segunda
        // implementação da mesma máscara.
        return [.. linhas.Select(l => new UsuarioDto(
            // `Sobrenome` só é nulo para usuário de plataforma (RN-GER-030), que o `Where` acima
            // já excluiu — a coalescência existe para o contrato, não para o dado.
            l.Id, l.UnidadeId, l.Nome, l.Sobrenome ?? string.Empty, Cpf.Formatar(l.Cpf), l.Email,
            l.PerfilNome ?? string.Empty, l.PerfilId ?? Guid.Empty, l.Ativo, l.DeveTrocarSenha))];
    }
}

/// <summary><c>SPEC-GER-004</c> / <c>SPEC-GER-007</c> — cria usuário na unidade, com perfil.</summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("usuario.gerenciar")]
public sealed record CriarUsuarioCommand(
    string Nome,
    string Sobrenome,
    string Cpf,
    string Email,
    Guid PerfilId,

    /// <summary>
    /// <c>RF-SEG-005</c> / <c>RN-GER-033</c> — <b>opcional, e a ausência é o caminho normal.</b>
    /// <para>
    /// Sem senha, o usuário nasce com uma credencial aleatória que ninguém conhece e recebe o
    /// <b>convite por e-mail</b> para definir a própria (<c>RN-GER-032</c>). É o desenho melhor: a
    /// senha que passa pelo WhatsApp de quem cadastrou não é senha.
    /// </para>
    /// <para>
    /// Continua aceitando senha porque há caso real de entrega presencial — o operador que não tem
    /// e-mail próprio, contratado na hora. Quem informa assume que duas pessoas a conhecem, e
    /// <c>RF-SEG-005</c> obriga a troca no primeiro acesso de qualquer forma.
    /// </para>
    /// </summary>
    string? SenhaInicial = null) : IRequest<UsuarioDto>;

public sealed class CriarUsuarioValidator : AbstractValidator<CriarUsuarioCommand>
{
    public CriarUsuarioValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);

        // RN-GER-023 — sobrenome é obrigatório na CRIAÇÃO, ainda que a coluna seja nulável:
        // a nulidade existe para o usuário de plataforma e para o que já está gravado, não
        // para permitir cadastro novo pela metade.
        RuleFor(x => x.Sobrenome).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Cpf)
            .NotEmpty()
            .Must(cpf => Domain.Common.Cpf.TentarDe(cpf, out _))
            .WithMessage("O CPF informado não é válido.");

        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PerfilId).NotEmpty();

        // Opcional: sem senha, o convite por e-mail define. Quando vem, o mínimo é o mesmo das
        // outras três rotas de senha — divergir aceitaria aqui o que a troca recusa depois.
        RuleFor(x => x.SenhaInicial)
            .MinimumLength(8).WithMessage("A senha inicial deve ter ao menos 8 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.SenhaInicial));
    }
}

public sealed class CriarUsuarioHandler(
    IAppDbContext db,
    IContextoDeExecucao contexto,
    IHashDeSenha hash,
    ITokensDeAcesso tokens,
    Acesso.EmissorDeLinkDeAcesso convites)
    : IRequestHandler<CriarUsuarioCommand, UsuarioDto>
{
    public async Task<UsuarioDto> Handle(CriarUsuarioCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var unidadeId = contexto.UnidadeId;

        // RN-GER-014 — a plataforma não cria usuário de restaurante por esta rota: ela não tem
        // unidade, e `unidadeId` viria vazio. A rota dela é a de plataforma, que informa a
        // unidade explicitamente (SPEC-GER-007 §4).
        if (contexto.EhPlataforma)
        {
            // ⚠ O ID da regra vai junto: é ele que liga o erro visto na tela ao catálogo, e esta
            // recusa vinha sem ele. Só apareceu quando a sessão de suporte saiu — o gate dela
            // devolvia 403 ANTES, e o motivo real desta recusa nunca chegava a ser visto.
            throw new RegraDeNegocioException(
                "O usuário de plataforma cria usuário pela rota de plataforma, informando a " +
                "unidade. Esta rota cria usuário na unidade de quem está autenticado.",
                "RN-GER-016");
        }

        // RN-GER-023 — o validator já garantiu que é válido; aqui só materializa. `De` lançaria
        // se o validator tivesse sido contornado, o que é o comportamento desejado.
        var cpf = Cpf.De(request.Cpf);

        // Único em TODO o sistema (exceção declarada a RN-GER-009, como o e-mail): a pessoa é a
        // mesma pessoa em qualquer restaurante. `IgnoreQueryFilters` porque a checagem tem de
        // atravessar unidade — se ficasse recortada, o mesmo CPF entraria em dois restaurantes e
        // a constraint estouraria como erro cru na cara do usuário.
        var cpfEmUso = await db.Usuarios
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Cpf == cpf, cancellationToken);

        if (cpfEmUso)
        {
            throw new RegraDeNegocioException("Já existe um usuário com este CPF.");
        }

        // O e-mail é único GLOBALMENTE (exceção declarada a RN-GER-009: o login resolve o usuário
        // antes de saber a unidade). Conferir aqui dá mensagem legível em vez de violação de
        // constraint crua — ADR-0009.
        var emailEmUso = await db.Usuarios
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (emailEmUso)
        {
            throw new RegraDeNegocioException("Já existe um usuário com este e-mail.");
        }

        // RN-GER-010 — o perfil precisa ser DESTA unidade. Sem esta checagem, informar o id de um
        // perfil de outro restaurante daria a ele as permissões de lá.
        var perfil = await db.Perfis
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PerfilId, cancellationToken)
            ?? throw new NaoEncontradoException("Perfil não encontrado.");

        // ── RN-GER-024 ⚠⚠ — a matriz de delegação ────────────────────────────────────────
        //
        // Esta é a checagem que torna `usuario.gerenciar` no Gerente segura. Sem ela, o gerente
        // criaria um Administrador e entraria com ele.
        //
        // O perfil de quem cria vem do BANCO, e não do claim `role` do token. O claim seria uma
        // consulta a menos e está errado: token emitido antes de o perfil do usuário mudar
        // continua afirmando o perfil antigo até expirar. Autorizar por dado que pode estar
        // vencido é exatamente o defeito que a matriz existe para impedir.
        var perfilDeQuemCria = await db.Usuarios
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.Id == contexto.UsuarioId)
            .Select(u => u.Perfil!.Nome)
            .FirstOrDefaultAsync(cancellationToken);

        if (!MatrizDeDelegacao.PodeAtribuir(perfilDeQuemCria, perfil.Nome))
        {
            throw new DelegacaoNegadaException(perfil.Nome);
        }

        var usuario = new Usuario(
            unidadeId,
            request.Nome,
            email,

            // Sem senha informada, entra uma credencial ALEATÓRIA que ninguém conhece: o acesso
            // vem do convite por e-mail (RN-GER-033). Não é senha em branco nem senha padrão —
            // as duas seriam porta aberta enquanto o convite não fosse usado.
            hash.Gerar(request.SenhaInicial ?? tokens.Gerar().Token),
            perfil.Id,

            // RF-SEG-005 — criado POR outra pessoa. Vale nos dois casos: com senha informada,
            // duas pessoas a conhecem; com senha aleatória, ninguém a conhece e o dono precisa
            // definir a sua pelo link.
            deveTrocarSenha: true,
            sobrenome: request.Sobrenome,
            cpf: cpf);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(cancellationToken);

        // RN-GER-032 / RN-GER-033 — o convite de primeiro acesso. DEPOIS de gravar, e sem `await`
        // sobre o correio: o envio é enfileirado e não pode desfazer o usuário que já existe.
        await convites.EmitirAsync(usuario, TipoDeToken.Convite, cancellationToken);

        return new UsuarioDto(
            usuario.Id, usuario.UnidadeId, usuario.Nome, usuario.Sobrenome, cpf.Formatado(),
            usuario.Email,
            perfil.Nome, perfil.Id, usuario.Ativo, true);
    }
}

/// <summary>
/// <c>RN-GER-002</c> — desativa. <b>Não exclui.</b>
/// <para>
/// <c>RN-RH-006</c> — a partir daqui o usuário não registra operação no sistema: o login o recusa.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("usuario.gerenciar")]
public sealed record DesativarUsuarioCommand(Guid UsuarioId) : IRequest;

public sealed class DesativarUsuarioHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<DesativarUsuarioCommand>
{
    public async Task Handle(DesativarUsuarioCommand request, CancellationToken cancellationToken)
    {
        var unidadeId = contexto.UnidadeId;

        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(
                u => u.Id == request.UsuarioId && u.UnidadeId == unidadeId && !u.EhPlataforma,
                cancellationToken)
            ?? throw new NaoEncontradoException("Usuário não encontrado.");

        // Desativar a si mesmo tranca o administrador fora do próprio restaurante, e o suporte
        // teria de resolver. Recusar é mais barato para todos.
        if (usuario.Id == contexto.UsuarioId)
        {
            throw new RegraDeNegocioException("Você não pode desativar o seu próprio usuário.");
        }

        usuario.Inativar();
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// <c>RF-SEG-005</c> — o próprio usuário troca a sua senha.
/// <para>
/// <b>Única operação liberada</b> enquanto <c>DeveTrocarSenha</c> estiver verdadeiro
/// (<c>TrocaDeSenhaBehavior</c>).
/// </para>
/// </summary>
[SemModulo("troca de senha é do usuário, não de módulo contratado: sem ela ele não opera nenhum")]
[SemPermissao("o usuário troca a PRÓPRIA senha — exigir permissão trancaria fora quem precisa trocá-la")]
public sealed record TrocarSenhaCommand(string SenhaAtual, string NovaSenha) : IRequest;

public sealed class TrocarSenhaValidator : AbstractValidator<TrocarSenhaCommand>
{
    public TrocarSenhaValidator()
    {
        RuleFor(x => x.SenhaAtual).NotEmpty();

        RuleFor(x => x.NovaSenha)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A nova senha deve ter ao menos 8 caracteres.")
            .NotEqual(x => x.SenhaAtual).WithMessage("A nova senha deve ser diferente da atual.");
    }
}

public sealed class TrocarSenhaHandler(IAppDbContext db, IContextoDeExecucao contexto, IHashDeSenha hash)
    : IRequestHandler<TrocarSenhaCommand>
{
    public async Task Handle(TrocarSenhaCommand request, CancellationToken cancellationToken)
    {
        if (contexto.UsuarioId is not { } usuarioId)
        {
            throw new NaoAutorizadoException("Sessão inválida.");
        }

        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken)
            ?? throw new NaoAutorizadoException("Sessão inválida.");

        if (!hash.Confere(request.SenhaAtual, usuario.SenhaHash))
        {
            // ⚠ 422 e NÃO 401, e a diferença é a que fazia a tela parecer quebrada.
            //
            // `NaoAutorizadoException` mapeia para 401, e o interceptor do cliente trata 401 como
            // "sessão expirada": limpa o token e joga para /entrar. Resultado: quem errava a senha
            // atual era EXPULSO da tela de troca sem ler a mensagem — e voltava achando que a
            // troca havia falhado depois de ter dado certo. O sintoma relatado era esse.
            //
            // Aqui quem está pedindo ESTÁ autenticado; o que falhou foi o valor digitado, que é
            // regra de negócio. É a mesma correção que a SPEC-GER-007 §11.2 fez com o 401 que
            // devia ser 403 na plataforma: o código de status precisa dizer o que faltou.
            //
            // A mensagem segue enxuta de propósito — confirmar "a atual está errada" ajuda quem
            // roubou o token a descobrir a senha por tentativa, então ela não diz mais que isso.
            throw new RegraDeNegocioException("Senha atual inválida.", "RF-SEG-005");
        }

        usuario.TrocarSenha(hash.Gerar(request.NovaSenha));
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record PerfilDaUnidadeDto(
    Guid Id,
    string Nome,
    string Descricao,
    bool Delegavel,

    /// <summary>
    /// <c>RN-GER-010</c> — o que este perfil concede, em linguagem de gente.
    /// <para>
    /// <b>Vem do servidor porque a tela precisa EXPLICAR a escolha</b>, não só executá-la: quem
    /// atribui um perfil está concedendo acesso, e escolher 'Gerente' sem saber o que ele abre é
    /// assinar em branco. Uma cópia dessa lista no JavaScript divergiria do catálogo na primeira
    /// permissão nova — o mesmo argumento que trouxe `Delegavel` para cá (SPEC-GER-007 §11.1).
    /// </para>
    /// </summary>
    IReadOnlyList<PermissaoDoPerfilDto> Permissoes);

/// <summary>Uma permissão do perfil — o código para o gate, a descrição para a pessoa.</summary>
public sealed record PermissaoDoPerfilDto(string Codigo, string Descricao);

/// <summary>
/// <c>SPEC-GER-007</c> — os perfis da unidade, com a marca de quem <b>quem está pedindo</b> pode
/// atribuir (<c>RN-GER-024</c>).
/// <para>
/// Existe porque a tela precisa do <c>perfilId</c> para criar usuário, e derivá-lo dos usuários
/// já existentes não funciona: numa unidade recém-criada só há o administrador, e o gerente nunca
/// conseguiria criar o primeiro operador.
/// </para>
/// <para>
/// <c>Delegavel</c> vem do servidor, e não da tela, porque a matriz é do domínio. Uma cópia da
/// matriz no JavaScript divergiria da do C# na primeira mudança — e a divergência apareceria como
/// "o sistema me ofereceu e depois recusou".
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("usuario.visualizar")]
public sealed record ListarPerfisQuery(Guid? UnidadeId = null)
    : IRequest<IReadOnlyList<PerfilDaUnidadeDto>>;

/// <summary>
/// ADR-0019 — convertida ao modelo de leitura em 18/08/2026.
/// <para>
/// ⚠ <c>Delegavel</c> continua vindo de <see cref="MatrizDeDelegacao"/>, que é DOMÍNIO
/// (<c>RN-GER-024</c>). Traduzi-la para a visão colocaria a matriz de delegação dentro de um
/// <c>CASE</c> de SQL, longe do teste que a cobre — e a resposta "o sistema me ofereceu e depois
/// recusou" é exatamente o que a matriz existe para evitar.
/// </para>
/// </summary>
public sealed class ListarPerfisHandler(ILeituraDb leitura, IContextoDeExecucao contexto)
    : IRequestHandler<ListarPerfisQuery, IReadOnlyList<PerfilDaUnidadeDto>>
{
    public async Task<IReadOnlyList<PerfilDaUnidadeDto>> Handle(
        ListarPerfisQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // O filtro global PASSA TUDO para a plataforma (ADR-0013) — sem o recorte explícito, o
        // suporte veria os perfis de todos os clientes num só select.
        var recorte = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        var perfis = await leitura.Perfis
            .Where(p => recorte == null || p.UnidadeId == recorte)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);

        // RN-GER-010 — o que cada perfil concede. UMA consulta para todos os perfis, e não uma
        // subconsulta por linha: a projeção correlacionada anterior emitia um `SELECT` aninhado
        // por perfil da unidade.
        var permissoes = await leitura.PermissoesDePerfil
            .Where(p => recorte == null || p.UnidadeId == recorte)
            .OrderBy(p => p.Codigo)
            .ToListAsync(cancellationToken);

        var porPerfil = permissoes
            .GroupBy(p => p.PerfilId)
            .ToDictionary(g => g.Key, g => g.Select(
                x => new PermissaoDoPerfilDto(x.Codigo, x.Descricao)).ToList());

        var eu = contexto.UsuarioId;

        var perfilDeQuemPede = await leitura.Usuarios
            .Where(u => u.Id == eu)
            .Select(u => u.PerfilNome)
            .FirstOrDefaultAsync(cancellationToken);

        return [.. perfis.Select(p => new PerfilDaUnidadeDto(
            p.Id,
            p.Nome,

            // A coluna admite nulo no banco e o contrato da API não: perfil semeado sempre tem
            // descrição, e a coalescência cobre o perfil criado a mão sem uma.
            p.Descricao ?? string.Empty,
            MatrizDeDelegacao.PodeAtribuir(perfilDeQuemPede, p.Nome),
            porPerfil.TryGetValue(p.Id, out var lista) ? lista : []))];
    }
}

/// <summary>
/// <c>RN-GER-028</c> — altera o cadastro de um usuário da unidade.
/// <para>
/// A §8 de <c>SPEC-GER-007</c> tinha posto isto <b>fora de escopo</b> ("só criação e desativação
/// nesta spec"). O pedido de produto de 17/08/2026 o trouxe de volta, e a regra foi escrita antes.
/// </para>
/// <para>
/// <b>Perfil não entra aqui.</b> Trocar perfil passa por <c>RN-GER-024</c>, e permitir a troca por
/// dentro de um "editar cadastro" faria ela escapar da matriz de delegação.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("usuario.gerenciar")]
public sealed record AlterarUsuarioCommand(
    Guid UsuarioId,
    string Nome,
    string Sobrenome,
    string Cpf,
    string Email) : IRequest<UsuarioDto>;

public sealed class AlterarUsuarioValidator : AbstractValidator<AlterarUsuarioCommand>
{
    public AlterarUsuarioValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sobrenome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();

        RuleFor(x => x.Cpf)
            .NotEmpty()
            .Must(cpf => Domain.Common.Cpf.TentarDe(cpf, out _))
            .WithMessage("O CPF informado não é válido.");
    }
}

public sealed class AlterarUsuarioHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<AlterarUsuarioCommand, UsuarioDto>
{
    public async Task<UsuarioDto> Handle(
        AlterarUsuarioCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuario = await CarregarDaMinhaUnidadeAsync(db, contexto, request.UsuarioId, cancellationToken);

        var email = request.Email.Trim().ToLowerInvariant();
        var cpf = Cpf.De(request.Cpf);

        // RN-GER-023 — unicidade global, e agora com `Id !=`: sem isso, salvar o usuário sem
        // mudar o e-mail acusaria conflito com ele mesmo.
        var emailDeOutro = await db.Usuarios
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email && u.Id != usuario.Id, cancellationToken);

        if (emailDeOutro)
        {
            throw new RegraDeNegocioException("Já existe um usuário com este e-mail.");
        }

        var cpfDeOutro = await db.Usuarios
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Cpf == cpf && u.Id != usuario.Id, cancellationToken);

        if (cpfDeOutro)
        {
            throw new RegraDeNegocioException("Já existe um usuário com este CPF.");
        }

        usuario.AlterarCadastro(request.Nome, request.Sobrenome, email, cpf);

        await db.SaveChangesAsync(cancellationToken);

        return new UsuarioDto(
            usuario.Id, usuario.UnidadeId, usuario.Nome, usuario.Sobrenome, cpf.Formatado(),
            usuario.Email, usuario.Perfil?.Nome ?? "—", usuario.PerfilId ?? Guid.Empty,
            usuario.Ativo, usuario.DeveTrocarSenha);
    }

    /// <summary>
    /// Carrega o usuário <b>da unidade de quem está pedindo</b> — <c>RN-GER-009</c>.
    /// <para>
    /// `usuario` está fora do filtro global (a entidade não implementa <c>IPertenceAUnidade</c>),
    /// então o recorte é explícito. Sem ele, um administrador editaria o usuário de outro
    /// restaurante informando o `id` dele.
    /// </para>
    /// </summary>
    internal static async Task<Usuario> CarregarDaMinhaUnidadeAsync(
        IAppDbContext db, IContextoDeExecucao contexto, Guid usuarioId, CancellationToken ct)
    {
        var recorte = UnidadeDaConsulta.Recorte(contexto, null);

        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw new NaoEncontradoException("Usuário não encontrado.");

        // RN-GER-016 — o usuário de plataforma não é administrável por restaurante nenhum.
        if (usuario.EhPlataforma)
        {
            throw new NaoEncontradoException("Usuário não encontrado.");
        }

        if (recorte is { } unidade && usuario.UnidadeId != unidade)
        {
            // 404 e não 403: dizer "existe, mas não é seu" já conta que existe.
            throw new NaoEncontradoException("Usuário não encontrado.");
        }

        return usuario;
    }
}

/// <summary>
/// <c>RN-GER-028</c> — <b>outra pessoa</b> redefinindo a senha de um usuário.
/// <para>
/// A senha redefinida <b>reativa</b> a troca obrigatória (<c>RF-SEG-005</c>), e é a diferença
/// inteira em relação a <c>TrocarSenhaCommand</c>: a senha que um terceiro conhece não é senha.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("usuario.gerenciar")]
public sealed record RedefinirSenhaCommand(Guid UsuarioId, string NovaSenha) : IRequest;

public sealed class RedefinirSenhaValidator : AbstractValidator<RedefinirSenhaCommand>
{
    public RedefinirSenhaValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();

        RuleFor(x => x.NovaSenha)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A senha deve ter ao menos 8 caracteres.");
    }
}

public sealed class RedefinirSenhaHandler(
    IAppDbContext db, IContextoDeExecucao contexto, IHashDeSenha hash)
    : IRequestHandler<RedefinirSenhaCommand>
{
    public async Task Handle(RedefinirSenhaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuario = await AlterarUsuarioHandler.CarregarDaMinhaUnidadeAsync(
            db, contexto, request.UsuarioId, cancellationToken);

        usuario.RedefinirSenha(hash.Gerar(request.NovaSenha));

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary><c>RN-GER-028</c> — reativa o acesso de um usuário inativado.</summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("usuario.gerenciar")]
public sealed record ReativarUsuarioCommand(Guid UsuarioId) : IRequest;

public sealed class ReativarUsuarioHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ReativarUsuarioCommand>
{
    public async Task Handle(ReativarUsuarioCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuario = await AlterarUsuarioHandler.CarregarDaMinhaUnidadeAsync(
            db, contexto, request.UsuarioId, cancellationToken);

        usuario.Reativar();

        await db.SaveChangesAsync(cancellationToken);
    }
}
