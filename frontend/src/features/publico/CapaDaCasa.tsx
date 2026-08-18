import { Grelha } from '../../components/Grelha';
import type { HorarioPublico } from '../../services/tipos';

/**
 * A capa da casa — usada na vitrine e no acompanhamento do pedido.
 *
 * **Componente, e não markup repetido nas duas telas.** Quando o cliente sai do cardápio para
 * acompanhar o pedido ele não muda de estabelecimento, e a tela precisa dizer isso sem que ele
 * pense a respeito: mesmo nome, mesma logo, mesmo endereço, no mesmo lugar.
 *
 * **`CHAPA-02`** — a listra da marca aparece **só quando a casa não tem logo**. Com logo, a marca
 * do GrelhaHub em cima dela competiria com a do restaurante na vitrine dele.
 */
export function CapaDaCasa({
  nomeFantasia,
  urlLogo,
  endereco,
  aberta,
  horarios,
  chamada,
}: {
  nomeFantasia: string;
  urlLogo?: string | null;
  endereco?: string | null;

  /** `undefined` some com o selo — o acompanhamento não precisa dizer se a loja está aberta. */
  aberta?: boolean;
  horarios?: HorarioPublico[];

  /** Substitui a assinatura "do pedido à brasa" — o acompanhamento diz o que a tela é. */
  chamada?: string;
}) {
  return (
    <header className="vitrine-capa">
      {/* RN-GER-026 — a logo da casa manda. A nossa marca só entra quando não há logo: numa
          vitrine de restaurante, quem assina é o restaurante. */}
      {urlLogo ? (
        <img className="logo-casa" src={urlLogo} alt={nomeFantasia} />
      ) : (
        <Grelha etapa={2} de={5} alta />
      )}

      <h1>{nomeFantasia}</h1>

      {/* CHAPA-10 — endereço é dado de contato, não rótulo: vai na cor do texto. */}
      {endereco && <p className="endereco-casa">{endereco}</p>}

      {aberta === false && (
        <p className="aviso-fechado">
          <strong>Fechado agora.</strong> {ProximaAbertura(horarios)}
        </p>
      )}

      <p className="tagline">{chamada ?? 'Do pedido à brasa'}</p>
    </header>
  );
}

const DIAS = ['domingo', 'segunda', 'terça', 'quarta', 'quinta', 'sexta', 'sábado'];

/**
 * "Abre terça às 18:00" — a frase que falta em toda loja fechada.
 *
 * Dizer só "fechado" deixa o cliente sem o que fazer com a informação: ele fecha a aba. Dizer
 * quando abre transforma a recusa em um horário para voltar.
 */
function ProximaAbertura(horarios?: HorarioPublico[]): string {
  if (!horarios || horarios.length === 0) {
    return '';
  }

  const hoje = new Date().getDay();

  // Sete passos a partir de hoje: o primeiro dia com abertura é a resposta. Começar em hoje
  // cobre a loja que abre mais tarde no mesmo dia.
  for (let passo = 0; passo < 7; passo += 1) {
    const dia = (hoje + passo) % 7;
    const horario = horarios.find((h) => h.diaDaSemana === dia && h.abre);

    if (horario) {
      const quando = passo === 0 ? 'hoje' : passo === 1 ? 'amanhã' : DIAS[dia];

      return `Abre ${quando} às ${(horario.abre ?? '').slice(0, 5)}.`;
    }
  }

  return '';
}
