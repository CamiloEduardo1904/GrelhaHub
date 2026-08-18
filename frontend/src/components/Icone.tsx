/**
 * Ícones em **SVG inline** — `ADR-0012`, `docs/08-design-system.md`.
 *
 * **Por que não FontAwesome.** O ADR-0012 decidiu **sem webfont**: o painel roda no tablet da
 * cozinha, e um ícone que depende de fonte baixando no meio do serviço aparece como quadrado vazio
 * exatamente quando o restaurante está cheio. FontAwesome é webfont (ou um pacote de ~1 MB para
 * usar quatro glifos).
 *
 * **E por que não emoji.** `CHAPA-06` — emoji renderiza diferente em cada sistema e não tem
 * contraste garantido.
 *
 * Estes são traçados à mão, com `currentColor` e `stroke-width` casando com o traço de 2 px da
 * Chapa. `aria-hidden` sempre: o significado vai no texto do botão, porque ícone sozinho não
 * comunica — e o botão precisa de nome acessível de qualquer forma.
 */

type Props = {
  /** Tamanho em pixels. 18 é o que casa com a altura da linha de tabela. */
  tamanho?: number;
};

function Base({ tamanho = 18, children }: Props & { children: React.ReactNode }) {
  return (
    <svg
      width={tamanho}
      height={tamanho}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="square"
      strokeLinejoin="miter"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  );
}

/** O olho — "ver os detalhes". */
export function IconeOlho(props: Props) {
  return (
    <Base {...props}>
      <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6-10-6-10-6z" />
      <circle cx="12" cy="12" r="3" />
    </Base>
  );
}

/** O lápis — "editar". */
export function IconeLapis(props: Props) {
  return (
    <Base {...props}>
      <path d="M4 20h4L20 8l-4-4L4 16v4z" />
      <path d="M14 6l4 4" />
    </Base>
  );
}

/** A chave — "redefinir senha". */
export function IconeChave(props: Props) {
  return (
    <Base {...props}>
      <circle cx="8" cy="12" r="4" />
      <path d="M12 12h9" />
      <path d="M17 12v4" />
      <path d="M20 12v3" />
    </Base>
  );
}

/**
 * O interruptor — "inativar / reativar".
 *
 * Um traço cortando o círculo, não um X: `RN-GER-002` — nada é excluído, e o X sugeriria exclusão.
 */
export function IconeDesligar(props: Props) {
  return (
    <Base {...props}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v6" />
    </Base>
  );
}

/** O sinal de mais — "novo". */
export function IconeMais(props: Props) {
  return (
    <Base {...props}>
      <path d="M12 5v14" />
      <path d="M5 12h14" />
    </Base>
  );
}

/** Impressora — `RN-VEN-036`: o cartaz com o QR do cardápio. */
export function IconeImpressora(props: Props) {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" {...props}>
      <path d="M6 9V3h12v6" />
      <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
      <path d="M6 14h12v7H6z" />
    </svg>
  );
}
