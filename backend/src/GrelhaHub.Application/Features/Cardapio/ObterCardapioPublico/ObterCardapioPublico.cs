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

    /// <summary><c>RN-VEN-042</c> — referência pública da imagem. Nula quando não há.</summary>
    string? UrlImagem);

/// <summary>
/// ADR-0019 — convertido ao modelo de leitura em 18/08/2026.
/// <para>
/// ⚠⚠ <b>A regra de "está aberta agora" NÃO foi reescrita em SQL.</b> Ela vive em
/// <see cref="ApresentacaoDaCasa.AbertaEm"/>, que <c>Unidade.AbertaEm</c> também chama — uma
/// implementação, dois chamadores. Traduzir <c>RN-VEN-048</c> para a visão criaria a segunda
/// implementação que <c>DiaOperacionalParidadeTests</c> existe para denunciar, e a divergência
/// apareceria como "a vitrine diz fechado e o painel diz aberto".
/// </para>
/// </summary>
public sealed class ObterCardapioPublicoHandler(
    ILeituraDb leitura, IContextoDeExecucao contexto, IModulos modulos, IRelogio relogio)
    : IRequestHandler<ObterCardapioPublicoQuery, CardapioPublicoResponse>
{
    public async Task<CardapioPublicoResponse> Handle(ObterCardapioPublicoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();

        // `vw_unidade` não é recortada por unidade — ela É a unidade, e aqui ainda não há uma no
        // contexto: é o slug que a resolve.
        var unidade = await leitura.Unidades
            .FirstOrDefaultAsync(u => u.Slug == slug && u.Ativa, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        // Fixa o escopo: a partir daqui o filtro global do contexto de leitura garante que nada de
        // outra unidade apareça (RN-VEN-037). Sem isto, cada consulta pública precisaria filtrar a
        // unidade a mão — e o esquecimento vazaria o cardápio de outro restaurante.
        contexto.DefinirUnidadePublica(unidade.Id);

        // RN-GER-017 / SPEC-GER-006 CA-7 — gate de módulo AQUI, e não no pipeline. O behavior roda
        // antes do handler, quando a unidade ainda não está no contexto. E a resposta certa é 404:
        // para quem está do lado de fora, o restaurante simplesmente não tem cardápio online.
        if (!await modulos.EstaHabilitadoAsync(unidade.Id, CodigoDeModulo.Vendas, em: null, cancellationToken))
        {
            throw new NaoEncontradoException("Restaurante não encontrado.");
        }

        var semana = await leitura.Horarios
            .Where(h => h.UnidadeId == unidade.Id)
            .OrderBy(h => h.DiaDaSemana)
            .ToListAsync(cancellationToken);

        // RN-VEN-044 — a categoria só entra na vitrine se tiver produto que o cliente possa
        // comprar. `ProdutosNaVitrine` é contado pela visão, e não por um `Any` correlacionado.
        var categorias = await leitura.Categorias
            .Where(c => c.Ativa && c.ProdutosNaVitrine > 0)
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);

        // RN-VEN-020 / RN-VEN-044 — produto inativo OU indisponível não aparece no cardápio. São
        // dois campos distintos de propósito, e o efeito para o cliente é o mesmo.
        var produtos = await leitura.ProdutosDoCardapio
            .Where(p => p.Ativo && p.Disponivel)
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);

        var porCategoria = produtos
            .GroupBy(p => p.CategoriaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new CardapioPublicoResponse(
            unidade.Slug,
            unidade.NomeFantasia,
            unidade.Telefone,
            unidade.UrlLogo,
            ApresentacaoDaCasa.Resumir(
                unidade.Logradouro, unidade.NumeroDoEndereco, unidade.Bairro,
                unidade.Cidade, unidade.Uf),

            // RN-VEN-048 — no fuso DA CASA, não no do servidor. O contêiner roda em UTC, e
            // perguntar "está aberta?" com a hora de Londres fecharia a loja três horas antes.
            ApresentacaoDaCasa.AbertaEm(
                [.. semana.Select(h => new ApresentacaoDaCasa.DiaDaCasa(h.DiaDaSemana, h.Abre, h.Fecha))],
                unidade.FusoHorario,
                relogio.Agora),

            [.. semana.Select(h => new HorarioPublicoDto(h.DiaDaSemana, h.Abre, h.Fecha))],

            [.. categorias.Select(categoria => new CategoriaPublicaDto(
                categoria.Id,
                categoria.Nome,
                porCategoria.TryGetValue(categoria.Id, out var lista)
                    ? [.. lista.Select(p => new ProdutoPublicoDto(
                        p.Id, p.Nome, p.Descricao, p.Preco, p.UrlImagem))]
                    : []))]);
    }
}
