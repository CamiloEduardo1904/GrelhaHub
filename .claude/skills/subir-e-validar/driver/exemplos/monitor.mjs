/**
 * A validação da **fila de monitoramento** — `SPEC-VEN-004`, `RN-VEN-054`, `RN-VEN-055`.
 *
 * É a validação real de 19/08/2026, guardada como exemplo porque foi ela que pegou o defeito que
 * nenhum gate podia ver: a fila subiu **vazia na tela e cheia na API**, porque a trava de "uma
 * requisição por vez" era um `useRef` e sobrevivia à remontagem do efeito.
 *
 * **Copie este arquivo como ponto de partida.** O que ele demonstra, e que vale para qualquer tela
 * ao vivo:
 *
 * 1. Sondar o DOM e **imprimir o que se viu** — não só olhar a captura
 * 2. **Medir** cadência de polling, em segundos, em vez de afirmar que "está atualizando"
 * 3. Provar que a corrente **suspende** ao sair da aba
 * 4. Provar que **nenhuma outra corrente** está perguntando em paralelo
 * 5. Provar que um dado proibido **não chegou** à tela
 *
 * ⚠ Roda depois de `subir.sh` e `semear.sh`. Leva ~4 min: a medida de cadência ociosa espera 150 s,
 * e não há como encurtar sem deixar de medir a coisa.
 */
import {
  abrir,
  aba,
  alturasDeToque,
  conferirChapa02,
  contador,
  entrar,
  foto,
  relatar,
  rolaNaHorizontal,
} from '../navegador.mjs';

const { navegador, pagina, problemas } = await abrir();

// ── A fila, com movimento ────────────────────────────────────────────────────
console.log('→ /painel · aba Monitor');
await entrar(pagina, 'loja');

const abas = await pagina.$$eval('.aba', (bs) => bs.map((b) => b.textContent.trim()));
console.log('  abas:', JSON.stringify(abas));

const naFila = contador(pagina, /\/pedidos\/fila/);
const naLista = contador(pagina, /\/v1\/pedidos(\?|$)/);

await aba(pagina, 'Monitor', '.comanda');
await pagina.waitForTimeout(1500);

const blocos = await pagina.$$eval('.bloco-de-fila', (bs) =>
  bs.map((b) => ({
    titulo: b.querySelector('.divisor-titulo')?.textContent,
    resumo: b.querySelector('.divisor-resumo')?.textContent,
    comandas: [...b.querySelectorAll('.comanda')].map((c) => ({
      numero: c.querySelector('.numero-pedido')?.textContent,
      dia: c.querySelector('.comanda-dia')?.textContent ?? null,
      selo: c.querySelector('.selo')?.textContent,
      espera: c.querySelector('.comanda-espera')?.textContent ?? null,
      acao: c.querySelector('.comanda-acoes button')?.textContent ?? null,
      temObservacao: !!c.querySelector('.comanda-obs'),
    })),
  })),
);
console.log(JSON.stringify(blocos, null, 2));
await foto(pagina, '01-monitor', { fullPage: true });

// RN-VEN-054 — os dois campos que a API NÃO devolve. A prova é o texto da tela, não o DTO:
// o compilador já sabe que a propriedade não existe; o que a regra promete é que o dado não
// atravessa a rede.
console.log('\n→ RN-VEN-054 · o que não pode estar na tela');
const naTela = await pagina.evaluate(() => document.body.innerText);
console.log('  telefone visível:', /\(?\d{2}\)?\s?9\d{4}-?\d{4}/.test(naTela));
console.log('  rótulo de observação visível:', /observa/i.test(naTela));

// CHAPA-02 — o grupo é `.bloco-de-fila > header`, com filho DIRETO. Ver o aviso em navegador.mjs.
console.log('\n→ CHAPA-02 · uma barra âmbar por grupo');
console.log(JSON.stringify(await conferirChapa02(pagina, '.bloco-de-fila > header')));

