using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// A unidade de negócio — o restaurante. Raiz de isolamento dos dados: todo registro
/// transacional pertence a exatamente uma unidade (<c>RN-GER-009</c>, ADR-0005).
/// </summary>
public sealed class Unidade : Entidade
{
    private Unidade() { }

    public Unidade(string razaoSocial, string nomeFantasia, string cnpj, string slug, string? telefone)
    {
        RazaoSocial = Exigir(razaoSocial, nameof(razaoSocial));
        NomeFantasia = Exigir(nomeFantasia, nameof(nomeFantasia));
        Cnpj = SomenteDigitos(cnpj);
        Slug = ValidarSlug(slug);
        Telefone = telefone is null ? null : SomenteDigitos(telefone);
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public string RazaoSocial { get; private set; } = null!;
    public string NomeFantasia { get; private set; } = null!;
    public string Cnpj { get; private set; } = null!;

    /// <summary>
    /// Identificador público do cardápio online — <c>RN-VEN-036</c>.
    /// <para>
    /// <b>Imutável após a criação:</b> o link circula em rede social e material impresso, e
    /// alterá-lo quebraria o que já foi distribuído. Não existe setter, nem público nem
    /// interno — mudar o slug exige decisão de produto e uma operação explícita.
    /// </para>
    /// </summary>
    public string Slug { get; private set; } = null!;

    public string? Telefone { get; private set; }
    /// <summary>
    /// <c>RN-GER-029</c> — a unidade está operando.
    /// <para>
    /// ⚠ <b>Esta propriedade existiu com <c>private set</c> e sem nenhum método por três specs.</b>
    /// <c>Autenticar</c> já a consultava (<c>RN-GER-027</c>), então o bloqueio de acesso estava
    /// pronto e era <b>inalcançável</b>: não havia como inativar. Comportamento correto e sem
    /// caminho é indistinguível de comportamento ausente.
    /// </para>
    /// </summary>
    public bool Ativa { get; private set; } = true;

    /// <summary>
    /// <c>RN-GER-029</c> — para a operação da unidade. <b>Não apaga nada</b>
    /// (<c>RN-GER-002</c>): o cardápio público para de responder, os usuários param de autenticar
    /// (<c>RN-GER-027</c>) e todo o histórico permanece.
    /// </summary>
    public void Inativar() => Ativa = false;

    /// <summary>
    /// <c>RN-GER-029</c> — devolve o acesso. Não restaura nada, porque nada foi removido.
    /// </summary>
    public void Reativar() => Ativa = true;
    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary><c>RN-GER-005</c> — a operação ocorre neste fuso.</summary>
    public string FusoHorario { get; private set; } = "America/Sao_Paulo";

    /// <summary>
    /// <c>RN-GER-037</c> — o endereço da casa, exibido no cardápio público.
    /// <para>
    /// <b>Todo o endereço é opcional, e a nulidade é decisão:</b> as unidades que existiam antes
    /// desta regra não têm endereço, e exigi-lo obrigaria a inventar um para elas ou a impedir que
    /// operassem. Quem não cadastrou simplesmente não exibe.
    /// </para>
    /// <para>
    /// <b>Guardado em campos separados, não numa linha só de texto.</b> Uma string "Rua X, 123 —
    /// Bairro" não se compara, não se agrupa por bairro e não se corrige: a primeira pergunta de
    /// logística (<c>RN-LOG-*</c>) que pedir bairro obrigaria a desmontar texto livre.
    /// </para>
    /// </summary>
    public string? Cep { get; private set; }

    public string? Logradouro { get; private set; }
    public string? NumeroDoEndereco { get; private set; }
    public string? Complemento { get; private set; }
    public string? Bairro { get; private set; }
    public string? Cidade { get; private set; }

    /// <summary>A sigla, dois caracteres — `SP`, não `São Paulo`.</summary>
    public string? Uf { get; private set; }

    /// <summary>
    /// <c>RN-GER-037</c> — grava o endereço. O CEP entra <b>só com dígitos</b> (<c>RN-GER-013</c>),
    /// pela mesma razão do CNPJ e do telefone: máscara é apresentação.
    /// </summary>
    public void DefinirEndereco(
        string? cep,
        string? logradouro,
        string? numero,
        string? complemento,
        string? bairro,
        string? cidade,
        string? uf)
    {
        Cep = ApenasDigitos(cep);
        Logradouro = Limpo(logradouro);
        NumeroDoEndereco = Limpo(numero);
        Complemento = Limpo(complemento);
        Bairro = Limpo(bairro);
        Cidade = Limpo(cidade);
        Uf = Limpo(uf)?.ToUpperInvariant();
    }

    /// <summary>O endereço em uma linha, para exibição. Nulo quando não há o suficiente.</summary>
    public string? EnderecoResumido()
    {
        if (string.IsNullOrWhiteSpace(Logradouro))
        {
            return null;
        }

        var partes = new List<string> { Logradouro };

        if (!string.IsNullOrWhiteSpace(NumeroDoEndereco))
        {
            partes[0] = $"{Logradouro}, {NumeroDoEndereco}";
        }

        if (!string.IsNullOrWhiteSpace(Bairro))
        {
            partes.Add(Bairro);
        }

        if (!string.IsNullOrWhiteSpace(Cidade))
        {
            partes.Add(string.IsNullOrWhiteSpace(Uf) ? Cidade : $"{Cidade}/{Uf}");
        }
        else if (!string.IsNullOrWhiteSpace(Uf))
        {
            partes.Add(Uf);
        }

        return string.Join(" · ", partes);
    }

    private static string? Limpo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? ApenasDigitos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var digitos = new string([.. valor.Where(char.IsAsciiDigit)]);

        return digitos.Length == 0 ? null : digitos;
    }

