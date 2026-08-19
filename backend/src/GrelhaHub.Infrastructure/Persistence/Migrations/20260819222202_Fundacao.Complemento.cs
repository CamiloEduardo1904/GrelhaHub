using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A metade da <c>Fundacao</c> que o EF Core não expressa — ADR-0022 §6.
    /// <para>
    /// <b>Por que uma migration só:</b> as 18 anteriores foram substituídas por esta, por
    /// decisão do dono do produto (o projeto não está em produção, e o esquema do Identity
    /// renomeia colunas de <c>usuario</c> e <c>perfil</c> — retrofit seria <c>ALTER</c> sobre
    /// <c>ALTER</c> contando uma história que não aconteceu). ⚠ <b>É a última vez que isso é
    /// barato: a partir do primeiro cliente real, migration se soma, nunca se apaga.</b>
    /// </para>
    /// <para>
    /// O SQL abaixo é o <b>estado final</b> do que as migrations apagadas construíram por
    /// partes: extensão, constraints que o EF não expressa (<c>EXCLUDE</c>, <c>CHECK</c> de
    /// catálogo), seeds de referência (<c>parametro</c>, <c>modulo</c>), a dimensão tempo e as
    /// 17 visões do modelo de leitura (ADR-0019). O que era conserto de dado antigo — backfill
    /// de <c>dh_cancelamento</c>, mapeamento de perfil por inteiro — não veio: num banco que
    /// nasce do zero não há passado a consertar.
    /// </para>
    /// <para>
    /// ⚠ <b>O que NÃO veio, por decisão da ADR-0022 §3:</b> a tabela <c>permissao</c> e os seus
    /// seeds. O <c>CatalogoDeAcesso</c> (código) é a lista do que existe; o banco guarda só o
    /// que foi concedido, como claim de papel em <c>perfil_permissao</c> — semeada por unidade
    /// no cadastro do restaurante, não por migration.
    /// </para>
    /// </summary>
    public partial class Fundacao
    {
        private static void AplicarObjetosDeBanco(MigrationBuilder migrationBuilder)
        {
            // ── Extensão e constraints que o EF Core não expressa ─────────────────────────

            // Exigida pelas EXCLUDE abaixo: elas combinam igualdade (=) com sobreposição de
            // intervalo (&&), e o gist padrão não sabe indexar igualdade de uuid.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist");

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

            // RH tem duas letras no catálogo desde a v0.1; uniformizar quebraria a busca por ID.
            migrationBuilder.Sql(
                "ALTER TABLE modulo ADD CONSTRAINT ck_modulo_codigo CHECK (codigo ~ '^[A-Z]{2,3}$')");

            // RN-GER-020 — a aresta trivial. Ciclo maior é impedido na aplicação: com dez
            // módulos e um grafo escrito à mão, trigger recursiva custaria mais do que resolve.
            migrationBuilder.Sql(
                "ALTER TABLE modulo_dependencia ADD CONSTRAINT ck_modulo_dependencia_ciclo " +
                "CHECK (modulo_id <> depende_de_id)");

            // RN-GER-021 — o padrão de vigência de parametro_valor, segunda aplicação.
            migrationBuilder.Sql("""
                ALTER TABLE unidade_modulo ADD CONSTRAINT ex_unidade_modulo_vigencia
                EXCLUDE USING gist (
                    unidade_id WITH =,
                    modulo_id  WITH =,
                    tstzrange(vigencia_inicio, vigencia_fim) WITH &&
                )
                """);

            migrationBuilder.Sql("COMMENT ON TABLE unidade_modulo IS " +
                "'RN-GER-017/021 — habilitacao com vigencia [inicio, fim). Desabilitar fecha a " +
                "vigencia e NUNCA apaga dado (RN-GER-018).'");

            // ⚠⚠ ADR-0017 — as validações da trilha, na ENTRADA da ponte (outbox).
            //
            // ⚠ A lista veio do ENUM, não da memória: Criacao, Alteracao, Cancelamento e
            // Exclusao. Lixo recusado aqui nunca chega ao banco de log, e o worker não precisa
            // saber o que fazer com uma linha que não consegue copiar — validar só na saída
            // deixaria a outbox entupida com uma linha impossível, bloqueando a fila atrás dela.
            migrationBuilder.Sql(
                "ALTER TABLE outbox_auditoria ADD CONSTRAINT ck_outbox_operacao " +
                "CHECK (operacao IN ('Criacao', 'Alteracao', 'Cancelamento', 'Exclusao'))");

            // RN-GER-011 — registro sem antes nem depois não diz nada.
            migrationBuilder.Sql(
                "ALTER TABLE outbox_auditoria ADD CONSTRAINT ck_outbox_valores " +
                "CHECK (valores_anteriores IS NOT NULL OR valores_novos IS NOT NULL)");

            // ── Seeds de referência ───────────────────────────────────────────────────────

            // Definições dos dez parâmetros de docs/04-parametros-do-sistema.md.
            //
            // Ids fixos e lista escrita à mão: migration é história. Referenciar
            // CatalogoDeParametros faria esta migration passar a semear onze linhas no dia em
            // que alguém acrescentasse o décimo primeiro — reescrevendo o passado.
            //
            // ⚠ PAR-GER-004 e PAR-GER-005 (lockout, RN-GER-040) NÃO entram: escopo global,
            // aplicados por LockoutOptions na partida — mesma decisão de PAR-GER-002/003.
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
                ('0193a000-0001-7000-8000-000000000009', 'PAR-FIS-001', 'Prazo legal para cancelamento fiscal',     'intervalo',  'duração positiva'),
                ('0193a000-0001-7000-8000-000000000010', 'PAR-VEN-004', 'Máximo de produtos ativos por categoria',  'inteiro',    '> 0')
                """);

            // Os dez módulos — RN-GER-017 a RN-GER-021, ADR-0014. Ids fixos, mesma razão acima.
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

            // ── SPEC-GER-002 — dim_tempo, a dimensão do dia operacional ──────────────────
            //
            // ⚠ A hora de virada está cravada em 4 horas aqui, igual ao DEFAULT da coluna
            // gerada: migration é história e não muda de significado porque uma constante do
            // C# mudou. A virada POR CASA (RN-GER-036) vive nas colunas copiadas para a linha
            // do pedido; esta dimensão serve ao módulo analítico (onda 9).
            //
            // Intervalo fixo, de 2024 a 2040: uma definição que dependesse de now() faria a
            // view mudar de conteúdo a cada REFRESH, e o teste de um dia específico passaria a
            // depender da data em que roda.
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW dim_tempo AS
                WITH dias AS (
                    SELECT generate_series(DATE '2024-01-01', DATE '2040-12-31', INTERVAL '1 day')::date AS data_operacional
                )
                SELECT
                    d.data_operacional,

                    -- ISODOW: segunda = 1 ... domingo = 7. `dow` começaria no domingo com 0,
                    -- e a semana brasileira de operação de restaurante começa na segunda.
                    EXTRACT(ISODOW FROM d.data_operacional)::smallint      AS dia_da_semana,

                    -- CASE e não to_char: to_char depende de lc_time do servidor, e o nome do
                    -- dia sairia em inglês num contêiner com locale padrão.
                    CASE EXTRACT(ISODOW FROM d.data_operacional)
                        WHEN 1 THEN 'segunda-feira'
                        WHEN 2 THEN 'terça-feira'
                        WHEN 3 THEN 'quarta-feira'
                        WHEN 4 THEN 'quinta-feira'
                        WHEN 5 THEN 'sexta-feira'
                        WHEN 6 THEN 'sábado'
                        ELSE        'domingo'
                    END                                                     AS nome_dia_semana,

                    EXTRACT(ISODOW FROM d.data_operacional) IN (6, 7)       AS eh_fim_de_semana,

                    EXTRACT(WEEK    FROM d.data_operacional)::smallint      AS semana_do_ano,
                    EXTRACT(MONTH   FROM d.data_operacional)::smallint      AS mes,
                    EXTRACT(QUARTER FROM d.data_operacional)::smallint      AS trimestre,
                    EXTRACT(YEAR    FROM d.data_operacional)::smallint      AS ano,

                    -- Limites exatos do dia operacional, já em UTC.
                    --
                    -- Existem para que consulta por período use índice em dh_criacao sem
                    -- recalcular a virada: `dh_criacao >= inicio_utc AND dh_criacao < fim_utc`.
                    --
                    -- AT TIME ZONE sobre timestamp sem fuso interpreta o valor COMO horário de
                    -- São Paulo e devolve timestamptz — então o deslocamento histórico do dia
                    -- é respeitado, inclusive em datas com horário de verão.
                    ((d.data_operacional + TIME '04:00') AT TIME ZONE 'America/Sao_Paulo')            AS inicio_utc,

                    -- EXCLUSIVO: é o início do dia operacional seguinte.
                    --
                    -- Limite exclusivo de propósito. Um `fim_utc` inclusivo obrigaria a escolher
                    -- entre 03:59:59 e 03:59:59.999999, e a venda registrada na fração restante
                    -- desapareceria da apuração sem ninguém notar.
                    (((d.data_operacional + 1) + TIME '04:00') AT TIME ZONE 'America/Sao_Paulo')     AS fim_utc
                FROM dias d
                """);

            // UNIQUE, e não só um índice: é o que permite REFRESH MATERIALIZED VIEW
            // CONCURRENTLY, que não bloqueia leitura enquanto atualiza.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_dim_tempo ON dim_tempo (data_operacional)");

            migrationBuilder.Sql(
                "CREATE INDEX ix_dim_tempo_intervalo ON dim_tempo (inicio_utc, fim_utc)");

            migrationBuilder.Sql("COMMENT ON MATERIALIZED VIEW dim_tempo IS " +
                "'SPEC-GER-002 / RN-GER-006 — dimensão do dia operacional (04h00 as 03h59). " +
                "fim_utc é EXCLUSIVO. Intervalo 2024-2040; estender exige migration.'");

            CriarVisoesDeLeitura(migrationBuilder);
        }

        /// <summary>
        /// ADR-0019 — as visões do modelo de <b>leitura</b>, no estado final que as migrations
        /// substituídas construíram por partes.
        /// <para>
        /// ⚠⚠ <b>Toda visão que pertence a uma unidade expõe <c>unidade_id</c></b> — é a coluna
        /// que o filtro global do contexto de leitura usa (<c>RN-GER-009</c>). E a exclusão
        /// lógica precisa ser repetida aqui: o filtro global do EF Core não alcança visão — o
        /// <c>WHERE dh_exclusao IS NULL</c> abaixo é o único lugar onde o pedido excluído sai
        /// da leitura.
        /// </para>
        /// </summary>
        private static void CriarVisoesDeLeitura(MigrationBuilder migrationBuilder)
        {
            // RN-VEN-058 ⚠⚠ — com instante e responsável do cancelamento. LEFT JOIN sempre:
            // pedido cancelado sem responsável registrado não pode SUMIR da lista — consulta
            // que perde linha ao ganhar coluna é o defeito mais caro que uma visão pode ter.
            migrationBuilder.Sql("""
                CREATE VIEW vw_pedido_resumo AS
                SELECT p.id,
                       p.unidade_id,
                       p.numero,
                       p.canal,
                       p.status,
                       c.nome                                  AS nome_cliente,
                       c.telefone                              AS telefone_cliente,
                       COALESCE(i.total, 0)::numeric(12,2)     AS total,
                       COALESCE(i.itens, 0)::int               AS quantidade_de_itens,
                       p.dh_criacao                            AS criado_em,
                       p.data_operacional,
                       p.observacao,
                       p.motivo_cancelamento,
                       p.dh_cancelamento                       AS cancelado_em,
                       NULLIF(TRIM(COALESCE(u.nome, '') || ' ' || COALESCE(u.sobrenome, '')), '')
                                                               AS cancelado_por
                  FROM pedido p
                  LEFT JOIN cliente c ON c.id = p.cliente_id
                  LEFT JOIN usuario u ON u.id = p.cancelado_por_usuario_id
                  LEFT JOIN LATERAL (
                        SELECT SUM(ip.preco_praticado * ip.quantidade) AS total,
                               COUNT(*)                                AS itens
                          FROM item_pedido ip
                         WHERE ip.pedido_id = p.id
                  ) i ON TRUE
                 WHERE p.dh_exclusao IS NULL
                """);

            migrationBuilder.Sql("""
                CREATE VIEW vw_pedido_item AS
                SELECT ip.pedido_id,
                       ip.unidade_id,
                       ip.nome_produto,
                       ip.quantidade,
                       ip.preco_praticado,
                       (ip.preco_praticado * ip.quantidade)::numeric(12,2) AS total,
                       ip.observacao
                  FROM item_pedido ip
                  JOIN pedido p ON p.id = ip.pedido_id
                 WHERE p.dh_exclusao IS NULL
                """);

            // RN-VEN-049 ⚠⚠ — a regra de O QUE CONTA COMO FATURAMENTO mora aqui, e é o ponto
            // todo desta visão: faturamento é só `Finalizado` (6). O que ainda pode ser
            // cancelado — confirmado (2), em produção (3), pronto (4), entregue (5) — vai em
            // coluna separada, porque número que encolhe sozinho depois destrói a confiança no
            // painel inteiro. Cancelado é 99.
            migrationBuilder.Sql("""
                CREATE VIEW vw_operacao_dia AS
                SELECT p.unidade_id,
                       p.data_operacional,
                       COALESCE(SUM(t.total) FILTER (WHERE p.status = 6), 0)::numeric(12,2)
                            AS faturamento,
                       COUNT(*) FILTER (WHERE p.status = 6)::int
                            AS pedidos_finalizados,
                       COUNT(*) FILTER (WHERE p.status BETWEEN 2 AND 5)::int
                            AS pedidos_em_andamento,
                       COALESCE(SUM(t.total) FILTER (WHERE p.status BETWEEN 2 AND 5), 0)::numeric(12,2)
                            AS valor_em_andamento,
                       COUNT(*) FILTER (WHERE p.status = 99)::int
                            AS pedidos_cancelados
                  FROM pedido p
                  LEFT JOIN LATERAL (
                        SELECT SUM(ip.preco_praticado * ip.quantidade) AS total
                          FROM item_pedido ip
                         WHERE ip.pedido_id = p.id
                  ) t ON TRUE
                 WHERE p.dh_exclusao IS NULL
                 GROUP BY p.unidade_id, p.data_operacional
                """);

            // ⚠ Agrupa por `nome_produto`, a CÓPIA feita na venda (RN-VEN-008) — não por
            // `produto_id`. Produto renomeado depois não reescreve o que foi vendido, pela
            // mesma razão de RN-GER-036 não reclassificar o passado.
            migrationBuilder.Sql("""
                CREATE VIEW vw_produto_vendido AS
                SELECT p.unidade_id,
                       p.data_operacional,
                       ip.nome_produto,
                       SUM(ip.quantidade)::int                                  AS quantidade,
                       SUM(ip.preco_praticado * ip.quantidade)::numeric(12,2)   AS total
                  FROM item_pedido ip
                  JOIN pedido p ON p.id = ip.pedido_id
                 WHERE p.dh_exclusao IS NULL
                   AND p.status = 6
                 GROUP BY p.unidade_id, p.data_operacional, ip.nome_produto
                """);

            // ADR-0017 + ADR-0019 — a trilha vive noutro banco; o nome do autor, não.
            //
            // ⚠ SEM unidade_id: o autor pode ser usuário de PLATAFORMA, que não tem unidade
            // (RN-GER-014). Recortar aqui exibiria um traço no lugar do nome de quem mexeu,
            // justamente no caso que mais importa auditar.
            migrationBuilder.Sql("CREATE VIEW vw_autor AS SELECT u.id, u.nome FROM usuario u");

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

            // RN-GER-020 — a aresta já com as duas siglas resolvidas, para o handler não
            // precisar de um dicionário em memória só para descobrir o código do dependente.
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

            // ⚠ SEM filtro de vigência aqui. Quem decide o que vigora é o instante que o
            // handler recebe — uma visão presa a `now()` tornaria "o que estava habilitado no
            // mês passado?" impossível de responder.
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

            // ⚠ ADR-0022 — `unidade_id` do perfil admite NULL desde o Identity (perfil da
            // própria plataforma, porta aberta pela ADR). O COALESCE para o uuid zero é o mesmo
            // sentinela de vw_usuario: o modelo de leitura declara Guid não nulo, o filtro
            // global compara com a unidade atual, e zero não é igual a unidade nenhuma — perfil
            // de plataforma fica invisível ao restaurante, visível à plataforma (que atravessa
            // o filtro).
            migrationBuilder.Sql("""
                CREATE VIEW vw_perfil AS
                SELECT COALESCE(p.unidade_id, '00000000-0000-0000-0000-000000000000'::uuid)
                                      AS unidade_id,
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

            // ADR-0022 §3 — a concessão agora é claim de papel: `perfil_permissao.valor` É o
            // código `recurso.acao`. Sem coluna de descrição, de propósito: a lista do que
            // existe (com o texto para gente) é o CatalogoDeAcesso, em código — o handler
            // resolve a descrição de lá.
            //
            // ⚠ `unidade_id` vem do PERFIL: a claim não tem unidade. Sem esta coluna a visão
            // não seria filtrável, e o administrador de uma casa leria as permissões dos
            // perfis de todas as outras.
            migrationBuilder.Sql("""
                CREATE VIEW vw_perfil_permissao AS
                SELECT COALESCE(p.unidade_id, '00000000-0000-0000-0000-000000000000'::uuid)
                                      AS unidade_id,
                       rc.perfil_id,
                       rc.valor       AS codigo
                  FROM perfil_permissao rc
                  JOIN perfil p ON p.id = rc.perfil_id
                 WHERE rc.tipo = 'permissao'
                """);

            // ⚠⚠ `COALESCE(unidade_id, uuid-zero)`, e a linha merece a explicação: `usuario` é
            // tabela onde `unidade_id` admite NULL, porque o usuário de PLATAFORMA não pertence
            // a restaurante nenhum (RN-GER-014).
            //
            // O modelo de leitura declara `Guid` e não `Guid?` de propósito: o filtro global do
            // contexto de leitura monta `unidade_id = @atual`, e nulo do lado esquerdo nunca
            // casa — o efeito seria o mesmo, mas a materialização falharia com 500 antes de
            // chegar lá. O zero é sentinela: ele não é igual a nenhuma unidade real, então o
            // usuário de plataforma continua invisível ao restaurante (RN-GER-016).
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
            // (`HasConversion<string>`), e o modelo de leitura não conhece o enum do domínio.
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

        private static void RemoverObjetosDeBanco(MigrationBuilder migrationBuilder)
        {
            foreach (var visao in new[]
                     {
                         "vw_parametro_valor", "vw_parametro", "vw_usuario", "vw_perfil_permissao",
                         "vw_perfil", "vw_unidade_modulo", "vw_modulo_dependencia", "vw_modulo",
                         "vw_cardapio_produto", "vw_cardapio_categoria", "vw_unidade_horario",
                         "vw_unidade", "vw_autor", "vw_produto_vendido", "vw_operacao_dia",
                         "vw_pedido_item", "vw_pedido_resumo",
                     })
            {
                migrationBuilder.Sql($"DROP VIEW IF EXISTS {visao}");
            }

            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS dim_tempo");
        }
    }
}
