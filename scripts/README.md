# Scripts

## `preparar-banco.sql`

Cria o papel e o banco de desenvolvimento. Rode **uma vez**:

```bash
psql -h localhost -U postgres -f scripts/preparar-banco.sql
```

Depois disso, a API aplica as migrations sozinha ao subir em `Development`
(ver `Program.cs`). Em produção as migrations são **step do pipeline**, nunca no
startup — ver [ADR-0006](../docs/adr/0006-deploy-em-vps-unica-com-docker.md).

Se preferir Docker em vez do PostgreSQL local:

```bash
docker run -d --name grelhahub-db -p 5432:5432 \
  -e POSTGRES_DB=grelhahub -e POSTGRES_USER=grelhahub -e POSTGRES_PASSWORD=grelhahub \
  postgres:16-alpine
```

A connection string de desenvolvimento está em
`backend/src/GrelhaHub.Api/appsettings.Development.json`. Se a sua instância usa outra
senha, ajuste ali — esse arquivo é só de desenvolvimento e não carrega segredo real.