    private readonly List<HorarioDeFuncionamento> _horarios = [];

    /// <summary><c>RN-GER-036</c> — o horário de funcionamento, por dia da semana.</summary>
    public IReadOnlyCollection<HorarioDeFuncionamento> Horarios => _horarios;

    /// <summary>
    /// <c>RN-GER-006</c> ⚠⚠ / <c>RN-GER-036</c> — a virada do dia operacional <b>desta casa</b>.
    /// <para>
    /// <b>Coluna derivada, e não calculada na leitura:</b> ela entra na expressão da coluna gerada
    /// de <c>pedido</c>, que precisa do valor no instante da inserção. Recalcular a cada consulta
    /// daria a resposta certa e chegaria tarde.
    /// </para>
    /// <para>
    /// O valor inicial é <c>PAR-GER-001</c> — quem nunca cadastrou horário continua operando como
    /// operava antes de 18/08/2026.
    /// </para>
    /// </summary>
    public TimeSpan HoraDeViradaOperacional { get; private set; } = ViradaOperacional.Padrao;

    /// <summary>
    /// <c>RN-GER-036</c> — substitui o horário da semana inteira e <b>rederiva a virada</b>.
    /// <para>
    /// Substitui em vez de alterar dia a dia porque o cadastro é sempre da semana toda: alterar um
    /// dia isolado deixaria a virada dependente da ordem das chamadas, e a virada é o valor de que
    /// toda a apuração depende.
    /// </para>
    /// <para>
    /// ⚠ <b>Muda daqui para a frente, nunca para trás.</b> Os pedidos já gravados carregam a virada
    /// que valia quando nasceram (<c>RN-VEN-008</c> aplicado ao tempo), então relatório fechado
    /// continua com o mesmo número depois de mexer no horário.
    /// </para>
    /// </summary>
    public void DefinirHorarios(IEnumerable<HorarioDeFuncionamento> horarios)
    {
        ArgumentNullException.ThrowIfNull(horarios);

        var novos = horarios.ToList();

        var duplicado = novos.GroupBy(h => h.DiaDaSemana).FirstOrDefault(g => g.Count() > 1);

        if (duplicado is not null)
        {
            throw new ArgumentException(
                $"RN-GER-036 — {duplicado.Key} aparece duas vezes no horário da semana.",
                nameof(horarios));
        }

        _horarios.Clear();
        _horarios.AddRange(novos);

        HoraDeViradaOperacional = ViradaOperacional.De(_horarios);
    }

