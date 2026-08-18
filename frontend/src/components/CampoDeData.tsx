import { useEffect, useId, useState } from 'react';

/**
 * O campo de data — `docs/08-design-system.md` §3.
 *
 * **Substitui `<input type="date">` pelo mesmo motivo que `<Dialogo />` substituiu `confirm()`:**
 * o calendário nativo não é estilizável. Ele chega com azul de sistema, cantos arredondados,
 * tipografia do navegador e um ícone que não é nosso — dentro de uma interface que recusa
 * gradiente, raio de desfoque e cor pastel (ADR-0012). Nenhuma regra de CSS alcança o popover:
 * `::-webkit-calendar-picker-indicator` muda o ícone e nada mais.
 *
 * **O que entra no lugar é digitação, não um calendário artesanal.** Calendário próprio é um mês
 * de teclado, foco e leitor de tela para resolver o que, nesta tela, é escolher um intervalo de
 * dia operacional — e os intervalos que a operação pede de verdade estão nos atalhos ao lado
 * (`hoje`, `ontem`, `7 dias`). Se um dia aparecer tela que precise navegar meses, o calendário
 * entra aqui dentro, e nenhuma chamada muda.
 *
 * O valor de entrada e de saída é **ISO** (`AAAA-MM-DD`), que é o que a API recebe; o que o
 * usuário lê e digita é `DD/MM/AAAA`, que é como se escreve data no Brasil. A tradução vive neste
 * arquivo, e é o motivo de ele existir em vez de dois `useState` espalhados por tela.
 */

export function CampoDeData({
  rotulo,
  valor,
  aoMudar,
  descricao,
}: {
  rotulo: string;

  /** ISO `AAAA-MM-DD`, ou vazio. */
  valor: string;

  /** Chamado apenas com data **válida** — ou com vazio, quando o campo é limpo. */
  aoMudar: (iso: string) => void;

  descricao?: string;
}) {
  const id = useId();
  const [texto, setTexto] = useState(() => paraBrasileiro(valor));
  const [tocado, setTocado] = useState(false);

  // O valor pode mudar por fora — é o que os atalhos fazem. Sem isto, clicar em "7 dias" moveria
  // a consulta e deixaria o campo exibindo a data antiga.
  useEffect(() => {
    setTexto(paraBrasileiro(valor));
  }, [valor]);

  const digitos = texto.replaceAll(/\D/gu, '');
  const completo = digitos.length === 8;
  const invalido = completo && paraIso(texto) === null;

  // Incompleto não é erro enquanto se digita: `1` a caminho de `18/08/2026` não é data errada.
  const mostrarErro = invalido || (tocado && digitos.length > 0 && !completo);

  function digitar(bruto: string) {
    const proximo = mascarar(bruto);
    setTexto(proximo);

    if (proximo === '') {
      aoMudar('');
      return;
    }

    const iso = paraIso(proximo);
    if (iso !== null) aoMudar(iso);
  }

  return (
    <label className="campo-data" htmlFor={id}>
      {rotulo}

      <input
        id={id}
        // `text` e não `date`: é a linha inteira desta decisão.
        type="text"
        inputMode="numeric"
        className="dado"
        value={texto}
        placeholder="DD/MM/AAAA"
        maxLength={10}
        aria-invalid={mostrarErro}
        aria-describedby={mostrarErro ? `${id}-erro` : undefined}
        onChange={(e) => digitar(e.target.value)}
        onBlur={() => setTocado(true)}
      />

      {descricao && <span className="ajuda">{descricao}</span>}

      {mostrarErro && (
        <span className="erro" id={`${id}-erro`}>
          {invalido ? 'Esta data não existe.' : 'Complete a data — DD/MM/AAAA.'}
        </span>
      )}
    </label>
  );
}

/** `AAAA-MM-DD` → `DD/MM/AAAA`. Entrada vazia ou fora do formato devolve vazio, sem lançar. */
function paraBrasileiro(iso: string): string {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/u.exec(iso);
  return partes ? `${partes[3]}/${partes[2]}/${partes[1]}` : '';
}

/**
 * `DD/MM/AAAA` → `AAAA-MM-DD`, ou `null` se a data não existir.
 *
 * ⚠ A conferência é por **ida e volta** no `Date`, e não por faixa de dia e mês: `31/02` passa em
 * qualquer teste de faixa, e o `Date` o transforma em 3 de março sem reclamar. Comparar o que
 * voltou com o que entrou é o que pega isso.
 */
function paraIso(texto: string): string | null {
  const partes = /^(\d{2})\/(\d{2})\/(\d{4})$/u.exec(texto);
  if (!partes) return null;

  const [, dia, mes, ano] = partes;

  // UTC: `new Date(2026, 7, 18)` é meia-noite LOCAL, e em fuso negativo o `getUTCDate` volta 17.
  const data = new Date(Date.UTC(Number(ano), Number(mes) - 1, Number(dia)));

  const mesmo =
    data.getUTCFullYear() === Number(ano) &&
    data.getUTCMonth() === Number(mes) - 1 &&
    data.getUTCDate() === Number(dia);

  return mesmo ? `${ano}-${mes}-${dia}` : null;
}

/** Só dígitos, com as barras postas nas posições certas enquanto se digita. */
function mascarar(bruto: string): string {
  const d = bruto.replaceAll(/\D/gu, '').slice(0, 8);

  if (d.length <= 2) return d;
  if (d.length <= 4) return `${d.slice(0, 2)}/${d.slice(2)}`;

  return `${d.slice(0, 2)}/${d.slice(2, 4)}/${d.slice(4)}`;
}

