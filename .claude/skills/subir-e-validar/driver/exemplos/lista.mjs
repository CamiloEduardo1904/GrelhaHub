/**
 * A **lista de pedidos por grupo** — `RN-VEN-057` ⚠, `RN-GER-006` ⚠⚠.
 *
 * ⚠ Rode com `PICO=54 bash semear.sh`: a regra nasceu de um problema de **volume**, e com 6 pedidos
 * o antes e o depois são visualmente iguais.
 *
 * O que este exemplo prova:
 *
 * 1. Os três grupos aparecem **separados por divisor** com o filtro em *todos*
 * 2. Concluído e cancelado chegam **fechados**; o que está na casa chega **aberto**
 * 3. O clique **abre e fecha**, nos dois sentidos
 * 4. O filtro reduz a tela, e as contagens dos chips continuam sendo as do **período**
 * 5. `CHAPA-04` — o alternador tem 48 px
 * 6. A rolagem que a regra existe para cortar, medida antes e depois do filtro
 */
import {
  abrir,
  aba,
  alturasDeToque,
  entrar,
  foto,
  relatar,
  rolaNaHorizontal,
} from '../navegador.mjs';

const { navegador, pagina, problemas } = await abrir({ largura: 1280, altura: 900 });

await entrar(pagina, 'loja');
await aba(pagina, 'Pedidos', '.filtro-de-grupo');
await pagina.waitForTimeout(1200);

const sondar = () =>
  pagina.evaluate(() => ({
    chips: [...document.querySelectorAll('.filtro-de-grupo .chip')].map((c) => ({
      rotulo: c.textContent.replace(/\s+/g, ' ').trim(),
      ativo: c.getAttribute('aria-pressed') === 'true',
      ambar: c.querySelectorAll('.grelha i.ativa').length,
    })),
    dias: document.querySelectorAll('.dia-de-pedidos').length,
    grupos: [...document.querySelectorAll('.grupo-de-pedidos')].map((g) => ({
      grupo: g.dataset.grupo,
      divisor: g.querySelector('.divisor-grupo')?.textContent.replace(/\s+/g, ' ').trim() ?? null,
      comandas: g.querySelectorAll('.comanda').length,
      fechadas: g.querySelectorAll('.comanda-fechada').length,
    })),
    alturaDaPagina: document.documentElement.scrollHeight,
    telasDeRolagem: +(document.documentElement.scrollHeight / 900).toFixed(1),
  }));

console.log('→ filtro em TODOS');
const todos = await sondar();
console.log(JSON.stringify(todos, null, 2));
await foto(pagina, '09-lista-todos', { fullPage: true });
await foto(pagina, '10-lista-primeira-tela');

// ⚠ A asserção central de RN-VEN-057: o que já saiu chega fechado, o que está na casa chega aberto.
for (const g of todos.grupos) {
  const esperado = g.grupo === 'casa' ? 0 : g.comandas;
  if (g.fechadas !== esperado) {
    problemas.push(
      `RN-VEN-057: grupo "${g.grupo}" com ${g.fechadas} fechadas de ${g.comandas}; esperado ${esperado}`,
    );
  }
}

// ⚠ A página NUNCA rola na horizontal — nem com o cartão fechado, que tem cinco elementos numa
// linha só e foi onde o total transbordou da coluna de 255 px.
const rolaLado = await rolaNaHorizontal(pagina);
console.log('\n→ rolagem horizontal:', rolaLado, '(precisa ser false)');
if (rolaLado) problemas.push('A lista rola na horizontal — algo transbordou do cartão');

console.log('\n→ CHAPA-04 · altura do alternador');
console.log(' ', JSON.stringify(await alturasDeToque(pagina, '.comanda-abre')), '(espera 48+)');

// ── o clique abre, e fecha de volta ─────────────────────────────────────────
console.log('\n→ o clique nos dois sentidos');
const primeiraFechada = '.grupo-de-pedidos[data-grupo="concluido"] .comanda-fechada .comanda-abre';
await pagina.click(primeiraFechada);
await pagina.waitForTimeout(400);

