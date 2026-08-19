using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>RN-VEN-058</c> ⚠⚠ — o cancelamento passa a guardar <b>instante e responsável</b>, e os
    /// três "quem" do pedido viram <b>referência</b>.
    /// <para>
    /// O motivo já existia (<c>motivo_cancelamento</c>, desde a <c>Fundacao</c>). Faltavam os outros
    /// dois terços do que <c>RN-GER-002</c> promete — e o resumo XML de <c>Pedido.Cancelar</c> dizia
    /// "com motivo e responsável" desde o primeiro dia, com o responsável em lugar nenhum.
    /// </para>
    /// <para>
    /// ⚠⚠ <b>E as três colunas de responsável ganham chave estrangeira:</b> <c>criado_por</c>,
    /// <c>cancelado_por_usuario_id</c> e <c>excluido_por_usuario_id</c> eram <c>uuid</c> soltos —
    /// nada impedia gravar um id inexistente, e o modelo não sabia que aquelas três colunas falavam
    /// de gente. O banco é a <b>primeira</b> camada da ordem de ADR-0009
    /// (<c>MOD → constraints → GEN → TRG → VW → APP</c>), e integridade referencial é literalmente o
    /// que ela existe para dar. ⚠ Se houver órfão no banco, esta migration <b>falha alto</b> — que é
    /// o comportamento certo de uma restrição sendo introduzida.
    /// </para>
    /// </summary>
    public partial class CancelamentoComResponsavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cancelado_por_usuario_id",
                table: "pedido",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dh_cancelamento",
                table: "pedido",
                type: "timestamp with time zone",
                nullable: true);

            // ⚠⚠ O INSTANTE das linhas antigas é recuperável, e recuperá-lo é obrigação.
            //
            // Até 19/08/2026 `Cancelar` gravava em `dh_finalizacao`. O dado existe; o que estava
            // errado era a coluna. Deixá-lo para trás transformaria uma correção de modelo em perda
            // de histórico — e `RN-GER-002` diz que nada de transacional se perde.
            //
            // ⚠ O RESPONSÁVEL não é recuperável aqui, e não se inventa. Ele existe na trilha, em
            // OUTRO banco (ADR-0017): buscá-lo de dentro de uma migration exigiria uma segunda
            // conexão no meio de um DDL. Fica nulo, e a tela omite a linha em vez de escrever
            // "sistema".
            migrationBuilder.Sql(
                "UPDATE pedido SET dh_cancelamento = dh_finalizacao "
                + "WHERE status = 99 AND dh_finalizacao IS NOT NULL");

            // ⚠ E `dh_finalizacao` é LIMPO nas linhas canceladas: manter os dois preenchidos
            // deixaria viva no banco a ambiguidade que acabou de ser resolvida no código. Cancelar
            // não é finalizar — o pedido cancelado não passou pelo fim do ciclo, ele saiu dele.
            migrationBuilder.Sql("UPDATE pedido SET dh_finalizacao = NULL WHERE status = 99");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_cancelado_por_usuario_id",
                table: "pedido",
                column: "cancelado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_criado_por",
                table: "pedido",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_excluido_por_usuario_id",
                table: "pedido",
                column: "excluido_por_usuario_id");

            migrationBuilder.AddForeignKey(
                name: "fk_pedido_cancelado_por",
                table: "pedido",
                column: "cancelado_por_usuario_id",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedido_criado_por",
                table: "pedido",
                column: "criado_por",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedido_excluido_por",
                table: "pedido",
                column: "excluido_por_usuario_id",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_pedido_resumo");
            migrationBuilder.Sql(VisaoComResponsavel);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ A volta devolve o instante a `dh_finalizacao` ANTES de derrubar a coluna. Sem isto,
            // um rollback apagaria o histórico que o `Up` acabou de preservar.
            migrationBuilder.Sql(
                "UPDATE pedido SET dh_finalizacao = dh_cancelamento "
                + "WHERE status = 99 AND dh_cancelamento IS NOT NULL");

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_pedido_resumo");
            migrationBuilder.Sql(VisaoAnterior);

            migrationBuilder.DropForeignKey(
                name: "fk_pedido_cancelado_por",
                table: "pedido");

            migrationBuilder.DropForeignKey(
                name: "fk_pedido_criado_por",
                table: "pedido");

            migrationBuilder.DropForeignKey(
                name: "fk_pedido_excluido_por",
                table: "pedido");

            migrationBuilder.DropIndex(
                name: "ix_pedido_cancelado_por_usuario_id",
                table: "pedido");

            migrationBuilder.DropIndex(
                name: "ix_pedido_criado_por",
                table: "pedido");

            migrationBuilder.DropIndex(
                name: "ix_pedido_excluido_por_usuario_id",
                table: "pedido");

            migrationBuilder.DropColumn(
                name: "cancelado_por_usuario_id",
                table: "pedido");

            migrationBuilder.DropColumn(
                name: "dh_cancelamento",
                table: "pedido");
        }

        /// <summary>
        /// A visão passa a resolver o <b>nome completo</b> de quem cancelou — <c>RN-VEN-058</c>.
        /// <para>
        /// ⚠⚠ <c>LEFT JOIN</c>, sempre. Pedido cancelado antes desta regra não tem responsável, e um
        /// <c>INNER</c> faria esses pedidos <b>sumirem da lista</b>. Consulta que perde linha ao
        /// ganhar coluna é o defeito mais caro que uma visão pode ter, porque ninguém procura o que
        /// não aparece — <c>CancelamentoRegistradoTests</c> tem um caso só para isso.
        /// </para>
        /// <para>
        /// O <c>NULLIF(TRIM(...))</c> devolve <c>NULL</c> — e não string vazia — quando não há
        /// responsável: a tela decide pela ausência, e string vazia desenharia a linha em branco.
        /// </para>
        /// </summary>
        private const string VisaoComResponsavel = """
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
                   p.observacao,
                   p.motivo_cancelamento,
                   p.dh_cancelamento                       AS cancelado_em,
                   NULLIF(TRIM(COALESCE(u.nome, '') || ' ' || COALESCE(u.sobrenome, '')), '')
                                                           AS cancelado_por
              FROM pedido p
              LEFT JOIN cliente c ON c.id = p.cliente_id
              LEFT JOIN usuario u ON u.id = p.cancelado_por_usuario_id
              LEFT JOIN LATERAL (
                    SELECT SUM(ip.preco_praticado * ip.quantidade) AS total,
                           COUNT(*)                                AS itens
                      FROM item_pedido ip
                     WHERE ip.pedido_id = p.id
              ) i ON TRUE
             WHERE p.dh_exclusao IS NULL
            """;

        private const string VisaoAnterior = """
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
            """;
    }
}
