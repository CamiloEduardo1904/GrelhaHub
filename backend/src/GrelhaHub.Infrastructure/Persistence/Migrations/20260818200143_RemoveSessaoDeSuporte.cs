using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
/// ADR-0020 — a sessão de suporte sai; a trilha fica.
/// <para>
/// A pré-condição era um portão aberto por quem ele deveria conter. O que <c>RN-GER-015</c>
/// garante — nenhuma operação de plataforma sem rastro — nunca dependeu dela, e continua imposto
/// pela trilha de auditoria, que com ADR-0017 ganha banco próprio e imutabilidade por gatilho.
/// </para>
/// </summary>
public partial class RemoveSessaoDeSuporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sessao_suporte");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sessao_suporte",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dh_abertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dh_encerramento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessao_suporte", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessao_suporte_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sessao_suporte_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sessao_suporte_aberta",
                table: "sessao_suporte",
                columns: new[] { "usuario_id", "dh_encerramento", "dh_abertura" });

            migrationBuilder.CreateIndex(
                name: "ix_sessao_suporte_unidade",
                table: "sessao_suporte",
                columns: new[] { "unidade_id", "dh_abertura" },
                descending: new[] { false, true });
        }
    }
}
