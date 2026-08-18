# ADR-0015 — Imagem em volume de disco, servida pelo Nginx

| Campo | Valor |
|-------|-------|
| Situação | ⚠ **Substituída** pela [ADR-0018](0018-imagem-como-binario-no-banco.md) em 18/08/2026 |
| Data | 17/08/2026 |
| Decide sobre | `RN-VEN-042` (imagem do produto), `RN-GER-026` (logo da unidade) |
| Relacionada | [ADR-0006](0006-deploy-em-vps-unica.md) (VPS única), [ADR-0009](0009-onde-as-regras-de-negocio-vivem.md) |
| Especificada em | [`SPEC-VEN-002`](../specs/SPEC-VEN-002-imagem-de-produto-e-logo.md) |


> ## ⚠⚠ Esta decisão foi SUBSTITUÍDA
>
> A [ADR-0018](0018-imagem-como-binario-no-banco.md) moveu o binário para o banco
> `grelhahub_arquivo` em 18/08/2026. O que está abaixo continua valendo como **registro do
> raciocínio** — inclusive das três defesas contra travessia de diretório, que a ADR-0018 herda em
> espírito ainda que o sistema de arquivos tenha deixado de existir do outro lado.
>
> **O que sobrou vivo desta decisão:** o `location /arquivos/` do Nginx, somente leitura, servindo
> as referências gravadas antes daquela data.


---

## Contexto

O cardápio público precisa mostrar a foto do lanche e a logo da casa. É a primeira vez que o
sistema recebe **arquivo binário** do usuário, e a decisão de onde ele fica é estrutural: muda
backup, muda deploy e muda o custo de mudar de ideia.

Três coisas verdadeiras sobre o nosso caso, e é delas que a decisão sai:

1. **Uma VPS só** (ADR-0006). Não há CDN contratada nem bucket, e o Nginx já está na frente
   servindo o SPA
2. **O volume é pequeno e previsível.** Uma hamburguearia tem dezenas de produtos, não milhões.
   5 MB × 60 produtos × algumas unidades cabe em centenas de megabytes
3. **A imagem é pública por natureza.** Ela vive no cardápio anônimo (`RN-VEN-037`). Não há
   segredo a proteger no arquivo — o que precisa de controle é **quem grava**, não quem lê

## Decisão

> **O arquivo vai para um volume de disco montado no contêiner da API, com nome gerado pelo
> sistema, e é servido como arquivo estático pelo Nginx. O banco guarda só o caminho.**

O acesso ao armazenamento é encapsulado em `IArmazenamentoDeArquivo`, na camada de aplicação.
A implementação de disco fica na infraestrutura.

### O que **não** entra no banco

Os bytes. `bytea` no PostgreSQL foi considerado e recusado:

- Todo `pg_dump` passa a carregar as imagens. O backup deixa de ser barato, e backup caro é
  backup que alguém adia
- A imagem entra na transação da escrita do produto. Um upload de 5 MB passa a ter o tempo de
  vida de uma transação de banco
- Sair de `bytea` depois significa migração de dados; sair de disco significa copiar arquivos

### O que **não** entra em objeto remoto

S3, R2 ou equivalente. Recusado **por ora**, e não por princípio: acrescenta credencial,
custo recorrente e dependência de rede para o caso em que 60 fotos precisam ser servidas de
uma VPS que já está de pé. É a troca óbvia quando houver mais de uma máquina — e é justamente
por isso que a interface existe.

## Consequências

### O que fica melhor

- Nenhuma dependência nova. Nem pacote, nem credencial, nem conta
- O Nginx serve arquivo estático melhor do que a aplicação serviria, com `Cache-Control` e
  sem passar pelo pipeline do ASP.NET
- Trocar para objeto remoto é implementar a interface. Nenhum handler muda

### O que fica pior, e é aceito

| Custo | Consequência prática |
|-------|----------------------|
| **O backup deixa de ser só o banco** | `pg_dump` sozinho passa a não ser backup completo. O volume precisa entrar na rotina, e isso está declarado no `infra/README.md` |
| **Estado no disco da máquina** | O contêiner deixa de ser descartável de verdade. Recriar o contêiner é seguro; recriar a **VPS** exige restaurar o volume |
| **Sem redimensionamento** | Guardamos o que o restaurante enviou, até 5 MB. Uma foto de 4 MB é baixada inteira pelo cliente no celular. Miniatura fica para quando doer — e vai doer antes do que parece |
| **Permissão do volume vira problema de deploy** ⚠ | O contêiner roda como UID 1001 (ADR-0006) e o Docker cria ponto de montagem como `root:root` quando o caminho não existe na imagem. Aconteceu em 18/08/2026: API no ar, cardápio servindo, e **só o upload** quebrando com 500 genérico. Duas correções entraram — o `Dockerfile` cria `/app/arquivos` com dono 1001, e `GarantirEscritaDeArquivos` **recusa a partida** com a raiz sem escrita. Volume que já existe guarda o dono antigo: ver "Imagens não gravam" em `infra/README.md` |

### Onde a segurança fica

A regra `RN-VEN-042` exige que o formato seja conferido **pelo conteúdo**. É aqui que essa
exigência encontra a razão de ser:

- **Nome gerado pelo sistema.** O nome que o usuário enviou nunca toca o disco: `../../` no
  nome do arquivo é o ataque mais antigo que existe contra upload
- **Assinatura conferida nos primeiros bytes.** `Content-Type` e extensão são declarados por
  quem envia. Um `.php` renomeado para `.jpg` passa nos dois e falha na assinatura
- **O diretório servido não executa nada.** É um `location` de arquivo estático no Nginx, sem
  interpretador atrás

> **O que esta decisão não resolve, e está registrado de propósito:** não há varredura de
> conteúdo. Uma imagem válida com conteúdo impróprio é problema de moderação, não de formato, e
> o MVP não tem moderação. Quem publica responde pelo que publica.

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| `bytea` no PostgreSQL | Infla backup e transação; migração de saída é de dados, não de arquivos |
| Objeto remoto (S3/R2) | Credencial, custo e dependência de rede sem necessidade em uma VPS só |
| URL externa informada pelo restaurante | Zero armazenamento, mas o cardápio quebra quando o link morre — e ele morre |
| Base64 no próprio JSON do cardápio | Infla a resposta do endpoint mais acessado do sistema |
