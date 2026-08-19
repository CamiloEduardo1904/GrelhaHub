import { useEffect, useState } from 'react';
import { aplicarTema, lerTema, ROTULO_DO_TEMA, TEMAS, type Tema } from '../services/tema';
import { Grelha } from './Grelha';

/**
 * A escolha do tema, na barra do aplicativo — design system §2.1.
 *
 * **Três posições, e a primeira é `Sistema`.** Ele não é o mesmo que `Claro`: acompanha o aparelho,
 * que muda sozinho à noite em quase todo celular. Um alternador de duas posições obrigaria a
 * escolher para sempre no primeiro clique, sem caminho de volta.
 *
 * **`CHAPA-02`** — exatamente uma barra âmbar no grupo: a posição ativa. É o mesmo controle da
 * `<FiltroDePeriodo>` e do filtro de grupo, e de propósito: um alternador com vocabulário próprio
 * seria a quarta forma de dizer "escolha uma destas" na mesma tela.
 *
 * **`CHAPA-06`** — nada de 🌞/🌙. Emoji renderiza diferente em cada sistema e não tem contraste
 * garantido; aqui o estado é **palavra**, que é o que a Chapa usa em toda parte.
 */
export function AlternadorDeTema() {
  const [tema, setTema] = useState<Tema>(lerTema);

  useEffect(() => {
    aplicarTema(tema);
  }, [tema]);

  return (
    <div className="alternador-de-tema chips" role="group" aria-label="Tema da interface">
      {TEMAS.map((opcao) => (
        <button
          key={opcao}
          type="button"
          className="chip"
          aria-pressed={tema === opcao}
          onClick={() => setTema(opcao)}
        >
          <Grelha etapa={tema === opcao ? 0 : -1} de={1} />
          {ROTULO_DO_TEMA[opcao]}
        </button>
      ))}
    </div>
  );
}
