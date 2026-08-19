/**
 * O **bloqueio por tentativa** e a **morte do token na troca de senha** — `RN-GER-040`,
 * `RN-GER-041`, ADR-0022.
 *
 * ⚠⚠ As duas regras nasceram de lacunas que a ADR-0021 procurou no código e **não encontrou**.
 * Este exemplo é o que impede as duas de voltarem a não existir sem ninguém notar.
 *
 * O que ele prova, na tela e no fio:
 *
 * 1. A 5ª tentativa errada bloqueia, e a **mensagem diz por quanto tempo**
 * 2. A senha **certa** é recusada durante o bloqueio — senão não é bloqueio
 * 3. A mensagem chega ao usuário, e não um "One or more validation errors occurred"
 * 4. O token emitido **antes** da troca de senha morre na requisição seguinte
 *
 * ⚠⚠ **Ele MUDA o estado do mundo — bloqueia conta e troca senha — e por isso desfaz tudo no fim.**
 * A primeira versão não desfazia, e a segunda execução passava **provando nada**: o login falhava, o
 * token vinha 401 antes e 401 depois, e a asserção "o token morreu" ficava verde por ele nunca ter
 * vivido. Teste que fica verde ao deixar de testar é o defeito mais caro que existe.
 */
import { execFileSync } from 'node:child_process';
import { abrir, foto, relatar } from '../navegador.mjs';

const BASE = `http://localhost:${process.env.PORTA_WEB ?? 5173}`;
const API = `${BASE}/api`;

const ALVO = 'dona@brasa.local';
const SEGUNDA = 'vazia@brasa.local';
const CERTA = 'senha-de-demo-1234';
const NOVA = 'senha-trocada-9876';

/**
 * ⚠⚠ **Zera o bloqueio antes de começar, e sem isto o exemplo só serve uma vez a cada 15 minutos.**
 *
 * `PAR-GER-005` bloqueia por 15 min. Na segunda execução a PRIMEIRA tentativa já responde "conta
 * bloqueada" — e o exemplo acusa "bloqueou na 1ª tentativa", um defeito que não existe. Validação
 * que grita à toa é validação que alguém desliga.
 *
 * ⚠ É a segunda escrita direta no banco de toda a skill — a outra é envelhecer pedido em
 * `semear.sh`. O motivo é o mesmo: **não existe rota para isto**, e criar uma seria abrir no produto
 * um desbloqueio administrativo que `RN-GER-040` recusou de propósito — o bloqueio expira sozinho, e
 * não vira fila para alguém atender.
 */
function zerarBloqueio(email) {
  execFileSync('docker', [
    'exec', process.env.CONTEINER ?? 'grelhahub-dev',
    'psql', '-U', 'grelhahub', '-d', 'grelhahub', '-q', '-c',
    `UPDATE usuario SET bloqueado_ate = NULL, falhas_de_acesso = 0 WHERE email = '${email}'`,
  ]);
}

const { navegador, pagina, problemas } = await abrir({ largura: 1280, altura: 900 });

const entrar = (email, senha) =>
  pagina.evaluate(
    async ([api, e, s]) => {
      const r = await fetch(`${api}/v1/autenticacao/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: e, senha: s }),
      });
      const corpo = await r.json().catch(() => ({}));
      return { status: r.status, detail: corpo.detail, title: corpo.title, token: corpo.token };
    },
    [API, email, senha],
  );

const trocarSenha = (token, atual, nova) =>
  pagina.evaluate(
    async ([api, t, a, n]) => {
      const r = await fetch(`${api}/v1/usuarios/minha-senha`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${t}` },
        body: JSON.stringify({ senhaAtual: a, novaSenha: n }),
      });
      return r.status;
    },
    [API, token, atual, nova],
  );

const chamarFila = (token) =>
  pagina.evaluate(
    async ([api, t]) => {
      const r = await fetch(`${api}/v1/pedidos/fila`, { headers: { Authorization: `Bearer ${t}` } });
      return r.status;
    },
    [API, token],
  );

await pagina.goto(`${BASE}/entrar`, { waitUntil: 'networkidle' });

// ── RN-GER-040 · o bloqueio ─────────────────────────────────────────────────
zerarBloqueio(ALVO);
console.log('→ RN-GER-040 · cinco tentativas erradas  (bloqueio zerado antes de começar)');

for (let i = 1; i <= 5; i += 1) {
  const r = await entrar(ALVO, `errada-${i}`);
  console.log(`  ${i}ª: ${r.status} · ${r.title ?? ''} · ${r.detail ?? ''}`);

  if (i < 5 && /bloque/i.test(r.detail ?? '')) {
    problemas.push(`RN-GER-040: bloqueou na ${i}ª tentativa; PAR-GER-004 são 5`);
  }
}

// ⚠ A senha CERTA durante o bloqueio. Sem esta asserção, "bloqueio" poderia ser só "a senha errada
// continua errada" — que não prova nada.
const comSenhaCerta = await entrar(ALVO, CERTA);
console.log('\n→ senha CERTA durante o bloqueio');
console.log(`  ${comSenhaCerta.status} · ${comSenhaCerta.title} · ${comSenhaCerta.detail}`);

