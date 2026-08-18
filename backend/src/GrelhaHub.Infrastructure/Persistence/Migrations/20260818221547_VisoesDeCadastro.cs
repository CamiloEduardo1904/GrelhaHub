using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR-0019, <b>segunda leva</b> — as visões do cadastro.
    /// <para>
    /// A primeira leva (<c>VisoesDeLeitura</c>) cobriu venda. Esta fecha a conversão: casa,
    /// cardápio, módulo, perfil, usuário e parâmetro. Depois dela nenhuma <c>Query</c> alcança o
    /// contexto de escrita, e a catraca de <c>SeparacaoDeCqrsTests</c> chega a zero.
    /// </para>
    /// <para>
    /// ⚠⚠ <b>Toda visão que PERTENCE a uma unidade expõe <c>unidade_id</c></b> — é a coluna que o
    /// filtro global do contexto de leitura usa (<c>RN-GER-009</c>). As três que não expõem —
    /// <c>vw_modulo</c>, <c>vw_modulo_dependencia</c> e <c>vw_parametro</c> — são catálogo global,
    /// iguais para todas as casas, e a ausência é declarada no modelo de leitura correspondente.
    /// </para>
    /// <para>
    /// ⚠ <b><c>vw_unidade</c> também não é recortada</b>, e pelo motivo oposto: ela DESCREVE a
    /// unidade, e o cardápio público a encontra pelo <c>slug</c> antes de existir unidade no
    /// contexto. Recortá-la faria a vitrine responder "restaurante não encontrado" para toda casa
    /// que existe.
    /// </para>
    /// </summary>
    public partial class VisoesDeCadastro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A casa, com as duas contagens que o painel pede.
            //
            // ⚠ `total_de_usuarios` exclui `eh_plataforma`: RN-GER-016 — o suporte não é usuário
            // do restaurante, e contá-lo faria a tela do cliente exibir uma pessoa que ele não
            // conhece nem consegue listar.
            migrationBuilder.Sql("""
                CREATE VIEW vw_unidade AS
                SELECT u.id,
                       u.slug,
                       u.razao_social,
                       u.nome_fantasia,
                       u.cnpj,
                       u.telefone,
                       u.url_logo,
                       u.ativa,
                       u.dh_criacao                     AS criado_em,
                       u.fuso_horario,
                       u.hora_virada_operacional        AS hora_de_virada_operacional,
                       u.cep,
                       u.logradouro,
                       u.numero_endereco                AS numero_do_endereco,
                       u.complemento,
                       u.bairro,
                       u.cidade,
                       u.uf,
                       COALESCE(us.total, 0)::int       AS total_de_usuarios,
                       COALESCE(pr.total, 0)::int       AS total_de_produtos_ativos
                  FROM unidade u
                  LEFT JOIN LATERAL (
                        SELECT COUNT(*) AS total
                          FROM usuario x
                         WHERE x.unidade_id = u.id AND NOT x.eh_plataforma
                  ) us ON TRUE
                  LEFT JOIN LATERAL (
                        SELECT COUNT(*) AS total
                          FROM produto p
                         WHERE p.unidade_id = u.id AND p.ativo
                  ) pr ON TRUE
                """);

            migrationBuilder.Sql("""
                CREATE VIEW vw_unidade_horario AS
                SELECT h.unidade_id,
                       h.dia_semana AS dia_da_semana,
                       h.abre,
                       h.fecha
                  FROM unidade_horario h
                """);

            // ⚠ `produtos_na_vitrine` conta o que o CLIENTE veria — ativo E disponível
            // (RN-VEN-044, dois campos distintos de propósito). É o número que decide se a
            // categoria aparece no cardápio público; a tela de gestão o ignora, porque lá
            // categoria vazia precisa aparecer para poder ser reativada (SPEC-VEN-003 §4).
            migrationBuilder.Sql("""
                CREATE VIEW vw_cardapio_categoria AS
                SELECT c.unidade_id,
                       c.id,
                       c.nome,
                       c.ordem,
                       c.ativa,
                       COALESCE(v.total, 0)::int AS produtos_na_vitrine
                  FROM categoria c
                  LEFT JOIN LATERAL (
                        SELECT COUNT(*) AS total
                          FROM produto p
                         WHERE p.categoria_id = c.id AND p.ativo AND p.disponivel
                  ) v ON TRUE
                """);

            migrationBuilder.Sql("""
                CREATE VIEW vw_cardapio_produto AS
                SELECT p.unidade_id,
                       p.id,
                       p.categoria_id,
                       p.nome,
                       p.descricao,
                       p.preco,
                       p.ativo,
                       p.disponivel,
                       p.url_imagem
                  FROM produto p
                """);

            migrationBuilder.Sql("""
                CREATE VIEW vw_modulo AS
                SELECT m.id, m.codigo, m.nome, m.nucleo, m.ordem
                  FROM modulo m
                """);

            // RN-GER-020 — a aresta já com as duas siglas resolvidas, para o handler não precisar
            // de um dicionário em memória só para descobrir o código do dependente.
            migrationBuilder.Sql("""
                CREATE VIEW vw_modulo_dependencia AS
                SELECT d.modulo_id,
                       m.codigo             AS codigo,
                       d.depende_de_id,
                       dep.codigo           AS depende_de_codigo
                  FROM modulo_dependencia d
                  JOIN modulo m   ON m.id = d.modulo_id
                  JOIN modulo dep ON dep.id = d.depende_de_id
                """);

            // ⚠ SEM filtro de vigência aqui. Quem decide o que vigora é o instante que o handler
            // recebe — `ListarModulosDaUnidade` aceita `Em`, e uma visão presa a `now()` tornaria
            // "o que estava habilitado no mês passado?" impossível de responder.
            migrationBuilder.Sql("""
                CREATE VIEW vw_unidade_modulo AS
                SELECT um.unidade_id,
                       um.modulo_id,
                       m.codigo,
                       um.vigencia_inicio,
                       um.vigencia_fim
                  FROM unidade_modulo um
                  JOIN modulo m ON m.id = um.modulo_id
                """);

            migrationBuilder.Sql("""
                CREATE VIEW vw_perfil AS
                SELECT p.unidade_id,
                       p.id,
                       p.nome,
                       p.descricao,
                       p.sistema,
                       COALESCE(u.total, 0)::int AS total_de_usuarios
                  FROM perfil p
                  LEFT JOIN LATERAL (
                        SELECT COUNT(*) AS total
                          FROM usuario x
                         WHERE x.perfil_id = p.id AND NOT x.eh_plataforma
                  ) u ON TRUE
                """);

            // ⚠ `unidade_id` vem do PERFIL, não da permissão: `permissao` é catálogo global
            // (SPEC-GER-004) e não tem unidade. Sem esta coluna a visão não seria filtrável, e o
            // administrador de uma casa leria as permissões dos perfis de todas as outras.
            migrationBuilder.Sql("""
                CREATE VIEW vw_perfil_permissao AS
                SELECT p.unidade_id,
                       pp.perfil_id,
                       pe.codigo,
                       pe.descricao
                  FROM perfil_permissao pp
                  JOIN perfil    p  ON p.id  = pp.perfil_id
                  JOIN permissao pe ON pe.id = pp.permissao_id
                """);

            // ⚠⚠ `COALESCE(unidade_id, uuid-zero)`, e a linha merece a explicação: `usuario` é a
            // ÚNICA tabela onde `unidade_id` admite NULL, porque o usuário de PLATAFORMA não
            // pertence a restaurante nenhum (RN-GER-014).
            //
            // O modelo de leitura declara `Guid` e não `Guid?` de propósito: o filtro global do
            // contexto de leitura monta `unidade_id = @atual`, e nulo do lado esquerdo nunca
            // casa — o efeito seria o mesmo, mas a materialização falharia com 500 antes de
            // chegar lá. O zero é sentinela: ele não é igual a nenhuma unidade real, então o
            // usuário de plataforma continua invisível ao restaurante, que é o enunciado de
            // RN-GER-016.
            //
            // ⚠ O zero NÃO vaza para a API: `ListarUsuarios` filtra `NOT eh_plataforma` (toda
            // linha tem unidade de verdade) e `ListarUsuariosDeSuporte` responde um DTO que não
            // tem o campo.
            migrationBuilder.Sql("""
                CREATE VIEW vw_usuario AS
                SELECT COALESCE(u.unidade_id, '00000000-0000-0000-0000-000000000000'::uuid)
                                      AS unidade_id,
                       u.id,
                       u.nome,
                       u.sobrenome,
                       u.cpf,
                       u.email,
                       u.perfil_id,
                       pf.nome        AS perfil_nome,
                       u.ativo,
                       u.deve_trocar_senha,
                       u.eh_plataforma,
                       u.dh_criacao   AS criado_em
                  FROM usuario u
                  LEFT JOIN perfil pf ON pf.id = u.perfil_id
                """);

            // ⚠ `tipo_valor` sai como TEXTO: no banco a coluna é o enum convertido para string
            // (`HasConversion<string>`), e o modelo de leitura não conhece o enum do domínio. O
            // contrato da API sempre foi a string — `TipoValor.ToString()` no handler antigo.
            migrationBuilder.Sql("""
                CREATE VIEW vw_parametro AS
                SELECT p.id, p.codigo, p.descricao, p.tipo_valor, p.dominio
                  FROM parametro p
                """);

            migrationBuilder.Sql("""
                CREATE VIEW vw_parametro_valor AS
                SELECT pv.unidade_id,
                       pv.parametro_id,
                       p.codigo,
                       pv.valor,
                       pv.vigencia_inicio,
                       pv.vigencia_fim
                  FROM parametro_valor pv
                  JOIN parametro p ON p.id = pv.parametro_id
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var visao in new[]
                     {
                         "vw_parametro_valor", "vw_parametro", "vw_usuario", "vw_perfil_permissao",
                         "vw_perfil", "vw_unidade_modulo", "vw_modulo_dependencia", "vw_modulo",
                         "vw_cardapio_produto", "vw_cardapio_categoria", "vw_unidade_horario",
                         "vw_unidade",
                     })
            {
                migrationBuilder.Sql($"DROP VIEW IF EXISTS {visao}");
            }
        }
    }
}
