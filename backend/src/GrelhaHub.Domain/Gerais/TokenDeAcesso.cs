namespace GrelhaHub.Domain.Gerais;

/// <summary>Para que serve o link — <c>RN-GER-033</c>.</summary>
public enum TipoDeToken
{
    /// <summary>Primeiro acesso de um usuário recém-criado. Validade `PAR-GER-003`.</summary>
    Convite = 1,

    /// <summary>"Esqueci minha senha". Validade `PAR-GER-002`, mais curta de propósito.</summary>
    RedefinicaoDeSenha = 2,
}

/// <summary>
/// Um link de acesso de uso único — <c>RN-GER-033</c> ⚠⚠, ADR-0016.
/// <para>
/// <b>O token não está aqui.</b> A tabela guarda o <see cref="Hash"/>, pela mesma razão de
/// <c>RNF-005</c> guardar hash de senha: quem lê o banco — dump, backup, tela de suporte — não pode
/// entrar na conta de ninguém. O valor em claro existe uma vez, na memória de quem o gerou, e vai
/// para o e-mail.
/// </para>
/// <para>
/// <b>Uso único e validade curta são a mesma preocupação vista de dois ângulos.</b> Token sem
/// validade é senha permanente que a pessoa não escolheu; token reutilizável é senha que fica
/// arquivada na caixa de e-mail dela para sempre.
/// </para>
/// <para>
/// <b>Sem <c>unidade_id</c>, e é exceção declarada a <c>RN-GER-009</c>:</b> o token pertence ao
/// usuário, e o usuário de plataforma (<c>RN-GER-014</c>) não tem unidade. Pendurar a unidade aqui
/// obrigaria a inventá-la justamente para quem mais precisa de recuperação de acesso — foi o beco
/// registrado na §8 da <c>SPEC-GER-007</c>.
/// </para>
/// </summary>
public sealed class TokenDeAcesso
{
    private TokenDeAcesso()
    {
    }

    public TokenDeAcesso(Guid usuarioId, TipoDeToken tipo, string hash, DateTimeOffset expiraEm)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("O token pertence a um usuário.", nameof(usuarioId));
        }

        UsuarioId = usuarioId;
        Tipo = tipo;
        Hash = string.IsNullOrWhiteSpace(hash)
            ? throw new ArgumentException("O hash é obrigatório.", nameof(hash))
            : hash;
        ExpiraEm = expiraEm;
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UsuarioId { get; private set; }
    public Usuario? Usuario { get; private set; }
    public TipoDeToken Tipo { get; private set; }

    /// <summary><c>[NaoAuditar]</c> pelo mesmo motivo do hash de senha: é credencial.</summary>
    [NaoAuditar]
    public string Hash { get; private set; } = null!;

    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>Instante do consumo. Nulo enquanto o link não foi usado.</summary>
    public DateTimeOffset? UsadoEm { get; private set; }

    /// <summary>
    /// Instante em que foi invalidado **sem ter sido usado** — porque outro token do mesmo tipo foi
    /// emitido para o mesmo usuário (<c>RN-GER-033</c>).
    /// <para>
    /// Coluna separada de <see cref="UsadoEm"/> de propósito: "o link que mandei não valeu porque
    /// pedi outro" e "o link foi usado" são histórias diferentes para quem atende o cliente, e
    /// juntá-las numa coluna só perderia justamente a distinção que a pergunta do suporte precisa.
    /// </para>
    /// </summary>
    public DateTimeOffset? InvalidadoEm { get; private set; }

    /// <summary>
    /// Serve para ser usado agora? Único lugar que responde isso — <c>RN-GER-033</c>.
    /// <para>
    /// O instante entra por parâmetro em vez de sair de <c>DateTimeOffset.UtcNow</c> aqui dentro:
    /// é o que permite testar expiração sem esperar uma hora, e mantém o relógio como dependência
    /// declarada (<c>IRelogio</c>), não escondida.
    /// </para>
    /// </summary>
    public bool Vigente(DateTimeOffset agora) =>
        UsadoEm is null && InvalidadoEm is null && ExpiraEm > agora;

    public void Consumir(DateTimeOffset agora) => UsadoEm = agora;

    public void Invalidar(DateTimeOffset agora)
    {
        // Token já usado não vira "invalidado": ele já contou a sua história, e sobrescrever
        // apagaria o fato de alguém ter entrado com ele.
        if (UsadoEm is null && InvalidadoEm is null)
        {
            InvalidadoEm = agora;
        }
    }
}
