using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Crm;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Vendas.CriarPedidoPublico;

/// <summary>
/// Pedido feito pelo cardápio público — <c>RN-VEN-038</c>.
/// </summary>
public sealed record CriarPedidoPublicoCommand(
    string Slug,
    string NomeCliente,
    string Telefone,
    string? Observacao,
    IReadOnlyList<ItemDoPedidoDto> Itens) : IRequest<PedidoCriadoResponse>;

public sealed record ItemDoPedidoDto(Guid ProdutoId, int Quantidade, string? Observacao);

public sealed record PedidoCriadoResponse(Guid Id, int Numero, decimal Total, string Status);

public sealed class CriarPedidoPublicoValidator : AbstractValidator<CriarPedidoPublicoCommand>
{
    public CriarPedidoPublicoValidator()
    {
        RuleFor(x => x.Slug).NotEmpty();

        // RN-CRM-003 — pedido de delivery exige cliente identificado.
        RuleFor(x => x.NomeCliente).NotEmpty().WithMessage("Informe seu nome.").MaximumLength(200);

        // RN-CRM-001 — o cliente é identificado unicamente pelo telefone.
        RuleFor(x => x.Telefone)
            .NotEmpty().WithMessage("Informe seu telefone.")
            .Must(t => t.Count(char.IsDigit) is >= 10 and <= 11)
            .WithMessage("Informe o telefone com DDD.");

        // RN-VEN-002 — todo pedido tem ao menos um item.
        RuleFor(x => x.Itens)
            .NotEmpty().WithMessage("O pedido precisa ter ao menos um item.");

        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.ProdutoId).NotEmpty();

            // RN-VEN-011 — a quantidade é sempre maior que zero.
            item.RuleFor(i => i.Quantidade)
                .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.")
                .LessThanOrEqualTo(99).WithMessage("Quantidade acima do limite por item.");
        });

        RuleFor(x => x.Observacao).MaximumLength(500);
    }
}

public sealed class CriarPedidoPublicoHandler(IAppDbContext db, IContextoDeExecucao contexto, IRelogio relogio)
    : IRequestHandler<CriarPedidoPublicoCommand, PedidoCriadoResponse>
{
    public async Task<PedidoCriadoResponse> Handle(CriarPedidoPublicoCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        var unidade = await db.Unidades
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == slug && u.Ativa, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        contexto.DefinirUnidadePublica(unidade.Id);

        // RN-CRM-001 — o telefone identifica o cliente. Se já pediu antes, reaproveita.
        var telefone = Cliente.NormalizarTelefone(request.Telefone);
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Telefone == telefone, cancellationToken);

        if (cliente is null)
        {
            cliente = new Cliente(unidade.Id, request.NomeCliente, telefone);
            db.Clientes.Add(cliente);
        }
        else
        {
            cliente.AtualizarNome(request.NomeCliente);
        }

        var numero = await ProximoNumeroAsync(cancellationToken);

        // RN-VEN-038 — canal delivery próprio, estado Aberto.
        // O pedido NÃO é confirmado automaticamente: a confirmação é ato do restaurante, e é
        // ela que dispara baixa de estoque e ordem de produção (RN-VEN-018).
        var pedido = new Pedido(
            unidade.Id, numero, CanalVenda.DeliveryProprio, cliente.Id, request.Observacao);

        var idsPedidos = request.Itens.Select(i => i.ProdutoId).Distinct().ToList();

        var produtos = await db.Produtos
            .AsNoTracking()
            .Where(p => idsPedidos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var item in request.Itens)
        {
            if (!produtos.TryGetValue(item.ProdutoId, out var produto))
            {
                // Produto de outra unidade cai aqui: o filtro global o tornou invisível.
                throw new RegraDeNegocioException("Produto indisponível.", "RN-VEN-020");
            }

            // RN-VEN-020 — produto inativo não pode ser lançado em pedido novo.
            if (!produto.PodeSerVendido)
            {
                throw new RegraDeNegocioException($"{produto.Nome} está indisponível.", "RN-VEN-020");
            }

            // RN-VEN-008 ⚠ — o preço praticado é COPIADO agora e não muda se o preço de
            // tabela for alterado depois.
            pedido.AdicionarItem(
                produto.Id, produto.Nome, produto.Preco, item.Quantidade, item.Observacao);
        }

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(cancellationToken);

        return new PedidoCriadoResponse(pedido.Id, pedido.Numero, pedido.Total, pedido.Status.ToString());
    }

    /// <summary>
    /// Sequencial por <b>dia operacional</b> (<c>RN-GER-006</c>) — o número que o cliente
    /// acompanha reinicia a cada dia de operação, não a cada meia-noite.
    /// </summary>
    private async Task<int> ProximoNumeroAsync(CancellationToken cancellationToken)
    {
        var hoje = relogio.DiaOperacionalDeHoje();

        // Corrida entre dois pedidos simultâneos é possível aqui. A UNIQUE
        // (unidade_id, data_operacional, numero) recusa o duplicado em vez de gerar dois
        // pedidos com o mesmo número — falha ruidosa, não silenciosa. A sequência por
        // unidade entra na spec de venda definitiva (onda 4).
        var ultimo = await db.Pedidos
            .Where(p => p.DataOperacional == hoje)
            .MaxAsync(p => (int?)p.Numero, cancellationToken);

        return (ultimo ?? 0) + 1;
    }
}
