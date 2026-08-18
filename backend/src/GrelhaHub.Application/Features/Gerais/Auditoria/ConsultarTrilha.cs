using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Auditoria;

public sealed record RegistroDaTrilhaDto(
    Guid Id,
    string Entidade,
    Guid RegistroId,
    string Operacao,
    Guid? UsuarioId,
    string? Usuario,
    DateTimeOffset OcorridoEm,
    DateOnly DataOperacional,
    string? ValoresAnteriores,
    string? ValoresNovos,
    string? Justificativa);

public sealed record TrilhaResponse(
    int Pagina,
    int Tamanho,
    int Total,
    IReadOnlyList<RegistroDaTrilhaDto> Registros);

/// <summary>
/// <c>SPEC-GER-005</c> §3 — consulta a trilha de auditoria.
/// <para>
/// <b>Restrita a quem pode ver custo</b> (`auditoria.consultar`, concedida a Administrador e
/// Gerente): a trilha guarda valores anteriores de campos sensíveis, e abri-la ao atendente
/// contornaria <c>RN-GER-012</c> por outra porta.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("auditoria.consultar")]
public sealed record ConsultarTrilhaQuery(
    /// <summary>
    /// <c>RN-GER-014</c> — só a plataforma escolhe. Para ela é <b>opcional</b> aqui, e não
    /// obrigatório como nas outras leituras: a trilha registra também o que não pertence a unidade
    /// alguma (a própria plataforma), e exigir o recorte esconderia justamente isso.
    /// </summary>
    Guid? UnidadeId,

    string? Entidade,
    Guid? RegistroId,
    Guid? UsuarioId,
    DateTimeOffset? De,
    DateTimeOffset? Ate,
    int Pagina = 1,
    int Tamanho = 50) : IRequest<TrilhaResponse>;

public sealed class ConsultarTrilhaValidator : AbstractValidator<ConsultarTrilhaQuery>
{
    /// <summary><c>RNF-002</c> — teto de página. Trilha cresce sem limite; resposta, não.</summary>
    public const int TamanhoMaximo = 50;

    public ConsultarTrilhaValidator()
    {
        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1);

        RuleFor(x => x.Tamanho)
            .InclusiveBetween(1, TamanhoMaximo)
            .WithMessage($"O tamanho da página vai de 1 a {TamanhoMaximo}.");

        RuleFor(x => x.Ate)
            .GreaterThan(x => x.De!.Value)
            .When(x => x.De.HasValue && x.Ate.HasValue)
            .WithMessage("A data final precisa ser posterior à inicial.");
    }
}

