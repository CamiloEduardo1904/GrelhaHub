using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Plataforma;

/// <summary>
/// <c>RN-GER-029</c> — inativa ou reativa a unidade. <b>Ato da plataforma</b>, com justificativa.
/// <para>
/// A propriedade <c>Unidade.Ativa</c> existiu por três specs sem nenhuma operação que a mudasse, e
/// <c>Autenticar</c> já a consultava (<c>RN-GER-027</c>): o bloqueio de acesso estava pronto e era
/// <b>inalcançável</b>. Este comando é o caminho que faltava.
/// </para>
/// <para>
/// <b>Não apaga nada</b> (<c>RN-GER-002</c>): o cardápio público para de responder, os usuários
/// param de autenticar, e todo o histórico permanece. Reativar devolve o acesso sem restaurar
/// coisa alguma, porque nada foi removido.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record AlternarSituacaoDaUnidadeCommand(Guid UnidadeId, bool Ativa, string Justificativa)
    : IRequest<EstabelecimentoDto>;

public sealed class AlternarSituacaoDaUnidadeValidator
    : AbstractValidator<AlternarSituacaoDaUnidadeCommand>
{
    public AlternarSituacaoDaUnidadeValidator()
    {
        RuleFor(x => x.UnidadeId).NotEmpty();

        // Mesmo mínimo de `AlterarModuloDaUnidadeCommand`: parar a operação de um cliente é ato
        // que alguém vai precisar explicar depois.
        RuleFor(x => x.Justificativa)
            .NotEmpty()
            .MinimumLength(10).WithMessage("A justificativa precisa de ao menos 10 caracteres.")
            .MaximumLength(500);
    }
}

public sealed class AlternarSituacaoDaUnidadeHandler(
    IAppDbContext db, IContextoDeExecucao contexto, ISender sender)
    : IRequestHandler<AlternarSituacaoDaUnidadeCommand, EstabelecimentoDto>
{
    public async Task<EstabelecimentoDto> Handle(
        AlternarSituacaoDaUnidadeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var unidade = await db.Unidades
            .FirstOrDefaultAsync(u => u.Id == request.UnidadeId, cancellationToken)
            ?? throw new NaoEncontradoException("Estabelecimento não encontrado.");

        if (unidade.Ativa == request.Ativa)
        {
            throw new RegraDeNegocioException(
                request.Ativa
                    ? "Este estabelecimento já está ativo."
                    : "Este estabelecimento já está inativo.",
                "RN-GER-029");
        }

        if (request.Ativa)
        {
            unidade.Reativar();
        }
        else
        {
            unidade.Inativar();
        }

        await db.SaveChangesAsync(cancellationToken);

        // Devolve a lista atualizada da própria casa, para a tela não precisar recarregar tudo.
        var todos = await sender.Send(new ListarEstabelecimentosQuery(), cancellationToken);

        return todos.Single(e => e.Id == request.UnidadeId);
    }
}

/// <summary>
/// Altera os dados cadastrais do estabelecimento — <b>ato da plataforma</b>.
/// <para>
/// <b>O <c>slug</c> não entra</b>, e não é esquecimento: <c>RN-VEN-036</c> o torna imutável porque
/// ele circula em rede social e material impresso. O CNPJ também não: ele identifica a pessoa
/// jurídica do contrato, e trocá-lo é cadastrar outro cliente.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("privativa da plataforma — a checagem é EhPlataforma, no handler")]
public sealed record AlterarEstabelecimentoCommand(
    Guid UnidadeId,
    string RazaoSocial,
    string NomeFantasia,
    string? Telefone) : IRequest<EstabelecimentoDto>;

public sealed class AlterarEstabelecimentoValidator : AbstractValidator<AlterarEstabelecimentoCommand>
{
    public AlterarEstabelecimentoValidator()
    {
        RuleFor(x => x.UnidadeId).NotEmpty();
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NomeFantasia).NotEmpty().MinimumLength(3).MaximumLength(200);
    }
}

public sealed class AlterarEstabelecimentoHandler(
    IAppDbContext db, IContextoDeExecucao contexto, ISender sender)
    : IRequestHandler<AlterarEstabelecimentoCommand, EstabelecimentoDto>
{
    public async Task<EstabelecimentoDto> Handle(
        AlterarEstabelecimentoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ListarEstabelecimentosHandler.ExigirPlataforma(contexto);

        var unidade = await db.Unidades
            .FirstOrDefaultAsync(u => u.Id == request.UnidadeId, cancellationToken)
            ?? throw new NaoEncontradoException("Estabelecimento não encontrado.");

        unidade.Renomear(request.RazaoSocial, request.NomeFantasia, request.Telefone);

        await db.SaveChangesAsync(cancellationToken);

        var todos = await sender.Send(new ListarEstabelecimentosQuery(), cancellationToken);

        return todos.Single(e => e.Id == request.UnidadeId);
    }
}

