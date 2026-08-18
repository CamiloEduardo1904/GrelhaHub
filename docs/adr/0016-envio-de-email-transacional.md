# ADR-0016 — E-mail transacional por SMTP próprio, enfileirado em memória

| Campo | Valor |
|-------|-------|
| Situação | **Aceita** |
| Data | 18/08/2026 |
| Decide sobre | `RN-GER-032` (envio não bloqueia), `RN-GER-033` (token de acesso), `RN-GER-034` (resposta uniforme) |
| Relacionada | [ADR-0006](0006-deploy-em-vps-unica.md) (VPS única), [ADR-0013](0013-acesso-de-plataforma.md) |
| Especificada em | [`SPEC-GER-008`](../specs/SPEC-GER-008-recuperacao-de-acesso.md) |

---

## Contexto

Senha esquecida só se resolvia com outra pessoa. O administrador redefine a do usuário da casa
(`RN-GER-028`), e **acima do acesso de plataforma não há ninguém** — o beco que a §8 da
`SPEC-GER-007` registrou ontem: quem perdesse a senha do suporte dependeria de acesso ao servidor,
porque a carga inicial não sobrescreve usuário existente.

Três fatos do nosso caso, e a decisão sai deles:

1. **O domínio tem caixa própria** — `sistema@grelhahub.com` na Hostinger, com alias
   `noreply@grelhahub.com`, e **SPF, DKIM e DMARC já publicados** no DNS. Sem isso, e-mail
   transacional de domínio novo cai em spam, e um link de recuperação que não chega é pior do que
   não existir: o usuário fica esperando
2. **Uma VPS só, um processo** (ADR-0006). Não há Redis, não há fila, não há worker separado
3. **O volume é ínfimo.** Recuperação de senha e convite de usuário — dezenas por mês, não por
   minuto

## Decisão

> **SMTP direto para a caixa do domínio, com o envio fora do caminho da requisição, por uma fila
> em memória do próprio processo.** Sem serviço externo, sem tabela de fila, sem worker.

| Item | Escolha |
|------|---------|
| Transporte | SMTP autenticado, `smtp.hostinger.com` |
| Porta | **587 com STARTTLS**; 465 (SSL implícito) fica disponível por configuração |
| Remetente | `noreply@grelhahub.com` — alias da caixa que autentica |
| Biblioteca | **MailKit**. `System.Net.Mail.SmtpClient` é desaconselhado pela própria Microsoft para código novo, e não faz STARTTLS com a robustez que o MailKit faz |
| Enfileiramento | `Channel` em memória + `BackgroundService` no mesmo processo |
| Corpo | HTML e texto puro no mesmo e-mail, montados no código — sem motor de template |

### Por que a fila, se o volume é ínfimo

Não é por vazão. É por `RN-GER-032`: **o envio não pode fazer parte da transação.** Um `SendAsync`
dentro do handler transforma a caixa de e-mail em dependência da criação de usuário — SMTP lento
segura a requisição, SMTP fora do ar derruba a operação inteira. Enfileirar é o que mantém a
operação de pé quando o correio cai.

## Consequências

### O que fica melhor

- Recuperação de acesso deixa de depender de outra pessoa, e o beco do acesso de plataforma fecha
- Nenhuma dependência externa nova além de um pacote NuGet; nada a contratar, nada a configurar
  fora do `.env`
- A caixa é do domínio, com autenticação DNS pronta — a entrega não depende de reputação de IP
  compartilhado

### O que fica pior, e é aceito

| Custo | Consequência prática |
|-------|----------------------|
| **A fila é volátil** ⚠ | Reinício do contêiner descarta o que ainda não saiu. Aceito porque **todo e-mail nosso é reemitível**: o usuário pede o link de novo. Se um dia houver e-mail que não se possa pedir de novo — nota fiscal, recibo —, esta decisão precisa ser revista, e é por isso que está escrita |
| **Sem retentativa persistente** | Falha de envio é registrada e morre ali. O usuário que não recebeu pede outro link, e o log diz o que aconteceu |
| **Sem métrica de entrega** | Não sabemos se o e-mail foi aberto, e não sabemos se caiu em spam. O DMARC está em `p=none`, que reporta e não bloqueia |
| **A caixa tem limite de envio** | Plano de e-mail compartilhado tem teto diário. No volume previsto não encosta, mas um dia de convite em massa encostaria |

### Onde a segurança fica

- **O token nunca é gravado em claro** (`RN-GER-033`). A tabela guarda o hash; quem lê o banco não
  entra na conta de ninguém. É a mesma doutrina de `RNF-005` para senha
- **A resposta não revela se a conta existe** (`RN-GER-034`) — mesma escolha de `RN-GER-027` no
  login
- **A senha nunca viaja por e-mail.** O que viaja é um link de uso único e validade curta. Senha em
  texto claro fica arquivada na caixa de quem recebeu, e em qualquer servidor pelo caminho
- **As credenciais SMTP vivem no `.env` da VPS** (`RNF-008`), repassadas pelo `docker-compose.yml`
  — a mesma cadeia do JWT e do usuário de plataforma, com o mesmo modo de falha já documentado

## Alternativas consideradas

| Alternativa | Por que não |
|-------------|-------------|
| **Serviço transacional** (Resend, SendGrid, SES) | Melhor entrega e métrica, e é o que eu escolheria em produção de verdade. Aqui custa uma conta externa e uma chave a mais para um volume de dezenas de e-mails, com a caixa do domínio já paga e autenticada |
| **Tabela de fila no banco, com retentativa** | Resolve a volatilidade — e paga com uma tabela, um job e um estado a mais para e-mails que são todos reemitíveis. Fica registrado como o caminho se a premissa mudar |
| **Envio síncrono no handler** | Simples, e viola `RN-GER-032`: acopla criar usuário a ter correio no ar |
| **Senha inicial no corpo do e-mail** | Dispensa token e tabela. Recusado: senha em texto claro num canal não controlado, arquivada para sempre na caixa de quem recebeu |
