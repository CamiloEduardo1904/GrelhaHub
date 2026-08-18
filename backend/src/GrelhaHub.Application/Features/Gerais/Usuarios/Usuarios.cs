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

public sealed class ListarUsuariosHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ListarUsuariosQuery, IReadOnlyList<UsuarioDto>>
{
    public async Task<IReadOnlyList<UsuarioDto>> Handle(
        ListarUsuariosQuery request, CancellationToken cancellationToken)
    {
        // `usuario` NÃO está sob o filtro global (a entidade não implementa IPertenceAUnidade,
        // porque o usuário de plataforma não tem unidade). O recorte aqui é explícito — e é
        // exatamente por isso que este handler carrega duas condições em vez de zero.
        //
        // RN-GER-014 / ADR-0013 — a plataforma informa QUAL casa quer ver, ou vê todas. Antes
        // este handler lançava "unidade não encontrada" para ela: o suporte simplesmente não
        // conseguia listar os usuários de um cliente, que é metade do trabalho de dar suporte.
        var recorte = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        var linhas = await db.Usuarios
            .AsNoTracking()
            .Include(u => u.Perfil)
            .Where(u => recorte == null || u.UnidadeId == recorte)

            // RN-GER-016 — invisível ao restaurante. Redundante com o recorte de unidade (ele não
            // tem unidade), e escrito de propósito: a redundância aqui custa nada e a ausência
            // custaria o vazamento no dia em que o recorte mudasse.
            .Where(u => !u.EhPlataforma)
            .OrderBy(u => u.Nome)
            .Select(u => new
            {
                u.Id, u.UnidadeId, u.Nome, u.Sobrenome, u.Cpf, u.Email,
                Perfil = u.Perfil!.Nome, PerfilId = u.PerfilId!.Value, u.Ativo, u.DeveTrocarSenha,
            })
            .ToListAsync(cancellationToken);

        // ⚠ Mapeado em MEMÓRIA, e não na projeção do SQL: o EF Core não traduz acesso a membro
        // de propriedade com conversor de valor, e `u.Cpf.Formatado()` não vira SQL
        // (SPEC-GER-001, a mesma armadilha de `p.Preco.Valor`). Filtro e ordenação continuam
        // no banco.
        return linhas
            .Select(l => new UsuarioDto(
                l.Id, l.UnidadeId, l.Nome, l.Sobrenome, l.Cpf?.Formatado(), l.Email,
                l.Perfil, l.PerfilId, l.Ativo, l.DeveTrocarSenha))
            .ToList();
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
    string SenhaInicial) : IRequest<UsuarioDto>;

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

        RuleFor(x => x.SenhaInicial)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A senha inicial deve ter ao menos 8 caracteres.");
    }
}

public sealed class CriarUsuarioHandler(
    IAppDbContext db,
    IContextoDeExecucao contexto,
    IHashDeSenha hash,
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
            throw new RegraDeNegocioException(
                "O usuário de plataforma cria usuário pela rota de plataforma, informando a " +
                "unidade. Esta rota cria usuário na unidade de quem está autenticado.");
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
            hash.Gerar(request.SenhaInicial),
            perfil.Id,

            // RF-SEG-005 — criado POR outra pessoa: a senha inicial é conhecida por quem a
            // escolheu, e por isso o dono da conta troca no primeiro acesso.
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
            // Mensagem genérica: quem já está autenticado sabe quem é, mas confirmar "a atual
            // está errada" ajuda quem roubou o token a descobrir a senha por tentativa.
            throw new NaoAutorizadoException("Senha atual inválida.");
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

public sealed class ListarPerfisHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<ListarPerfisQuery, IReadOnlyList<PerfilDaUnidadeDto>>
{
    public async Task<IReadOnlyList<PerfilDaUnidadeDto>> Handle(
        ListarPerfisQuery request, CancellationToken cancellationToken)
    {
        // `perfil` implementa IPertenceAUnidade, mas o filtro global PASSA TUDO para a
        // plataforma (ADR-0013) — sem o WHERE explícito, o suporte veria os perfis de todos os
        // clientes num só select. Mesmo motivo de `ListarPedidos`.
        var recorte = UnidadeDaConsulta.Recorte(contexto, request.UnidadeId);

        var perfis = await db.Perfis
            .AsNoTracking()
            .Where(p => recorte == null || p.UnidadeId == recorte)
            .OrderBy(p => p.Nome)
            .Select(p => new
            {
                p.Id,
                p.Nome,
                p.Descricao,

                // RN-GER-010 — o que o perfil concede. Projetado no banco: trazer o agregado
                // inteiro carregaria a tabela de junção de todos os perfis da unidade.
                // ⚠ `OrderBy` na ORIGEM, antes do `Select`.
                //
                // Ordenar por propriedade de um record já construído não atravessa a árvore de
                // expressão: o EF Core não sabe traduzir `x.Codigo` de um DTO que só existe em
                // memória, e a consulta falha em tempo de execução — 500 sem pista, porque o
                // erro não é de compilação. Mesma família da armadilha do `Guid?` em
                // SPEC-VEN-003 §11.7.
                Permissoes = p.Permissoes
                    .OrderBy(concedida => concedida.Permissao!.Codigo)
                    .Select(concedida => new PermissaoDoPerfilDto(
                        concedida.Permissao!.Codigo, concedida.Permissao!.Descricao))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var perfilDeQuemPede = await db.Usuarios
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.Id == contexto.UsuarioId)
            .Select(u => u.Perfil!.Nome)
            .FirstOrDefaultAsync(cancellationToken);

        return perfis
            .Select(p => new PerfilDaUnidadeDto(
                p.Id,
                p.Nome,
                p.Descricao,
                MatrizDeDelegacao.PodeAtribuir(perfilDeQuemPede, p.Nome),
                p.Permissoes))
            .ToList();
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
