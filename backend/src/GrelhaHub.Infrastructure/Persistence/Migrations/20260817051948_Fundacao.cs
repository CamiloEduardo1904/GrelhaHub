using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fundacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    razao_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fuso_horario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidade", x => x.id);
                    table.CheckConstraint("ck_unidade_cnpj", "cnpj ~ '^[0-9]{14}$'");
                    table.CheckConstraint("ck_unidade_slug", "slug ~ '^[a-z0-9-]{3,60}$'");
                });

            migrationBuilder.CreateTable(
                name: "categoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categoria", x => x.id);
                    table.ForeignKey(
                        name: "fk_categoria_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cliente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cliente", x => x.id);
                    table.ForeignKey(
                        name: "fk_cliente_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    perfil = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "produto",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    preco = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    disponivel = table.Column<bool>(type: "boolean", nullable: false),
                    url_imagem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produto", x => x.id);
                    table.CheckConstraint("ck_produto_preco", "preco > 0");
                    table.ForeignKey(
                        name: "fk_produto_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produto_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedido",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    canal = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dh_confirmacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dh_finalizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    data_operacional = table.Column<DateOnly>(type: "date", nullable: false, computedColumnSql: "(((dh_criacao AT TIME ZONE 'America/Sao_Paulo') - interval '4 hours')::date)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedido", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedido_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedido_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_pedido",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preco_praticado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    nome_produto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_pedido", x => x.id);
                    table.CheckConstraint("ck_item_preco", "preco_praticado >= 0");
                    table.CheckConstraint("ck_item_quantidade", "quantidade > 0");
                    table.ForeignKey(
                        name: "fk_item_pedido_pedidos_pedido_id",
                        column: x => x.pedido_id,
                        principalTable: "pedido",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_item_pedido_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_categoria_nome",
                table: "categoria",
                columns: new[] { "unidade_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_cliente_telefone",
                table: "cliente",
                columns: new[] { "unidade_id", "telefone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_pedido",
                table: "item_pedido",
                columns: new[] { "unidade_id", "pedido_id" });

            migrationBuilder.CreateIndex(
                name: "ix_item_pedido_pedido_id",
                table: "item_pedido",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_pedido_produto_id",
                table: "item_pedido",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_cliente_id",
                table: "pedido",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_dia_status",
                table: "pedido",
                columns: new[] { "unidade_id", "data_operacional", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_pedido_numero",
                table: "pedido",
                columns: new[] { "unidade_id", "data_operacional", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produto_categoria",
                table: "produto",
                columns: new[] { "unidade_id", "categoria_id" });

            migrationBuilder.CreateIndex(
                name: "ix_produto_categoria_id",
                table: "produto",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "uq_produto_nome",
                table: "produto",
                columns: new[] { "unidade_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_unidade_cnpj",
                table: "unidade",
                column: "cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_unidade_slug",
                table: "unidade",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_email",
                table: "usuario",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "uq_usuario_email",
                table: "usuario",
                columns: new[] { "unidade_id", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_pedido");

            migrationBuilder.DropTable(
                name: "usuario");

            migrationBuilder.DropTable(
                name: "pedido");

            migrationBuilder.DropTable(
                name: "produto");

            migrationBuilder.DropTable(
                name: "cliente");

            migrationBuilder.DropTable(
                name: "categoria");

            migrationBuilder.DropTable(
                name: "unidade");
        }
    }
}
