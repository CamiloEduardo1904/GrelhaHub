using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArquivoNoBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arquivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pasta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamanho_em_bytes = table.Column<int>(type: "integer", nullable: false),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dh_exclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arquivo", x => x.id);
                    table.CheckConstraint("ck_arquivo_tamanho", "tamanho_em_bytes > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_arquivo_unidade_pasta",
                table: "arquivo",
                columns: new[] { "unidade_id", "pasta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arquivo");
        }
    }
}
