# ADR-0005 — Multi-unidade por coluna discriminadora

**Status:** Aceito · **Data:** Agosto/2026 · **Confirmado por:** RN-GER-009

> **Nomenclatura.** Este ADR foi escrito com o termo `estabelecimento`. Na consolidação com
> o Catálogo de Regras, prevaleceu **`unidade`** (RN-GER-009), que é o termo usado em todo o
> modelo. Leia `estabelecimento` como `unidade` no texto abaixo.
>
> **Convergência independente:** o catálogo chegou à mesma decisão por outro caminho — *"A
> operação ocorre em uma única unidade, mas todo registro transacional é vinculado a uma
> unidade para permitir expansão futura."* Duas análises independentes concluindo o mesmo é
> o sinal mais forte disponível antes da entrevista.

> ## ⚠⚠ Revisado pelo [ADR-0013](0013-acesso-de-plataforma.md) — 17/08/2026
>
> A invariante central deste ADR — **nenhuma consulta vê duas unidades** — passou a ter **uma
> exceção**: o usuário de plataforma (`RN-GER-014`), para quem o filtro global é desligado.
>
> Leia este ADR sabendo disso. O que continua verdadeiro é o desenho para o usuário de
> restaurante, que é 100% dos usuários de um cliente. O que deixou de ser verdadeiro é a
> garantia absoluta: um defeito que marque `EhPlataforma` indevidamente agora vaza dado entre
> clientes, e é o risco declarado no ADR-0013.

## Contexto

O GrelhaHub é posicionado como sistema para "pequenos e médios restaurantes" — ou seja,
um produto vendável a vários clientes, não uma instalação única. Mesmo o cliente primário
(hamburgueria de 1 a 3 unidades) pode ter mais de um endereço, cada um com seu estoque,
caixa e mapa de mesas.

A demonstração acadêmica usará um único estabelecimento (premissa P4). A questão é se o
modelo de dados deve prever a multiplicidade desde já.

**O ponto decisivo:** adicionar isolamento por estabelecimento depois exige alterar
*todas* as tabelas transacionais, todas as chaves únicas, todas as consultas e toda a
autorização. É a mudança mais cara possível de se adiar. Prevê-la agora custa uma coluna.

## Decisão

Modelar o sistema como **multi-estabelecimento desde a base**, no padrão de *banco único
com coluna discriminadora*:

- Existe a entidade `estabelecimento`
- Toda tabela transacional carrega `estabelecimento_id` não nulo
- O `estabelecimento_id` do usuário autenticado viaja no JWT
- Um **filtro global** no EF Core aplica o recorte automaticamente em toda consulta — o
  isolamento não depende de o desenvolvedor lembrar de filtrar
- Chaves únicas de negócio são compostas com `estabelecimento_id` (ex.: número da mesa é
  único *por estabelecimento*, não globalmente)

O MVP opera com um estabelecimento cadastrado. Nada na interface expõe a troca entre
estabelecimentos — é capacidade do modelo, não funcionalidade entregue.

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Single-tenant puro (sem `estabelecimento_id`) | Mais simples hoje, mas inviabiliza o posicionamento do produto e torna a migração futura uma reescrita de schema |
| Um banco por estabelecimento | Isolamento mais forte, mas multiplica migrations, conexões e backups — inviável numa VPS única |
| Um schema PostgreSQL por estabelecimento | Meio-termo razoável, porém complica migrations e consultas consolidadas entre unidades |

## Consequências

**Positivas**
- O produto nasce vendável a múltiplos clientes sem redesenho
- Relatórios consolidados entre unidades da mesma rede ficam triviais
- Migrations e backup únicos — cabe na restrição R1 (VPS única)

**Negativas / riscos assumidos**
- **Risco de vazamento entre estabelecimentos**: uma consulta que escape do filtro global
  expõe dados de outro cliente. Mitigação: filtro global obrigatório no `DbContext`,
  jamais filtro manual por consulta; testes de integração específicos para isolamento
- Índices precisam considerar `estabelecimento_id` como primeira coluna, sob pena de
  degradar com o crescimento da base
- Consultas ficam ligeiramente mais verbosas e as chaves únicas, compostas
