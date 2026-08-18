import { CampoDeData } from '../../components/CampoDeData';
import { Grelha } from '../../components/Grelha';
import {
  invertido,
  periodoDeAtras,
  periodoDeHoje,
  type Periodo,
} from '../../services/periodo';

/**
 * O filtro de período — `RN-GER-006` ⚠⚠, `RN-VEN-050`.
 *
 * O componente **não decide o padrão**: quem chama informa o período vigente. A lista de pedidos
 * abre em hoje, o painel de operação abre em sete dias, e a diferença é de produto — um responde
 * "o que está no salão", o outro "como foi a semana", e nenhum painel comparativo responde nada
 * abrindo num ponto só.
 *
 * O recorte é de **dia operacional**: ver `services/periodo.ts`.
 */

/** Sete e trinta porque é assim que a casa fala: "a semana", "o mês". */
const ATALHOS: { rotulo: string; dias: number }[] = [
  { rotulo: 'Hoje', dias: 0 },
  { rotulo: '7 dias', dias: 6 },
  { rotulo: '30 dias', dias: 29 },
];

export function FiltroDePeriodo({
  periodo,
  aoMudar,
  ocupado = false,
}: {
  periodo: Periodo;
  aoMudar: (periodo: Periodo) => void;
  ocupado?: boolean;
}) {
  /** Qual atalho corresponde ao período atual — ou nenhum, quando as datas foram digitadas. */
  const atalhoAtivo = ATALHOS.findIndex(({ dias }) => {
    const candidato = dias === 0 ? periodoDeHoje() : periodoDeAtras(dias);

    return candidato.de === periodo.de && candidato.ate === periodo.ate;
  });

  return (
    <div className="filtro-periodo">
      <div className="filtro-datas">
        <CampoDeData rotulo="De" valor={periodo.de} aoMudar={(de) => aoMudar({ ...periodo, de })} />

        <CampoDeData
          rotulo="Até"
          valor={periodo.ate}
          aoMudar={(ate) => aoMudar({ ...periodo, ate })}
        />
      </div>

      {/* CHAPA-02 — uma barra âmbar por grupo: o atalho vigente. NENHUMA quando as datas foram
          digitadas à mão, porque aí nenhum atalho está ativo — barra acesa num atalho que não
          corresponde ao período seria mentira visual. */}
      <div className="filtro-atalhos" role="group" aria-label="Períodos rápidos">
        {ATALHOS.map(({ rotulo, dias }, indice) => (
          <button
            key={rotulo}
            type="button"
            className="chip"
            aria-pressed={indice === atalhoAtivo}
            disabled={ocupado}
            onClick={() => aoMudar(dias === 0 ? periodoDeHoje() : periodoDeAtras(dias))}
          >
            <Grelha etapa={indice === atalhoAtivo ? 0 : -1} de={1} />
            {rotulo}
          </button>
        ))}
      </div>

      {/* A recusa de verdade é do servidor, com 422 e a regra citada (ADR-0009). Esta mensagem
          existe para a pessoa não precisar enviar para descobrir — e some sozinha ao corrigir. */}
      {invertido(periodo) && <p className="erro">A data final não pode ser anterior à inicial.</p>}
    </div>
  );
}
