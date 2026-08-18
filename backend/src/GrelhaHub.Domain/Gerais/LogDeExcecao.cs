namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// Uma falha não tratada, guardada para poder ser contada — ADR-0017.
/// <para>
/// <b>Antes disto, o vestígio de uma exceção em produção era a saída de log do contêiner</b> — que
/// o <c>docker compose logs</c> trunca e o reinício apaga. Não dava para responder "quantas vezes
/// isso aconteceu esta semana", que é a primeira pergunta de qualquer investigação.
/// </para>
/// <para>
/// ⚠ <b>Não tem <c>unidade_id</c> obrigatório, e é deliberado.</b> Boa parte das exceções acontece
/// ANTES de haver unidade no contexto — no login, na rota anônima do cardápio, na própria resolução
/// do slug. Exigir a unidade faria justamente as falhas mais externas não terem onde ser gravadas.
/// </para>
/// </summary>
public sealed class LogDeExcecao
{
    // O EF Core precisa dele; ninguém mais.
    private LogDeExcecao()
    {
    }

    public LogDeExcecao(
        string tipo,
        string mensagem,
        string? pilha,
        string? rota,
        string? metodo,
        int? statusHttp,
        Guid? unidadeId,
        Guid? usuarioId,
        string? correlacao,
        DateTimeOffset ocorridoEm)
    {
        Id = Guid.CreateVersion7();
        Tipo = tipo;
        Mensagem = mensagem;
        Pilha = pilha;
        Rota = rota;
        Metodo = metodo;
        StatusHttp = statusHttp;
        UnidadeId = unidadeId;
        UsuarioId = usuarioId;
        Correlacao = correlacao;
        OcorridoEm = ocorridoEm;
    }

    public Guid Id { get; private set; }

    /// <summary>O nome do tipo da exceção — é por ele que se agrupa "o que mais quebrou".</summary>
    public string Tipo { get; private set; } = null!;

    public string Mensagem { get; private set; } = null!;

    /// <summary>
    /// A pilha, quando houver.
    /// <para>
    /// ⚠ Anulável porque exceção relançada de outra camada pode chegar sem pilha útil, e gravar
    /// string vazia mentiria dizendo que a pilha existe e está em branco.
    /// </para>
    /// </summary>
    public string? Pilha { get; private set; }

    public string? Rota { get; private set; }

    public string? Metodo { get; private set; }

    public int? StatusHttp { get; private set; }

    /// <summary><b>Sem</b> filtro global: ver o resumo da classe.</summary>
    public Guid? UnidadeId { get; private set; }

    public Guid? UsuarioId { get; private set; }

    /// <summary>
    /// O identificador da requisição (<c>TraceIdentifier</c>).
    /// <para>
    /// É o que liga esta linha ao erro que o usuário viu na tela: o corpo do <c>ProblemDetails</c>
    /// carrega o mesmo valor, e é por ele que o suporte encontra a falha sem adivinhar horário.
    /// </para>
    /// </summary>
    public string? Correlacao { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }
}
