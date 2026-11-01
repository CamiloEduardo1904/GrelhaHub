CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;
CREATE TABLE arquivo_binario (
    id uuid NOT NULL,
    conteudo bytea NOT NULL,
    tipo_mime character varying(100) NOT NULL,
    CONSTRAINT pk_arquivo_binario PRIMARY KEY (id)
);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260818220335_EsquemaDeArquivo', '10.0.4');

COMMIT;

