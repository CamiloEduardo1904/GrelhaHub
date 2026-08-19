/**
 * O mínimo para dirigir um Chrome de verdade contra o GrelhaHub local.
 *
 * **`playwright-core` e não `playwright`, de propósito:** o pacote completo baixa três navegadores
 * (centenas de MB) para dirigir um que já está instalado. `playwright-core` é um pacote e nenhum
 * download — e o Chrome do Windows serve.
 *
 * ⚠ **Caminho com barra NORMAL.** `'C:\\Program Files\\...'` sobrevive num arquivo e **não**
 * sobrevive a um heredoc do Bash: as barras somem e o erro sai como
 * `executable doesn't exist at C:Program FilesGoogleChrome...`. Escreva o script com a ferramenta
 * de arquivo, nunca por `cat <<EOF`.
 */
import { chromium } from 'playwright-core';

const CANDIDATOS = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
];

export const CREDENCIAIS = {
  loja: { email: 'dona@brasa.local', senha: 'senha-de-demo-1234' },
  vazia: { email: 'vazia@brasa.local', senha: 'senha-de-demo-1234' },
  plataforma: { email: 'suporte@grelhahub.local', senha: 'suporte-local-1234' },
};

// ⚠ 5173 é o padrão do Vite, e portanto a porta de todo projeto Vite da máquina. Quando `subir.sh`
// precisar de outra (`PORTA_WEB=5174`), passe `BASE` aqui também — os dois leem do ambiente.
const BASE = process.env.BASE ?? `http://localhost:${process.env.PORTA_WEB ?? 5173}`;
const SAIDA = process.env.SAIDA ?? '.';

/**
 * Abre o navegador e devolve a página, mais o que ela reclamou.
 *
 * ⚠ `problemas` é parte do resultado, não enfeite. Erro de console e requisição que falhou são
 * defeito que a captura **não** mostra: a tela sai bonita e o console está gritando.
 */
export async function abrir({ largura = 1280, altura = 900 } = {}) {
  const { existsSync } = await import('node:fs');
  const executablePath = CANDIDATOS.find((c) => existsSync(c));

  if (!executablePath) {
    throw new Error(`Nenhum navegador encontrado. Procurei em:\n  ${CANDIDATOS.join('\n  ')}`);
  }

  const navegador = await chromium.launch({ executablePath, headless: true });
  const ctx = await navegador.newContext({ viewport: { width: largura, height: altura } });
  const pagina = await ctx.newPage();
  const problemas = [];

  pagina.on('console', (m) => m.type() === 'error' && problemas.push(`console: ${m.text()}`));
  pagina.on('pageerror', (e) => problemas.push(`pageerror: ${e.message}`));
  pagina.on('requestfailed', (r) =>
    problemas.push(`requisição falhou: ${r.url()} — ${r.failure()?.errorText}`),
  );

  return { navegador, pagina, problemas };
}

/**
 * Entra no painel. `quem` é uma chave de {@link CREDENCIAIS}.
 *
 * ⚠ **Derruba a sessão antes**, e isto foi um defeito real deste driver: `Entrar.tsx` redireciona
 * quem já tem sessão para `/painel` (ou `/plataforma`), então trocar de usuário no meio de uma
 * validação batia em `Timeout waiting for input[type="email"]` — a tela do formulário nunca chega a
 * montar. Não é problema do teste: é `RF-SEG-005` funcionando. Quem valida duas sessões na mesma
 * corrida precisa sair da primeira.
 */
export async function entrar(pagina, quem = 'loja') {
  const { email, senha } = CREDENCIAIS[quem];

  // Primeiro `goto` só para ter a origem e poder limpar o `localStorage` (a chave é
  // `grelhahub.token`, ver `services/api.ts`). O redirecionamento aqui é esperado.
  await pagina.goto(`${BASE}/entrar`, { waitUntil: 'domcontentloaded' });
  await pagina.evaluate(() => localStorage.clear());

  await pagina.goto(`${BASE}/entrar`, { waitUntil: 'networkidle' });
  await pagina.fill('input[type="email"]', email);
  await pagina.fill('input[type="password"]', senha);
  await pagina.click('button[type="submit"]');
  await pagina.waitForURL('**/painel**', { timeout: 15_000 });
  await pagina.waitForLoadState('networkidle');
}

