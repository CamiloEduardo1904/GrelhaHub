# ADR-0004 — PostgreSQL como SGBD

**Status:** Aceito · **Data:** Agosto/2026

## Contexto

O sistema tem operações que exigem consistência forte (baixa de estoque junto com a
confirmação do pedido, fechamento de caixa) e, ao mesmo tempo, entregas analíticas
previstas para a Sprint 7 & 8 (BI e IA) que pedem consultas agregadas sobre séries
temporais de venda. Tudo isso precisa rodar numa VPS modesta, junto com a API e o Nginx.

## Decisão

**PostgreSQL** como banco único, em container Docker na rede interna, nunca exposto
publicamente (RNF-007).

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| SQL Server | Excelente com .NET, mas o licenciamento e o consumo de memória são desproporcionais para uma VPS pequena. A edição Express limita o banco a 10 GB |
| MySQL / MariaDB | Menos recursos analíticos: window functions e CTEs chegaram tarde e são mais limitadas; tipos como `JSONB` e `numeric` de precisão arbitrária são superiores no Postgres |
| SQLite | Sem concorrência de escrita adequada para múltiplos atendentes simultâneos |
| Banco NoSQL (MongoDB) | O domínio é fortemente relacional e transacional — pedido, itens, estoque e caixa precisam de integridade referencial e ACID. Seria trabalhar contra a ferramenta |

## Consequências

**Positivas**
- Tipo `numeric` com precisão exata para valores monetários — sem erro de ponto flutuante
  em cálculo de CMV e fechamento de caixa (RN-062)
- Window functions e CTEs atendem os relatórios de BI sem processamento na aplicação
- Transações ACID cobrem RNF-016 (pedido + estoque + pagamento em transação única)
- Gratuito e leve o bastante para coexistir com a API na mesma VPS
- Suporte de primeira classe no EF Core via Npgsql

**Negativas / custos aceitos**
- Time com mais familiaridade em SQL Server precisa se adaptar a diferenças de sintaxe e
  de comportamento (case sensitivity de identificadores, `RETURNING`, tipos)
- Backup e restore ficam por conta do time (volume `pgdata`), sem serviço gerenciado —
  ver RNF-017
- Convenção `snake_case` no banco exige configuração explícita de mapeamento no EF Core

## Convenções adotadas

- Identificadores em `snake_case`, sem aspas
- Chaves primárias `uuid` (evita colisão em carga inicial e futura sincronização mobile)
- Timestamps em `timestamptz`, sempre em UTC (RN-063)
- Valores monetários em `numeric(12,2)`; quantidades de insumo em `numeric(12,3)`
