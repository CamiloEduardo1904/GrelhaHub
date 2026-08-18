using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Gerais.Parametros;

/// <summary>
/// <c>SPEC-GER-003</c> §3 — valor de um parâmetro na data do evento.
/// <para>
/// Consulta liberada a qualquer perfil autenticado: o atendente precisa conhecer a taxa de
/// serviço e o teto de desconto durante a operação.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Gerais)]
[ExigePermissao("parametro.visualizar")]
public sealed record ObterParametroQuery(string Codigo, DateTimeOffset? VigenteEm)
    : IRequest<ParametroResponse>;

public sealed record ParametroResponse(
    string Codigo,
    string Descricao,
    string TipoValor,
    string? Dominio,
    string Valor,
    DateTimeOffset VigenciaInicio,
    DateTimeOffset? VigenciaFim);

public sealed class ObterParametroValidator : AbstractValidator<ObterParametroQuery>
{
    public ObterParametroValidator() =>
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .Matches("^PAR-[A-Za-z]{3}-[0-9]{3}$")
            .WithMessage("O código do parâmetro segue o formato PAR-MOD-nnn.");
}

public sealed class ObterParametroHandler(IAppDbContext db, IRelogio relogio)
    : IRequestHandler<ObterParametroQuery, ParametroResponse>
{
    public async Task<ParametroResponse> Handle(
        ObterParametroQuery request, CancellationToken cancellationToken)
    {
        var codigo = request.Codigo.Trim().ToUpperInvariant();
        var instante = request.VigenteEm ?? relogio.Agora;

        var parametro = await db.Parametros
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Codigo == codigo, cancellationToken)
            ?? throw new NaoEncontradoException("Parâmetro não encontrado.");

        // Filtro global recorta a unidade (RN-GER-009).
        var vigente = await db.ValoresDeParametro
            .AsNoTracking()
            .Where(valor => valor.ParametroId == parametro.Id)
            .Where(valor => valor.VigenciaInicio <= instante)
            .Where(valor => valor.VigenciaFim == null || valor.VigenciaFim > instante)
            .OrderByDescending(valor => valor.VigenciaInicio)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new RegraDeNegocioException(
                $"O parâmetro {codigo} não tem valor vigente na data informada.");

        return new ParametroResponse(
            parametro.Codigo,
            parametro.Descricao,
            parametro.TipoValor.ToString(),
            parametro.Dominio,
            vigente.Valor,
            vigente.VigenciaInicio,
            vigente.VigenciaFim);
    }
}
