/**
 * Datas do dia civil, para valor inicial de campo e para os atalhos de período.
 *
 * ⚠ **Nada aqui sabe de `RN-GER-006`.** Estes são dias de calendário; o recorte por **dia
 * operacional** — 04h00 às 03h59 — é feito no servidor, pela coluna gerada `data_operacional`.
 * Quem quiser calcular dia operacional no cliente estará escrevendo a segunda implementação de uma
 * regra estrutural, que é exatamente o que `DiaOperacionalParidadeTests` existe para impedir.
 *
 * Arquivo próprio, e não dentro de `CampoDeData.tsx`, porque arquivo de componente exporta
 * componente: o `react-refresh` avisa, e ele está certo — trocar uma função utilitária não deve
 * derrubar o estado da tela no recarregamento.
 */

/** `AAAA-MM-DD` de hoje, no fuso de quem está olhando. */
export function hojeIso(): string {
  return isoDe(new Date());
}

/** `AAAA-MM-DD` de N dias atrás. Negativo não é caso de uso, e por isso não é tratado. */
export function diasAtras(dias: number): string {
  const data = new Date();
  data.setDate(data.getDate() - dias);

  return isoDe(data);
}

function isoDe(data: Date): string {
  const mes = String(data.getMonth() + 1).padStart(2, '0');
  const dia = String(data.getDate()).padStart(2, '0');

  return `${data.getFullYear()}-${mes}-${dia}`;
}
