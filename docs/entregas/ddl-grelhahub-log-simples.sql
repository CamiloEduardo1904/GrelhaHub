-- =====================================================================================
-- GrelhaHub — DDL do banco de LOG (grelhahub_log) — versão SIMPLES e LIMPA (ADR-0017)
-- Trilha de auditoria + log de excecoes. Rastro do EF removido. DDL puro.
-- Inclui os gatilhos de imutabilidade da trilha (recusam UPDATE/DELETE/TRUNCATE).
-- =====================================================================================

CREATE TABLE log_auditoria (
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
    CONSTRAINT pk_log_auditoria PRIMARY KEY (id)
);

CREATE TABLE log_excecao (
    id uuid NOT NULL,
    tipo character varying(200) NOT NULL,
    mensagem character varying(2000) NOT NULL,
    pilha text,
    rota character varying(500),
    metodo character varying(10),
    status_http integer,
    unidade_id uuid,
    usuario_id uuid,
    correlacao character varying(100),
    dh_ocorrencia timestamp with time zone NOT NULL,
    CONSTRAINT pk_log_excecao PRIMARY KEY (id)
);

CREATE INDEX ix_log_auditoria_autor ON log_auditoria (unidade_id, usuario_id, data_operacional);

CREATE INDEX ix_log_auditoria_registro ON log_auditoria (unidade_id, entidade, registro_id, dh_ocorrencia);

CREATE INDEX ix_log_excecao_correlacao ON log_excecao (correlacao);

CREATE INDEX ix_log_excecao_tipo ON log_excecao (tipo, dh_ocorrencia);



CREATE OR REPLACE FUNCTION fn_log_auditoria_imutavel()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'log_auditoria e imutavel — % recusado. '
        'A trilha admite apenas INSERT e SELECT.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$

CREATE TRIGGER trg_log_auditoria_imutavel
BEFORE UPDATE OR DELETE ON log_auditoria
FOR EACH ROW
EXECUTE FUNCTION fn_log_auditoria_imutavel()

CREATE TRIGGER trg_log_auditoria_sem_truncate
BEFORE TRUNCATE ON log_auditoria
FOR EACH STATEMENT
EXECUTE FUNCTION fn_log_auditoria_imutavel()



