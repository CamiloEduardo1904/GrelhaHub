/**
 * O **alternador de tema** — design system §2.1.
 *
 * ⚠⚠ **O que este exemplo existe para pegar é contraste que só falha num dos temas.** A folha
 * define a paleta clara em `:root` e a escura em dois lugares, e um valor esquecido num deles não
 * aparece em `tsc`, em `eslint` nem em teste nenhum: aparece como texto ilegível na cozinha, à noite.
 *
 * Ele conferre, nos três estados:
 *
 * 1. `Sistema` **remove** o atributo — não escreve `data-theme="sistema"`
 * 2. A escolha **sobrevive ao recarregamento**, e sem piscada
 * 3. As superfícies principais têm `color` declarado junto do `background` (`CHAPA-11`)
 * 4. `CHAPA-02` — exatamente uma barra âmbar no grupo
 */
import { abrir, entrar, foto, relatar } from '../navegador.mjs';

const { navegador, pagina, problemas } = await abrir({ largura: 1280, altura: 900 });

await entrar(pagina, 'loja');

const ler = () =>
  pagina.evaluate(() => {
    const cor = (sel, prop) => {
      const el = document.querySelector(sel);
      return el ? getComputedStyle(el)[prop] : null;
    };

    return {
      atributo: document.documentElement.getAttribute('data-theme'),
      guardado: localStorage.getItem('grelhahub.tema'),
      ambaresNoGrupo: document.querySelectorAll('.alternador-de-tema .grelha i.ativa').length,
      ativo: [...document.querySelectorAll('.alternador-de-tema .chip')]
        .filter((c) => c.getAttribute('aria-pressed') === 'true')
        .map((c) => c.textContent.trim())[0],
      corpo: { fundo: cor('body', 'backgroundColor'), texto: cor('body', 'color') },
      barra: { fundo: cor('.barra-app', 'backgroundColor'), texto: cor('.barra-app', 'color') },
      comanda: { fundo: cor('.comanda', 'backgroundColor'), texto: cor('.comanda', 'color') },
      aba: { fundo: cor('.aba', 'backgroundColor'), texto: cor('.aba', 'color') },
    };
  });

for (const alvo of ['Claro', 'Escuro', 'Sistema']) {
  console.log(`\n→ ${alvo}`);
  await pagina.click(`.alternador-de-tema .chip:has-text("${alvo}")`);
  await pagina.waitForTimeout(400);

  const r = await ler();
  console.log(JSON.stringify(r, null, 2));

  // ⚠ `Sistema` REMOVE o atributo. Um `data-theme="sistema"` deixaria a página presa no claro,
  // que é o oposto de "acompanhe o aparelho".
  const esperado = { Claro: 'light', Escuro: 'dark', Sistema: null }[alvo];
  if (r.atributo !== esperado) {
    problemas.push(`Tema "${alvo}": data-theme é ${r.atributo}, esperado ${esperado}`);
  }
  if (r.ambaresNoGrupo !== 1) {
    problemas.push(`CHAPA-02: ${r.ambaresNoGrupo} barras âmbar no alternador`);
  }
  // CHAPA-11 — quem declara `background` declara `color`. Superfície sem cor de texto herda a da
  // seção que a contém, e foi assim que a comanda da capa ficou creme sobre creme.
  for (const [nome, s] of Object.entries(r)) {
    if (s && typeof s === 'object' && s.fundo && s.fundo !== 'rgba(0, 0, 0, 0)' && !s.texto) {
      problemas.push(`CHAPA-11: ${nome} declara fundo e não declara cor de texto no tema ${alvo}`);
    }
  }

  await foto(pagina, `13-tema-${alvo.toLowerCase()}`);
}

// ── a escolha sobrevive ao recarregamento, e sem piscada ────────────────────
console.log('\n→ persistência');
await pagina.click('.alternador-de-tema .chip:has-text("Claro")');
await pagina.waitForTimeout(300);
await pagina.reload({ waitUntil: 'domcontentloaded' });

// ⚠ Lido no `domcontentloaded`, ANTES de o React montar: é o script inline do `index.html` que
// tem de ter aplicado o tema. Se este valor vier nulo, existe uma piscada — e a piscada é a
// tela escura na cara de quem escolheu claro.
const antesDoReact = await pagina.evaluate(() =>
  document.documentElement.getAttribute('data-theme'),
);
console.log('  data-theme antes do React montar:', antesDoReact, '(precisa ser "light")');
if (antesDoReact !== 'light') {
  problemas.push('O tema não é aplicado antes da primeira pintura — há piscada');
}

const houveProblema = relatar(problemas);
await navegador.close();

process.exitCode = houveProblema ? 1 : 0;
