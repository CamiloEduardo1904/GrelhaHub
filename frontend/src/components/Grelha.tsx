/**
 * A listra da grelha — elemento estrutural da direção Chapa (ADR-0012).
 *
 * CHAPA-02 — em qualquer grupo, exatamente UMA barra é âmbar: a ativa.
 * Barras anteriores em laranja, seguintes apagadas.
 *
 * É decorativa (`aria-hidden`): quem comunica o estado é o `<Selo>`, porque
 * cor sozinha não comunica.
 */
export function Grelha({
  etapa,
  de,
  alta = false,
  morta = false,
}: {
  /** Índice da barra ativa, base 0. Use −1 para nenhuma. */
  etapa: number;
  de: number;
  alta?: boolean;
  /** Pedido cancelado: a régua inteira morre em vez de mostrar progresso. */
  morta?: boolean;
}) {
  return (
    <span className={alta ? 'grelha alta' : 'grelha'} aria-hidden="true">
      {Array.from({ length: de }, (_, i) => (
        <i key={i} className={classeDaBarra(i, etapa, morta)} />
      ))}
    </span>
  );
}

function classeDaBarra(indice: number, etapa: number, morta: boolean): string {
  if (morta) return 'morta';
  if (indice < etapa) return 'feita';
  if (indice === etapa) return 'ativa';
  return '';
}
