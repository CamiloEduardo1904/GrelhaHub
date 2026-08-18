using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// ADR-0017 — a trilha sai deste banco e vira OUTBOX.
    /// <para>
    /// ⚠⚠ <b>DESTRUTIVA: <c>log_auditoria</c> é removida daqui.</b> O histórico existente NÃO é
    /// migrado para o banco de log. Aceito porque não há cliente em produção — havendo, esta
    /// migration teria de copiar as linhas antes de derrubar a tabela, e trilha perdida numa
    /// migração é exatamente o que RN-GER-002 existe para impedir.
    /// </para>
    /// </summary>
    public partial class TrilhaViaOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "log_auditoria");

            migrationBuilder.CreateTable(
                name: "outbox_auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    registro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valores_anteriores = table.Column<string>(type: "jsonb", maxLength: 200, nullable: true),
                    valores_novos = table.Column<string>(type: "jsonb", maxLength: 200, nullable: true),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dh_ocorrencia = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_operacional = table.Column<DateOnly>(type: "date", nullable: false),
                    dh_enfileiramento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultimo_erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_auditoria", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_auditoria_fila",
                table: "outbox_auditoria",
                column: "dh_enfileiramento");

            // ⚠⚠ AS MESMAS VALIDAÇÕES QUE A TRILHA TINHA, agora na ENTRADA da ponte.
            //
            // ⚠ A lista veio do ENUM, não da memória: os valores são Criacao, Alteracao,
            // Cancelamento e Exclusao. Escrevi "Inclusao" de cabeça na primeira versão e o CHECK
            // passou a recusar TODA linha válida — a suíte inteira de auditoria caiu, e a mensagem
            // do PostgreSQL culpava a linha, não a restrição.
            //
            // Lixo recusado aqui nunca chega ao banco de log, e o worker não precisa saber o que
            // fazer com uma linha que não consegue copiar. Validar só na saída deixaria a outbox
            // entupida com uma linha impossível, bloqueando toda a fila atrás dela.
            migrationBuilder.Sql(
                "ALTER TABLE outbox_auditoria ADD CONSTRAINT ck_outbox_operacao " +
                "CHECK (operacao IN ('Criacao', 'Alteracao', 'Cancelamento', 'Exclusao'))");

            // RN-GER-011 — registro sem antes nem depois não diz nada.
            migrationBuilder.Sql(
                "ALTER TABLE outbox_auditoria ADD CONSTRAINT ck_outbox_valores " +
                "CHECK (valores_anteriores IS NOT NULL OR valores_novos IS NOT NULL)");

            // ADR-0017 + ADR-0019 — a trilha vive noutro banco; o nome do autor, não.
            //
            // ⚠ SEM unidade_id: o autor pode ser usuário de PLATAFORMA, que não tem unidade
            // (RN-GER-014). Recortar aqui exibiria um traço no lugar do nome de quem mexeu,
            // justamente no caso que mais importa auditar.
            migrationBuilder.Sql("CREATE VIEW vw_autor AS SELECT u.id, u.nome FROM usuario u");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_auditoria");

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_autor");

            migrationBuilder.CreateTable(
                name: "log_auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_operacional = table.Column<DateOnly>(type: "date", nullable: false, computedColumnSql: "(((dh_ocorrencia AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)", stored: true),
                    entidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dh_ocorrencia = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    operacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valores_anteriores = table.Column<string>(type: "jsonb", maxLength: 200, nullable: true),
                    valores_novos = table.Column<string>(type: "jsonb", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_log_auditoria", x => x.id);
                    table.ForeignKey(
                        name: "fk_log_auditoria_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_log_auditoria_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_log_auditoria_usuario_id",
                table: "log_auditoria",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_log_registro",
                table: "log_auditoria",
                columns: new[] { "unidade_id", "entidade", "registro_id", "dh_ocorrencia" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_log_usuario",
                table: "log_auditoria",
                columns: new[] { "unidade_id", "usuario_id", "data_operacional" });
        }
    }
}
