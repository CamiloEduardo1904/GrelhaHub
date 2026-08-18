using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TokenDeAcessoEEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "token_acesso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    hash_token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    dh_expiracao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dh_uso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dh_invalidacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_acesso", x => x.id);
                    table.ForeignKey(
                        name: "fk_token_acesso_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_token_acesso_vigente",
                table: "token_acesso",
                columns: new[] { "usuario_id", "tipo", "dh_uso", "dh_invalidacao" });

            migrationBuilder.CreateIndex(
                name: "ux_token_acesso_hash",
                table: "token_acesso",
                column: "hash_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "token_acesso");
        }
    }
}
