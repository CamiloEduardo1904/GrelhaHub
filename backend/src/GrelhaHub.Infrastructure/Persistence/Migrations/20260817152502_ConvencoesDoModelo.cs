using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvencoesDoModelo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "fuso_horario",
                table: "unidade",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "America/Sao_Paulo",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "ativa",
                table: "unidade",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddCheckConstraint(
                name: "ck_unidade_razao_social",
                table: "unidade",
                sql: "length(trim(razao_social)) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_unidade_razao_social",
                table: "unidade");

            migrationBuilder.AlterColumn<string>(
                name: "fuso_horario",
                table: "unidade",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "America/Sao_Paulo");

            migrationBuilder.AlterColumn<bool>(
                name: "ativa",
                table: "unidade",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
