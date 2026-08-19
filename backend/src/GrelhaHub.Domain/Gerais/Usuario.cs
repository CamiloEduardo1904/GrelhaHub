using System.Diagnostics.CodeAnalysis;
using GrelhaHub.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Nomes dos perfis semeados em toda unidade — <c>SPEC-GER-004</c>, <c>RN-GER-022</c>.
/// <para>
/// <b>É rótulo, não autorização.</b> A simplificação de SPEC-VEN-001 §8 — perfil como
/// <c>enum</c> verificado no código — foi substituída por <c>perfil</c> e permissão em claim
/// de papel (ADR-0022). Este enum sobrevive apenas para nomear o que a carga inicial cria e
/// evitar literal solto em seed e em teste.
/// </para>
/// <para>
/// <b>Nunca decida autorização a partir dele.</b> A pergunta é sobre permissão
/// (`pedido.cancelar`), nunca sobre nome de perfil — ver <see cref="CatalogoDeAcesso"/>.
/// </para>
/// </summary>
public enum PerfilUsuario
{
    Administrador = 1,
    Gerente = 2,
    Atendente = 3,
    Cozinha = 4,
    Operador = 5,
    Entregador = 6,
}

/// <summary>
/// Quem acessa o sistema — <b>uma pessoa, uma linha</b> (ADR-0022 §1).
/// <para>
/// Herda de <c>IdentityUser&lt;Guid&gt;</c>: e-mail, hash de senha, contadores de lockout
/// (<c>RN-GER-040</c>) e o <c>SecurityStamp</c> (<c>RN-GER-041</c>) passam a vir do Identity;
/// CPF, unidade, perfil e as regras continuam aqui. É a exceção declarada à regra "Domínio sem
/// dependências": o pacote referenciado traz só POCOs, nunca EF Core nem runtime.
/// </para>
/// <para>
/// <b>Não implementa <c>IPertenceAUnidade</c>, e isso é deliberado</b> (<c>RN-GER-014</c>,
/// ADR-0013): o usuário de plataforma não tem unidade, e a interface exige <c>Guid</c> não
/// nulo. Consequência assumida: <c>usuario</c> fica <b>fora</b> do filtro global, e o recorte
/// de qualquer listagem de usuário é responsabilidade explícita da consulta — com
/// <c>RN-GER-016</c> exigindo, além disso, esconder o usuário de plataforma do restaurante.
/// </para>
/// </summary>
public sealed class Usuario : IdentityUser<Guid>, IAuditavel, IAuditavelDetalhado
{
    private Usuario() { }

    public Usuario(
        Guid unidadeId,
        string nome,
        string email,
        string senhaHash,
        Guid perfilId,
        bool deveTrocarSenha = true,
        string? sobrenome = null,
        Cpf? cpf = null)
        : this(nome, email, senhaHash)
    {
        if (unidadeId == Guid.Empty)
        {
            throw new ArgumentException(
                "RN-GER-009 — usuário de restaurante pertence a uma unidade. Para o usuário de " +
                "plataforma use CriarDePlataforma.", nameof(unidadeId));
        }

        if (perfilId == Guid.Empty)
        {
            throw new ArgumentException(
                "RN-GER-010 — todo usuário pertence a um perfil.", nameof(perfilId));
        }

        UnidadeId = unidadeId;
        PerfilId = perfilId;
        DeveTrocarSenha = deveTrocarSenha;

        // RN-GER-023 — opcionais no construtor, obrigatórios no validator. O construtor precisa
        // aceitar a ausência porque os usuários criados antes desta regra existem no banco e são
        // materializados por aqui; exigir no domínio quebraria a leitura do que já está gravado.
        Sobrenome = string.IsNullOrWhiteSpace(sobrenome) ? null : sobrenome.Trim();
        Cpf = cpf;
    }

