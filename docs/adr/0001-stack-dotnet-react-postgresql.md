# ADR-0001 — Stack: .NET + React + PostgreSQL

**Status:** Aceito · **Data:** Agosto/2026

## Contexto

O GrelhaHub precisa de uma stack que atenda três exigências simultâneas: sustentar um
domínio transacional com regras de consistência fortes (estoque, caixa), servir uma
interface de uso intenso em tablet no balcão, e permitir um app mobile futuro sem
reescrever a camada de negócio. O time tem experiência prévia em C#/.NET, e o projeto
precisa ser defendido em banca — o que valoriza tecnologias com documentação madura e
justificativa técnica clara.

## Decisão

- **Backend:** C# / .NET (versão LTS mais atual) com ASP.NET Core Web API
- **Frontend:** React (versão mais atual) com TypeScript
- **Banco:** PostgreSQL
- **ORM:** Entity Framework Core

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| Node.js + NestJS no backend | Tipagem e validação de domínio menos rigorosas que C#; o time não teria ganho de produtividade que compensasse a perda de familiaridade |
| ASP.NET MVC com Razor (monolito server-rendered) | Inviabilizaria o app mobile futuro sem duplicar a lógica; contraria a estratégia de produto |
| Blazor Server | Acopla a UI ao servidor por WebSocket — frágil em conexão instável de restaurante; e não resolve o mobile nativo |
| MySQL | Menos recursos analíticos (CTEs recursivas, window functions, `JSONB`) que serão úteis no BI da Sprint 7 & 8 |

## Consequências

**Positivas**
- Uma única API serve web e, futuramente, mobile — sem duplicação de regra
- EF Core dá migrations versionadas, atendendo RNF-014
- C# fortemente tipado reduz a classe de erros mais perigosa no domínio: cálculo monetário

**Negativas / custos aceitos**
- Duas stacks para manter (C# e TypeScript), com dois pipelines de build
- Consumo de memória do .NET exige atenção na VPS — ver [ADR-0006](0006-deploy-em-vps-unica-com-docker.md) e risco RS3
- Sem renderização no servidor, o primeiro carregamento do front é mais pesado — aceitável, já que o sistema é usado em sessões longas, não em visitas pontuais
