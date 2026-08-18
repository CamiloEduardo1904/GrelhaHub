using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HorarioEnderecoEViradaPorUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bairro",
                table: "unidade",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cep",
                table: "unidade",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cidade",
                table: "unidade",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "complemento",
                table: "unidade",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "hora_virada_operacional",
                table: "unidade",
                type: "interval",
                nullable: false,
                defaultValueSql: "interval '4 hours'");

            migrationBuilder.AddColumn<string>(
                name: "logradouro",
                table: "unidade",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_endereco",
                table: "unidade",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "uf",
                table: "unidade",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuso_operacional",
                table: "pedido",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "America/Sao_Paulo");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "hora_virada_operacional",
                table: "pedido",
                type: "interval",
                nullable: false,
                defaultValueSql: "interval '4 hours'");

            // ⚠ RN-GER-006 / RN-VEN-018 — os DOIS índices que contêm `data_operacional` caem junto
            // com a coluna, e é preciso recriá-los à mão.
            //
            // Trocar a expressão de uma coluna gerada é DROP + ADD no PostgreSQL, e `DROP COLUMN`
            // leva embora todo índice que a inclua — sem erro e sem aviso. O EF Core não recria o
            // que não sabe ter perdido: sem estas linhas, `uq_pedido_numero` desapareceria e a
            // unicidade do número do pedido por dia operacional deixaria de existir em produção,
            // calada, até dois pedidos nascerem com o mesmo número na mesma noite.
            migrationBuilder.DropIndex(name: "uq_pedido_numero", table: "pedido");
            migrationBuilder.DropIndex(name: "ix_pedido_dia_status", table: "pedido");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "data_operacional",
                table: "pedido",
                type: "date",
                nullable: false,
                computedColumnSql: "(((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date)",
                stored: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldComputedColumnSql: "(((dh_criacao AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)",
                oldStored: true);

            migrationBuilder.CreateIndex(
                name: "uq_pedido_numero",
                table: "pedido",
                columns: new[] { "unidade_id", "data_operacional", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedido_dia_status",
                table: "pedido",
                columns: new[] { "unidade_id", "data_operacional", "status" });

            migrationBuilder.CreateTable(
                name: "unidade_horario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dia_semana = table.Column<int>(type: "integer", nullable: false),
                    abre = table.Column<TimeOnly>(type: "time", nullable: true),
                    fecha = table.Column<TimeOnly>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidade_horario", x => x.id);
                    table.CheckConstraint("ck_unidade_horario_par", "(abre IS NULL AND fecha IS NULL) OR (abre IS NOT NULL AND fecha IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_unidade_horario_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_unidade_horario_dia",
                table: "unidade_horario",
                columns: new[] { "unidade_id", "dia_semana" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unidade_horario");

            migrationBuilder.DropColumn(
                name: "bairro",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "cep",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "cidade",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "complemento",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "hora_virada_operacional",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "logradouro",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "numero_endereco",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "uf",
                table: "unidade");

            migrationBuilder.DropColumn(
                name: "fuso_operacional",
                table: "pedido");

            migrationBuilder.DropColumn(
                name: "hora_virada_operacional",
                table: "pedido");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "data_operacional",
                table: "pedido",
                type: "date",
                nullable: false,
                computedColumnSql: "(((dh_criacao AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)",
                stored: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldComputedColumnSql: "(((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date)",
                oldStored: true);
        }
    }
}
