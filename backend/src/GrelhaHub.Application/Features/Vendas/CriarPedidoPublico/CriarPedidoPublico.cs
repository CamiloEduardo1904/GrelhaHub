using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Crm;
using GrelhaHub.Domain.Vendas;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Vendas.CriarPedidoPublico;

/// <summary>
/// Pedido feito pelo cardápio público — <c>RN-VEN-038</c>.
/// </summary>
[SemModulo("pertence a VEN, mas o gate vive no handler: a unidade só é conhecida depois de resolver o slug, e RN-VEN-037 exige 404 e não 403 para quem está fora")]
[SemPermissao("rota pública anônima — RN-VEN-038")]
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

        // RN-VEN-052 ⚠ — a observação do cliente é instrução de preparo, e cabe inteira na
        // comanda. O campo da vitrine já tem `maxLength`, mas quem não passou pela vitrine chega
        // aqui: a tela evita a viagem, o validator decide — a mesma divisão do mínimo do motivo
        // no <Dialogo />.
        RuleFor(x => x.Observacao)
            .MaximumLength(Pedido.MaximoDeCaracteresNaObservacao)
            .WithMessage(
                $"A observação deve ter no máximo {Pedido.MaximoDeCaracteresNaObservacao} caracteres.");

        // RN-VEN-052 — a observação do item segue o mesmo teto. Hoje nenhuma tela a preenche;
        // o validator existe para que o dia em que uma preencher não invente outro número.
        RuleForEach(x => x.Itens).ChildRules(item =>
            item.RuleFor(i => i.Observacao)
                .MaximumLength(Pedido.MaximoDeCaracteresNaObservacao)
                .WithMessage(
                    $"A observação do item deve ter no máximo {Pedido.MaximoDeCaracteresNaObservacao} caracteres."));
    }
}

public sealed class CriarPedidoPublicoHandler(IAppDbContext db, IContextoDeExecucao contexto, IRelogio relogio, IModulos modulos)
    : IRequestHandler<CriarPedidoPublicoCommand, PedidoCriadoResponse>
{
    public async Task<PedidoCriadoResponse> Handle(CriarPedidoPublicoCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        var unidade = await db.Unidades
            .AsNoTracking()
            .Include(u => u.Horarios)
            .FirstOrDefaultAsync(u => u.Slug == slug && u.Ativa, cancellationToken)
            ?? throw new NaoEncontradoException("Restaurante não encontrado.");

        contexto.DefinirUnidadePublica(unidade.Id);

        // RN-VEN-048 — fora do horário, a casa não recebe pedido.
        //
        // A recusa é AQUI, e não só na tela: esconder o botão não impede um POST, e um pedido
        // aceito com a cozinha apagada vira comida que ninguém faz e cliente esperando. Unidade
        // sem horário cadastrado é considerada aberta — a regra nova não fecha loja de ninguém no
        // dia em que entrou.
        if (!unidade.AbertaEm(relogio.Agora))
        {
            throw new RegraDeNegocioException(
                "Este restaurante está fechado agora. Confira o horário de funcionamento.",
                "RN-VEN-048");
        }
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
            unidade.Id, numero, CanalVenda.DeliveryProprio, cliente.Id, request.Observacao,

            // RN-GER-036 ⚠⚠ — a virada e o fuso DESTA casa, copiados agora. A coluna gerada
            // `data_operacional` calcula a partir destas duas colunas, e não de constante: é o
            // que permite cada restaurante ter a sua madrugada sem que ninguém reclassifique o
            // passado do outro.
            unidade.HoraDeViradaOperacional,
            unidade.FusoHorario);

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

        return new PedidoCriadoResponse(
            pedido.Id, pedido.Numero, pedido.Total.Valor, pedido.Status.ToString());
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
