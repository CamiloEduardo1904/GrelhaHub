/**
 * O **cancelamento na comanda** — `RN-VEN-058` ⚠⚠, `RN-GER-002`.
 *
 * Motivo, instante **com hora** e responsável **com nome e sobrenome**.
 *
 * ⚠⚠ O que este exemplo existe para pegar é a linha que aparece **pela metade**: motivo sem
 * instante não diz se foi agora ou na semana passada, e instante sem responsável não diz a quem
 * perguntar. Os três andam juntos, ou o bloco não aparece.
 */
import { abrir, aba, entrar, foto, relatar } from '../navegador.mjs';

const { navegador, pagina, problemas } = await abrir({ largura: 1280, altura: 900 });

await entrar(pagina, 'loja');
await aba(pagina, 'Pedidos', '.filtro-de-grupo');
await pagina.waitForTimeout(1200);

// O grupo dos cancelados chega FECHADO (RN-VEN-057): abrir é parte do caminho real.
await pagina.click('.filtro-de-grupo .chip:has-text("Cancelados")');
await pagina.waitForTimeout(500);
await pagina.click('.comanda-fechada .comanda-abre');
await pagina.waitForTimeout(500);

const bloco = await pagina.evaluate(() => {
  const c = document.querySelector('.comanda-cancelamento');
  if (!c) return null;

  return {
    rotulo: c.querySelector('.rotulo')?.textContent,
    motivo: c.querySelector('.comanda-obs-texto')?.textContent,
    quem: c.querySelector('.comanda-cancelamento-quem')?.textContent.trim(),
    // CHAPA-10 — o rótulo é metadado (`--muted`), o motivo é PROSA (cor do texto).
    corDoRotulo: getComputedStyle(c.querySelector('.rotulo')).color,
    corDoMotivo: getComputedStyle(c.querySelector('.comanda-obs-texto')).color,
  };
});

console.log('→ o bloco de cancelamento');
console.log(JSON.stringify(bloco, null, 2));

if (!bloco) {
  problemas.push('RN-VEN-058: o bloco de cancelamento não apareceu na comanda aberta');
} else {
  if (!bloco.motivo) problemas.push('RN-VEN-058: sem motivo');

  // ⚠ Instante COM HORA: "cancelado em 19/08" não distingue a abertura do fecho, e o cancelamento
  // é o registro que alguém relê justamente para reconstruir a sequência de um turno.
  if (!/\d{2}\/\d{2}\/\d{4},? \d{2}:\d{2}/.test(bloco.quem ?? '')) {
    problemas.push(`RN-VEN-058: instante sem data e hora — "${bloco.quem}"`);
  }

  // ⚠ Nome E sobrenome: uma palavra só significa que a visão perdeu o sobrenome no `TRIM`.
  const nome = (bloco.quem ?? '').replace(/\d|\/|:|,/g, '').trim();
  if (nome.split(/\s+/).filter(Boolean).length < 2) {
    problemas.push(`RN-VEN-058: responsável sem sobrenome — "${nome}"`);
  }

  if (bloco.corDoRotulo === bloco.corDoMotivo) {
    problemas.push('CHAPA-10: rótulo e motivo na mesma cor — um deles é metadado, o outro é prosa');
  }
}

await foto(pagina, '15-cancelamento-na-comanda');

// ── e o painel de senha, na vertical ────────────────────────────────────────
console.log('\n→ RN-VEN-054 · o painel com fluxo vertical');
await aba(pagina, 'Monitor', '.mural-numeros');
await pagina.waitForTimeout(1200);

const mural = await pagina.evaluate(() => {
  const lista = document.querySelector('.mural-coluna[data-bloco="aberto"] .mural-numeros');
  const itens = [...lista.querySelectorAll('li')];
  const topo = (el) => Math.round(el.getBoundingClientRect().top);
  const esq = (el) => Math.round(el.getBoundingClientRect().left);

  return {
    quantos: itens.length,
    // ⚠ VERTICAL: o segundo item fica ABAIXO do primeiro, não ao lado. Com a grade antiga eles
    // ficavam na mesma linha, e o olho tinha de varrer na horizontal para achar um número.
    segundoAbaixoDoPrimeiro: itens.length > 1 && topo(itens[1]) > topo(itens[0]),
    segundoNaMesmaColuna: itens.length > 1 && esq(itens[1]) === esq(itens[0]),
    tamanho: getComputedStyle(document.querySelector('.mural-numero')).fontSize,
    fluxo: getComputedStyle(lista).columnWidth,
  };
});

console.log(JSON.stringify(mural, null, 2));

if (!mural.segundoAbaixoDoPrimeiro || !mural.segundoNaMesmaColuna) {
  problemas.push('RN-VEN-054: os números não estão fluindo na vertical');
}

await foto(pagina, '16-mural-vertical');

const houveProblema = relatar(problemas);
await navegador.close();

process.exitCode = houveProblema ? 1 : 0;
