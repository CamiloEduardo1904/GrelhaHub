using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Gerais.ObterMeuRestaurante;

[ExigeModulo(CodigoDeModulo.Gerais)]
[SemPermissao("dados do próprio restaurante: qualquer usuário autenticado da unidade os vê")]
public sealed record ObterMeuRestauranteQuery : IRequest<MeuRestauranteResponse>;

public sealed record MeuRestauranteResponse(
    Guid Id,
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string? Telefone,
    string Slug,
    string LinkDoCardapio,

    /// <summary><c>RN-GER-005</c> — o fuso em que a operação acontece. SPEC-GER-001 §3.</summary>
    string FusoHorario,

    int TotalDeProdutos,
    int PedidosDoDia,

    /// <summary><c>RN-GER-026</c> — a logo. Nula é estado válido: a vitrine exibe o nome.</summary>
    string? UrlLogo);

/// <summary>
/// ADR-0019 — convertido ao modelo de leitura em 18/08/2026.
/// <para>
/// ⚠ A contagem de produtos ativos saiu do handler e entrou em <c>vw_unidade</c>: eram duas
/// viagens ao banco para montar um cartão. A contagem de pedidos do dia continua separada porque
/// ela depende do <b>dia operacional de hoje</b> — que é pergunta do relógio, não da visão.
/// </para>
/// </summary>
public sealed class ObterMeuRestauranteHandler(
    ILeituraDb leitura, IContextoDeExecucao contexto, IRelogio relogio)
    : IRequestHandler<ObterMeuRestauranteQuery, MeuRestauranteResponse>
{
    public async Task<MeuRestauranteResponse> Handle(ObterMeuRestauranteQuery request, CancellationToken cancellationToken)
    {
        var unidadeId = contexto.UnidadeId;

        var unidade = await leitura.Unidades
            .FirstOrDefaultAsync(u => u.Id == unidadeId, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        // RN-GER-006 ⚠⚠ — a contagem do dia usa data_operacional, NUNCA a data de criação.
        var hoje = relogio.DiaOperacionalDeHoje();

        var pedidosDoDia = await leitura.PedidosResumo
            .CountAsync(p => p.UnidadeId == unidadeId && p.DataOperacional == hoje, cancellationToken);

        return new MeuRestauranteResponse(
            unidade.Id, unidade.RazaoSocial, unidade.NomeFantasia, unidade.Cnpj,
            unidade.Telefone, unidade.Slug, $"/c/{unidade.Slug}", unidade.FusoHorario,
            unidade.TotalDeProdutosAtivos, pedidosDoDia, unidade.UrlLogo);
    }
}
