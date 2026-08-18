import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Grelha } from './Grelha';

/**
 * O diálogo de confirmação — `docs/08-design-system.md` §3.
 *
 * **Substitui `confirm()` e `prompt()` do navegador.** Os nativos não são estilizáveis: aparecem
 * roxos, com cantos arredondados e o nome do host — `localhost:5173 diz` —, tudo o que o ADR-0012
 * recusa. E o `prompt()` é pior que feio: não valida nada, então um motivo vazio ou de um caractere
 * chegava ao servidor para tomar 422 depois.
 *
 * **Usa o `<dialog>` nativo, e não uma `div` posicionada.** De graça vêm o `::backdrop`, a
 * armadilha de foco, o fechamento por `Esc` e o `aria-modal` — quatro coisas que modal artesanal
 * costuma errar, e as três últimas são de acessibilidade, que o ADR-0012 assumiu como custo nosso.
 */

export interface CampoDeMotivo {
  rotulo: string;

  /** Mínimo de caracteres, espelhando o validator do servidor. */
  minimo: number;

  exemplo?: string;
}

export function Dialogo({
  titulo,
  descricao,
  motivo,
  children,
  impedido: impedidoPeloDono = false,
  rotuloConfirmar,
  perigo = false,
  aoConfirmar,
  aoFechar,
}: {
  titulo: string;
  descricao?: string;

  /** Quando informado, o diálogo exige um texto antes de confirmar. */
  motivo?: CampoDeMotivo;

  /**
   * Campos próprios do formulário — nome, preço, ordem. Renderizados entre a descrição e as ações.
   *
   * **Quem passa `children` valida por conta própria, com `impedido`.** O diálogo sabe conferir um
   * texto com mínimo de caracteres (`motivo`); não tem como saber se um preço é positivo ou se uma
   * categoria foi escolhida.
   */
  children?: ReactNode;

  /** Trava o botão de confirmar. Para uso com `children`; combina com a regra do `motivo`. */
  impedido?: boolean;

  rotuloConfirmar: string;

  /** Ação destrutiva ou irreversível: o botão vem em `--erro`. */
  perigo?: boolean;

  aoConfirmar: (motivo: string) => void | Promise<void>;
  aoFechar: () => void;
}) {
  const referencia = useRef<HTMLDialogElement>(null);
  const [texto, setTexto] = useState('');
  const [ocupado, setOcupado] = useState(false);

  useEffect(() => {
    // `showModal` é o que liga o `::backdrop` e a armadilha de foco. `open` no atributo abre o
    // diálogo SEM nada disso — é o erro mais comum com este elemento.
    referencia.current?.showModal();
  }, []);

  const falta = motivo ? motivo.minimo - texto.trim().length : 0;
  const impedido = ocupado || falta > 0 || impedidoPeloDono;

  async function confirmar() {
    setOcupado(true);

    try {
      await aoConfirmar(texto.trim());
    } finally {
      setOcupado(false);
    }
  }

  return (
    <dialog
      className="dialogo"
      ref={referencia}
      // `Esc` dispara `cancel`: sem isto o diálogo fecharia e o estado de quem o abriu continuaria
      // dizendo que está aberto — e ele nunca mais reabriria.
      onCancel={(evento) => {
        evento.preventDefault();
        aoFechar();
      }}
    >
      <header className="dialogo-cabecalho">
        {/* CHAPA-02 — uma barra âmbar: este diálogo é o "agora" da tela. */}
        <Grelha etapa={0} de={1} alta morta={perigo} />
        <h2>{titulo}</h2>
      </header>

      {descricao && <p className="dialogo-texto">{descricao}</p>}

      {/* Os campos do dono vêm antes do motivo: quando as duas coisas existem, o motivo é sempre
          a última pergunta — é o que justifica o que foi preenchido acima. */}
      {children && <div className="dialogo-campos">{children}</div>}

      {motivo && (
        <label>
          {motivo.rotulo}
          <input
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder={motivo.exemplo}
            autoFocus
            onKeyDown={(e) => {
              // Enter confirma, como no `prompt()` que este componente substituiu — mas só quando
              // o motivo já é válido.
              if (e.key === 'Enter' && !impedido) {
                e.preventDefault();
                void confirmar();
              }
            }}
          />
        </label>
      )}

      {motivo && texto.length > 0 && falta > 0 && (
        <p className="secundario">
          Faltam {falta} caractere{falta === 1 ? '' : 's'} — o motivo fica registrado na trilha e é
          lido depois.
        </p>
      )}

      <div className="dialogo-acoes">
        <button type="button" className="btn calada" disabled={ocupado} onClick={aoFechar}>
          Voltar
        </button>

        <button
          type="button"
          className={perigo ? 'btn perigo' : 'btn'}
          disabled={impedido}
          onClick={() => void confirmar()}
        >
          {ocupado ? 'Confirmando…' : rotuloConfirmar}
        </button>
      </div>
    </dialog>
  );
}
