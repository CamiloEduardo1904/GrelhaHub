# ADR-0002 — Clean Architecture com CQRS via MediatR

**Status:** Aceito · **Data:** Agosto/2026

## Contexto

O domínio do GrelhaHub tem dois perfis de uso muito diferentes convivendo no mesmo
sistema:

- **Escritas críticas e pouco frequentes** — confirmar pedido, baixar estoque, fechar
  caixa. Cada uma envolve várias tabelas, precisa de transação, validação e regras de
  negócio densas (RN-040, RN-052, RN-017).
- **Leituras massivas e simples** — mapa de mesas, fila da cozinha, cardápio, dashboards.
  São consultadas continuamente durante o serviço, precisam ser rápidas e não têm regra
  de negócio nenhuma — só formatação de dados.

Tratar os dois pelo mesmo caminho (repositório genérico → serviço → controller) faz a
leitura carregar o peso da escrita: agregados completos hidratados pelo ORM para exibir
seis campos numa tela.

## Decisão

Adotar **Clean Architecture** em quatro projetos:

```
GrelhaHub.Domain          entidades, value objects, regras invariantes — sem dependências
GrelhaHub.Application     commands, queries, handlers, validators — depende só de Domain
GrelhaHub.Infrastructure  EF Core, repositórios, integrações — implementa contratos da Application
GrelhaHub.Api             controllers, autenticação, injeção de dependência
```

Regra de dependência: as setas apontam sempre para dentro. `Domain` não referencia nada.

Sobre essa base, aplicar **CQRS** com **MediatR**:
- **Commands** — alteram estado, passam por validação (FluentValidation) e executam em
  transação. Retornam pouco ou nada.
- **Queries** — leem estado, podem projetar direto para DTO com `Select`, sem carregar
  agregados nem rastreamento de mudanças (`AsNoTracking`).

Pipeline behaviors do MediatR cuidam de preocupações transversais: validação, log,
transação e auditoria — sem repetir código em cada handler.

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Arquitetura em camadas clássica (Controller → Service → Repository) | Serviços incham até virar classes de mil linhas; a separação leitura/escrita não acontece naturalmente |
| CQRS com bancos separados de leitura e escrita (event sourcing) | Complexidade desproporcional ao porte do sistema; exigiria sincronização e consistência eventual, que o caixa não tolera |
| Vertical Slice puro, sem camadas | Boa alternativa, mas Clean Architecture comunica melhor a intenção na banca e força o isolamento do domínio de forma verificável |

## Consequências

**Positivas**
- Queries de dashboard e KDS otimizáveis sem tocar em nada de escrita
- Cada caso de uso é uma classe própria e testável isoladamente — atende RNF-013
- Validação centralizada no pipeline garante que nenhum command escape do FluentValidation
- O domínio, sem dependência de EF Core, pode ser testado sem banco

**Negativas / custos aceitos**
- Mais arquivos por funcionalidade (command + handler + validator + DTO) — cerimônia
  visível em CRUDs simples, onde o ganho é menor
- Curva de aprendizado maior para quem entra no projeto
- Risco de over-engineering em cadastros triviais; mitigado permitindo queries diretas ao
  `DbContext` na camada Application, sem repositório intermediário para leitura