/// <summary>
/// ADR-0017 + ADR-0019 — lê do <b>banco de log</b>, e não do banco da aplicação.
/// <para>
/// ⚠ A trilha mudou de banco, e este handler foi junto. Ele já não usava agregado nenhum — só
/// projetava — então a conversão ao lado da leitura foi consequência da mudança de banco, e não
/// trabalho a mais: <c>ILogDb</c> não expõe o contexto de escrita, e a catraca do ADR-0019 pôde
/// dar baixa nele.
/// </para>
/// <para>
/// ⚠⚠ <b>O recorte por unidade é EXPLÍCITO aqui, e precisa ser.</b> O banco de log não tem filtro
/// global — de propósito: ele registra também o que a plataforma fez sobre o dado do cliente, e
/// sujeitá-lo ao recorte esconderia justamente o que precisa ser auditado. O preço é que esquecer
/// o <c>Where</c> aqui vaza a trilha de uma casa para outra, e nenhum filtro salva.
/// </para>
/// </summary>
public sealed class ConsultarTrilhaHandler(
    ILogDb logDb, ILeituraDb leitura, IContextoDeExecucao contexto)
    : IRequestHandler<ConsultarTrilhaQuery, TrilhaResponse>
{
    public async Task<TrilhaResponse> Handle(
        ConsultarTrilhaQuery request, CancellationToken cancellationToken)
    {
        // O recorte é explícito — ver o resumo da classe. O usuário de plataforma vê todas as
        // unidades porque é ele quem dá suporte (RN-GER-014, ADR-0013).
        var consulta = logDb.Trilha.AsNoTracking();

        if (!contexto.EhPlataforma)
        {
            var unidadeId = contexto.UnidadeId;

            if (unidadeId == Guid.Empty)
            {
                throw new NaoEncontradoException("Unidade não encontrada.");
            }

            // RN-GER-016 — o restaurante vê a sua trilha, e o parâmetro dele é ignorado.
            consulta = consulta.Where(registro => registro.UnidadeId == unidadeId);
        }
        else if (request.UnidadeId is { } daCasa && daCasa != Guid.Empty)
        {
            consulta = consulta.Where(registro => registro.UnidadeId == daCasa);
        }

        if (!string.IsNullOrWhiteSpace(request.Entidade))
        {
            var entidade = request.Entidade.Trim().ToLowerInvariant();
            consulta = consulta.Where(registro => registro.Entidade == entidade);
        }

        if (request.RegistroId is { } registroId)
        {
            consulta = consulta.Where(registro => registro.RegistroId == registroId);
        }

        if (request.UsuarioId is { } usuarioId)
        {
            consulta = consulta.Where(registro => registro.UsuarioId == usuarioId);
        }

        if (request.De is { } de)
        {
            consulta = consulta.Where(registro => registro.OcorridoEm >= de);
        }

        if (request.Ate is { } ate)
        {
            consulta = consulta.Where(registro => registro.OcorridoEm < ate);
        }

        var total = await consulta.CountAsync(cancellationToken);

        var registros = await consulta
            .OrderByDescending(registro => registro.OcorridoEm)
            .Skip((request.Pagina - 1) * request.Tamanho)
            .Take(request.Tamanho)
            .Select(registro => new
            {
                registro.Id,
                registro.Entidade,
                registro.RegistroId,
                registro.Operacao,
                registro.UsuarioId,
                registro.OcorridoEm,
                registro.DataOperacional,
                registro.ValoresAnteriores,
                registro.ValoresNovos,
                registro.Justificativa,
            })
            .ToListAsync(cancellationToken);

        var autores = await NomesDosAutoresAsync(registros.Select(r => r.UsuarioId), cancellationToken);

        return new TrilhaResponse(
            request.Pagina,
            request.Tamanho,
            total,
            registros.Select(registro => new RegistroDaTrilhaDto(
                registro.Id,
                registro.Entidade,
                registro.RegistroId,
                registro.Operacao.ToString(),
                registro.UsuarioId,
                registro.UsuarioId is { } autor ? autores.GetValueOrDefault(autor) : null,
                registro.OcorridoEm,
                registro.DataOperacional,
                registro.ValoresAnteriores,
                registro.ValoresNovos,
                registro.Justificativa)).ToList());
    }

    /// <summary>
    /// Nome de quem fez, em uma consulta só — não uma por linha.
    /// <para>
    /// <c>IgnoreQueryFilters</c> porque o autor pode ser o usuário de plataforma, que não tem
    /// unidade: exibir "—" no lugar do nome de quem mexeu esvaziaria a trilha justamente no caso
    /// que mais importa auditar.
    /// </para>
    /// </summary>
    /// <para>
    /// ⚠⚠ <b>DUAS CONSULTAS, EM DOIS BANCOS.</b> A trilha está no banco de log e o usuário no da
    /// aplicação — não há mais <c>JOIN</c> entre eles, e o ADR-0017 registrou esse custo ao separar.
    /// O que era uma junção virou uma segunda ida ao banco, com os ids que a primeira devolveu.
    /// </para>
    private async Task<Dictionary<Guid, string>> NomesDosAutoresAsync(
        IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var distintos = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        if (distintos.Count == 0)
        {
            return [];
        }

        return await leitura.Autores
            .Where(autor => distintos.Contains(autor.Id))
            .ToDictionaryAsync(autor => autor.Id, autor => autor.Nome, cancellationToken);
    }
}