const abriu = await pagina.evaluate(() => {
  const g = document.querySelector('.grupo-de-pedidos[data-grupo="concluido"]');
  const primeira = g.querySelector('.comanda');
  return {
    aindaFechada: primeira.classList.contains('comanda-fechada'),
    temItens: !!primeira.querySelector('.comanda-itens'),
    temTotal: !!primeira.querySelector('.comanda-soma'),
    temObservacao: !!primeira.querySelector('.comanda-obs'),
    ariaExpanded: primeira.querySelector('.comanda-abre')?.getAttribute('aria-expanded'),
  };
});
console.log('  depois de abrir:', JSON.stringify(abriu));

if (abriu.aindaFechada || !abriu.temItens) {
  problemas.push('RN-VEN-057: o clique não abriu a comanda');
}
// ⚠⚠ "Colapsar não é esconder": a observação do cliente tem de VOLTAR inteira (RN-VEN-052).
if (!abriu.temObservacao) {
  problemas.push('RN-VEN-052: a observação não voltou ao abrir a comanda');
}

await pagina.click('.grupo-de-pedidos[data-grupo="concluido"] .comanda button.comanda-cab');
await pagina.waitForTimeout(400);
const fechou = await pagina.evaluate(() =>
  document
    .querySelector('.grupo-de-pedidos[data-grupo="concluido"] .comanda')
    .classList.contains('comanda-fechada'),
);
console.log('  fechou de volta:', fechou);
if (!fechou) problemas.push('RN-VEN-057: o colapso é via de mão única — não fechou de volta');

// ── o filtro ───────────────────────────────────────────────────────────────
for (const rotulo of ['Na casa', 'Concluídos', 'Cancelados']) {
  console.log(`\n→ filtro em ${rotulo}`);
  await pagina.click(`.filtro-de-grupo .chip:has-text("${rotulo}")`);
  await pagina.waitForTimeout(600);

  const r = await sondar();
  console.log(
    '  grupos na tela:',
    JSON.stringify(r.grupos.map((g) => g.grupo)),
    '· divisor de grupo:',
    r.grupos[0]?.divisor ?? '(nenhum, correto)',
    '· telas:',
    r.telasDeRolagem,
  );
  console.log('  chips:', JSON.stringify(r.chips.map((c) => c.rotulo)));

  // Com um grupo em foco o subdivisor não aparece: o chip já disse o que está na tela.
  if (r.grupos.some((g) => g.divisor !== null)) {
    problemas.push(`RN-VEN-057: subdivisor visível com "${rotulo}" em foco`);
  }
  // CHAPA-02 — exatamente um chip âmbar.
  const ambares = r.chips.filter((c) => c.ambar === 1).length;
  if (ambares !== 1) problemas.push(`CHAPA-02: ${ambares} chips âmbar no filtro`);
}

await foto(pagina, '11-lista-filtrada', { fullPage: true });

// ── o número que justifica a regra ──────────────────────────────────────────
//
// ⚠ "A tela ficou menor" não vale nada como afirmação. O que vale é a MESMA lista medida nos dois
// estados: como ela chega, e como ela ficaria se nada colapsasse. A diferença é o que RN-VEN-057
// entrega — e se um dia ela encolher, é aqui que aparece.
console.log('\n→ quanto o colapso economiza, na mesma lista');
await pagina.click('.filtro-de-grupo .chip:has-text("Todos")');
await pagina.waitForTimeout(600);

const comColapso = (await sondar()).telasDeRolagem;

const fechadas = await pagina.$$('.comanda-fechada .comanda-abre');
for (const alvo of fechadas) {
  await alvo.click();
}
await pagina.waitForTimeout(800);

const semColapso = (await sondar()).telasDeRolagem;

console.log(`  como chega:      ${comColapso} telas`);
console.log(`  tudo aberto:     ${semColapso} telas  (${fechadas.length} comandas abertas à mão)`);
console.log(`  economia:        ${(semColapso - comColapso).toFixed(1)} telas`);
await foto(pagina, '12-lista-tudo-aberto', { fullPage: true });

const houveProblema = relatar(problemas);
await navegador.close();

process.exitCode = houveProblema ? 1 : 0;
