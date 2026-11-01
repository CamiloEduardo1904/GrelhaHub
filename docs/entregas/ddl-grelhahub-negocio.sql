-- =====================================================================================
-- GrelhaHub — DDL do banco de NEGÓCIO (grelhahub)
-- Gerado das migrations do EF Core (dotnet ef migrations script --idempotent), 31/10/2026.
-- Contexto: GrelhaHubDbContext. 22 tabelas, 16 CHECK, 16 índices únicos, 2 EXCLUDE,
-- 1 coluna gerada (data_operacional) e 2 gatilhos. Idempotente: cada comando roda só se
-- a sua migration ainda não foi aplicada (bloco DO $EF$ ... IF NOT EXISTS ...).
-- Pré-requisito: role, banco e extensão btree_gist — ver scripts/preparar-banco.sql.
-- Trechos comentados (tabela independente, dependente, associativa N:N, restrições):
--   docs/entregas/recorte-ddl-A-B-C-D.md
-- Os outros dois bancos (ADR-0017): ddl-grelhahub-log.sql e ddl-grelhahub-arquivo.sql.
-- =====================================================================================

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE arquivo (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        pasta character varying(30) NOT NULL,
        tipo_mime character varying(100) NOT NULL,
        tamanho_em_bytes integer NOT NULL,
        dh_criacao timestamp with time zone NOT NULL,
        dh_exclusao timestamp with time zone,
        CONSTRAINT pk_arquivo PRIMARY KEY (id),
        CONSTRAINT ck_arquivo_tamanho CHECK (tamanho_em_bytes > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE modulo (
        id uuid NOT NULL,
        codigo character varying(3) NOT NULL,
        nome character varying(60) NOT NULL,
        nucleo boolean NOT NULL DEFAULT FALSE,
        ordem smallint NOT NULL,
        CONSTRAINT pk_modulo PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE outbox_auditoria (
        id uuid NOT NULL,
        unidade_id uuid,
        entidade character varying(100) NOT NULL,
        registro_id uuid NOT NULL,
        operacao character varying(20) NOT NULL,
        valores_anteriores jsonb,
        valores_novos jsonb,
        justificativa character varying(500),
        usuario_id uuid,
        dh_ocorrencia timestamp with time zone NOT NULL,
        data_operacional date NOT NULL,
        dh_enfileiramento timestamp with time zone NOT NULL,
        tentativas integer NOT NULL DEFAULT 0,
        ultimo_erro character varying(500),
        CONSTRAINT pk_outbox_auditoria PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE parametro (
        id uuid NOT NULL,
        codigo character varying(20) NOT NULL,
        descricao character varying(200) NOT NULL,
        tipo_valor character varying(20) NOT NULL,
        dominio character varying(100),
        CONSTRAINT pk_parametro PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE unidade (
        id uuid NOT NULL,
        razao_social character varying(200) NOT NULL,
        nome_fantasia character varying(200) NOT NULL,
        cnpj character varying(14) NOT NULL,
        slug character varying(60) NOT NULL,
        telefone character varying(11),
        ativa boolean NOT NULL DEFAULT TRUE,
        dh_criacao timestamp with time zone NOT NULL,
        fuso_horario character varying(50) NOT NULL DEFAULT 'America/Sao_Paulo',
        cep character varying(8),
        logradouro character varying(200),
        numero_endereco character varying(20),
        complemento character varying(100),
        bairro character varying(100),
        cidade character varying(100),
        uf character(2),
        hora_virada_operacional interval NOT NULL DEFAULT (interval '4 hours'),
        url_logo character varying(500),
        CONSTRAINT pk_unidade PRIMARY KEY (id),
        CONSTRAINT ck_unidade_cnpj CHECK (cnpj ~ '^[0-9]{14}$'),
        CONSTRAINT ck_unidade_razao_social CHECK (length(trim(razao_social)) > 0),
        CONSTRAINT ck_unidade_slug CHECK (slug ~ '^[a-z0-9-]{3,60}$')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE modulo_dependencia (
        modulo_id uuid NOT NULL,
        depende_de_id uuid NOT NULL,
        CONSTRAINT pk_modulo_dependencia PRIMARY KEY (modulo_id, depende_de_id),
        CONSTRAINT fk_modulo_dependencia_modulo_depende_de_id FOREIGN KEY (depende_de_id) REFERENCES modulo (id) ON DELETE RESTRICT,
        CONSTRAINT fk_modulo_dependencia_modulo_modulo_id FOREIGN KEY (modulo_id) REFERENCES modulo (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE categoria (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        nome character varying(200) NOT NULL,
        ordem integer NOT NULL,
        ativa boolean NOT NULL,
        CONSTRAINT pk_categoria PRIMARY KEY (id),
        CONSTRAINT fk_categoria_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE cliente (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        nome character varying(200) NOT NULL,
        telefone character varying(11) NOT NULL,
        dh_criacao timestamp with time zone NOT NULL,
        CONSTRAINT pk_cliente PRIMARY KEY (id),
        CONSTRAINT fk_cliente_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE perfil (
        id uuid NOT NULL,
        unidade_id uuid,
        descricao character varying(200) NOT NULL,
        sistema boolean NOT NULL DEFAULT FALSE,
        nome character varying(60) NOT NULL,
        nome_normalizado character varying(60) NOT NULL,
        carimbo_de_concorrencia character varying(200),
        CONSTRAINT pk_perfil PRIMARY KEY (id),
        CONSTRAINT fk_perfil_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE unidade_horario (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        dia_semana integer NOT NULL,
        abre time,
        fecha time,
        CONSTRAINT pk_unidade_horario PRIMARY KEY (id),
        CONSTRAINT ck_unidade_horario_par CHECK ((abre IS NULL AND fecha IS NULL) OR (abre IS NOT NULL AND fecha IS NOT NULL)),
        CONSTRAINT fk_unidade_horario_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE produto (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        categoria_id uuid NOT NULL,
        nome character varying(200) NOT NULL,
        descricao character varying(500),
        preco numeric(12,2) NOT NULL,
        ativo boolean NOT NULL,
        disponivel boolean NOT NULL,
        url_imagem character varying(500),
        CONSTRAINT pk_produto PRIMARY KEY (id),
        CONSTRAINT ck_produto_preco CHECK (preco > 0),
        CONSTRAINT fk_produto_categoria_categoria_id FOREIGN KEY (categoria_id) REFERENCES categoria (id) ON DELETE RESTRICT,
        CONSTRAINT fk_produto_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE perfil_permissao (
        id integer GENERATED BY DEFAULT AS IDENTITY,
        perfil_id uuid NOT NULL,
        tipo character varying(60),
        valor character varying(200),
        CONSTRAINT pk_perfil_permissao PRIMARY KEY (id),
        CONSTRAINT fk_perfil_permissao_perfil FOREIGN KEY (perfil_id) REFERENCES perfil (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE usuario (
        id uuid NOT NULL,
        unidade_id uuid,
        eh_plataforma boolean NOT NULL DEFAULT FALSE,
        nome character varying(200) NOT NULL,
        sobrenome character varying(200),
        cpf char(11),
        email character varying(256) NOT NULL,
        senha_hash character varying(500) NOT NULL,
        carimbo_de_seguranca character varying(64) NOT NULL,
        carimbo_de_concorrencia character varying(200),
        perfil_id uuid,
        deve_trocar_senha boolean NOT NULL DEFAULT TRUE,
        ativo boolean NOT NULL,
        dh_criacao timestamp with time zone NOT NULL,
        criado_por uuid,
        nome_de_usuario character varying(256) NOT NULL,
        nome_de_usuario_normalizado character varying(256) NOT NULL,
        email_normalizado character varying(256) NOT NULL,
        email_confirmado boolean NOT NULL,
        telefone character varying(20),
        telefone_confirmado boolean NOT NULL,
        dois_fatores_habilitado boolean NOT NULL,
        bloqueado_ate timestamp with time zone,
        bloqueio_habilitado boolean NOT NULL,
        falhas_de_acesso integer NOT NULL,
        CONSTRAINT pk_usuario PRIMARY KEY (id),
        CONSTRAINT ck_usuario_cpf CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$'),
        CONSTRAINT fk_usuario_criado_por FOREIGN KEY (criado_por) REFERENCES usuario (id) ON DELETE RESTRICT,
        CONSTRAINT fk_usuario_perfil_perfil_id FOREIGN KEY (perfil_id) REFERENCES perfil (id) ON DELETE RESTRICT,
        CONSTRAINT fk_usuario_unidade_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE parametro_valor (
        id uuid NOT NULL,
        parametro_id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        valor character varying(50) NOT NULL,
        vigencia_inicio timestamp with time zone NOT NULL,
        vigencia_fim timestamp with time zone,
        justificativa character varying(500),
        dh_criacao timestamp with time zone NOT NULL,
        criado_por uuid,
        CONSTRAINT pk_parametro_valor PRIMARY KEY (id),
        CONSTRAINT ck_parametro_valor_vigencia CHECK (vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio),
        CONSTRAINT fk_parametro_valor_criado_por FOREIGN KEY (criado_por) REFERENCES usuario (id) ON DELETE RESTRICT,
        CONSTRAINT fk_parametro_valor_parametro_parametro_id FOREIGN KEY (parametro_id) REFERENCES parametro (id) ON DELETE RESTRICT,
        CONSTRAINT fk_parametro_valor_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE pedido (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        hora_virada_operacional interval NOT NULL DEFAULT (interval '4 hours'),
        fuso_operacional character varying(50) NOT NULL DEFAULT 'America/Sao_Paulo',
        numero integer NOT NULL,
        canal integer NOT NULL,
        status integer NOT NULL,
        cliente_id uuid,
        observacao character varying(500),
        motivo_cancelamento character varying(500),
        dh_cancelamento timestamp with time zone,
        cancelado_por_usuario_id uuid,
        dh_criacao timestamp with time zone NOT NULL,
        criado_por uuid,
        dh_confirmacao timestamp with time zone,
        dh_finalizacao timestamp with time zone,
        dh_exclusao timestamp with time zone,
        excluido_por_usuario_id uuid,
        motivo_exclusao character varying(500),
        data_operacional date GENERATED ALWAYS AS ((((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date)) STORED NOT NULL,
        CONSTRAINT pk_pedido PRIMARY KEY (id),
        CONSTRAINT fk_pedido_cancelado_por FOREIGN KEY (cancelado_por_usuario_id) REFERENCES usuario (id) ON DELETE RESTRICT,
        CONSTRAINT fk_pedido_cliente_cliente_id FOREIGN KEY (cliente_id) REFERENCES cliente (id) ON DELETE RESTRICT,
        CONSTRAINT fk_pedido_criado_por FOREIGN KEY (criado_por) REFERENCES usuario (id) ON DELETE RESTRICT,
        CONSTRAINT fk_pedido_excluido_por FOREIGN KEY (excluido_por_usuario_id) REFERENCES usuario (id) ON DELETE RESTRICT,
        CONSTRAINT fk_pedido_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE token_acesso (
        id uuid NOT NULL,
        usuario_id uuid NOT NULL,
        tipo character varying(30) NOT NULL,
        hash_token character varying(128) NOT NULL,
        dh_expiracao timestamp with time zone NOT NULL,
        dh_criacao timestamp with time zone NOT NULL,
        dh_uso timestamp with time zone,
        dh_invalidacao timestamp with time zone,
        CONSTRAINT pk_token_acesso PRIMARY KEY (id),
        CONSTRAINT fk_token_acesso_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE unidade_modulo (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        modulo_id uuid NOT NULL,
        vigencia_inicio timestamp with time zone NOT NULL,
        vigencia_fim timestamp with time zone,
        justificativa character varying(500),
        dh_criacao timestamp with time zone NOT NULL,
        criado_por uuid,
        CONSTRAINT pk_unidade_modulo PRIMARY KEY (id),
        CONSTRAINT ck_unidade_modulo_vigencia CHECK (vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio),
        CONSTRAINT fk_unidade_modulo_criado_por FOREIGN KEY (criado_por) REFERENCES usuario (id) ON DELETE RESTRICT,
        CONSTRAINT fk_unidade_modulo_modulo_modulo_id FOREIGN KEY (modulo_id) REFERENCES modulo (id) ON DELETE RESTRICT,
        CONSTRAINT fk_unidade_modulo_unidade_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE usuario_claim (
        id integer GENERATED BY DEFAULT AS IDENTITY,
        usuario_id uuid NOT NULL,
        tipo character varying(60),
        valor character varying(200),
        CONSTRAINT pk_usuario_claim PRIMARY KEY (id),
        CONSTRAINT fk_usuario_claim_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE usuario_login (
        provedor character varying(128) NOT NULL,
        chave_no_provedor character varying(128) NOT NULL,
        nome_do_provedor character varying(200),
        usuario_id uuid NOT NULL,
        CONSTRAINT pk_usuario_login PRIMARY KEY (provedor, chave_no_provedor),
        CONSTRAINT fk_usuario_login_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE usuario_perfil (
        usuario_id uuid NOT NULL,
        perfil_id uuid NOT NULL,
        CONSTRAINT pk_usuario_perfil PRIMARY KEY (usuario_id, perfil_id),
        CONSTRAINT fk_usuario_perfil_perfil FOREIGN KEY (perfil_id) REFERENCES perfil (id) ON DELETE CASCADE,
        CONSTRAINT fk_usuario_perfil_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE usuario_token (
        usuario_id uuid NOT NULL,
        provedor character varying(128) NOT NULL,
        nome character varying(128) NOT NULL,
        valor character varying(200),
        CONSTRAINT pk_usuario_token PRIMARY KEY (usuario_id, provedor, nome),
        CONSTRAINT fk_usuario_token_usuario FOREIGN KEY (usuario_id) REFERENCES usuario (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE TABLE item_pedido (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        pedido_id uuid NOT NULL,
        produto_id uuid NOT NULL,
        preco_praticado numeric(12,2) NOT NULL,
        nome_produto character varying(200) NOT NULL,
        quantidade integer NOT NULL,
        observacao character varying(300),
        CONSTRAINT pk_item_pedido PRIMARY KEY (id),
        CONSTRAINT ck_item_preco CHECK (preco_praticado >= 0),
        CONSTRAINT ck_item_quantidade CHECK (quantidade > 0),
        CONSTRAINT fk_item_pedido_pedidos_pedido_id FOREIGN KEY (pedido_id) REFERENCES pedido (id) ON DELETE RESTRICT,
        CONSTRAINT fk_item_pedido_produtos_produto_id FOREIGN KEY (produto_id) REFERENCES produto (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_arquivo_unidade_pasta ON arquivo (unidade_id, pasta);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_categoria_nome ON categoria (unidade_id, nome);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_cliente_telefone ON cliente (unidade_id, telefone);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_item_pedido ON item_pedido (unidade_id, pedido_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_item_pedido_pedido_id ON item_pedido (pedido_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_item_pedido_produto_id ON item_pedido (produto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_modulo_codigo ON modulo (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_modulo_dependencia_depende_de_id ON modulo_dependencia (depende_de_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_outbox_auditoria_fila ON outbox_auditoria (dh_enfileiramento);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_parametro_codigo ON parametro (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_parametro_valor_busca ON parametro_valor (unidade_id, parametro_id, vigencia_inicio DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_parametro_valor_criado_por ON parametro_valor (criado_por);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_parametro_valor_parametro_id ON parametro_valor (parametro_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_pedido_cancelado_por_usuario_id ON pedido (cancelado_por_usuario_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_pedido_cliente_id ON pedido (cliente_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_pedido_criado_por ON pedido (criado_por);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_pedido_dia_status ON pedido (unidade_id, data_operacional, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_pedido_excluido_por_usuario_id ON pedido (excluido_por_usuario_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_pedido_numero ON pedido (unidade_id, data_operacional, numero);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_perfil_nome ON perfil (unidade_id, nome_normalizado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_perfil_permissao_perfil ON perfil_permissao (perfil_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_perfil_permissao ON perfil_permissao (perfil_id, tipo, valor);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_produto_categoria ON produto (unidade_id, categoria_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_produto_categoria_id ON produto (categoria_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_produto_nome ON produto (unidade_id, nome);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_token_acesso_vigente ON token_acesso (usuario_id, tipo, dh_uso, dh_invalidacao);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX ux_token_acesso_hash ON token_acesso (hash_token);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_unidade_cnpj ON unidade (cnpj);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_unidade_slug ON unidade (slug);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_unidade_horario_dia ON unidade_horario (unidade_id, dia_semana);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_unidade_modulo_busca ON unidade_modulo (unidade_id, modulo_id, vigencia_inicio DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_unidade_modulo_criado_por ON unidade_modulo (criado_por);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_unidade_modulo_modulo_id ON unidade_modulo (modulo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_usuario_criado_por ON usuario (criado_por);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_usuario_email_normalizado ON usuario (email_normalizado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_usuario_perfil_id ON usuario (perfil_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_usuario_unidade_id ON usuario (unidade_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_usuario_cpf ON usuario (cpf);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_usuario_email ON usuario (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_usuario_nome_de_usuario ON usuario (nome_de_usuario_normalizado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_usuario_claim_usuario ON usuario_claim (usuario_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_usuario_login_usuario ON usuario_login (usuario_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_usuario_perfil_perfil ON usuario_perfil (perfil_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE EXTENSION IF NOT EXISTS btree_gist
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    ALTER TABLE parametro ADD CONSTRAINT ck_parametro_tipo CHECK (
        tipo_valor IN ('percentual','dinheiro','dias','tempo','intervalo','enum','inteiro','fator')
    )
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    ALTER TABLE parametro_valor ADD CONSTRAINT ex_parametro_valor_vigencia
    EXCLUDE USING gist (
        parametro_id WITH =,
        unidade_id   WITH =,
        tstzrange(vigencia_inicio, vigencia_fim) WITH &&
    )
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    ALTER TABLE modulo ADD CONSTRAINT ck_modulo_codigo CHECK (codigo ~ '^[A-Z]{2,3}$')
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    ALTER TABLE modulo_dependencia ADD CONSTRAINT ck_modulo_dependencia_ciclo CHECK (modulo_id <> depende_de_id)
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    ALTER TABLE unidade_modulo ADD CONSTRAINT ex_unidade_modulo_vigencia
    EXCLUDE USING gist (
        unidade_id WITH =,
        modulo_id  WITH =,
        tstzrange(vigencia_inicio, vigencia_fim) WITH &&
    )
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    COMMENT ON TABLE unidade_modulo IS 'RN-GER-017/021 — habilitacao com vigencia [inicio, fim). Desabilitar fecha a vigencia e NUNCA apaga dado (RN-GER-018).'
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    ALTER TABLE outbox_auditoria ADD CONSTRAINT ck_outbox_operacao CHECK (operacao IN ('Criacao', 'Alteracao', 'Cancelamento', 'Exclusao'))
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    ALTER TABLE outbox_auditoria ADD CONSTRAINT ck_outbox_valores CHECK (valores_anteriores IS NOT NULL OR valores_novos IS NOT NULL)
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE UNIQUE INDEX uq_dim_tempo ON dim_tempo (data_operacional)
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE INDEX ix_dim_tempo_intervalo ON dim_tempo (inicio_utc, fim_utc)
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    COMMENT ON MATERIALIZED VIEW dim_tempo IS 'SPEC-GER-002 / RN-GER-006 — dimensão do dia operacional (04h00 as 03h59). fim_utc é EXCLUSIVO. Intervalo 2024-2040; estender exige migration.'
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE VIEW vw_autor AS SELECT u.id, u.nome FROM usuario u
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE VIEW vw_unidade_horario AS
    SELECT h.unidade_id,
           h.dia_semana AS dia_da_semana,
           h.abre,
           h.fecha
      FROM unidade_horario h
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE VIEW vw_modulo AS
    SELECT m.id, m.codigo, m.nome, m.nucleo, m.ordem
      FROM modulo m
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE VIEW vw_modulo_dependencia AS
    SELECT d.modulo_id,
           m.codigo             AS codigo,
           d.depende_de_id,
           dep.codigo           AS depende_de_codigo
      FROM modulo_dependencia d
      JOIN modulo m   ON m.id = d.modulo_id
      JOIN modulo dep ON dep.id = d.depende_de_id
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE VIEW vw_unidade_modulo AS
    SELECT um.unidade_id,
           um.modulo_id,
           m.codigo,
           um.vigencia_inicio,
           um.vigencia_fim
      FROM unidade_modulo um
      JOIN modulo m ON m.id = um.modulo_id
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE VIEW vw_perfil_permissao AS
    SELECT COALESCE(p.unidade_id, '00000000-0000-0000-0000-000000000000'::uuid)
                          AS unidade_id,
           rc.perfil_id,
           rc.valor       AS codigo
      FROM perfil_permissao rc
      JOIN perfil p ON p.id = rc.perfil_id
     WHERE rc.tipo = 'permissao'
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE VIEW vw_parametro AS
    SELECT p.id, p.codigo, p.descricao, p.tipo_valor, p.dominio
      FROM parametro p
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    CREATE VIEW vw_parametro_valor AS
    SELECT pv.unidade_id,
           pv.parametro_id,
           p.codigo,
           pv.valor,
           pv.vigencia_inicio,
           pv.vigencia_fim
      FROM parametro_valor pv
      JOIN parametro p ON p.id = pv.parametro_id
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260819222202_Fundacao') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260819222202_Fundacao', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE TABLE conta_receber (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        pedido_id uuid NOT NULL,
        valor numeric(12,2) NOT NULL,
        hora_virada_operacional interval NOT NULL DEFAULT (interval '4 hours'),
        fuso_operacional character varying(50) NOT NULL DEFAULT 'America/Sao_Paulo',
        dh_criacao timestamp with time zone NOT NULL,
        criado_por uuid,
        data_operacional date GENERATED ALWAYS AS ((((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date)) STORED NOT NULL,
        CONSTRAINT pk_conta_receber PRIMARY KEY (id),
        CONSTRAINT ck_conta_receber_valor CHECK (valor > 0),
        CONSTRAINT fk_conta_receber_criado_por FOREIGN KEY (criado_por) REFERENCES usuario (id) ON DELETE RESTRICT,
        CONSTRAINT fk_conta_receber_pedido FOREIGN KEY (pedido_id) REFERENCES pedido (id) ON DELETE RESTRICT,
        CONSTRAINT fk_conta_receber_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE TABLE recebimento (
        id uuid NOT NULL,
        unidade_id uuid NOT NULL,
        conta_receber_id uuid NOT NULL,
        tipo integer NOT NULL,
        valor numeric(12,2) NOT NULL,
        forma_pagamento integer NOT NULL,
        hora_virada_operacional interval NOT NULL DEFAULT (interval '4 hours'),
        fuso_operacional character varying(50) NOT NULL DEFAULT 'America/Sao_Paulo',
        dh_criacao timestamp with time zone NOT NULL,
        criado_por uuid,
        data_operacional date GENERATED ALWAYS AS ((((dh_criacao AT TIME ZONE fuso_operacional) - hora_virada_operacional)::date)) STORED NOT NULL,
        CONSTRAINT pk_recebimento PRIMARY KEY (id),
        CONSTRAINT ck_recebimento_valor CHECK (valor > 0),
        CONSTRAINT fk_recebimento_conta_receber_conta_receber_id FOREIGN KEY (conta_receber_id) REFERENCES conta_receber (id) ON DELETE RESTRICT,
        CONSTRAINT fk_recebimento_criado_por FOREIGN KEY (criado_por) REFERENCES usuario (id) ON DELETE RESTRICT,
        CONSTRAINT fk_recebimento_unidades_unidade_id FOREIGN KEY (unidade_id) REFERENCES unidade (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE INDEX ix_conta_receber_criado_por ON conta_receber (criado_por);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE INDEX ix_conta_receber_dia ON conta_receber (unidade_id, data_operacional);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE INDEX ix_conta_receber_pedido_id ON conta_receber (pedido_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE UNIQUE INDEX uq_conta_receber_pedido ON conta_receber (unidade_id, pedido_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE INDEX ix_recebimento_conta ON recebimento (unidade_id, conta_receber_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE INDEX ix_recebimento_conta_receber_id ON recebimento (conta_receber_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE INDEX ix_recebimento_criado_por ON recebimento (criado_por);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE INDEX ix_recebimento_dia ON recebimento (unidade_id, data_operacional);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE VIEW vw_conta_receber AS
    SELECT cr.unidade_id,
           cr.id,
           cr.pedido_id,
           p.numero                                         AS pedido_numero,
           cr.valor,
           COALESCE(r.recebido, 0)::numeric(12,2)           AS recebido,
           (cr.valor - COALESCE(r.recebido, 0))::numeric(12,2) AS saldo,
           CASE
               WHEN COALESCE(r.recebido, 0) <= 0        THEN 'Aberta'
               WHEN COALESCE(r.recebido, 0) >= cr.valor THEN 'Quitada'
               ELSE                                          'Parcial'
           END                                              AS situacao,
           cr.data_operacional
      FROM conta_receber cr
      JOIN pedido p ON p.id = cr.pedido_id
      LEFT JOIN LATERAL (
            SELECT SUM(CASE WHEN rc.tipo = 1 THEN rc.valor ELSE -rc.valor END) AS recebido
              FROM recebimento rc
             WHERE rc.conta_receber_id = cr.id
      ) r ON TRUE
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE VIEW vw_pedido_pagamento AS
    SELECT cr.unidade_id,
           cr.pedido_id,
           CASE
               WHEN COALESCE(r.recebido, 0) <= 0        THEN 'Pendente'
               WHEN COALESCE(r.recebido, 0) >= cr.valor THEN 'Pago'
               ELSE                                          'Parcial'
           END AS status_pagamento
      FROM conta_receber cr
      LEFT JOIN LATERAL (
            SELECT SUM(CASE WHEN rc.tipo = 1 THEN rc.valor ELSE -rc.valor END) AS recebido
              FROM recebimento rc
             WHERE rc.conta_receber_id = cr.id
      ) r ON TRUE
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    CREATE VIEW vw_fluxo_caixa AS
    SELECT r.unidade_id,
           r.data_operacional,
           SUM(CASE WHEN r.tipo = 1 THEN r.valor ELSE -r.valor END)::numeric(12,2) AS total_recebido,
           0::numeric(12,2)                                                        AS total_pago,
           SUM(CASE WHEN r.tipo = 1 THEN r.valor ELSE -r.valor END)::numeric(12,2) AS saldo_liquido
      FROM recebimento r
     GROUP BY r.unidade_id, r.data_operacional
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261025232207_Financeiro') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261025232207_Financeiro', '10.0.4');
    END IF;
END $EF$;
COMMIT;

