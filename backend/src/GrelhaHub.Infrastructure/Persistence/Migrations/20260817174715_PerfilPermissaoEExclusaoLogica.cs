using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerfilPermissaoEExclusaoLogica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_item_pedido_pedidos_pedido_id",
                table: "item_pedido");

            migrationBuilder.AddColumn<bool>(
                name: "deve_trocar_senha",
                table: "usuario",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "perfil_id",
                table: "usuario",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dh_exclusao",
                table: "pedido",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "excluido_por_usuario_id",
                table: "pedido",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_exclusao",
                table: "pedido",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "perfil",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sistema = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfil", x => x.id);
                    table.ForeignKey(
                        name: "fk_perfil_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    modulo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perfil_permissao",
                columns: table => new
                {
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissao_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfil_permissao", x => new { x.perfil_id, x.permissao_id });
                    table.ForeignKey(
                        name: "fk_perfil_permissao_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_perfil_permissao_permissoes_permissao_id",
                        column: x => x.permissao_id,
                        principalTable: "permissao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_usuario_perfil_id",
                table: "usuario",
                column: "perfil_id");

            migrationBuilder.CreateIndex(
                name: "uq_perfil_nome",
                table: "perfil",
                columns: new[] { "unidade_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_perfil_permissao_permissao_id",
                table: "perfil_permissao",
                column: "permissao_id");

            migrationBuilder.CreateIndex(
                name: "uq_permissao_codigo",
                table: "permissao",
                column: "codigo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_item_pedido_pedidos_pedido_id",
                table: "item_pedido",
                column: "pedido_id",
                principalTable: "pedido",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_usuario_perfil_perfil_id",
                table: "usuario",
                column: "perfil_id",
                principalTable: "perfil",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ══════════════════════════════════════════════════════════════════════════════
            // SPEC-GER-004 — semeadura das permissoes e MIGRACAO DO DADO EXISTENTE.
            //
            // Esta migration nao pode ser so DDL: existe base em producao com usuario cujo perfil
            // era um inteiro na coluna `perfil`. Se a coluna caisse antes de mapearmos, o usuario
            // ficaria com perfil_id nulo — sem permissao nenhuma, incapaz de operar o proprio
            // restaurante, e o defeito apareceria como "perdi o acesso depois do deploy".
            // ══════════════════════════════════════════════════════════════════════════════

            // Catalogo global de permissoes. Ids fixos e lista escrita a mao: migration e
            // historia, e referenciar CatalogoDeAcesso faria esta semeadura mudar de conteudo no
            // dia em que alguem acrescentasse uma permissao.
            migrationBuilder.Sql("""
                INSERT INTO permissao (id, codigo, descricao, modulo) VALUES
                ('0193c000-0001-7000-8000-000000000001', 'parametro.visualizar', 'Consultar parâmetros do sistema',        'GER'),
                ('0193c000-0001-7000-8000-000000000002', 'parametro.alterar',    'Alterar valor de parâmetro',             'GER'),
                ('0193c000-0001-7000-8000-000000000003', 'usuario.visualizar',   'Consultar usuários da unidade',          'GER'),
                ('0193c000-0001-7000-8000-000000000004', 'usuario.gerenciar',    'Criar, alterar e desativar usuários',    'GER'),
                ('0193c000-0001-7000-8000-000000000005', 'modulo.visualizar',    'Consultar módulos contratados',          'GER'),
                ('0193c000-0001-7000-8000-000000000006', 'cardapio.visualizar',  'Ver o cardápio pela gestão',             'VEN'),
                ('0193c000-0001-7000-8000-000000000007', 'cardapio.gerenciar',   'Criar e alterar produto e disponibilidade', 'VEN'),
                ('0193c000-0001-7000-8000-000000000008', 'pedido.visualizar',    'Ver pedidos',                            'VEN'),
                ('0193c000-0001-7000-8000-000000000009', 'pedido.avancar',       'Confirmar e avançar o pedido no ciclo',  'VEN'),
                ('0193c000-0001-7000-8000-00000000000a', 'pedido.cancelar',      'Cancelar pedido com motivo',             'VEN'),
                ('0193c000-0001-7000-8000-00000000000b', 'pedido.excluir',       'Excluir logicamente pedido lançado por engano', 'VEN'),
                ('0193c000-0001-7000-8000-00000000000c', 'custo.visualizar',     'Ver custo, CMV e margem',                'VEN')
                """);

            // Os seis perfis, para CADA unidade que ja existe.
            migrationBuilder.Sql("""
                INSERT INTO perfil (id, unidade_id, nome, descricao, sistema)
                SELECT gen_random_uuid(), u.id, p.nome, p.descricao, true
                  FROM unidade u
                 CROSS JOIN (VALUES
                        ('Administrador', 'Acesso total à unidade'),
                        ('Gerente',       'Operação completa, custo e parâmetros'),
                        ('Atendente',     'Atendimento e lançamento de pedido'),
                        ('Cozinha',       'Produção — avança o pedido no ciclo'),
                        ('Operador',      'Visualização simplista — provisório, ver RN-GER-022'),
                        ('Entregador',    'Entrega — permissões entram com o módulo LOG')
                       ) AS p(nome, descricao)
                """);

            // As concessoes. Entregador nasce SEM permissao: as dele sao `entrega.*`, do modulo
            // LOG, que nao existe — perfil vazio e honesto, perfil com permissao inventada nao.
            migrationBuilder.Sql("""
                INSERT INTO perfil_permissao (perfil_id, permissao_id)
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON TRUE
                 WHERE pf.nome = 'Administrador'
                UNION ALL
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON pm.codigo IN (
                        'parametro.visualizar','parametro.alterar','usuario.visualizar',
                        'modulo.visualizar','cardapio.visualizar','cardapio.gerenciar',
                        'pedido.visualizar','pedido.avancar','pedido.cancelar','custo.visualizar')
                 WHERE pf.nome = 'Gerente'
                UNION ALL
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON pm.codigo IN (
                        'parametro.visualizar','modulo.visualizar','cardapio.visualizar',
                        'pedido.visualizar','pedido.avancar')
                 WHERE pf.nome = 'Atendente'
                UNION ALL
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON pm.codigo IN (
                        'modulo.visualizar','pedido.visualizar','pedido.avancar')
                 WHERE pf.nome = 'Cozinha'
                UNION ALL
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON pm.codigo IN (
                        'modulo.visualizar','cardapio.visualizar','pedido.visualizar')
                 WHERE pf.nome = 'Operador'
                """);

            // Mapeia o usuario existente do inteiro antigo para o perfil novo, na SUA unidade.
            // 1=Administrador, 2=Gerente, 3=Atendente, 4=Cozinha — a ordem do enum anterior.
            migrationBuilder.Sql("""
                UPDATE usuario u
                   SET perfil_id = pf.id
                  FROM perfil pf
                 WHERE pf.unidade_id = u.unidade_id
                   AND pf.nome = CASE u.perfil
                                    WHEN 1 THEN 'Administrador'
                                    WHEN 2 THEN 'Gerente'
                                    WHEN 3 THEN 'Atendente'
                                    WHEN 4 THEN 'Cozinha'
                                    ELSE 'Operador'
                                 END
                   AND u.unidade_id IS NOT NULL
                """);

            // Usuario que ja existia escolheu a propria senha: nao faz sentido exigir troca de
            // quem nunca recebeu senha de terceiro. RF-SEG-005 vale para usuario criado POR outra
            // pessoa, e daqui para frente o default da coluna cuida disso.
            migrationBuilder.Sql("UPDATE usuario SET deve_trocar_senha = false");

            // Falha ruidosa: se sobrou usuario de restaurante sem perfil, o mapeamento errou e o
            // deploy TEM de parar aqui — nao depois, com alguem sem acesso ao proprio sistema.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    orfaos integer;
                BEGIN
                    SELECT count(*) INTO orfaos
                      FROM usuario
                     WHERE unidade_id IS NOT NULL AND perfil_id IS NULL;

                    IF orfaos > 0 THEN
                        RAISE EXCEPTION
                          'RN-GER-010: % usuario(s) de restaurante ficaram sem perfil apos a migracao.', orfaos;
                    END IF;
                END $$;
                """);

            // Só agora a coluna antiga pode cair.
            migrationBuilder.DropColumn(
                name: "perfil",
                table: "usuario");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_item_pedido_pedidos_pedido_id",
                table: "item_pedido");

            migrationBuilder.DropForeignKey(
                name: "fk_usuario_perfil_perfil_id",
                table: "usuario");

            migrationBuilder.DropTable(
                name: "perfil_permissao");

            migrationBuilder.DropTable(
                name: "perfil");

            migrationBuilder.DropTable(
                name: "permissao");

            migrationBuilder.DropIndex(
                name: "ix_usuario_perfil_id",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "deve_trocar_senha",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "perfil_id",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "dh_exclusao",
                table: "pedido");

            migrationBuilder.DropColumn(
                name: "excluido_por_usuario_id",
                table: "pedido");

            migrationBuilder.DropColumn(
                name: "motivo_exclusao",
                table: "pedido");

            migrationBuilder.AddColumn<int>(
                name: "perfil",
                table: "usuario",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "fk_item_pedido_pedidos_pedido_id",
                table: "item_pedido",
                column: "pedido_id",
                principalTable: "pedido",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
