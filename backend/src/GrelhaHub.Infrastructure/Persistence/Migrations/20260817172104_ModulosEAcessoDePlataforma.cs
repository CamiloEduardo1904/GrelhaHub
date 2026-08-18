using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModulosEAcessoDePlataforma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_usuario_email",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "uq_usuario_email",
                table: "usuario");

            migrationBuilder.AlterColumn<Guid>(
                name: "unidade_id",
                table: "usuario",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "eh_plataforma",
                table: "usuario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "modulo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nucleo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ordem = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modulo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modulo_dependencia",
                columns: table => new
                {
                    modulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depende_de_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modulo_dependencia", x => new { x.modulo_id, x.depende_de_id });
                    table.ForeignKey(
                        name: "fk_modulo_dependencia_modulo_depende_de_id",
                        column: x => x.depende_de_id,
                        principalTable: "modulo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_modulo_dependencia_modulo_modulo_id",
                        column: x => x.modulo_id,
                        principalTable: "modulo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unidade_modulo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vigencia_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vigencia_fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidade_modulo", x => x.id);
                    table.CheckConstraint("ck_unidade_modulo_vigencia", "vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio");
                    table.ForeignKey(
                        name: "fk_unidade_modulo_modulo_modulo_id",
                        column: x => x.modulo_id,
                        principalTable: "modulo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_unidade_modulo_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_usuario_unidade_id",
                table: "usuario",
                column: "unidade_id");

            migrationBuilder.CreateIndex(
                name: "uq_usuario_email",
                table: "usuario",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_modulo_codigo",
                table: "modulo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modulo_dependencia_depende_de_id",
                table: "modulo_dependencia",
                column: "depende_de_id");

            migrationBuilder.CreateIndex(
                name: "ix_unidade_modulo_busca",
                table: "unidade_modulo",
                columns: new[] { "unidade_id", "modulo_id", "vigencia_inicio" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_unidade_modulo_modulo_id",
                table: "unidade_modulo",
                column: "modulo_id");
            // ────────────────────────────────────────────────────────────────────────────
            // RN-GER-017 a RN-GER-021 / ADR-0014 — o que o EF Core não expressa.
            // ────────────────────────────────────────────────────────────────────────────

            // RH tem duas letras no catálogo desde a v0.1; uniformizar quebraria a busca por ID.
            migrationBuilder.Sql(
                "ALTER TABLE modulo ADD CONSTRAINT ck_modulo_codigo CHECK (codigo ~ '^[A-Z]{2,3}$')");

            // RN-GER-020 — a aresta trivial. Ciclo maior é impedido na aplicação: com dez
            // módulos e um grafo escrito à mão, trigger recursiva custaria mais do que resolve.
            migrationBuilder.Sql(
                "ALTER TABLE modulo_dependencia ADD CONSTRAINT ck_modulo_dependencia_ciclo " +
                "CHECK (modulo_id <> depende_de_id)");

            // RN-GER-021 — o padrão de vigência de parametro_valor, TERCEIRA aplicação.
            // btree_gist já foi habilitada pela migration ParametrosComVigencia.
            migrationBuilder.Sql("""
                ALTER TABLE unidade_modulo ADD CONSTRAINT ex_unidade_modulo_vigencia
                EXCLUDE USING gist (
                    unidade_id WITH =,
                    modulo_id  WITH =,
                    tstzrange(vigencia_inicio, vigencia_fim) WITH &&
                )
                """);

            // Os dez módulos. Ids fixos e lista escrita à mão: migration é história, e
            // referenciar uma constante do C# faria esta semeadura mudar de conteúdo no dia em
            // que alguém acrescentasse um módulo.
            migrationBuilder.Sql("""
                INSERT INTO modulo (id, codigo, nome, nucleo, ordem) VALUES
                ('0193b000-0001-7000-8000-000000000001', 'GER', 'Gerais e transversais', true,  1),
                ('0193b000-0001-7000-8000-000000000002', 'VEN', 'Vendas e Comercial',    false, 2),
                ('0193b000-0001-7000-8000-000000000003', 'PRO', 'Produção',              false, 3),
                ('0193b000-0001-7000-8000-000000000004', 'EST', 'Estoque',               false, 4),
                ('0193b000-0001-7000-8000-000000000005', 'COM', 'Compras',               false, 5),
                ('0193b000-0001-7000-8000-000000000006', 'FIN', 'Financeiro',            false, 6),
                ('0193b000-0001-7000-8000-000000000007', 'CRM', 'Relacionamento',        false, 7),
                ('0193b000-0001-7000-8000-000000000008', 'LOG', 'Logística e Frota',     false, 8),
                ('0193b000-0001-7000-8000-000000000009', 'RH',  'Recursos Humanos',      false, 9),
                ('0193b000-0001-7000-8000-00000000000a', 'FIS', 'Fiscal',                false, 10)
                """);

            // RN-GER-020 — o grafo de dependências, na ordem em que o guia de specs declara as
            // ondas: venda depende da base; estoque depende da ficha técnica; financeiro
            // depende de venda; logística depende de venda e de RH (RN-LOG-005 exige entregador
            // colaborador ativo); fiscal depende de venda.
            migrationBuilder.Sql("""
                INSERT INTO modulo_dependencia (modulo_id, depende_de_id)
                SELECT dependente.id, dependencia.id
                  FROM (VALUES
                        ('VEN', 'GER'),
                        ('PRO', 'VEN'),
                        ('EST', 'PRO'),
                        ('COM', 'EST'),
                        ('FIN', 'VEN'),
                        ('CRM', 'VEN'),
                        ('LOG', 'VEN'),
                        ('LOG', 'RH'),
                        ('FIS', 'VEN')
                       ) AS arestas(de, para)
                  JOIN modulo dependente  ON dependente.codigo  = arestas.de
                  JOIN modulo dependencia ON dependencia.codigo = arestas.para
                """);

            migrationBuilder.Sql("COMMENT ON TABLE unidade_modulo IS " +
                "'RN-GER-017/021 — habilitacao com vigencia [inicio, fim). Desabilitar fecha a " +
                "vigencia e NUNCA apaga dado (RN-GER-018).'");

        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "modulo_dependencia");

            migrationBuilder.DropTable(
                name: "unidade_modulo");

            migrationBuilder.DropTable(
                name: "modulo");

            migrationBuilder.DropIndex(
                name: "ix_usuario_unidade_id",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "uq_usuario_email",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "eh_plataforma",
                table: "usuario");

            migrationBuilder.AlterColumn<Guid>(
                name: "unidade_id",
                table: "usuario",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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
    }
}
