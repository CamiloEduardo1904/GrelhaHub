/**
 * O tema da interface — design system §2.1.
 *
 * **Os dois temas já existiam; o que faltava era a escolha.** A folha define a paleta clara em
 * `:root`, e a escura em dois lugares — `@media (prefers-color-scheme: dark)` para quem não
 * escolheu, e `:root[data-theme="dark"]` para quem escolheu. Este módulo só mexe no atributo do
 * `<html>`; nenhuma cor vive aqui.
 *
 * > **Escuro = cozinha. Claro = administrativo.** Não é enfeite: são os dois ambientes reais do
 * > produto. O tablet ao lado da chapa e o computador do escritório não pedem a mesma tela.
 *
 * ⚠ **Três estados, e não dois.** `sistema` é o padrão e **não é o mesmo que `claro`**: ele
 * acompanha o aparelho, que muda sozinho à noite em quase todo celular. Um alternador de duas
 * posições obrigaria a escolher para sempre no primeiro clique, e não haveria caminho de volta.
 */
export type Tema = 'sistema' | 'claro' | 'escuro';

/**
 * ⚠ **`localStorage`, e não o perfil do usuário no banco.**
 *
 * A preferência é do **aparelho**, não da pessoa: a mesma gerente usa o tablet da cozinha no escuro
 * e o computador do escritório no claro. Guardar no servidor levaria a escolha de um para o outro,
 * que é exatamente o contrário do que ela quis.
 *
 * A chave segue o padrão de `services/api.ts` (`grelhahub.token`).
 */
const CHAVE = 'grelhahub.tema';

export const TEMAS: Tema[] = ['sistema', 'claro', 'escuro'];

export const ROTULO_DO_TEMA: Record<Tema, string> = {
  sistema: 'Sistema',
  claro: 'Claro',
  escuro: 'Escuro',
};

export function lerTema(): Tema {
  const guardado = localStorage.getItem(CHAVE);

  return TEMAS.includes(guardado as Tema) ? (guardado as Tema) : 'sistema';
}

/**
 * Aplica o tema e o guarda.
 *
 * ⚠ **`sistema` REMOVE o atributo**, não escreve `data-theme="sistema"`. A folha decide pela
 * ausência: sem atributo, o `@media (prefers-color-scheme: dark)` volta a mandar. Um valor
 * inventado ali deixaria a página presa no claro, que é o oposto de "acompanhe o aparelho".
 */
export function aplicarTema(tema: Tema): void {
  if (tema === 'sistema') {
    document.documentElement.removeAttribute('data-theme');
    localStorage.removeItem(CHAVE);
    return;
  }

  document.documentElement.setAttribute('data-theme', tema === 'claro' ? 'light' : 'dark');
  localStorage.setItem(CHAVE, tema);
}

/**
 * O que `index.html` roda **antes da primeira pintura**, para não haver piscada.
 *
 * ⚠ Sem isto, quem escolheu claro vê a tela **escura por um quadro** antes de o React montar — e o
 * inverso na cozinha, que é pior: um lampejo branco no escuro, a meio metro do rosto, no meio do
 * serviço. É o mesmo motivo de a Chapa não usar webfont: o que aparece antes de o pacote carregar
 * também é interface.
 *
 * Exportado daqui para o script inline e este módulo **não divergirem** — o `<script>` do HTML é
 * o único lugar do produto que roda antes do pacote, e nada nele deve ser escrito duas vezes.
 */
export const APLICAR_ANTES_DA_PINTURA = `
try {
  var t = localStorage.getItem('${CHAVE}');
  if (t === 'claro') document.documentElement.setAttribute('data-theme', 'light');
  if (t === 'escuro') document.documentElement.setAttribute('data-theme', 'dark');
} catch (e) {
  /* Modo privado sem storage: cai no tema do sistema, que é o padrão. */
}
`.trim();
