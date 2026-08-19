/**
 * O **painel de senha** no pico — `RN-VEN-054` ⚠⚠, `RN-VEN-056`, `SPEC-VEN-004`.
 *
 * ⚠⚠ **Rode com `PICO=54 bash semear.sh`.** Com 6 pedidos qualquer desenho de fila parece certo; é
 * com 60 que a escala aparece. Foi assim que a versão de comanda completa se revelou errada:
 * quinze linhas de cartão, ~6 telas de rolagem, com a coluna "pronto" embaixo de todas elas.
 *
 * O que este exemplo mede, e que uma captura sozinha não diz:
 *
 * 1. **Quantas telas de rolagem** o painel ocupa com 60 pedidos
 * 2. Se as duas colunas estão **lado a lado** e separadas por traço
 * 3. Se a coluna **pronto** é alcançável sem rolar
 * 4. `CHAPA-02` — uma barra âmbar entre os dois cabeçalhos
 * 5. Que nem nome, nem telefone, nem observação chegaram à tela
 * 6. O modo mural (`RN-VEN-056`) e o tamanho do número nele
 */
import { abrir, aba, conferirChapa02, entrar, foto, relatar } from '../navegador.mjs';

const { navegador, pagina, problemas } = await abrir({ largura: 1280, altura: 900 });

await entrar(pagina, 'loja');
await aba(pagina, 'Monitor', '.mural-numeros');
await pagina.waitForTimeout(1500);

const medida = await pagina.evaluate(() => {
  const colunas = [...document.querySelectorAll('.mural-coluna')];
  const numeros = [...document.querySelectorAll('.mural-numero')];
  const pronto = document.querySelector('.mural-coluna[data-bloco="pronto"]');
  const estilo = (el, prop) => getComputedStyle(el)[prop];

  return {
    numerosNaTela: numeros.length,
    contagens: colunas.map((c) => ({
      bloco: c.dataset.bloco,
      rotulo: c.querySelector('h3')?.textContent,
      conta: c.querySelector('.mural-conta')?.textContent,
      numeros: c.querySelectorAll('.mural-numero').length,
    })),

    // Lado a lado: os dois topos na MESMA altura. Empilhadas, não estariam.
    ladoALado:
      colunas.length === 2
      && colunas[0].getBoundingClientRect().top === colunas[1].getBoundingClientRect().top,
    tracoQueDivide: estilo(colunas[1], 'borderLeftWidth'),

    // ⚠ A medida que reprovou o desenho anterior: quantas telas de 900 px o painel ocupa.
    alturaDaPagina: document.documentElement.scrollHeight,
    telasDeRolagem: +(document.documentElement.scrollHeight / 900).toFixed(2),

    // A coluna "pronto" está visível sem rolar?
    prontoAcimaDaDobra: pronto.getBoundingClientRect().top < 900,

    tamanhoDoNumero: estilo(numeros[0], 'fontSize'),
    familiaDoNumero: estilo(numeros[0], 'fontFamily').split(',')[0],
    diasMarcados: document.querySelectorAll('.mural-dia').length,
    assinaturaDaCasa: document.querySelector('.monitor .tagline') !== null,
    botaoTelaCheia: !!document.querySelector('.mural-controles .btn'),
  };
});

console.log('→ painel de senha com 60 pedidos vivos');
console.log(JSON.stringify(medida, null, 2));

console.log('\n→ CHAPA-02 · uma barra âmbar entre os dois cabeçalhos');
console.log(JSON.stringify(await conferirChapa02(pagina, '.mural-coluna > header')));

console.log('\n→ RN-VEN-054 · o que não pode estar na tela');
const texto = await pagina.evaluate(() => document.body.innerText);
for (const [rotulo, achou] of [
  ['nome de cliente', /Marina|Rogerio|Camila|Pico \d/.test(texto)],
  ['telefone', /\d{10,11}/.test(texto.replace(/\d{4}-\d{2}-\d{2}/g, ''))],
  ['observação', /cebola|maionese/i.test(texto)],
  ['assinatura "Do pedido à brasa"', /pedido à brasa/i.test(texto)],
]) {
  console.log(`  ${rotulo}: ${achou ? '⚠ PRESENTE' : 'ausente'}`);
  if (achou) problemas.push(`RN-VEN-054: ${rotulo} apareceu no painel de senha`);
}

await foto(pagina, '06-mural-60-pedidos', { fullPage: true });
await foto(pagina, '07-mural-primeira-tela');

// ── RN-VEN-056 · o modo mural ───────────────────────────────────────────────
//
// ⚠ A API de tela cheia exige gesto do usuário — o `click` do Playwright é um gesto de verdade.
// Em modo headless o navegador pode não redimensionar a janela; o que se confere aqui é o
// CONTRATO: a classe entra, os tokens do painel mudam de patamar, e o rótulo do botão inverte.
console.log('\n→ RN-VEN-056 · tela cheia');
await pagina.click('.mural-controles .btn');
await pagina.waitForTimeout(800);

const cheia = await pagina.evaluate(() => {
  const monitor = document.querySelector('.monitor');
  return {
    classeMural: monitor.classList.contains('mural'),
    fullscreenElement: document.fullscreenElement === monitor,
    rotuloDoBotao: document.querySelector('.mural-controles .btn')?.textContent,
    tamanhoDoNumero: getComputedStyle(document.querySelector('.mural-numero')).fontSize,
    fundoDeclarado: getComputedStyle(monitor).backgroundColor,
  };
});
console.log(JSON.stringify(cheia, null, 2));

if (cheia.classeMural) {
  await foto(pagina, '08-mural-tela-cheia');
}

const houveProblema = relatar(problemas);
await navegador.close();

process.exitCode = houveProblema ? 1 : 0;
