-- =====================================================================================
-- GrelhaHub — DDL do banco de ARQUIVO (grelhahub_arquivo) — SIMPLES e LIMPA (ADR-0018)
-- Uma tabela: arquivo_binario (os bytes). O id e o MESMO do metadado no banco de negocio.
-- Rastro do EF removido. DDL puro.
-- =====================================================================================

CREATE TABLE arquivo_binario (
    id uuid NOT NULL,
    conteudo bytea NOT NULL,
    tipo_mime character varying(100) NOT NULL,
    CONSTRAINT pk_arquivo_binario PRIMARY KEY (id)
);



