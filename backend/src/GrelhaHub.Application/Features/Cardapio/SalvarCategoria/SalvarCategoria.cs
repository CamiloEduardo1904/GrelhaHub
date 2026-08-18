using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CategoriaDomain = GrelhaHub.Domain.Cardapio.Categoria;

namespace GrelhaHub.Application.Features.Cardapio.SalvarCategoria;

/// <summary>
/// Cria ou atualiza uma categoria do cardápio — <c>RN-VEN-043</c>, <c>SPEC-VEN-003</c>.
/// <para>
/// <b>Não existia comando algum para categoria.</b> O domínio trazia <c>Renomear</c> e
/// <c>Inativar</c> desde a <c>SPEC-VEN-001</c>, sem um único chamador em todo o repositório: um
/// restaurante que se cadastrasse pelo caminho normal chegava ao painel com cardápio vazio e
/// nenhum botão.
/// </para>
/// </summary>
[ExigeModulo(CodigoDeModulo.Vendas)]
[ExigePermissao("cardapio.gerenciar")]
public sealed record SalvarCategoriaCommand(Guid? Id, string Nome, int Ordem) : IRequest<Guid>;

public sealed class SalvarCategoriaValidator : AbstractValidator<SalvarCategoriaCommand>
{
    public SalvarCategoriaValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);

        // Domínio, não política (ADR-0009): nenhuma regra diz quantas categorias cabem no
        // cardápio. O teto existe para que "ordem" continue sendo um número que gente digita.
        RuleFor(x => x.Ordem).InclusiveBetween(0, 999);
    }
}

public sealed class SalvarCategoriaHandler(IAppDbContext db, IContextoDeExecucao contexto)
    : IRequestHandler<SalvarCategoriaCommand, Guid>
{
    public async Task<Guid> Handle(SalvarCategoriaCommand request, CancellationToken cancellationToken)
    {
        var nome = request.Nome.Trim();

        // RN-VEN-043 — nome único NA UNIDADE. O índice `uq_categoria_nome (unidade_id, nome)` já
        // recusava o duplicado; sem esta conferência o usuário recebia 500 com o texto de violação
        // de índice do PostgreSQL, o que pelo ADR-0009 é defeito da aplicação:
        //
        //   o banco garante que o dado nunca fica inválido; a aplicação garante que o usuário
        //   nunca chega até lá.
        //
        // ⚠ Isto NÃO substitui o índice: entre este SELECT e o INSERT existe uma corrida, e duas
        // requisições simultâneas passariam as duas. O índice é o que fecha; a mensagem legível é
        // o que a aplicação deve.
        //
        // ⚠ A comparação é SENSÍVEL a caixa, exatamente como o índice. "X-Tudo" e "x-tudo" passam
        // as duas hoje.
        //
        // Deixar a aplicação insensível e o índice sensível seria pior do que a limitação: as duas
        // camadas discordariam da mesma regra, que é o modo de falha que o teste de paridade do
        // dia operacional existe para pegar. Insensibilidade de verdade exige índice sobre
        // `lower(nome)` — migration, e antes disso uma decisão do cliente (pergunta 21 da fila de
        // validação). Até lá, aplicação e banco dizem a mesma coisa.
        var duplicada = await db.Categorias
            .AsNoTracking()
            .AnyAsync(c => c.Id != request.Id && c.Nome == nome, cancellationToken);

        if (duplicada)
        {
            throw new RegraDeNegocioException("Já existe uma categoria com este nome.");
        }

        if (request.Id is null)
        {
            var nova = new CategoriaDomain(contexto.UnidadeId, nome, request.Ordem);

            db.Categorias.Add(nova);
            await db.SaveChangesAsync(cancellationToken);

            return nova.Id;
        }

        // O filtro global por unidade (RN-GER-009) já se aplica: o id de outra unidade resulta em
        // "não encontrada", e não em alteração silenciosa de dado alheio.
        var categoria = await db.Categorias
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NaoEncontradoException("Categoria não encontrada.");

        categoria.Renomear(nome, request.Ordem);
        await db.SaveChangesAsync(cancellationToken);

        return categoria.Id;
    }
}
