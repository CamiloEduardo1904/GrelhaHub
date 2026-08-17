# ADR-0009 — Onde as regras de negócio vivem

**Status:** Aceito · **Data:** Agosto/2026 · **Resolve:** conflito C9 do
[cruzamento](../07-cruzamento-catalogo-x-levantamento.md#c9--onde-as-regras-são-implementadas--arquitetural)

## Contexto

Dois artefatos do projeto prescrevem lugares diferentes para a mesma regra.

O **Catálogo de Regras** estabelece a ordem de preferência
`MOD → NN/UQ/FK/CK/EX/ENU → GEN → TRG → VW → APP` e afirma: *"A camada de aplicação é o
último recurso, não o primeiro: o que está sendo avaliado é o modelo."*

O **ADR-0002** estabelece Clean Architecture com o domínio em C#, regras nos handlers e
validators, e o banco como detalhe de persistência.

Ambos estão certos dentro da sua finalidade. O catálogo é artefato de modelagem de dados —
as Sprints 3 a 6 avaliam o modelo, e uma regra que só existe em C# é, para efeito de
avaliação, uma regra que o modelo não garante. O ADR-0002 é artefato de arquitetura de
aplicação — regras enterradas em triggers são invisíveis no código, difíceis de testar e de
evoluir, e produzem sistemas em que ninguém sabe por que um `INSERT` falhou.

A tentação é escolher um lado. Escolher um lado produz ou um banco burro com regras
duplicadas em cada caminho de escrita, ou uma aplicação que não pode confiar no próprio
banco.

## Decisão

**As duas camadas, com papéis distintos e declarados regra a regra.**

| Camada | Responsabilidade | Critério de escolha | Exemplo |
|--------|------------------|---------------------|---------|
| **Modelo** — constraint, `ENUM`, `EXCLUDE`, coluna gerada, FK | Invariante que nunca pode ser violada por **nenhum** caminho — inclusive carga inicial, script de manutenção e correção manual em produção | "Se um DBA rodar um `INSERT` à mão, isso pode passar?" Se a resposta é não, é constraint | `EXCLUDE` de uma comanda aberta por mesa (RN-VEN-006); quantidade > 0 (RN-VEN-011) |
| **Banco** — trigger, view | Derivação e consistência multi-tabela que precisa valer independentemente de quem escreveu | "Isso é um valor derivado de outros, ou um efeito que precisa ser atômico com a escrita?" | Saldo derivado de movimentos (RN-EST-001); recebimento gera entrada + conta a pagar (RN-COM-008) |
| **Domínio** — entidade C#, value object | Invariante de agregado que se expressa melhor em código, e que o modelo também garante | "É regra de estado de um agregado?" | Transições válidas do pedido (RN-VEN-018), também protegidas por trigger |
| **Aplicação** — validator, behavior, handler | Fluxo, orquestração, autorização, mensagem ao usuário, e política que muda por decisão comercial | "Muda sem migration? Depende de quem é o usuário? Precisa de mensagem boa?" | Autorização de desconto acima de `PAR-VEN-001` (RN-VEN-013) |

### O princípio

> **O banco garante que o dado nunca fica inválido. A aplicação garante que o usuário nunca
> chega até lá.**

A validação duplicada não é redundância acidental — é defesa em profundidade, e a
duplicação é **declarada**, regra a regra, na coluna `Impl.` do catálogo e na seção 2 de
cada spec.

### Regras de aplicação da decisão

1. Toda regra do catálogo tem sua camada declarada na spec que a implementa. Uma regra sem
   camada declarada não pode ser implementada.
2. A mensagem de erro ao usuário **sempre** vem da aplicação. Uma violação de constraint
   que chega ao usuário como erro do PostgreSQL é um defeito da aplicação, não do banco —
   significa que a validação de fluxo faltou.
3. Trigger é permitido, mas cada um exige justificativa na spec e teste de integração
   próprio. Trigger sem teste é regra invisível.
4. Regra parametrizada (`PAR-*`) **nunca** vira `CHECK` com número cravado. `CHECK` valida
   domínio (`percentual entre 0 e 100`), nunca política (`percentual <= 10`).
5. Nenhuma regra de negócio vive apenas no frontend. O front valida por usabilidade; a
   autoridade é sempre do servidor (ADR-0003).

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Só aplicação (banco como persistência burra) | Reprova o critério das Sprints 3–6: o modelo precisa expressar as regras. E qualquer escrita fora da API — carga inicial, correção manual — passa por cima de tudo |
| Só banco (triggers e constraints para tudo) | Regras invisíveis no código, testes lentos, mensagens de erro impróprias para o usuário, e política comercial exigindo migration para mudar |
| Escolher por módulo | Fronteira arbitrária: o mesmo caso de uso tem invariantes e políticas misturadas |

## Consequências

**Positivas**
- O modelo é defensável na banca: as invariantes estão nas constraints, não em prosa
- A aplicação pode confiar no banco, o que simplifica os handlers
- A duplicação é explícita e rastreável, em vez de acidental e esquecida

**Negativas / custos aceitos**
- Escrever a mesma regra duas vezes, em linguagens diferentes, com risco de divergirem.
  **Mitigação:** o teste de integração exercita o caminho real (aplicação → banco); a regra
  só é considerada implementada quando ambos passam
- Testes de integração exigem PostgreSQL real (Testcontainers), não banco em memória — o
  in-memory provider do EF Core não executa constraint, trigger nem coluna gerada, e daria
  falsa confiança
- Migrations ficam mais densas, com trigger e `EXCLUDE` escritos em SQL bruto dentro do
  `migrationBuilder.Sql(...)`
