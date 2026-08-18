import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';

/**
 * Endereço e horário de funcionamento — `RN-GER-036` ⚠⚠, `RN-GER-037`.
 *
 * **A tela mostra a virada do dia operacional que saiu do horário, e não deixa editá-la.** Ela é
 * derivada (`RN-GER-006`): o campo editável seria uma segunda fonte da mesma verdade. Mas escondê-la
 * seria pior — é a consequência mais importante deste cadastro, e quem mexe no horário precisa ver
 * que a régua do relatório mudou junto.
 */

interface Endereco {
  cep?: string | null;
  logradouro?: string | null;
  numero?: string | null;
  complemento?: string | null;
  bairro?: string | null;
  cidade?: string | null;
  uf?: string | null;
}

interface HorarioDoDia {
  diaDaSemana: number;
  abre?: string | null;
  fecha?: string | null;
}

interface Operacao {
  endereco: Endereco;
  enderecoResumido?: string | null;
  horarios: HorarioDoDia[];
  horaDeViradaOperacional: string;
}

const DIAS = [
  { indice: 0, nome: 'Domingo' },
  { indice: 1, nome: 'Segunda' },
  { indice: 2, nome: 'Terça' },
  { indice: 3, nome: 'Quarta' },
  { indice: 4, nome: 'Quinta' },
  { indice: 5, nome: 'Sexta' },
  { indice: 6, nome: 'Sábado' },
];

const VAZIO: Endereco = {};

