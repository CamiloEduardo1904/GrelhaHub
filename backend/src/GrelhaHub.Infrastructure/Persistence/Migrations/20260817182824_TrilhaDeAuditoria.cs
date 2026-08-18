using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrilhaDeAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "criado_por",
                table: "usuario",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "criado_por",
                table: "unidade_modulo",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "criado_por",
                table: "pedido",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "criado_por",
                table: "parametro_valor",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "log_auditoria",
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
                    data_operacional = table.Column<DateOnly>(type: "date", nullable: false, computedColumnSql: "(((dh_ocorrencia AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)", stored: true)
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

            // SPEC-GER-005 — o CHECK do dominio de operacao.
            //
            // `Exclusao` CONSTA, e a §6 original a omitia de proposito: argumentava que
            // RN-GER-002 torna a exclusao impossivel, e admiti-la sugeriria que ela pode
            // acontecer. O argumento continua valendo para exclusao FISICA; a exclusao LOGICA
            // nasceu depois, com RN-VEN-041, e precisa ser distinguivel de um cancelamento.
            migrationBuilder.Sql("""
                ALTER TABLE log_auditoria ADD CONSTRAINT ck_log_operacao CHECK (
                    operacao IN ('Criacao','Alteracao','Cancelamento','Exclusao')
                )
                """);

            // Registro sem antes nem depois nao diz nada.
            migrationBuilder.Sql("""
                ALTER TABLE log_auditoria ADD CONSTRAINT ck_log_valores CHECK (
                    valores_anteriores IS NOT NULL OR valores_novos IS NOT NULL
                )
                """);

            migrationBuilder.Sql("COMMENT ON TABLE log_auditoria IS " +
                "'RN-GER-011 / SPEC-GER-005 — livro-razao: apenas INSERT e SELECT. " +
                "Nenhuma rota altera ou exclui. Uma trilha alteravel nao e trilha.'");

            // ── A trilha e imutavel NO BANCO, nao so na aplicacao ─────────────────────────────
            //
            // A definicao de pronto da SPEC-GER-005 pedia "permissao de banco restrita a INSERT e
            // SELECT". Nao funciona: a aplicacao e DONA da tabela, e o dono reconcede o que lhe for
            // revogado. REVOKE aqui daria a aparencia de garantia sem a garantia.
            //
            // Gatilho da. ADR-0009 prefere TRG a APP justamente por isto: o interceptor impede que
            // o usuario chegue ate aqui, e o gatilho impede o UPDATE que vem por fora da aplicacao
            // — psql na mao, script de correcao, ORM futuro. Sao os dois casos em que uma trilha
            // costuma ser adulterada, e nenhum deles passa pelo interceptor.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_log_auditoria_imutavel()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'RN-GER-011 / SPEC-GER-005: log_auditoria e imutavel — % recusado. '
                        'A trilha admite apenas INSERT e SELECT.', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_log_auditoria_imutavel
                BEFORE UPDATE OR DELETE ON log_auditoria
                FOR EACH ROW
                EXECUTE FUNCTION fn_log_auditoria_imutavel()
                """);

            // TRUNCATE nao dispara gatilho FOR EACH ROW — e e exatamente o comando que apagaria a
            // trilha inteira de uma vez.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_log_auditoria_sem_truncate
                BEFORE TRUNCATE ON log_auditoria
                FOR EACH STATEMENT
                EXECUTE FUNCTION fn_log_auditoria_imutavel()
                """);

            // A permissao que restringe a consulta da trilha (RN-GER-012: ela guarda valores
            // anteriores de campos sensiveis). Concedida a Administrador e Gerente nas unidades
            // que ja existem.
            migrationBuilder.Sql("""
                INSERT INTO permissao (id, codigo, descricao, modulo)
                VALUES ('0193c000-0001-7000-8000-00000000000d',
                        'auditoria.consultar', 'Consultar a trilha de auditoria', 'GER')
                """);

            migrationBuilder.Sql("""
                INSERT INTO perfil_permissao (perfil_id, permissao_id)
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON pm.codigo = 'auditoria.consultar'
                 WHERE pf.nome IN ('Administrador', 'Gerente')
                   AND NOT EXISTS (
                        SELECT 1 FROM perfil_permissao pp
                         WHERE pp.perfil_id = pf.id AND pp.permissao_id = pm.id)
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM perfil_permissao WHERE permissao_id IN " +
                "(SELECT id FROM permissao WHERE codigo = 'auditoria.consultar')");

            migrationBuilder.Sql("DELETE FROM permissao WHERE codigo = 'auditoria.consultar'");

            // Os gatilhos caem com a tabela; a FUNCTION nao — ela e objeto de esquema independente.
            migrationBuilder.DropTable(
                name: "log_auditoria");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_log_auditoria_imutavel()");

            migrationBuilder.DropColumn(
                name: "criado_por",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "criado_por",
                table: "unidade_modulo");

            migrationBuilder.DropColumn(
                name: "criado_por",
                table: "pedido");

            migrationBuilder.DropColumn(
                name: "criado_por",
                table: "parametro_valor");
        }
    }
}
