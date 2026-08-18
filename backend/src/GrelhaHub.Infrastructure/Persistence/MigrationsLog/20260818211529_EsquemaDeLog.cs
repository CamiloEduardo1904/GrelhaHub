using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.MigrationsLog
{
    /// <inheritdoc />
    public partial class EsquemaDeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "log_auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    registro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valores_anteriores = table.Column<string>(type: "jsonb", nullable: true),
                    valores_novos = table.Column<string>(type: "jsonb", nullable: true),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dh_ocorrencia = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_operacional = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_log_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "log_excecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    pilha = table.Column<string>(type: "text", nullable: true),
                    rota = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    metodo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    status_http = table.Column<int>(type: "integer", nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dh_ocorrencia = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_log_excecao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_log_auditoria_autor",
                table: "log_auditoria",
                columns: new[] { "unidade_id", "usuario_id", "data_operacional" });

            migrationBuilder.CreateIndex(
                name: "ix_log_auditoria_registro",
                table: "log_auditoria",
                columns: new[] { "unidade_id", "entidade", "registro_id", "dh_ocorrencia" });

            migrationBuilder.CreateIndex(
                name: "ix_log_excecao_correlacao",
                table: "log_excecao",
                column: "correlacao");

            migrationBuilder.CreateIndex(
                name: "ix_log_excecao_tipo",
                table: "log_excecao",
                columns: new[] { "tipo", "dh_ocorrencia" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "log_auditoria");

            migrationBuilder.DropTable(
                name: "log_excecao");
        }
    }
}
