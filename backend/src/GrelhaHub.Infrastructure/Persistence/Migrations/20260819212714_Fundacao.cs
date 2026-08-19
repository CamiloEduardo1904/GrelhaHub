using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR-0022 §6 — a fundação ÚNICA: substitui as 18 migrations anteriores, com o esquema já
    /// no formato do Identity. A metade que o EF Core não expressa (extensão, EXCLUDE, seeds,
    /// dim_tempo e as visões do ADR-0019) vive no arquivo parceiro
    /// <c>20260819212714_Fundacao.Complemento.cs</c> — este aqui é GERADO pelo
    /// <c>dotnet ef migrations add</c>, e regenerá-lo não pode custar o SQL escrito à mão.
    /// </summary>
    public partial class Fundacao : Migration
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
                name: "outbox_auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    registro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valores_anteriores = table.Column<string>(type: "jsonb", maxLength: 200, nullable: true),
                    valores_novos = table.Column<string>(type: "jsonb", maxLength: 200, nullable: true),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dh_ocorrencia = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_operacional = table.Column<DateOnly>(type: "date", nullable: false),
                    dh_enfileiramento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultimo_erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_auditoria", x => x.id);
                });

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
                name: "unidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    razao_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fuso_horario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "America/Sao_Paulo"),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero_endereco = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    hora_virada_operacional = table.Column<TimeSpan>(type: "interval", nullable: false, defaultValueSql: "interval '4 hours'"),
                    url_logo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidade", x => x.id);
                    table.CheckConstraint("ck_unidade_cnpj", "cnpj ~ '^[0-9]{14}$'");
                    table.CheckConstraint("ck_unidade_razao_social", "length(trim(razao_social)) > 0");
                    table.CheckConstraint("ck_unidade_slug", "slug ~ '^[a-z0-9-]{3,60}$'");
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
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "perfil",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sistema = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    carimbo_de_concorrencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
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
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true)
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
                name: "perfil_permissao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    valor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfil_permissao", x => x.id);
                    table.ForeignKey(
                        name: "fk_perfil_permissao_perfil",
                        column: x => x.perfil_id,
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    eh_plataforma = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sobrenome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cpf = table.Column<string>(type: "char(11)", nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    carimbo_de_seguranca = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    carimbo_de_concorrencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deve_trocar_senha = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_de_usuario = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    nome_de_usuario_normalizado = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email_normalizado = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email_confirmado = table.Column<bool>(type: "boolean", nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefone_confirmado = table.Column<bool>(type: "boolean", nullable: false),
                    dois_fatores_habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    bloqueado_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bloqueio_habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    falhas_de_acesso = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                    table.CheckConstraint("ck_usuario_cpf", "cpf IS NULL OR cpf ~ '^[0-9]{11}$'");
                    table.ForeignKey(
                        name: "fk_usuario_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuario_unidade_unidade_id",
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
                    hora_virada_operacional = table.Column<TimeSpan>(type: "interval", nullable: false, defaultValueSql: "interval '4 hours'"),
                    fuso_operacional = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "America/Sao_Paulo"),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    canal = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dh_cancelamento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dh_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    dh_confirmacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dh_finalizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dh_exclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    excluido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_exclusao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    data_operacional = table.Column<DateOnly>(type: "date", nullable: false, computedColumnSql: "(((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedido", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedido_cancelado_por",
                        column: x => x.cancelado_por_usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedido_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedido_criado_por",
                        column: x => x.criado_por,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedido_excluido_por",
                        column: x => x.excluido_por_usuario_id,
                        principalTable: "usuario",
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
                        name: "fk_token_acesso_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    valor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_claim", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_claim_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_login",
                columns: table => new
                {
                    provedor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    chave_no_provedor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    nome_do_provedor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_login", x => new { x.provedor, x.chave_no_provedor });
                    table.ForeignKey(
                        name: "fk_usuario_login_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_perfil",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_perfil", x => new { x.usuario_id, x.perfil_id });
                    table.ForeignKey(
                        name: "fk_usuario_perfil_perfil",
                        column: x => x.perfil_id,
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usuario_perfil_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_token",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provedor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    nome = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    valor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_token", x => new { x.usuario_id, x.provedor, x.nome });
                    table.ForeignKey(
                        name: "fk_usuario_token_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_item_pedido_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arquivo_unidade_pasta",
                table: "arquivo",
                columns: new[] { "unidade_id", "pasta" });

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
                name: "uq_modulo_codigo",
                table: "modulo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modulo_dependencia_depende_de_id",
                table: "modulo_dependencia",
                column: "depende_de_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_auditoria_fila",
                table: "outbox_auditoria",
                column: "dh_enfileiramento");

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

            migrationBuilder.CreateIndex(
                name: "ix_pedido_cancelado_por_usuario_id",
                table: "pedido",
                column: "cancelado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_cliente_id",
                table: "pedido",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_criado_por",
                table: "pedido",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_dia_status",
                table: "pedido",
                columns: new[] { "unidade_id", "data_operacional", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_pedido_excluido_por_usuario_id",
                table: "pedido",
                column: "excluido_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "uq_pedido_numero",
                table: "pedido",
                columns: new[] { "unidade_id", "data_operacional", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_perfil_nome",
                table: "perfil",
                columns: new[] { "unidade_id", "nome_normalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_perfil_permissao_perfil",
                table: "perfil_permissao",
                column: "perfil_id");

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
                name: "ix_token_acesso_vigente",
                table: "token_acesso",
                columns: new[] { "usuario_id", "tipo", "dh_uso", "dh_invalidacao" });

            migrationBuilder.CreateIndex(
                name: "ux_token_acesso_hash",
                table: "token_acesso",
                column: "hash_token",
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
                name: "uq_unidade_horario_dia",
                table: "unidade_horario",
                columns: new[] { "unidade_id", "dia_semana" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unidade_modulo_busca",
                table: "unidade_modulo",
                columns: new[] { "unidade_id", "modulo_id", "vigencia_inicio" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_unidade_modulo_modulo_id",
                table: "unidade_modulo",
                column: "modulo_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_email_normalizado",
                table: "usuario",
                column: "email_normalizado");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_perfil_id",
                table: "usuario",
                column: "perfil_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_unidade_id",
                table: "usuario",
                column: "unidade_id");

            migrationBuilder.CreateIndex(
                name: "uq_usuario_cpf",
                table: "usuario",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_usuario_email",
                table: "usuario",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_usuario_nome_de_usuario",
                table: "usuario",
                column: "nome_de_usuario_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_claim_usuario",
                table: "usuario_claim",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_login_usuario",
                table: "usuario_login",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_perfil_perfil",
                table: "usuario_perfil",
                column: "perfil_id");

            // A metade escrita à mão — ver o arquivo Complemento.
            AplicarObjetosDeBanco(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // As visões e a dimensão saem ANTES das tabelas de que dependem.
            RemoverObjetosDeBanco(migrationBuilder);

            migrationBuilder.DropTable(
                name: "arquivo");

            migrationBuilder.DropTable(
                name: "item_pedido");

            migrationBuilder.DropTable(
                name: "modulo_dependencia");

            migrationBuilder.DropTable(
                name: "outbox_auditoria");

            migrationBuilder.DropTable(
                name: "parametro_valor");

            migrationBuilder.DropTable(
                name: "perfil_permissao");

            migrationBuilder.DropTable(
                name: "token_acesso");

            migrationBuilder.DropTable(
                name: "unidade_horario");

            migrationBuilder.DropTable(
                name: "unidade_modulo");

            migrationBuilder.DropTable(
                name: "usuario_claim");

            migrationBuilder.DropTable(
                name: "usuario_login");

            migrationBuilder.DropTable(
                name: "usuario_perfil");

            migrationBuilder.DropTable(
                name: "usuario_token");

            migrationBuilder.DropTable(
                name: "pedido");

            migrationBuilder.DropTable(
                name: "produto");

            migrationBuilder.DropTable(
                name: "parametro");

            migrationBuilder.DropTable(
                name: "modulo");

            migrationBuilder.DropTable(
                name: "usuario");

            migrationBuilder.DropTable(
                name: "cliente");

            migrationBuilder.DropTable(
                name: "categoria");

            migrationBuilder.DropTable(
                name: "perfil");

            migrationBuilder.DropTable(
                name: "unidade");
        }
    }
}
