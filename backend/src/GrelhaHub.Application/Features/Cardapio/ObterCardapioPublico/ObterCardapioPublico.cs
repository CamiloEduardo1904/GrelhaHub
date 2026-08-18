using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Cardapio.ObterCardapioPublico;

/// <summary>
/// Cardápio público do restaurante — <c>RN-VEN-036</c>, <c>RN-VEN-037</c>.
/// Acessível <b>sem autenticação</b>.
/// </summary>
[SemModulo("pertence a VEN, mas o gate vive no handler: a unidade só é conhecida depois de resolver o slug, e RN-VEN-037 exige 404 e não 403 para quem está fora")]
[SemPermissao("rota pública anônima — RN-VEN-037")]
public sealed record ObterCardapioPublicoQuery(string Slug) : IRequest<CardapioPublicoResponse>;

public sealed record CardapioPublicoResponse(
    string Slug,
    string NomeFantasia,
    string? Telefone,

    /// <summary>
    /// <c>RN-GER-026</c> — a logo. <b>Nula é resposta válida:</b> o cardápio sem logo exibe o nome
    /// fantasia, e um placeholder de "sem imagem" na vitrine de um restaurante é pior do que o
    /// nome escrito.
    /// </summary>
    string? UrlLogo,

    /// <summary><c>RN-GER-037</c> — o endereço em uma linha. Nulo quando a casa não cadastrou.</summary>
    string? Endereco,

    /// <summary>
    /// <c>RN-VEN-048</c> — a casa está aberta AGORA. Falso fecha a vitrine: sem botão de pedir e
    /// com o aviso de quando abre.
    /// </summary>
    bool Aberta,

    /// <summary><c>RN-GER-036</c> — a semana, para a vitrine dizer quando abre.</summary>
    IReadOnlyList<HorarioPublicoDto> Horarios,

    IReadOnlyList<CategoriaPublicaDto> Categorias);

/// <summary><c>RN-GER-036</c> — um dia da semana na vitrine. <c>Abre</c> nulo é dia fechado.</summary>
public sealed record HorarioPublicoDto(DayOfWeek DiaDaSemana, TimeOnly? Abre, TimeOnly? Fecha);

public sealed record CategoriaPublicaDto(Guid Id, string Nome, IReadOnlyList<ProdutoPublicoDto> Produtos);

/// <summary>
/// <c>RN-GER-012</c> — <b>não existe campo de custo, CMV ou margem neste DTO.</b>
/// A restrição é de dado, não de tela: o cardápio público é onde vazar custo seria mais
/// grave, porque qualquer pessoa o acessa. Devolver e esconder no front continuaria
/// visível em qualquer inspeção de rede.
/// </summary>
public sealed record ProdutoPublicoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    decimal Preco,

    /// <summary><c>RN-VEN-042</c> — caminho público da imagem. Nulo quando não há.</summary>
    string? UrlImagem);

public sealed class ObterCardapioPublicoHandler(
    IAppDbContext db, IContextoDeExecucao contexto, IModulos modulos, IRelogio relogio)
    : IRequestHandler<ObterCardapioPublicoQuery, CardapioPublicoResponse>
{
    public async Task<CardapioPublicoResponse> Handle(ObterCardapioPublicoQuery request, CancellationToken cancellationToken)
    {
        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();

        // `unidade` não é filtrada por unidade_id — ela É a unidade.
        var unidade = await db.Unidades
            .AsNoTracking()
            .Include(u => u.Horarios)
            .FirstOrDefaultAsync(u => u.Slug == slug && u.Ativa, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        // Fixa o escopo: a partir daqui o filtro global do EF Core garante que nada de outra
        // unidade apareça (RN-VEN-037). Sem isto, cada consulta pública precisaria filtrar a
        // unidade a mão — e o esquecimento vazaria o cardápio de outro restaurante.
        contexto.DefinirUnidadePublica(unidade.Id);
        // RN-GER-017 / SPEC-GER-006 CA-7 — gate de módulo AQUI, e não no pipeline.
        //
        // O behavior roda antes do handler, quando a unidade ainda não está no contexto: ela só
        // é conhecida depois de resolver o slug. E a resposta certa é 404, não 403: para quem
        // está do lado de fora, o restaurante simplesmente não tem cardápio online — 403
        // anunciaria que existe algo ali que não foi contratado.
        if (!await modulos.EstaHabilitadoAsync(unidade.Id, CodigoDeModulo.Vendas, em: null, cancellationToken))
        {
            throw new NaoEncontradoException("Restaurante não encontrado.");
        }


        // Filtro e ordenação no SQL; a conversão de Dinheiro para o decimal do contrato
        // acontece em memória, porque o EF Core não traduz acesso a membro de propriedade com
        // conversor de valor (SPEC-GER-001 §4).
        var categorias = await db.Categorias
            .AsNoTracking()
            .Where(c => c.Ativa && c.Produtos.Any(p => p.Ativo && p.Disponivel))
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .Select(c => new
            {
                c.Id,
                c.Nome,
                Produtos = c.Produtos
                    // RN-VEN-020 — produto inativo não aparece no cardápio.
                    .Where(p => p.Ativo && p.Disponivel)
                    .OrderBy(p => p.Nome)
                    .Select(p => new { p.Id, p.Nome, p.Descricao, p.Preco, p.UrlImagem })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var publicas = categorias
            .Select(categoria => new CategoriaPublicaDto(
                categoria.Id,
                categoria.Nome,
                categoria.Produtos
                    .Select(produto => new ProdutoPublicoDto(
                        produto.Id, produto.Nome, produto.Descricao, produto.Preco.Valor,
                        produto.UrlImagem))
                    .ToList()))
            .ToList();

        return new CardapioPublicoResponse(
            unidade.Slug,
            unidade.NomeFantasia,
            unidade.Telefone,
            unidade.UrlLogo,
            unidade.EnderecoResumido(),

            // RN-VEN-048 — no fuso DA CASA, não no do servidor. O contêiner roda em UTC, e
            // perguntar "está aberta?" com a hora de Londres fecharia a loja três horas antes.
            unidade.AbertaEm(relogio.Agora),

            [.. unidade.Horarios
                .OrderBy(h => h.DiaDaSemana)
                .Select(h => new HorarioPublicoDto(h.DiaDaSemana, h.Abre, h.Fecha))],

            publicas);
    }
}
