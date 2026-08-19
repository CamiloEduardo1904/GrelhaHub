import { useState } from 'react';
import { formatarCpf, mascararCpf } from '../services/cpf';

/**
 * O CPF numa tabela — `RN-GER-039` ⚠.
 *
 * Mascarado por padrão (`•••.752.818-••`), inteiro só por ação explícita **nesta linha**. O
 * princípio é o da minimização: numa lista, o CPF não identifica ninguém melhor que nome e
 * e-mail, e a tela que os empilha concentra risco sem entregar utilidade — o caso pior é a
 * listagem da plataforma, que atravessa todas as casas (`RN-GER-014`).
 *
 * ⚠⚠ **Reduz exposição de tela, não acesso.** O número vem na resposta da API; quem abre o
 * inspetor o lê. Está dito na regra e na `SPEC-GER-007` como dívida: a minimização de verdade é a
 * listagem parar de devolvê-lo, o que exige separá-la da leitura que alimenta o formulário de
 * edição.
 *
 * **Por que não é o cadeado que a `CHAPA-05` proíbe.** Lá, o campo que o perfil não pode ver **não
 * vem** da API, e desenhar cadeado sugeriria que veio e foi escondido no cliente. Aqui quem olha
 * tem direito ao número — o que se controla é *quando ele acende*, e o botão diz exatamente isso.
 */
export function CpfNaListagem({ cpf }: { cpf?: string | null }) {
  const [revelado, setRevelado] = useState(false);

  // `RN-GER-023` — o usuário de plataforma não tem CPF, e ausência é estado válido.
  if (!cpf) {
    return <span className="secundario">—</span>;
  }

  return (
    <span className="cpf-listagem">
      {/* CHAPA-03 — CPF é dado: mono com tabular-nums, para a coluna não dançar entre as linhas. */}
      <span className="dado">{revelado ? formatarCpf(cpf) : mascararCpf(cpf)}</span>

      <button
        type="button"
        className="btn calada minuscula"
        aria-pressed={revelado}
        onClick={() => setRevelado((estava) => !estava)}
      >
        {revelado ? 'Ocultar' : 'Mostrar'}
      </button>
    </span>
  );
}