/// <summary>O endereço da casa — <c>RN-GER-037</c>. Tudo opcional; ver <see cref="Unidade"/>.</summary>
public sealed record EnderecoDto(
    string? Cep,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf);

/// <summary>Um dia da semana — <c>RN-GER-036</c>. <c>Abre</c> nulo é dia fechado.</summary>
public sealed record HorarioDoDiaDto(DayOfWeek DiaDaSemana, TimeOnly? Abre, TimeOnly? Fecha);

/// <summary>
/// <c>RN-GER-036</c> ⚠⚠ / <c>RN-GER-037</c> — endereço e horário de funcionamento.
/// <para>
/// <b>Um comando só para os dois, e a semana inteira de uma vez.</b> A virada do dia operacional é
/// derivada do conjunto (<c>RN-GER-006</c>): salvar dia a dia deixaria a virada dependente da ordem
/// das chamadas, e no meio do caminho a casa apuraria por uma régua que ninguém escolheu.
/// </para>
/// <para>
/// Exige sessão de suporte quando quem chama é a plataforma: é dado do cliente sendo alterado em
/// nome dele, que é a definição de suporte em <c>RN-GER-015</c> — a mesma linha de
/// <c>AlterarEstabelecimentoCommand</c>.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("unidade.gerenciar")]
public sealed record DefinirOperacaoDaUnidadeCommand(
    Guid? UnidadeId,
    EnderecoDto? Endereco,
    IReadOnlyList<HorarioDoDiaDto>? Horarios) : IRequest<OperacaoDaUnidadeDto>;

/// <summary>O que a casa cadastrou, e a virada que saiu disso.</summary>
public sealed record OperacaoDaUnidadeDto(
    /// <summary>
    /// De QUAL casa é esta operação — <c>RN-GER-009</c>.
    /// <para>
    /// ⚠ Faltava, e o vazio só apareceu quando o primeiro teste tentou afirmar que a resposta era
    /// da casa certa. Para o restaurante é redundante — ele só recebe a própria —, mas a
    /// plataforma lê a operação de <b>qualquer</b> unidade (<c>RN-GER-014</c>), e sem este campo o
    /// cliente não tem como conferir se recebeu o que pediu. Resposta que não se identifica só
    /// funciona enquanto ninguém erra o parâmetro.
    /// </para>
    /// </summary>
    Guid UnidadeId,

    EnderecoDto Endereco,
    string? EnderecoResumido,
    IReadOnlyList<HorarioDoDiaDto> Horarios,

    /// <summary><c>RN-GER-006</c> — derivada, nunca digitada. A tela exibe para explicar a apuração.</summary>
    TimeOnly HoraDeViradaOperacional);

public sealed class DefinirOperacaoDaUnidadeValidator
    : AbstractValidator<DefinirOperacaoDaUnidadeCommand>
{
    public DefinirOperacaoDaUnidadeValidator()
    {
        // RN-GER-013 — documento e CEP só com dígitos. Oito, e o domínio para por aí: se o CEP
        // existe é pergunta dos Correios, não nossa.
        RuleFor(x => x.Endereco!.Cep)
            .Must(cep => string.IsNullOrWhiteSpace(cep) || SoDigitos(cep).Length == 8)
            .WithMessage("O CEP precisa ter 8 dígitos.")
            .When(x => x.Endereco is not null);

        RuleFor(x => x.Endereco!.Uf)
            .Must(uf => string.IsNullOrWhiteSpace(uf) || uf.Trim().Length == 2)
            .WithMessage("A UF é a sigla de dois caracteres.")
            .When(x => x.Endereco is not null);

        RuleForEach(x => x.Horarios!)
            .Must(h => h.Abre.HasValue == h.Fecha.HasValue)
            .WithMessage("Informe abertura e fechamento juntos, ou marque o dia como fechado.")
            .When(x => x.Horarios is not null);
    }

    private static string SoDigitos(string valor) => new([.. valor.Where(char.IsAsciiDigit)]);
}

