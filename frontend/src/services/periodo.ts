/**
 * O período de **dia operacional** — `RN-GER-006` ⚠⚠, `RN-VEN-050`.
 *
 * ⚠ **O que estas funções montam é recorte de dia operacional, não de data de calendário.**
 * "De 10 a 12" são três dias operacionais, cada um da virada cadastrada da casa (`RN-GER-036`) à
 * virada seguinte — e não 72 horas corridas. O pedido da 01h30 do dia 13 pertence ao dia 12. Quem
 * trocar isto por um recorte de `dh_criacao` vai produzir números plausíveis e errados, que é a
 * pior categoria de defeito de relatório.
 *
 * Vive em `services/` e não junto do componente porque **duas telas dependem dele** — a lista de
 * pedidos e o painel de operação —, e porque arquivo que exporta componente e função ao mesmo
 * tempo perde o recarregamento rápido do Vite.
 */

export interface Periodo {
  de: string;
  ate: string;
}

/** `AAAA-MM-DD` no fuso de quem está olhando — a data que a pessoa vê no relógio dela. */
export function iso(data: Date): string {
  return `${data.getFullYear()}-${String(data.getMonth() + 1).padStart(2, '0')}-${String(
    data.getDate(),
  ).padStart(2, '0')}`;
}

/** O dia corrente dos dois lados. É o padrão da lista de pedidos. */
export function periodoDeHoje(): Periodo {
  const hoje = iso(new Date());

  return { de: hoje, ate: hoje };
}

/** Os últimos `dias` dias, contando hoje: `periodoDeAtras(6)` são sete dias. */
export function periodoDeAtras(dias: number): Periodo {
  const fim = new Date();
  const inicio = new Date();
  inicio.setDate(inicio.getDate() - dias);

  return { de: iso(inicio), ate: iso(fim) };
}

/** `de=&ate=` para a consulta. */
export function comoConsulta(periodo: Periodo): string {
  return `de=${periodo.de}&ate=${periodo.ate}`;
}

/** O período está de trás para frente? A API recusa com 422 (`RN-GER-006`); a tela avisa antes. */
export function invertido(periodo: Periodo): boolean {
  return periodo.de !== '' && periodo.ate !== '' && periodo.ate < periodo.de;
}