export function CadastroDeOperacao({ unidadeId }: { unidadeId?: string }) {
  const [endereco, setEndereco] = useState<Endereco>(VAZIO);
  const [horarios, setHorarios] = useState<HorarioDoDia[]>([]);
  const [virada, setVirada] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [avisoCep, setAvisoCep] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [salvo, setSalvo] = useState(false);

  const carregar = useCallback(async () => {
    try {
      const { data } = await api.get<Operacao>('/v1/restaurante/operacao', {
        params: unidadeId ? { unidadeId } : undefined,
      });

      setEndereco(data.endereco ?? VAZIO);
      setHorarios(data.horarios ?? []);
      setVirada(data.horaDeViradaOperacional);
      setErro(null);
    } catch (e) {
      setErro(mensagemDeErro(e));
    }
  }, [unidadeId]);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  function doDia(indice: number): HorarioDoDia {
    return horarios.find((h) => h.diaDaSemana === indice) ?? { diaDaSemana: indice };
  }

  function alterarDia(indice: number, mudanca: Partial<HorarioDoDia>) {
    setHorarios((atuais) => {
      const outros = atuais.filter((h) => h.diaDaSemana !== indice);
      const atual = doDia(indice);

      return [...outros, { ...atual, ...mudanca }].sort((a, b) => a.diaDaSemana - b.diaDaSemana);
    });
  }

  /**
   * ViaCEP — preenche o que der e **nunca bloqueia**.
   *
   * ⚠ Se a API estiver fora do ar, o cadastro continua: os campos são editáveis do mesmo jeito, e
   * o aviso explica por que nada foi preenchido. Um formulário que depende de serviço de terceiro
   * para gravar endereço é um formulário que para de funcionar sem aviso no dia em que o terceiro
   * cai — e quem está cadastrando não tem como saber que o problema não é ele.
   */
  async function buscarCep(cep: string) {
    const digitos = cep.replace(/\D/gu, '');

    if (digitos.length !== 8) {
      return;
    }

    setAvisoCep(null);

    try {
      const resposta = await fetch(`https://viacep.com.br/ws/${digitos}/json/`);

      if (!resposta.ok) {
        throw new Error('consulta indisponível');
      }

      const dados = (await resposta.json()) as {
        erro?: boolean | string;
        logradouro?: string;
        bairro?: string;
        localidade?: string;
        uf?: string;
      };

      if (dados.erro) {
        setAvisoCep('CEP não encontrado. Preencha o endereço à mão.');
        return;
      }

      setEndereco((atual) => ({
        ...atual,
        // Só preenche o que veio: sobrescrever com vazio apagaria o que a pessoa já digitou.
        logradouro: dados.logradouro || atual.logradouro,
        bairro: dados.bairro || atual.bairro,
        cidade: dados.localidade || atual.cidade,
        uf: dados.uf || atual.uf,
      }));
    } catch {
      setAvisoCep('Não consegui consultar o CEP agora. Pode preencher à mão.');
    }
  }

  async function salvar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setSalvando(true);

    try {
      const { data } = await api.put<Operacao>('/v1/restaurante/operacao', {
        unidadeId,
        endereco,
        // A semana inteira, sempre: a virada é derivada do conjunto, e mandar um dia só
        // deixaria a régua dependente da ordem das chamadas.
        horarios: DIAS.map(({ indice }) => {
          const dia = doDia(indice);

          return {
            diaDaSemana: indice,
            abre: dia.abre || null,
            fecha: dia.fecha || null,
          };
        }),
      });

      setVirada(data.horaDeViradaOperacional);
      setSalvo(true);
      setTimeout(() => setSalvo(false), 2500);
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <form className="placa form" onSubmit={salvar}>
      <fieldset>
        <legend>Endereço</legend>

        <div className="linha">
          <label>
            CEP
            <input
              className="dado"
              value={endereco.cep ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, cep: e.target.value }))}
              onBlur={(e) => void buscarCep(e.target.value)}
              inputMode="numeric"
              maxLength={9}
              placeholder="00000-000"
            />
          </label>

          <label style={{ flex: 2 }}>
            Rua
            <input
              value={endereco.logradouro ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, logradouro: e.target.value }))}
            />
          </label>

          <label>
            Número
            <input
              className="dado"
              value={endereco.numero ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, numero: e.target.value }))}
            />
          </label>
        </div>

        {avisoCep && <p className="erro">{avisoCep}</p>}

        <div className="linha">
          <label>
            Complemento
            <input
              value={endereco.complemento ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, complemento: e.target.value }))}
            />
          </label>

          <label>
            Bairro
            <input
              value={endereco.bairro ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, bairro: e.target.value }))}
            />
          </label>

          <label>
            Cidade
            <input
              value={endereco.cidade ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, cidade: e.target.value }))}
            />
          </label>

          <label style={{ maxWidth: '6rem' }}>
            UF
            <input
              className="dado"
              value={endereco.uf ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, uf: e.target.value.toUpperCase() }))}
              maxLength={2}
            />
          </label>
        </div>

        <p>
          O CEP consulta os Correios e preenche o resto. <strong>Se a consulta falhar</strong>, os
          campos continuam editáveis — o cadastro não depende dela.
        </p>
      </fieldset>

      <fieldset>
        <legend>Horário de funcionamento</legend>

        <ul className="semana">
          {DIAS.map(({ indice, nome }) => {
            const dia = doDia(indice);
            const fechado = !dia.abre;

            return (
              <li key={indice} className={fechado ? 'dia fechado' : 'dia'}>
                {/* CHAPA-02 — uma barra âmbar marca o dia em que a casa abre. */}
                <Grelha etapa={fechado ? -1 : 0} de={1} />
                <span className="dia-nome">{nome}</span>

                <input
                  type="time"
                  className="dado"
                  aria-label={`Abre ${nome}`}
                  value={(dia.abre ?? '').slice(0, 5)}
                  onChange={(e) => alterarDia(indice, { abre: e.target.value || null })}
                />

                <span className="ate">às</span>

                <input
                  type="time"
                  className="dado"
                  aria-label={`Fecha ${nome}`}
                  value={(dia.fecha ?? '').slice(0, 5)}
                  onChange={(e) => alterarDia(indice, { fecha: e.target.value || null })}
                />

                <button
                  type="button"
                  className="btn calada"
                  onClick={() => alterarDia(indice, { abre: null, fecha: null })}
                >
                  Fechado
                </button>
              </li>
            );
          })}
        </ul>

        {/* RN-GER-006 ⚠⚠ — a consequência que ninguém adivinha olhando para os horários. */}
        <p>
          O dia operacional desta casa vira às{' '}
          <strong className="dado">{(virada ?? '04:00:00').slice(0, 5)}</strong>. Pedido feito
          antes desse horário conta no dia anterior — é assim que a noite de sábado não vira
          faturamento de domingo.
        </p>

        <p>
          A virada sai do fechamento que passa da meia-noite. Quem fecha antes dela tem dia
          operacional igual ao dia do calendário.
        </p>
      </fieldset>

      {erro && <p className="erro">{erro}</p>}

      <button type="submit" className="btn primario" disabled={salvando}>
        {salvando ? 'Salvando…' : salvo ? 'Salvo' : 'Salvar endereço e horário'}
      </button>
    </form>
  );
}