public sealed class DefinirOperacaoDaUnidadeHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<DefinirOperacaoDaUnidadeCommand, OperacaoDaUnidadeDto>
{
    public async Task<OperacaoDaUnidadeDto> Handle(
        DefinirOperacaoDaUnidadeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // RN-GER-014 — a plataforma informa QUAL casa; o restaurante mexe na própria, e o
        // `unidadeId` que ele mandar é ignorado. Mesmo desenho de `ListarUsuariosQuery`.
        //
        // O gate é a permissão `unidade.gerenciar` (RN-GER-010), e o usuário de plataforma passa
        // pelo bypass de `PermissaoBehavior`: ele não tem perfil de unidade, logo não tem
        // permissão de unidade.
        var alvo = contexto.EhPlataforma
            ? request.UnidadeId ?? throw new RegraDeNegocioException(
                "Informe o estabelecimento.")
            : contexto.UnidadeId;

        if (alvo == Guid.Empty)
        {
            throw new NaoAutorizadoException("Sessão inválida.");
        }

        var unidade = await db.Unidades
            .IgnoreQueryFilters()
            .Include(u => u.Horarios)
            .FirstOrDefaultAsync(u => u.Id == alvo, cancellationToken)
            ?? throw new NaoEncontradoException("Estabelecimento não encontrado.");

        if (request.Endereco is { } endereco)
        {
            unidade.DefinirEndereco(
                endereco.Cep, endereco.Logradouro, endereco.Numero, endereco.Complemento,
                endereco.Bairro, endereco.Cidade, endereco.Uf);
        }

        if (request.Horarios is { } horarios)
        {
            var novos = horarios
                .Select(h => new HorarioDeFuncionamento(unidade.Id, h.DiaDaSemana, h.Abre, h.Fecha))
                .ToList();

            // O domínio valida a semana e rederiva a virada; os antigos saem por órfãos (Cascade).
            unidade.DefinirHorarios(novos);

            // ⚠ INSERÇÃO EXPLÍCITA, e não pela navegação.
            //
            // `HorarioDeFuncionamento` nasce com `Id` preenchido, e filho com chave já definida
            // descoberto pela navegação de um pai RASTREADO é classificado como `Modified`: o EF
            // manda um `UPDATE` de linha que não existe, zero linhas afetadas, e o erro chega como
            // `DbUpdateConcurrencyException` — que parece conflito de concorrência e manda
            // investigar o lugar errado.
            db.HorariosDeFuncionamento.AddRange(novos);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Montar(unidade);
    }

    internal static OperacaoDaUnidadeDto Montar(Unidade unidade) =>
        new(
            unidade.Id,
            new EnderecoDto(
                unidade.Cep, unidade.Logradouro, unidade.NumeroDoEndereco, unidade.Complemento,
                unidade.Bairro, unidade.Cidade, unidade.Uf),
            unidade.EnderecoResumido(),
            [.. unidade.Horarios
                .OrderBy(h => h.DiaDaSemana)
                .Select(h => new HorarioDoDiaDto(h.DiaDaSemana, h.Abre, h.Fecha))],
            TimeOnly.FromTimeSpan(unidade.HoraDeViradaOperacional));
}

/// <summary><c>RN-GER-036</c> / <c>RN-GER-037</c> — o que a casa tem cadastrado.</summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("unidade.visualizar")]
public sealed record ObterOperacaoDaUnidadeQuery(Guid? UnidadeId = null)
    : IRequest<OperacaoDaUnidadeDto>;

/// <summary>
/// ADR-0019 — convertida ao modelo de leitura em 18/08/2026.
/// <para>
/// ⚠ A ESCRITA continua carregando o agregado (<c>DefinirOperacaoDaUnidadeHandler</c>): é ele que
/// valida a semana e rederiva a virada operacional. O que mudou é só o caminho da leitura — e o
/// formato da resposta continua sendo montado por uma função só, agora
/// <see cref="DefinirOperacaoDaUnidadeHandler.MontarDe"/>, para que as duas rotas não divirjam.
/// </para>
/// </summary>
public sealed class ObterOperacaoDaUnidadeHandler(ILeituraDb leitura, IContextoDeExecucao contexto)
    : IRequestHandler<ObterOperacaoDaUnidadeQuery, OperacaoDaUnidadeDto>
{
    public async Task<OperacaoDaUnidadeDto> Handle(
        ObterOperacaoDaUnidadeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var alvo = contexto.EhPlataforma
            ? request.UnidadeId ?? throw new RegraDeNegocioException("Informe o estabelecimento.")
            : contexto.UnidadeId;

        if (alvo == Guid.Empty)
        {
            throw new NaoAutorizadoException("Sessão inválida.");
        }

        var unidade = await leitura.Unidades
            .FirstOrDefaultAsync(u => u.Id == alvo, cancellationToken)
            ?? throw new NaoEncontradoException("Estabelecimento não encontrado.");

        var semana = await leitura.Horarios
            .Where(h => h.UnidadeId == alvo)
            .OrderBy(h => h.DiaDaSemana)
            .ToListAsync(cancellationToken);

        return new OperacaoDaUnidadeDto(
            unidade.Id,
            new EnderecoDto(
                unidade.Cep, unidade.Logradouro, unidade.NumeroDoEndereco, unidade.Complemento,
                unidade.Bairro, unidade.Cidade, unidade.Uf),
            ApresentacaoDaCasa.Resumir(
                unidade.Logradouro, unidade.NumeroDoEndereco, unidade.Bairro,
                unidade.Cidade, unidade.Uf),
            [.. semana.Select(h => new HorarioDoDiaDto(h.DiaDaSemana, h.Abre, h.Fecha))],
            TimeOnly.FromTimeSpan(unidade.HoraDeViradaOperacional));
    }
}
