using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ParametrosComVigencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Exigida pela EXCLUDE mais abaixo: ela combina igualdade (=) com sobreposição de
            // intervalo (&&), e o gist padrão não sabe indexar igualdade de uuid.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist");

            migrationBuilder.CreateTable(
                name: "parametro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_valor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dominio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parametro", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parametro_valor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parametro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    vigencia_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vigencia_fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parametro_valor", x => x.id);
                    table.CheckConstraint("ck_parametro_valor_vigencia", "vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio");
                    table.ForeignKey(
                        name: "fk_parametro_valor_parametro_parametro_id",
                        column: x => x.parametro_id,
                        principalTable: "parametro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parametro_valor_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_parametro_codigo",
                table: "parametro",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parametro_valor_busca",
                table: "parametro_valor",
                columns: new[] { "unidade_id", "parametro_id", "vigencia_inicio" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_parametro_valor_parametro_id",
                table: "parametro_valor",
                column: "parametro_id");

            // CHECK do domínio de tipo_valor. Texto e não enum do PostgreSQL: acrescentar um
            // tipo passa a ser ALTER de constraint, visível na revisão, em vez de ALTER TYPE.
            migrationBuilder.Sql("""
                ALTER TABLE parametro ADD CONSTRAINT ck_parametro_tipo CHECK (
                    tipo_valor IN ('percentual','dinheiro','dias','tempo','intervalo','enum','inteiro','fator')
                )
                """);

            // ────────────────────────────────────────────────────────────────────────────
            // O PADRÃO DE VIGÊNCIA DO PROJETO — reusado por preco_produto (RN-VEN-010) e
            // ficha_tecnica (RN-PRO-002).
            //
            // Impede duas vigências sobrepostas para o mesmo parâmetro na mesma unidade.
            // `tstzrange(inicio, fim)` é semiaberto — [inicio, fim) —, então uma vigência que
            // começa exatamente onde a outra termina NÃO conflita, que é como o fechamento
            // encadeado precisa funcionar.
            //
            // Vive aqui e não no modelo porque o EF Core não expressa EXCLUDE. E precisa ser
            // no banco: a aplicação já valida, mas carga inicial, script de manutenção e
            // correção manual não passam por ela — e é exatamente aí que a sobreposição nasce.
            // ────────────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                ALTER TABLE parametro_valor ADD CONSTRAINT ex_parametro_valor_vigencia
                EXCLUDE USING gist (
                    parametro_id WITH =,
                    unidade_id   WITH =,
                    tstzrange(vigencia_inicio, vigencia_fim) WITH &&
                )
                """);

            // Definições dos nove parâmetros de docs/04-parametros-do-sistema.md.
            //
            // Ids fixos, e a lista escrita à mão: migration é história. Referenciar
            // CatalogoDeParametros faria esta migration passar a semear dez linhas no dia em
            // que alguém acrescentasse o décimo — reescrevendo o passado.
            migrationBuilder.Sql("""
                INSERT INTO parametro (id, codigo, descricao, tipo_valor, dominio) VALUES
                ('0193a000-0001-7000-8000-000000000001', 'PAR-GER-001', 'Hora de virada do dia operacional',        'tempo',      '00:00..23:59'),
                ('0193a000-0001-7000-8000-000000000002', 'PAR-VEN-001', 'Desconto máximo sem autorização',          'percentual', '0..100'),
                ('0193a000-0001-7000-8000-000000000003', 'PAR-VEN-002', 'Percentual da taxa de serviço',            'percentual', '0..100'),
                ('0193a000-0001-7000-8000-000000000004', 'PAR-VEN-003', 'Fricção na falta de saldo de insumo',      'enum',       'bloquear|avisar|permitir'),
                ('0193a000-0001-7000-8000-000000000005', 'PAR-EST-001', 'Validade mínima aceita no recebimento',    'dias',       '>= 0'),
                ('0193a000-0001-7000-8000-000000000006', 'PAR-EST-002', 'Dias de estoque de segurança',             'dias',       '>= 0'),
                ('0193a000-0001-7000-8000-000000000007', 'PAR-COM-001', 'Valor mínimo que exige cotação',           'dinheiro',   '> 0'),
                ('0193a000-0001-7000-8000-000000000008', 'PAR-CRM-001', 'Multiplicador do intervalo médio para em risco', 'fator', '> 0'),
                ('0193a000-0001-7000-8000-000000000009', 'PAR-FIS-001', 'Prazo legal para cancelamento fiscal',     'intervalo',  'duração positiva')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parametro_valor");

            migrationBuilder.DropTable(
                name: "parametro");
        }
    }
}
