import { useEffect, useId, useState } from 'react';

/**
 * O campo de hora — `docs/08-design-system.md` §3.
 *
 * **Substitui `<input type="time">` pelo mesmo motivo de `<CampoDeData />`:** o widget nativo não é
 * estilizável. Ele chega com o ícone de relógio do navegador, o seletor de AM/PM em alguns locales
 * e a tipografia do sistema — três coisas que não são nossas, dentro de uma interface que recusa
 * exatamente isso (ADR-0012). Numa lista de sete dias o efeito é pior do que num campo isolado: as
 * sete linhas parecem um formulário de outro produto.
 *
 * Entra e sai em `HH:MM` (ou `HH:MM:SS`, que é o que a API devolve — o componente corta os
 * segundos). Vazio é estado válido e significa **dia fechado** em `RN-GER-036`.
 */

export function CampoDeHora({
  rotulo,
  valor,
  aoMudar,
  desabilitado = false,
}: {
  /** Rótulo acessível. Não é exibido: numa grade de dias, o rótulo visível é o dia. */
  rotulo: string;

  /** `HH:MM`, `HH:MM:SS` ou vazio. */
  valor: string;

  /** Recebe `HH:MM` quando a hora é válida, ou vazio. */
  aoMudar: (hora: string) => void;

  desabilitado?: boolean;
}) {
  const id = useId();
  const [texto, setTexto] = useState(() => normalizar(valor));

  // O valor muda por fora quando o formulário carrega ou quando o dia é marcado como fechado.
  useEffect(() => {
    setTexto(normalizar(valor));
  }, [valor]);

  const digitos = texto.replaceAll(/\D/gu, '');
  const invalido = digitos.length === 4 && horaValida(texto) === null;

  function digitar(bruto: string) {
    const proximo = mascarar(bruto);
    setTexto(proximo);

    if (proximo === '') {
      aoMudar('');
      return;
    }

    const hora = horaValida(proximo);
    if (hora !== null) aoMudar(hora);
  }

  return (
    <input
      id={id}
      // `text` e não `time`: é a linha inteira desta decisão.
      type="text"
      inputMode="numeric"
      className="dado campo-hora"
      aria-label={rotulo}
      aria-invalid={invalido}
      value={texto}
      placeholder="--:--"
      maxLength={5}
      disabled={desabilitado}
      onChange={(e) => digitar(e.target.value)}
    />
  );
}

/** `HH:MM:SS` → `HH:MM`. O que a API devolve tem segundos; o que a pessoa lê, não. */
function normalizar(valor: string): string {
  if (!valor) {
    return '';
  }

  const partes = /^(\d{2}):(\d{2})/u.exec(valor);

  return partes ? `${partes[1]}:${partes[2]}` : '';
}

/**
 * `HH:MM` válido, ou `null`.
 *
 * ⚠ `24:00` é recusado: o dia tem 24 horas e a última é `23`. Uma casa que fecha "às 24h" fecha à
 * meia-noite, que é `00:00` — e aceitar `24:00` produziria uma virada de dia operacional que o
 * PostgreSQL trata como 24 horas de deslocamento, jogando a apuração um dia inteiro para trás.
 */
function horaValida(texto: string): string | null {
  const partes = /^(\d{2}):(\d{2})$/u.exec(texto);
  if (!partes) return null;

  const hora = Number(partes[1]);
  const minuto = Number(partes[2]);

  if (hora > 23 || minuto > 59) return null;

  return `${partes[1]}:${partes[2]}`;
}

/** Só dígitos, com os dois-pontos no lugar enquanto se digita. */
function mascarar(bruto: string): string {
  const d = bruto.replaceAll(/\D/gu, '').slice(0, 4);

  return d.length <= 2 ? d : `${d.slice(0, 2)}:${d.slice(2)}`;
}
