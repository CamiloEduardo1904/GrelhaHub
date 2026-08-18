using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR-0019 — as visões do modelo de <b>leitura</b>.
    /// <para>
    /// ⚠ <b>É aqui que a agregação passa a morar.</b> O ADR-0009 fixa a ordem
    /// <c>MOD → constraints → GEN → TRG → VW → APP</c>, com a aplicação em último: somar em memória
    /// dentro do handler era o pior lugar da lista, e o único que existia antes destas visões.
    /// </para>
    /// <para>
    /// ⚠⚠ <b>Toda visão expõe <c>unidade_id</c>, e isso é obrigatório</b> — é a coluna que o filtro
    /// global do contexto de leitura usa para recortar por unidade (<c>RN-GER-009</c>). Visão sem
    /// ela não é filtrável, e vazamento entre restaurantes na LEITURA é invisível: o número
    /// continua plausível, só que somando a casa do vizinho.
    /// </para>
    /// <para>
    /// ⚠ <b>A exclusão lógica precisa ser repetida aqui.</b> O filtro global do EF Core que aplica
    /// <c>dh_exclusao IS NULL</c> vale para o modelo de escrita e <b>não alcança visão</b> — o SQL
    /// abaixo é o único lugar onde o pedido excluído sai da leitura.
    /// </para>
    /// </summary>
    public partial class VisoesDeLeitura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Uma linha por pedido, com o total somado no banco.
            //
            // O total sai de `item_pedido`, e não de coluna do pedido: `Pedido.Total` é derivado
            // dos itens no domínio, e ter as duas coisas gravadas seria duas fontes da mesma
            // verdade (mesma doutrina de RN-EST-001 para saldo).
            migrationBuilder.Sql("""
                CREATE VIEW vw_pedido_resumo AS
                SELECT p.id,
                       p.unidade_id,
                       p.numero,
                       p.canal,
                       p.status,
                       c.nome                                  AS nome_cliente,
                       c.telefone                              AS telefone_cliente,
                       COALESCE(i.total, 0)::numeric(12,2)     AS total,
                       COALESCE(i.itens, 0)::int               AS quantidade_de_itens,
                       p.dh_criacao                            AS criado_em,
                       p.data_operacional,
                       p.observacao
                  FROM pedido p
                  LEFT JOIN cliente c ON c.id = p.cliente_id
                  LEFT JOIN LATERAL (
                        SELECT SUM(ip.preco_praticado * ip.quantidade) AS total,
                               COUNT(*)                                AS itens
                          FROM item_pedido ip
                         WHERE ip.pedido_id = p.id
                  ) i ON TRUE
                 WHERE p.dh_exclusao IS NULL
                """);

            migrationBuilder.Sql("""
                CREATE VIEW vw_pedido_item AS
                SELECT ip.pedido_id,
                       ip.unidade_id,
                       ip.nome_produto,
                       ip.quantidade,
                       ip.preco_praticado,
                       (ip.preco_praticado * ip.quantidade)::numeric(12,2) AS total,
                       ip.observacao
                  FROM item_pedido ip
                  JOIN pedido p ON p.id = ip.pedido_id
                 WHERE p.dh_exclusao IS NULL
                """);

            // RN-VEN-049 ⚠⚠ — a regra de O QUE CONTA COMO FATURAMENTO mora aqui, e é o ponto todo
            // desta visão: faturamento é só `Finalizado` (6). O que ainda pode ser cancelado —
            // confirmado (2), em produção (3), pronto (4), entregue (5) — vai em coluna separada,
            // porque número que encolhe sozinho depois destrói a confiança no painel inteiro.
            // Cancelado é 99.
            migrationBuilder.Sql("""
                CREATE VIEW vw_operacao_dia AS
                SELECT p.unidade_id,
                       p.data_operacional,
                       COALESCE(SUM(t.total) FILTER (WHERE p.status = 6), 0)::numeric(12,2)
                            AS faturamento,
                       COUNT(*) FILTER (WHERE p.status = 6)::int
                            AS pedidos_finalizados,
                       COUNT(*) FILTER (WHERE p.status BETWEEN 2 AND 5)::int
                            AS pedidos_em_andamento,
                       COALESCE(SUM(t.total) FILTER (WHERE p.status BETWEEN 2 AND 5), 0)::numeric(12,2)
                            AS valor_em_andamento,
                       COUNT(*) FILTER (WHERE p.status = 99)::int
                            AS pedidos_cancelados
                  FROM pedido p
                  LEFT JOIN LATERAL (
                        SELECT SUM(ip.preco_praticado * ip.quantidade) AS total
                          FROM item_pedido ip
                         WHERE ip.pedido_id = p.id
                  ) t ON TRUE
                 WHERE p.dh_exclusao IS NULL
                 GROUP BY p.unidade_id, p.data_operacional
                """);

            // ⚠ Agrupa por `nome_produto`, a CÓPIA feita na venda (RN-VEN-008) — não por
            // `produto_id`. Produto renomeado depois não reescreve o que foi vendido, pela mesma
            // razão de RN-GER-036 não reclassificar o passado.
            migrationBuilder.Sql("""
                CREATE VIEW vw_produto_vendido AS
                SELECT p.unidade_id,
                       p.data_operacional,
                       ip.nome_produto,
                       SUM(ip.quantidade)::int                                  AS quantidade,
                       SUM(ip.preco_praticado * ip.quantidade)::numeric(12,2)   AS total
                  FROM item_pedido ip
                  JOIN pedido p ON p.id = ip.pedido_id
                 WHERE p.dh_exclusao IS NULL
                   AND p.status = 6
                 GROUP BY p.unidade_id, p.data_operacional, ip.nome_produto
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_produto_vendido");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_operacao_dia");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_pedido_item");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_pedido_resumo");
        }
    }
}
