-- Prepara o banco de desenvolvimento do GrelhaHub.
--
-- Rode uma vez, como superusuário:
--   psql -h localhost -U postgres -f scripts/preparar-banco.sql
--
-- As migrations criam as tabelas — este script só cria o papel e o banco.
-- Em produção, a senha vem do .env e NUNCA fica em arquivo versionado (RNF-008).

CREATE ROLE grelhahub WITH LOGIN PASSWORD 'grelhahub';
CREATE DATABASE grelhahub OWNER grelhahub ENCODING 'UTF8';

-- A extensão btree_gist é exigida pelas EXCLUDE constraints de vigência
-- (SPEC-GER-003, RN-VEN-010, RN-PRO-002). Ainda não usada nesta fatia, mas
-- habilitar agora evita descobrir a falta no meio da onda 1.
\connect grelhahub
CREATE EXTENSION IF NOT EXISTS btree_gist;
