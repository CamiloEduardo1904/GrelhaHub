using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Domain.Vendas;

/// <summary>
/// Unidade central de venda — <c>RN-VEN-001</c>, <c>RN-VEN-018</c>.
/// </summary>
public sealed class Pedido
    : Entidade, IPertenceAUnidade, IPossuiDiaOperacional, IExcluivelLogicamente, IAuditavel, IAuditavelDetalhado
{
    /// <summary>
    /// <c>RN-VEN-052</c> ⚠ — o teto da observação que o <b>cliente</b> escreve.
    /// <para>
    /// O número existe para que a comanda mostre a observação <b>inteira</b>: com teto, o cartão
    /// comporta o texto e nunca precisa truncar — e reticências numa instrução de preparo escondem
    /// justamente a parte que ninguém sabe que está faltando.
    /// </para>
    /// <para>
    /// ⚠ <b>A coluna continua em 500.</b> Este é o limite do que se digita <i>agora</i>, imposto
    /// na tela e no validator; encurtar o <c>varchar</c> reclassificaria pedido já gravado, que é o
    /// oposto do que <c>RN-GER-036</c> decidiu sobre não mexer no passado.
    /// </para>
    /// </summary>
    public const int MaximoDeCaracteresNaObservacao = 80;

    /// <summary>
    /// <c>RN-VEN-018</c> ⚠ — as únicas transições admitidas.
    /// <para>
    /// A tabela existe para que a regra seja legível de uma vez, em vez de espalhada em
    /// <c>if</c>s. Cancelamento é tratado à parte porque parte de qualquer estado anterior a
    /// Finalizado.
    /// </para>
    /// </summary>
    private static readonly Dictionary<StatusPedido, StatusPedido> Proximo = new()
    {
        [StatusPedido.Aberto] = StatusPedido.Confirmado,
        [StatusPedido.Confirmado] = StatusPedido.EmProducao,
        [StatusPedido.EmProducao] = StatusPedido.Pronto,
        [StatusPedido.Pronto] = StatusPedido.Entregue,
        [StatusPedido.Entregue] = StatusPedido.Finalizado,
    };

    private readonly List<ItemPedido> _itens = [];

    private Pedido() { }

    public Pedido(
        Guid unidadeId,
        int numero,
        CanalVenda canal,
        Guid? clienteId,
        string? observacao,
        TimeSpan horaDeViradaOperacional,
        string fusoOperacional)
    {
        UnidadeId = unidadeId;
        Numero = numero;
        Canal = canal;
        ClienteId = clienteId;
        Observacao = observacao?.Trim();
        Status = StatusPedido.Aberto;
        CriadoEm = DateTimeOffset.UtcNow;

        // RN-GER-006 / RN-GER-036 ⚠⚠ — a virada e o fuso APLICADOS, copiados da unidade.
        //
        // É o mesmo movimento de RN-VEN-008 com o preço: o pedido guarda o que valia quando ele
        // nasceu. Sem a cópia, mudar o horário de funcionamento reclassificaria vendas passadas —
        // o faturamento de um sábado fechado mudaria de dia sozinho, e ninguém perceberia, porque
        // relatório que muda em silêncio não avisa.
        HoraDeViradaOperacional = horaDeViradaOperacional;
        FusoOperacional = fusoOperacional;
    }

    public Guid UnidadeId { get; private set; }

    /// <summary><c>RN-GER-036</c> — a virada que valia nesta unidade quando o pedido nasceu.</summary>
    public TimeSpan HoraDeViradaOperacional { get; private set; } = ViradaOperacional.Padrao;

    /// <summary><c>RN-GER-005</c> — o fuso que valia. Entra na mesma expressão da coluna gerada.</summary>
    public string FusoOperacional { get; private set; } = "America/Sao_Paulo";

    /// <summary>Sequencial por dia operacional — é o número que o cliente acompanha.</summary>
    public int Numero { get; private set; }

    public CanalVenda Canal { get; private set; }
    public StatusPedido Status { get; private set; }
    public Guid? ClienteId { get; private set; }

    /// <summary>
    /// <c>RN-VEN-052</c> — o que o cliente escreveu para a cozinha. Até
    /// <see cref="MaximoDeCaracteresNaObservacao"/> caracteres no que entra hoje; o histórico pode
    /// trazer textos mais longos, e eles continuam válidos.
    /// </summary>
    public string? Observacao { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>
    /// <c>RN-GER-001</c> — preenchido pelo interceptor. <b>Nulo no pedido do cardápio público</b>:
    /// quem o criou foi o cliente, que não é usuário do sistema (<c>RN-VEN-038</c>).
    /// </summary>
    public Guid? CriadoPorUsuarioId { get; private set; }

    public DateTimeOffset? ConfirmadoEm { get; private set; }
    public DateTimeOffset? FinalizadoEm { get; private set; }

    /// <summary>
    /// <c>RN-VEN-041</c> — instante da exclusão lógica. Nulo enquanto o pedido vale.
    /// <para>
    /// <b>Não confundir com cancelamento.</b> Cancelar é fato de negócio: o cliente desistiu, e
    /// isso é métrica. Excluir é reconhecer que o pedido <b>nunca deveria ter existido</b> —
    /// lançamento em duplicidade, teste, erro de operação — e por isso ele não pode poluir a
    /// taxa de cancelamento.
    /// </para>
    /// <para>
    /// <c>RN-GER-002</c> — o registro <b>permanece</b>. Excluir marca, guarda quem, quando e por
    /// quê, e retira o pedido de consulta e apuração. Nada é apagado.
    /// </para>
    /// </summary>
    public DateTimeOffset? ExcluidoEm { get; private set; }

    public Guid? ExcluidoPorUsuarioId { get; private set; }

    public string? MotivoExclusao { get; private set; }

    public bool Excluido => ExcluidoEm is not null;

    /// <summary>
    /// <c>RN-GER-006</c> ⚠⚠ — coluna <b>gerada pelo banco</b>. A aplicação lê, nunca escreve.
    /// Nenhuma apuração diária usa <see cref="CriadoEm"/>.
    /// </summary>
    public DateOnly DataOperacional { get; private set; }

    public IReadOnlyCollection<ItemPedido> Itens => _itens;

    /// <summary><c>RN-VEN-039</c> — total é a soma dos itens. Sem taxas nesta fatia.</summary>
    public Dinheiro Total => Dinheiro.Somar(_itens.Select(i => i.Total));

    /// <summary>
    /// <c>RN-VEN-040</c> — a composição do pedido é alterável apenas em Aberto e Confirmado.
    /// <para>
    /// A fronteira é a <b>produção</b>, não a confirmação: entre confirmar e produzir ainda
    /// pode haver fila, e nesse intervalo o restaurante consegue acomodar um "esqueci a
    /// batata". Depois de a cozinha começar, o insumo já foi baixado e o item já está sendo
    /// preparado.
    /// </para>
    /// </summary>
    public bool AceitaAlteracaoDeItens =>
        Status is StatusPedido.Aberto or StatusPedido.Confirmado;

    /// <summary>
    /// <c>RN-VEN-003</c>, <c>RN-VEN-008</c>, <c>RN-VEN-011</c>, <c>RN-VEN-040</c>.
    /// O preço praticado é <b>copiado</b> aqui e não muda se o preço de tabela mudar depois.
    /// </summary>
    public ItemPedido AdicionarItem(Guid produtoId, string nomeProduto, Dinheiro precoPraticado, int quantidade, string? observacao = null)
    {
        if (!AceitaAlteracaoDeItens)
        {
            throw new InvalidOperationException(
                $"RN-VEN-040 — pedido em {Status} não aceita alteração de itens.");
        }

        var item = new ItemPedido(UnidadeId, Id, produtoId, nomeProduto, precoPraticado, quantidade, observacao);
        _itens.Add(item);
        return item;
    }

    /// <summary>
    /// <c>RN-VEN-002</c> — pedido sem itens não pode ser confirmado.
    /// A confirmação é o gatilho de baixa de estoque e ordem de produção
    /// (<c>RN-VEN-018</c>, <c>RN-PRO-008</c>), que ainda não existem nesta fatia.
    /// </summary>
    public void Confirmar()
    {
        if (_itens.Count == 0)
        {
            throw new InvalidOperationException(
                "RN-VEN-002 — um pedido sem itens não pode ser confirmado.");
        }

        Avancar(StatusPedido.Confirmado);
        ConfirmadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Avança um estado no ciclo — <c>RN-VEN-018</c>.</summary>
    public void AvancarStatus()
    {
        if (!Proximo.TryGetValue(Status, out var destino))
        {
            throw new InvalidOperationException(
                $"RN-VEN-019 — pedido em {Status} não avança para nenhum outro estado.");
        }

        if (destino == StatusPedido.Confirmado)
        {
            Confirmar();
            return;
        }

        Avancar(destino);
    }

    /// <summary>
    /// <c>RN-GER-002</c> — não se exclui um registro transacional: o desfazimento é
    /// registrado como cancelamento, <b>com motivo e responsável</b>.
    /// <para>
    /// <b>Não devolve estoque nesta fatia porque ainda não há estoque.</b> Quando houver:
    /// cancelado <i>após</i> entrar em produção gera <b>perda</b>, não devolução
    /// (<c>RN-EST-018</c>); cancelado <i>antes</i> não gera movimento (<c>RN-EST-019</c>).
    /// </para>
    /// </summary>
    public void Cancelar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException(
                "RN-GER-002 — o cancelamento exige motivo.", nameof(motivo));
        }

        if (Status is StatusPedido.Finalizado or StatusPedido.Cancelado)
        {
            throw new InvalidOperationException(
                $"RN-VEN-019 — pedido {Status} não retorna a nenhum estado anterior.");
        }

        Status = StatusPedido.Cancelado;
        MotivoCancelamento = motivo.Trim();
        FinalizadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// <c>RN-VEN-041</c> — exclusão lógica pelo administrador da unidade.
    /// <para>
    /// Permitida em <b>qualquer</b> estado, inclusive Finalizado: o erro que se está corrigindo
    /// pode ter sido descoberto depois do fechamento. O que a exclusão nunca faz é mudar o
    /// estado — o pedido excluído guarda o estado que tinha, porque a trilha precisa dizer o que
    /// foi excluído, não apenas que algo foi.
    /// </para>
    /// </summary>
    public void ExcluirLogicamente(Guid usuarioId, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException(
                "RN-VEN-041 — a exclusão exige motivo.", nameof(motivo));
        }

        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "RN-VEN-041 — a exclusão registra quem a fez.", nameof(usuarioId));
        }

        if (Excluido)
        {
            throw new InvalidOperationException(
                "RN-VEN-041 — este pedido já está excluído.");
        }

        ExcluidoEm = DateTimeOffset.UtcNow;
        ExcluidoPorUsuarioId = usuarioId;
        MotivoExclusao = motivo.Trim();
    }

    /// <summary>
    /// <c>RN-GER-011</c> — a explicação que o próprio pedido guarda. A exclusão tem precedência
    /// sobre o cancelamento: se ambos existirem, o motivo relevante é o da última operação.
    /// </summary>
    public string? JustificativaDaAuditoria => MotivoExclusao ?? MotivoCancelamento;

    /// <summary>
    /// <c>RN-GER-011</c> — distingue cancelar de excluir de simplesmente alterar.
    /// <para>
    /// Quem classifica é o pedido, e não o interceptor: um interceptor que soubesse ler
    /// <c>Status == Cancelado</c> passaria a conhecer o ciclo de vida do pedido, e a próxima
    /// entidade auditável exigiria mais um <c>if</c> lá dentro.
    /// </para>
    /// </summary>
    public OperacaoDeAuditoria ClassificarAuditoria(
        TipoDeMudanca mudanca, IReadOnlySet<string> propriedadesAlteradas)
    {
        ArgumentNullException.ThrowIfNull(propriedadesAlteradas);

        if (mudanca == TipoDeMudanca.Insercao)
        {
            return OperacaoDeAuditoria.Criacao;
        }

        // RN-VEN-041 — exclusão lógica primeiro: ela pode acontecer sobre um pedido já cancelado,
        // e é a operação que a trilha precisa mostrar.
        if (propriedadesAlteradas.Contains(nameof(ExcluidoEm)))
        {
            return OperacaoDeAuditoria.Exclusao;
        }

        return Status == StatusPedido.Cancelado && propriedadesAlteradas.Contains(nameof(Status))
            ? OperacaoDeAuditoria.Cancelamento
            : OperacaoDeAuditoria.Alteracao;
    }

    private void Avancar(StatusPedido destino)
    {
        if (Status is StatusPedido.Finalizado or StatusPedido.Cancelado)
        {
            throw new InvalidOperationException(
                $"RN-VEN-019 — pedido {Status} não retorna a nenhum estado anterior.");
        }

        if (!Proximo.TryGetValue(Status, out var esperado) || esperado != destino)
        {
            throw new InvalidOperationException(
                $"RN-VEN-018 — não é possível ir de {Status} para {destino}.");
        }

        Status = destino;

        if (destino == StatusPedido.Finalizado)
        {
            FinalizadoEm = DateTimeOffset.UtcNow;
        }
    }
}