/** Vai para uma aba do painel pelo RÓTULO — que é o que o usuário vê (`CHAPA-12`). */
export async function aba(pagina, rotulo, esperarPor) {
  await pagina.click(`.aba:has-text("${rotulo}")`);
  if (esperarPor) await pagina.waitForSelector(esperarPor, { timeout: 20_000 });
}

/**
 * Captura.
 *
 * `fullPage` para composição; viewport fixo para conferir **o que cabe sem rolar**.
 * ⚠ **Olhe a imagem.** Captura em branco é falha de subida, não sucesso.
 */
export async function foto(pagina, nome, opcoes = {}) {
  await pagina.screenshot({ path: `${SAIDA}/${nome}.png`, ...opcoes });
  console.log(`  📸 ${nome}.png`);
}

/**
 * Conta respostas de uma rota por um tempo — a medida que separa "a tela parece certa" de "a tela
 * está certa". É com isto que se mede cadência de polling.
 *
 * Devolve os instantes em segundos desde o início, e os intervalos entre eles.
 */
export function contador(pagina, padrao) {
  const marcas = [];
  pagina.on('response', (r) => padrao.test(r.url()) && marcas.push(Date.now()));

  return {
    zerar: () => {
      marcas.length = 0;
    },
    async medir(segundos) {
      const t0 = Date.now();
      marcas.length = 0;
      await pagina.waitForTimeout(segundos * 1000);

      const em = marcas.map((t) => Math.round((t - t0) / 1000));
      const intervalos = marcas.slice(1).map((t, i) => Math.round((t - marcas[i]) / 1000));

      return { total: marcas.length, em, intervalos };
    },
    /**
     * ⚠ Total **acumulado desde a criação do contador**, não "durante a janela". Chame `zerar()`
     * antes de abrir a janela que você quer medir — foi assim que este exemplo acusou "3 consultas
     * à lista com o monitor à frente" quando as três eram da aba anterior. `medir()` zera sozinho;
     * `total` não.
     */
    get total() {
      return marcas.length;
    },
  };
}

/**
 * `CHAPA-02` — exatamente uma barra âmbar por grupo.
 *
 * ⚠⚠ **`seletorDoGrupo` precisa ser o GRUPO, com filho direto.** Medida primeiro com
 * `header .grelha i.ativa` dentro do bloco da fila, a sonda acusou duas barras âmbar — uma violação
 * que não existia: ela pegava também o `<header>` de cada `<Comanda>`, que tem a régua própria.
 * `.bloco-de-fila > header` é o grupo. **Sonda mal escopada acusa defeito falso.**
 */
export async function conferirChapa02(pagina, seletorDoGrupo) {
  return pagina.$$eval(seletorDoGrupo, (grupos) =>
    grupos.map((g) => ({
      texto: g.textContent.trim().slice(0, 40),
      ambares: g.querySelectorAll(':scope .grelha i.ativa').length,
    })),
  );
}

/** `CHAPA-04` — alvo de toque de 48 px na operação. Devolve as alturas distintas encontradas. */
export async function alturasDeToque(pagina, seletor) {
  return pagina.$$eval(seletor, (bs) => [
    ...new Set(bs.map((b) => Math.round(b.getBoundingClientRect().height))),
  ]);
}

/** A página rola na horizontal? No celular, nunca deve. */
export async function rolaNaHorizontal(pagina) {
  return pagina.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
}

/** O texto visível contém isto? Para provar que um dado NÃO chegou à tela. */
export async function textoContem(pagina, padrao) {
  return pagina.evaluate((p) => new RegExp(p, 'i').test(document.body.innerText), padrao.source ?? padrao);
}

export function relatar(problemas) {
  console.log('\n=== console, rede e erro de página ===');
  console.log(problemas.length ? problemas.join('\n') : 'nenhum');
  return problemas.length;
}