    private Usuario(string nome, string email, string senhaHash)
    {
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome é obrigatório.", nameof(nome))
            : nome.Trim();

        Email = string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("O e-mail é obrigatório.", nameof(email))
            : email.Trim().ToLowerInvariant();

        PasswordHash = senhaHash;

        // O Guid do próprio projeto (ADR-0004, uuid v7 ordenável), e não o que o Identity
        // geraria: a chave continua nascendo na aplicação — ver AplicarChaveGeradaPelaAplicacao.
        Id = Guid.CreateVersion7();

        // O Identity exige um "nome de usuário"; aqui a credencial de entrada É o e-mail
        // (SPEC-VEN-001), então os dois andam juntos. Os normalizados existem porque o
        // UserManager procura por eles — e como a criação não passa por UserManager.CreateAsync,
        // ninguém os preencheria por nós.
        UserName = Email;
        NormalizedUserName = Email.ToUpperInvariant();
        NormalizedEmail = NormalizedUserName;

        // RN-GER-041 — o carimbo nasce com o usuário e VIAJA NO JWT. Trocar a senha o regenera,
        // e é essa troca que derruba as sessões anteriores.
        SecurityStamp = Guid.NewGuid().ToString("N");

        // RN-GER-040 — o lockout vale para TODO usuário, inclusive o de plataforma. Sem esta
        // marca o SignInManager nem conta as falhas.
        LockoutEnabled = true;

        CriadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// <c>RN-GER-009</c> — a unidade do usuário. <b>Nulo apenas para o usuário de plataforma</b>
    /// (<c>RN-GER-014</c>): ele não pertence a restaurante nenhum e enxerga todos.
    /// </summary>
    public Guid? UnidadeId { get; private set; }

    /// <summary>
    /// <c>RN-GER-014</c> ⚠⚠ — usuário de suporte do GrelhaHub, não do restaurante.
    /// <para>
    /// Para ele o recorte por unidade não se aplica: o filtro global do EF Core é desligado
    /// (ADR-0013, que revisa a invariante do ADR-0005). É a marca mais perigosa do modelo, e
    /// existe um único caminho para criá-la — <see cref="CriarDePlataforma"/>.
    /// </para>
    /// </summary>
    public bool EhPlataforma { get; private set; }

    public string Nome { get; private set; } = null!;

    /// <summary>
    /// <c>RN-GER-023</c> — o sobrenome. <b>Nulável por duas razões declaradas:</b> o usuário de
    /// plataforma (<c>RN-GER-014</c>) tem um nome só, e os usuários criados antes desta regra
    /// não têm sobrenome gravado. Exigir aqui obrigaria a inventar dado para eles.
    /// </summary>
    public string? Sobrenome { get; private set; }

    /// <summary>
    /// <c>RN-GER-023</c> — o CPF, <b>único em todo o sistema</b>.
    /// <para>
    /// Unicidade global e não por unidade: <b>exceção declarada a <c>RN-GER-009</c></b>, pela
    /// mesma razão do e-mail em <c>SPEC-GER-004</c> — a pessoa é a mesma pessoa em qualquer
    /// restaurante, e dois cadastros do mesmo CPF são a mesma pessoa duas vezes, não duas
    /// pessoas.
    /// </para>
    /// <para>
    /// Nulo no usuário de plataforma: ele não é pessoa física de restaurante nenhum.
    /// </para>
    /// </summary>
    public Cpf? Cpf { get; private set; }

    /// <summary>Nome de exibição. Sem sobrenome, devolve só o nome — não "Nome null".</summary>
    public string NomeCompleto =>
        string.IsNullOrWhiteSpace(Sobrenome) ? Nome : $"{Nome} {Sobrenome}";

    /// <summary>
    /// O e-mail — coluna do Identity, contrato do domínio.
    /// <para>
    /// ⚠ A base declara <c>string?</c>; aqui todo usuário nasce com e-mail (o construtor exige),
    /// então o override devolve não-nulo e poupa cada chamador de um <c>!</c> que nada
    /// significaria. <c>[AllowNull]</c> silencia a variância do setter, que o Identity ainda
    /// chama com o tipo da base.
    /// </para>
    /// </summary>
    [AllowNull]
    public override string Email
    {
        get => base.Email ?? string.Empty;
        set => base.Email = value;
    }

    /// <summary>
    /// <c>RNF-005</c> — hash com salt, agora na coluna do Identity (<c>senha_hash</c> no banco).
    /// Nunca a senha em texto claro.
    /// <para>
    /// <c>[NaoAuditar]</c> — <c>SPEC-GER-005</c> CA-6: o hash não entra na trilha nem como valor
    /// anterior. A trilha é lida pela gerência, e um hash antigo nela é um hash a mais em
    /// circulação.
    /// </para>
    /// </summary>
    [NaoAuditar]
    [AllowNull]
    public override string PasswordHash
    {
        get => base.PasswordHash ?? string.Empty;
        set => base.PasswordHash = value;
    }

    /// <summary>
    /// <c>RN-GER-041</c> — o carimbo de segurança. Vai no JWT e é conferido a cada requisição:
    /// carimbo divergente é sessão emitida antes da última troca de senha, e morre ali.
    /// <para>
    /// <c>[NaoAuditar]</c> — não é segredo como o hash, mas é credencial de sessão: um carimbo
    /// antigo legível na trilha não serve a auditoria nenhuma e ainda documenta o que valia.
    /// </para>
    /// </summary>
    [NaoAuditar]
    public override string? SecurityStamp
    {
        get => base.SecurityStamp;
        set => base.SecurityStamp = value;
    }

    /// <summary>Controle de concorrência do Identity — ruído para a trilha, nunca negócio.</summary>
    [NaoAuditar]
    public override string? ConcurrencyStamp
    {
        get => base.ConcurrencyStamp;
        set => base.ConcurrencyStamp = value;
    }

    /// <summary>
    /// <c>RN-GER-010</c> — o perfil determina as operações permitidas.
    /// <b>Nulo apenas para o usuário de plataforma</b>, que não pertence a unidade e portanto
    /// não pode pertencer a um perfil dela (<c>RN-GER-014</c>).
    /// <para>
    /// ⚠ Continua sendo <b>a</b> fonte do vínculo usuário → perfil. A tabela
    /// <c>usuario_perfil</c> (o N:N que o Identity traz) existe no esquema e fica <b>vazia</b>:
    /// <c>RN-GER-010</c> fixa UM perfil por usuário, e duas fontes do mesmo vínculo divergem em
    /// silêncio.
    /// </para>
    /// </summary>
    public Guid? PerfilId { get; private set; }

    public Perfil? Perfil { get; private set; }

    /// <summary>
    /// <c>RF-SEG-005</c> — troca obrigatória no primeiro acesso. Bloqueia todas as rotas exceto
    /// a de troca de senha: senha inicial conhecida por quem criou o usuário não é senha.
    /// </summary>
    public bool DeveTrocarSenha { get; private set; } = true;

    public bool Ativo { get; private set; } = true;

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary><c>RN-GER-001</c> — quem criou este usuário. Nulo no administrador fundador,
    /// criado junto com a própria unidade.</summary>
    public Guid? CriadoPorUsuarioId { get; private set; }

    /// <summary>
    /// Desativar é <b>inativação</b> (<c>RN-GER-002</c>) — na trilha aparece como exclusão lógica,
    /// que é o efeito percebido: o usuário não entra mais.
    /// </summary>
    public OperacaoDeAuditoria ClassificarAuditoria(
        TipoDeMudanca mudanca, IReadOnlySet<string> propriedadesAlteradas)
    {
        ArgumentNullException.ThrowIfNull(propriedadesAlteradas);

        if (mudanca == TipoDeMudanca.Insercao)
        {
            return OperacaoDeAuditoria.Criacao;
        }

        return propriedadesAlteradas.Contains(nameof(Ativo)) && !Ativo
            ? OperacaoDeAuditoria.Exclusao
            : OperacaoDeAuditoria.Alteracao;
    }

    public Unidade? Unidade { get; private set; }

    /// <summary>
    /// <c>RN-GER-014</c>, <c>RN-GER-016</c> — cria o usuário de suporte da plataforma.
    /// <para>
    /// Método próprio, e não um parâmetro do construtor normal, para que a criação de um usuário
    /// que enxerga todos os clientes seja <b>impossível por descuido</b>: quem chama isto está
    /// dizendo o que está fazendo, e um <c>grep</c> por <c>CriarDePlataforma</c> lista todos os
    /// pontos onde a marca nasce.
    /// </para>
    /// <para>
    /// <paramref name="deveTrocarSenha"/> separa os <b>dois</b> caminhos de <c>RN-GER-016</c>, e a
    /// diferença não é de gosto:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Carga inicial</b> (<c>false</c>) — a senha vem do <c>.env</c> do servidor. Não há
    /// "outra pessoa" a quem ela foi entregue: quem tem acesso ao arquivo é o dono da conta.
    /// Exigir troca aqui pediria a troca a ninguém.</item>
    /// <item><b>Criado por outro usuário de plataforma</b> (<c>true</c>) — <c>RF-SEG-005</c>: são
    /// duas pessoas, e enquanto a senha inicial valer o dono da conta não é o único que entra.</item>
    /// </list>
    /// </summary>
    public static Usuario CriarDePlataforma(
        string nome, string email, string senhaHash, bool deveTrocarSenha = false) =>
        new(nome, email, senhaHash) { EhPlataforma = true, DeveTrocarSenha = deveTrocarSenha };

    /// <summary><c>RN-GER-002</c> — não se exclui; inativa-se.</summary>
    public void Inativar() => Ativo = false;

    /// <summary><c>RN-GER-028</c> — reativa o acesso de quem foi inativado.</summary>
    public void Reativar() => Ativo = true;

    /// <summary>
    /// <c>RN-GER-028</c> — altera o cadastro. <b>Nome, sobrenome, CPF e e-mail.</b>
    /// <para>
    /// Perfil não entra aqui: ele passa por <see cref="DefinirPerfil"/> e pela matriz de delegação
    /// (<c>RN-GER-024</c>), que é checagem de aplicação. Misturar os dois faria a troca de perfil
    /// escapar da matriz por dentro de um "editar cadastro".
    /// </para>
    /// </summary>
    public void AlterarCadastro(string nome, string sobrenome, string email, Cpf cpf)
    {
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome é obrigatório.", nameof(nome))
            : nome.Trim();

        Sobrenome = string.IsNullOrWhiteSpace(sobrenome)
            ? throw new ArgumentException("O sobrenome é obrigatório.", nameof(sobrenome))
            : sobrenome.Trim();

        Email = string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("O e-mail é obrigatório.", nameof(email))
            : email.Trim().ToLowerInvariant();

        // O Identity procura pelo normalizado; e-mail que muda sem o normalizado acompanhar é
        // usuário que some do FindByEmail sem sair do banco.
        UserName = Email;
        NormalizedUserName = Email.ToUpperInvariant();
        NormalizedEmail = NormalizedUserName;

        Cpf = cpf;
    }

    /// <summary>
    /// A própria pessoa trocando a sua senha. <b>Encerra</b> a pendência de <c>RF-SEG-005</c>.
    /// <para>
    /// <c>RN-GER-041</c> ⚠⚠ — regenera o <c>SecurityStamp</c>, e é isso que derruba as sessões
    /// anteriores: o carimbo antigo continua nos tokens já emitidos, e a conferência por
    /// requisição os recusa. O carimbo gira <b>aqui, no domínio</b>, e não num
    /// <c>UserManager</c> que o handler poderia esquecer de chamar — mesmo argumento do
    /// interceptor de auditoria.
    /// </para>
    /// </summary>
    public void TrocarSenha(string novoHash)
    {
        PasswordHash = novoHash;
        DeveTrocarSenha = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// <c>RN-GER-028</c> — <b>outra pessoa</b> redefinindo a senha deste usuário.
    /// <para>
    /// <b>Reativa</b> a troca obrigatória (<c>RF-SEG-005</c>), e é a diferença inteira em relação a
    /// <see cref="TrocarSenha"/>: a senha que um terceiro conhece não é senha. O mesmo argumento
    /// que faz o usuário criado por alguém nascer com a pendência vale quando alguém a redefine.
    /// </para>
    /// <para>
    /// <c>RN-GER-041</c> — o carimbo gira aqui também: quem estava dentro com a senha antiga —
    /// inclusive quem motivou a redefinição — sai na requisição seguinte.
    /// </para>
    /// </summary>
    public void RedefinirSenha(string novoHash)
    {
        PasswordHash = novoHash;
        DeveTrocarSenha = true;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    /// <summary><c>RN-GER-010</c> — muda o perfil, e com ele as permissões.</summary>
    public void DefinirPerfil(Guid perfilId)
    {
        if (EhPlataforma)
        {
            throw new InvalidOperationException(
                "RN-GER-014 — usuário de plataforma não pertence a perfil de unidade.");
        }

        PerfilId = perfilId == Guid.Empty
            ? throw new ArgumentException("RN-GER-010 — o perfil é obrigatório.", nameof(perfilId))
            : perfilId;
    }

    // RN-GER-010 — NÃO existe aqui um `PodeVerCusto` derivado do nome do perfil.
    //
    // A versão anterior tinha `Perfil is Administrador or Gerente`, e era exatamente o que a
    // regra proíbe: criar o perfil Operador (RN-GER-022) obrigaria a caçar essa condicional e
    // todas as suas irmãs. A pergunta certa é "tem a permissão `custo.visualizar`?", e quem
    // responde é o contexto de execução, a partir das permissões do token.
}