// ── RN-VEN-055 · a cadência COM movimento ───────────────────────────────────
console.log('\n→ RN-VEN-055 · cadência com a fila cheia (40 s) — espera PAR-VEN-005, 15 s');
// ⚠ Zerar ANTES da janela: `total` é acumulado desde a criação do contador, e as consultas que a
// aba Pedidos fez durante o login contariam aqui como se fossem do período medido. Foi assim que
// este exemplo acusou "3 consultas com o monitor à frente" — as três eram da aba anterior.
naLista.zerar();
console.log(' ', JSON.stringify(await naFila.medir(40)));

// ⚠⚠ A corrente da LISTA não pode estar perguntando junto. Antes de 19/08/2026 o `setInterval` do
// painel era incondicional, e abrir o monitor SOMAVA uma corrente em vez de trocá-la.
console.log('  consultas à lista no período (espera 0):', naLista.total);

// ── A ação reconsulta na hora, e o pedido SAI da fila ───────────────────────
console.log('\n→ avançando um pedido pronto (RN-VEN-018)');
const antes = naFila.total;
const comandasAntes = await pagina.$$eval('.comanda', (c) => c.length);

await pagina.click('.bloco-de-fila:nth-of-type(2) .comanda-acoes .btn');
await pagina.waitForTimeout(2500);

console.log('  consultas disparadas pela ação:', naFila.total - antes, '(espera 1)');
console.log(
  `  comandas: ${comandasAntes} → ${await pagina.$$eval('.comanda', (c) => c.length)}`,
  '— entregue SAI da fila',
);
console.log(
  '  blocos:',
  JSON.stringify(await pagina.$$eval('.divisor-resumo', (s) => s.map((x) => x.textContent))),
);

// ── A suspensão por aba ─────────────────────────────────────────────────────
console.log('\n→ RN-VEN-055 · outra aba à frente (35 s) — espera ZERO consultas');
await aba(pagina, 'Cardápio', '.cardapio-gestao');
console.log(' ', JSON.stringify(await naFila.medir(35)));

console.log('\n→ de volta em Pedidos (35 s) — a corrente da LISTA volta');
await aba(pagina, 'Pedidos');
console.log(' ', JSON.stringify(await naLista.medir(35)));

// ── Celular ─────────────────────────────────────────────────────────────────
console.log('\n→ 390×844 · celular');
await pagina.setViewportSize({ width: 390, height: 844 });
await aba(pagina, 'Monitor', '.comanda');
await pagina.waitForTimeout(1500);

console.log('  rolagem horizontal:', await rolaNaHorizontal(pagina), '(precisa ser false)');
console.log(
  '  CHAPA-04 · altura dos botões:',
  JSON.stringify(await alturasDeToque(pagina, '.comanda-acoes .btn')),
  '(precisa ser 48)',
);
await foto(pagina, '02-monitor-celular', { fullPage: true });

// ── A cadência OCIOSA, na casa sem pedido ───────────────────────────────────
//
// ⚠ Precisa de outra sessão: a cadência ociosa só existe com a fila VAZIA, e `casa-vazia` é o
// restaurante que o semeador cria justamente para isto.
console.log('\n→ RN-VEN-055 · cadência OCIOSA em casa-vazia (150 s)');
console.log('   espera 15 s, 15 s, 15 s e depois 60 s — PAR-VEN-009 = 4 ciclos, PAR-VEN-008 = 60 s');

await pagina.setViewportSize({ width: 1280, height: 900 });
await entrar(pagina, 'vazia');
await aba(pagina, 'Monitor', '.bloco-de-fila');

const medida = await naFila.medir(150);
console.log(' ', JSON.stringify(medida));
console.log('  aviso na tela:', await pagina.$eval('.ao-vivo', (e) => e.textContent));
await foto(pagina, '03-monitor-fila-vazia');

const houveProblema = relatar(problemas);
await navegador.close();

process.exitCode = houveProblema ? 1 : 0;
