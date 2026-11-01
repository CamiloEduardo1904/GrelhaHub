CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260818220335_EsquemaDeArquivo') THEN
    CREATE TABLE arquivo_binario (
        id uuid NOT NULL,
        conteudo bytea NOT NULL,
        tipo_mime character varying(100) NOT NULL,
        CONSTRAINT pk_arquivo_binario PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260818220335_EsquemaDeArquivo') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260818220335_EsquemaDeArquivo', '10.0.4');
    END IF;
END $EF$;
COMMIT;