    /// <summary>
    /// <c>RN-VEN-048</c> — a casa está aberta neste instante (já no fuso dela)?
    /// <para>
    /// ⚠ <b>Sem horário cadastrado, está ABERTA.</b> Ausência de cadastro não pode desligar a venda
    /// de quem já vendia — a regra nova não pode fechar loja nenhuma no dia em que entrou.
    /// </para>
    /// <para>
    /// O dia que atravessa a meia-noite é conferido <b>a partir do dia anterior</b>: à 01h00 de
    /// domingo quem manda é o horário de sábado, e olhar só o dia corrente responderia "fechada"
    /// com a casa cheia.
    /// </para>
    /// </summary>
    public bool AbertaEm(DateTimeOffset instante)
    {
        if (_horarios.Count == 0)
        {
            return true;
        }

        // ⚠ A conversão para o fuso DA CASA acontece AQUI, e não em quem chama.
        //
        // O contêiner roda em UTC. Um chamador que esquecesse de converter perguntaria "está
        // aberta?" com a hora de Londres e fecharia a loja três horas antes — sem erro nenhum,
        // só uma vitrine fechada no meio da noite de sábado. Método que recebe o instante bruto
        // e converte sozinho não tem como ser chamado errado.
        var instanteLocal = TimeZoneInfo.ConvertTime(
            instante, TimeZoneInfo.FindSystemTimeZoneById(FusoHorario));

        var hora = TimeOnly.FromTimeSpan(instanteLocal.TimeOfDay);

        var hoje = _horarios.FirstOrDefault(h => h.DiaDaSemana == instanteLocal.DayOfWeek);

        if (hoje is { Fechado: false })
        {
            var abre = hoje.Abre!.Value;
            var fecha = hoje.Fecha!.Value;

            var dentro = hoje.AtravessaMeiaNoite ? hora >= abre : hora >= abre && hora < fecha;

            if (dentro)
            {
                return true;
            }
        }

        var ontem = _horarios.FirstOrDefault(
            h => h.DiaDaSemana == instanteLocal.AddDays(-1).DayOfWeek);

        return ontem is { Fechado: false, AtravessaMeiaNoite: true } && hora < ontem.Fecha!.Value;
    }

    /// <summary>
    /// <c>RN-GER-026</c> — o caminho da logomarca, exibida no cardápio público
    /// (<c>RN-VEN-037</c>).
    /// <para>
    /// <b>Nula é estado válido:</b> unidade sem logo exibe o nome fantasia. A ausência não é erro
    /// a ser corrigido com imagem genérica — placeholder de "sem logo" numa vitrine de
    /// restaurante é pior do que o nome escrito.
    /// </para>
    /// </summary>
    public string? UrlLogo { get; private set; }

    /// <summary>
    /// <c>RN-GER-026</c> — aponta para a logo nova. <b>Não apaga a anterior</b>
    /// (<c>RN-GER-002</c>), pelo mesmo motivo de <c>Produto.DefinirImagem</c>.
    /// </summary>
    public void DefinirLogo(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            throw new ArgumentException("O caminho da logo é obrigatório.", nameof(caminho));
        }

        UrlLogo = caminho;
    }

    /// <summary>Limpa a referência. O arquivo permanece, por <c>RN-GER-002</c>.</summary>
    public void RemoverLogo() => UrlLogo = null;

    public void Renomear(string razaoSocial, string nomeFantasia, string? telefone)
    {
        RazaoSocial = Exigir(razaoSocial, nameof(razaoSocial));
        NomeFantasia = Exigir(nomeFantasia, nameof(nomeFantasia));
        Telefone = telefone is null ? null : SomenteDigitos(telefone);
    }

    private static string Exigir(string valor, string campo) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException($"{campo} é obrigatório.", campo)
            : valor.Trim();

    /// <summary><c>RN-GER-013</c> — documento e telefone sem máscara. A máscara é apresentação.</summary>
    private static string SomenteDigitos(string valor) =>
        new(valor.Where(char.IsDigit).ToArray());

    private static string ValidarSlug(string slug)
    {
        var limpo = (slug ?? string.Empty).Trim().ToLowerInvariant();

        if (!System.Text.RegularExpressions.Regex.IsMatch(limpo, "^[a-z0-9-]{3,60}$"))
        {
            throw new ArgumentException(
                "RN-VEN-036 — o identificador público aceita apenas letras minúsculas, " +
                "números e hífen, com 3 a 60 caracteres.", nameof(slug));
        }

        return limpo;
    }
}