if (comSenhaCerta.token) {
  problemas.push('RN-GER-040: a senha certa entrou durante o bloqueio — não há bloqueio');
}
if (!/bloque/i.test(comSenhaCerta.detail ?? '')) {
  problemas.push(`RN-GER-040: a mensagem não diz que está bloqueada — "${comSenhaCerta.detail}"`);
}
// ⚠ A mensagem tem de dizer POR QUANTO TEMPO. "Conta bloqueada" sem prazo deixa a pessoa tentando
// de minuto em minuto sem saber se adianta.
if (!/\d/.test(comSenhaCerta.detail ?? '')) {
  problemas.push(`RN-GER-040: a mensagem não diz o prazo — "${comSenhaCerta.detail}"`);
}

// ── a mensagem na TELA, e não só no fio ─────────────────────────────────────
console.log('\n→ a mensagem na tela de login');
await pagina.fill('input[type="email"]', ALVO);
await pagina.fill('input[type="password"]', CERTA);
await pagina.click('button[type="submit"]');
await pagina.waitForTimeout(1500);

const naTela = await pagina.evaluate(() => document.querySelector('.erro')?.textContent ?? null);
console.log(`  "${naTela}"`);

if (!naTela || !/bloque/i.test(naTela)) {
  problemas.push(`RN-GER-040: a tela não mostra o bloqueio — "${naTela}"`);
}
// ⚠ O defeito que `api.ts` já documenta: `ProblemDetails` de validação traz o `title` genérico do
// ASP.NET em INGLÊS. Se ele vazar aqui, o usuário lê "One or more validation errors occurred".
if (/validation errors/i.test(naTela ?? '')) {
  problemas.push('A frase genérica do ASP.NET, em inglês, chegou ao usuário');
}

await foto(pagina, '17-login-bloqueado');

// ⚠ Devolve a conta ao estado limpo: ela é a mesma que os outros exemplos usam para entrar.
zerarBloqueio(ALVO);

// ── RN-GER-041 · o token morre com a senha ──────────────────────────────────
console.log('\n→ RN-GER-041 · o token emitido antes da troca de senha');
zerarBloqueio(SEGUNDA);

let sessao = await entrar(SEGUNDA, CERTA);
let senhaDaSessao = CERTA;

// Execução anterior interrompida no meio pode ter deixado a senha trocada.
if (!sessao.token) {
  sessao = await entrar(SEGUNDA, NOVA);
  senhaDaSessao = NOVA;
  console.log('  (a senha estava trocada de uma execução anterior — recuperado)');
}

console.log('  token obtido:', !!sessao.token);

if (!sessao.token) {
  problemas.push('RN-GER-041: não foi possível autenticar — o exemplo não testou nada');
} else {
  const antes = await chamarFila(sessao.token);
  console.log('  antes da troca:', antes, '(espera 200)');

  // ⚠⚠ Sem esta asserção, um token que NUNCA funcionou faz o teste passar por acidente: 401 antes,
  // 401 depois, "o token morreu". Ele não morreu — ele nunca viveu.
  if (antes !== 200) {
    problemas.push(`RN-GER-041: o token não funcionava ANTES da troca (${antes}) — nada foi provado`);
  }

  const destino = senhaDaSessao === CERTA ? NOVA : CERTA;
  await trocarSenha(sessao.token, senhaDaSessao, destino);

  const depois = await chamarFila(sessao.token);
  console.log('  depois da troca:', depois, '(espera 401)');

  if (depois !== 401) {
    problemas.push(
      `RN-GER-041: o token de antes da troca ainda vale (${depois}) — senha vazada, senha trocada, atacante dentro`,
    );
  }

  // ⚠ DEVOLVE A SENHA ao valor que o semeador criou. Sem isto o exemplo só roda uma vez, e a
  // execução seguinte passa sem testar — que foi exatamente o que aconteceu ao escrevê-lo.
  if (destino !== CERTA) {
    const volta = await entrar(SEGUNDA, NOVA);
    const devolvida = volta.token ? await trocarSenha(volta.token, NOVA, CERTA) : 0;

    if (devolvida >= 200 && devolvida < 300) {
      console.log('  senha devolvida ao valor original');
    } else {
      problemas.push(`Não devolvi a senha de ${SEGUNDA} — a próxima execução começa suja`);
    }
  }
}

// ⚠⚠ Os 401 do console são NOSSOS — este exemplo erra a senha cinco vezes de propósito e depois usa
// um token que ele mesmo matou. Deixá-los na lista faria o script acusar defeito em toda execução
// bem-sucedida, e script que grita à toa é script que ninguém lê. Some só o ruído previsto;
// qualquer outro erro de console continua reprovando.
const ruidoPrevisto = /Failed to load resource.*401|ERR_ABORTED/i;
const reais = problemas.filter((p) => !ruidoPrevisto.test(p));

console.log(`\n(${problemas.length - reais.length} erros de console eram os 401 provocados aqui)`);

const houveProblema = relatar(reais);
await navegador.close();

process.exitCode = houveProblema ? 1 : 0;
