/**
 * A validação do **painel de operação** — `RN-VEN-049` a `RN-VEN-051`, e a régua da série longa
 * (`SPEC-VEN-004` §2).
 *
 * O que ela prova, e que nenhum teste de backend alcança: que o mês inteiro **cabe na tela** sem
 * que nenhum dia desapareça do gráfico.
 *
 * ⚠⚠ **`RN-VEN-050` proíbe a saída fácil.** Dia sem pedido não some, porque série com dia faltando
 * faz o olho ler queda onde houve fechamento. A régua muda o que cada barra **diz** — rótulo rareia,
 * valor só no melhor dia, tabela vira dobra — e **nunca quantas barras existem**. É essa distinção
 * que as asserções abaixo checam: `barras === dias do período`, sempre.
 */
import { abrir, aba, entrar, foto, relatar } from '../navegador.mjs';

const { navegador, pagina, problemas } = await abrir();

await entrar(pagina, 'loja');
await aba(pagina, 'Operação', '.operacao-painel');
await pagina.waitForTimeout(1500);

// A ORDEM é decisão (19/08/2026): o ranking sobe para logo abaixo dos indicadores, porque "o que
// vende" é acionável no mesmo turno e o gráfico de 30 dias empurrava tudo para baixo da dobra.
const ordem = await pagina.$$eval('.operacao-painel > *', (ns) =>
  ns.map((n) => n.className.split(' ')[0] || n.tagName.toLowerCase()),
);
console.log('→ ordem dos blocos:', JSON.stringify(ordem));
console.log('  esperada: filtro-periodo, indicadores, mais-vendidos, grafico');

const sondar = () =>
  pagina.evaluate(() => {
    const barras = [...document.querySelectorAll('.barra-dia')];
    const dobra = document.querySelector('.dobra');
    const preenchida = (s) => barras.filter((b) => b.querySelector(s)?.textContent.trim()).length;

    return {
      barras: barras.length,
      comRotulo: preenchida('.barra-rotulo'),
      comValor: preenchida('.barra-valor'),
      serieLonga: document.querySelector('.grafico-barras')?.getAttribute('data-longa') === 'true',
      dobraAberta: dobra?.hasAttribute('open') ?? null,
      linhasNaDobra: dobra?.querySelectorAll('tbody tr').length ?? 0,
      alturaDaPagina: document.documentElement.scrollHeight,
    };
  });

for (const [rotulo, dias] of [
  ['7 dias', 7],
  ['30 dias', 30],
]) {
  console.log(`\n→ ${rotulo}`);
  await pagina.click(`.chip:has-text("${rotulo}")`);
  await pagina.waitForTimeout(2000);

  const s = await sondar();
  console.log(' ', JSON.stringify(s, null, 2));

  // ⚠ A asserção que importa: NENHUMA barra some. O resto é apresentação.
  if (s.barras !== dias) {
    problemas.push(`RN-VEN-050: ${rotulo} devia ter ${dias} barras e tem ${s.barras}`);
  }
  if (s.linhasNaDobra !== dias) {
    problemas.push(`RN-VEN-050: a dobra devia ter ${dias} linhas e tem ${s.linhasNaDobra}`);
  }

  await foto(pagina, `04-operacao-${dias}dias`, { fullPage: true });
}

// A dobra abre e devolve tudo — dobrar não é truncar.
console.log('\n→ abrindo a dobra em 30 dias');
await pagina.click('.dobra summary');
await pagina.waitForTimeout(500);
console.log(' ', JSON.stringify(await sondar()));
await foto(pagina, '05-operacao-30dias-dobra-aberta', { fullPage: true });

const houveProblema = relatar(problemas);
await navegador.close();

process.exitCode = houveProblema ? 1 : 0;
