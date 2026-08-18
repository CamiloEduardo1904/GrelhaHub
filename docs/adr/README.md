# Architecture Decision Records — GrelhaHub

Registro das decisões arquiteturais do projeto, com o contexto que as motivou, as
alternativas consideradas e as consequências assumidas. Um ADR não é apagado quando muda
de ideia — cria-se um novo que o supersede, preservando o raciocínio histórico.

**Formato:** contexto → decisão → alternativas → consequências → status.

**Status possíveis:** `Proposto` · `Aceito` · `Substituído por ADR-nnnn` · `Descontinuado`.

| # | Decisão | Status |
|---|---------|--------|
| [0001](0001-stack-dotnet-react-postgresql.md) | Stack: .NET + React + PostgreSQL | Aceito |
| [0002](0002-clean-architecture-com-cqrs.md) | Clean Architecture com CQRS via MediatR | Aceito |
| [0003](0003-frontend-e-backend-desacoplados.md) | Frontend e backend desacoplados (REST + JWT) | Aceito |
| [0004](0004-postgresql-como-sgbd.md) | PostgreSQL como SGBD | Aceito |
| [0005](0005-multi-estabelecimento-por-coluna-discriminadora.md) | Multi-unidade por coluna discriminadora | Aceito — **revisado pelo ADR-0013** |
| [0006](0006-deploy-em-vps-unica-com-docker.md) | Deploy em VPS única com Docker | Aceito — revisado: Traefik é a borda |
| [0007](0007-core-generico-com-nicho-por-configuracao.md) | Core genérico com nicho por configuração | Aceito |
| [0008](0008-estoque-como-livro-razao.md) | Estoque como livro-razão de movimentos | Aceito — revisado na consolidação |
| [0009](0009-onde-as-regras-de-negocio-vivem.md) | Onde as regras de negócio vivem (modelo × aplicação) | Aceito |
| [0010](0010-custeio-por-lote-com-fefo.md) | Custeio específico por lote com consumo FEFO | Aceito |
| [0011](0011-dia-operacional.md) | Dia operacional como dimensão do modelo | Aceito |
| [0012](0012-direcao-visual-chapa.md) | Direção visual **Chapa** — base da interface | Aceito |
| [0013](0013-acesso-de-plataforma.md) ⚠⚠ | Acesso de plataforma: o suporte enxerga todas as unidades | Aceito — **revisa o ADR-0005** |
| [0014](0014-modularidade-comercial.md) | Modularidade comercial: módulo habilitado por unidade | Aceito |
| [0015](0015-armazenamento-de-imagem.md) | Imagem em volume de disco, servida pelo Nginx — não `bytea`, não objeto remoto | Aceito |
| [0016](0016-envio-de-email-transacional.md) | E-mail transacional por SMTP próprio, enfileirado em memória — o envio nunca entra na transação | Aceito |
| [0017](0017-tres-bancos-aplicacao-arquivo-e-log.md) | **Três bancos** — aplicação, arquivo e log. A trilha atravessa por *outbox*, para não perder "não existe operação sem registro" | Aceito |
| [0018](0018-imagem-como-binario-no-banco.md) | Imagem como **binário no banco**, servida em base64 — substitui o ADR-0015 | Aceito |
| [0019](0019-cqrs-com-modelo-de-leitura-proprio.md) | **CQRS de verdade**: a leitura ganha modelo próprio, lido de visões. Emenda o ADR-0002, que declarou a sigla sem a separação | Aceito |
| [0020](0020-a-trilha-substitui-a-sessao-de-suporte.md) | **A sessão de suporte é removida**: o controle passa a ser a trilha, não o portão. Emenda o ADR-0013 | Aceito |

## Decisões ainda em aberto

Registradas aqui para não se perderem; viram ADR quando decididas.

| Assunto | Depende de | Onde está descrito |
|---------|-----------|--------------------|
| Modelagem do item estocável recursivo (sub-receita) | RN-PRO-004 validada em entrevista | [Catálogo, PRO](../03-catalogo-de-regras-de-negocio.md#pro--produção) |
| Comanda como pedido do tipo salão × entidade que agrega pedidos | Sprint 3 & 4 | [Levantamento §12](../01-documento-de-levantamento.md) |
| Migrations EF Core no startup × step do pipeline | Sprint de deploy | [ADR-0006](0006-deploy-em-vps-unica-com-docker.md) |
| Escopo real dos módulos RH e FIS | Pergunta 20 da fila de validação | [Cruzamento §7](../07-cruzamento-catalogo-x-levantamento.md) |
