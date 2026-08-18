/**
 * Os tipos da área de plataforma — `RN-GER-014`, ADR-0013.
 *
 * Ficam aqui e não em `services/tipos.ts` porque nada disto existe para o restaurante: são
 * contratos das rotas `/v1/plataforma/*`, que o painel da loja nunca chama.
 */

export interface Estabelecimento {
  id: string;
  nomeFantasia: string;
  razaoSocial: string;
  cnpj: string;
  slug: string;
  ativa: boolean;
  criadoEm: string;
  urlLogo?: string | null;
  usuarios: number;

  /** Siglas dos módulos **vigentes agora** — `RN-GER-021`. */
  modulos: string[];
}

export interface ModuloDoCatalogo {
  sigla: string;
  nome: string;
  nucleo: boolean;
  ordem: number;

  /** Dependências **diretas** — `RN-GER-020`. O fecho transitivo é calculado na tela. */
  dependeDe: string[];
}

/**
 * `RN-GER-020` — o fecho transitivo de `siglas` sobre o grafo, incluindo as próprias.
 *
 * O grafo vem do servidor (`GET /v1/plataforma/modulos`): uma cópia dele aqui divergiria do banco,
 * e a divergência apareceria ao vendedor como "marquei e o sistema recusou sem explicar".
 */
export function comDependencias(siglas: string[], catalogo: ModuloDoCatalogo[]): string[] {
  const porSigla = new Map(catalogo.map((m) => [m.sigla, m]));
  const resultado = new Set(siglas);
  const fila = [...siglas];

  while (fila.length > 0) {
    const atual = fila.pop()!;

    for (const dependencia of porSigla.get(atual)?.dependeDe ?? []) {
      // `has` antes de `add` é o que faz isto TERMINAR se o grafo tiver ciclo.
      if (!resultado.has(dependencia)) {
        resultado.add(dependencia);
        fila.push(dependencia);
      }
    }
  }

  return [...resultado];
}

/** Quem, dentro de `siglas`, depende (direta ou transitivamente) de `sigla`. */
export function dependentesDe(
  sigla: string,
  siglas: string[],
  catalogo: ModuloDoCatalogo[],
): string[] {
  return siglas.filter(
    (candidata) => candidata !== sigla && comDependencias([candidata], catalogo).includes(sigla),
  );
}

/**
 * Quais módulos já têm código.
 *
 * **Não é regra de negócio** — é `CHAPA-09` aplicada à tela interna: habilitar um módulo sem
 * implementação entrega ao cliente uma sigla na fatura e nenhuma tela. O servidor aceita, porque a
 * habilitação é fato comercial legítimo (a modelagem reserva o espaço); quem precisa avisar é a
 * interface de quem vende.
 */
export const COM_CODIGO = new Set(['GER', 'VEN']);
