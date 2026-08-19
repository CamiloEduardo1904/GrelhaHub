import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { CampoDeHora } from '../../components/CampoDeHora';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';
import { lerSessao, pode } from '../admin/sessao';

/**
 * Endereço e horário de funcionamento — `RN-GER-036` ⚠⚠, `RN-GER-037`.
 *
 * **A tela mostra a virada do dia operacional e não deixa editá-la.** Ela é derivada
 * (`RN-GER-006`): campo editável seria uma segunda fonte da mesma verdade. Mas escondê-la seria
 * pior — é a consequência mais importante deste cadastro, e quem mexe no horário precisa ver que a
 * régua do relatório mudou junto.
 *
 * **`RN-GER-010`** — sem `unidade.gerenciar` a tela é somente leitura: os campos ficam
 * desabilitados e o botão não existe. Esconder é conveniência; o servidor recusa de todo jeito.
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
  { indice: 0, nome: 'Domingo', curto: 'Dom' },
  { indice: 1, nome: 'Segunda', curto: 'Seg' },
  { indice: 2, nome: 'Terça', curto: 'Ter' },
  { indice: 3, nome: 'Quarta', curto: 'Qua' },
  { indice: 4, nome: 'Quinta', curto: 'Qui' },
  { indice: 5, nome: 'Sexta', curto: 'Sex' },
  { indice: 6, nome: 'Sábado', curto: 'Sáb' },
];

const VAZIO: Endereco = {};

export function CadastroDeOperacao({ unidadeId }: { unidadeId?: string }) {
  const sessao = lerSessao();

  // RN-GER-010 — a plataforma não tem perfil de unidade e passa pelo bypass do servidor; para ela
  // a tela é sempre editável. Para o restaurante, quem manda é a permissão.
  const podeEditar = (sessao?.ehPlataforma ?? false) || pode(sessao, 'unidade.gerenciar');

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
      const atual = atuais.find((h) => h.diaDaSemana === indice) ?? { diaDaSemana: indice };

      return [...outros, { ...atual, ...mudanca }].sort((a, b) => a.diaDaSemana - b.diaDaSemana);
    });
  }

  /**
   * Copia o horário de um dia para os outros — achado 5.12 da revisão de UX.
   *
   * Sete linhas preenchidas à mão, uma por dia, e a maioria das casas tem o mesmo horário em
   * quase todos eles. É o atalho padrão deste tipo de formulário, e a sua ausência custava sete
   * digitações na configuração inicial de cada loja.
   *
   * ⚠ **Só replica dia que ABRE.** Copiar um dia fechado sobre a semana apagaria o horário de
   * todos os outros com um clique — e o clique mais provável de errar é justamente esse, porque
   * "aplicar em todos" soa inofensivo. Quem quer fechar a semana fecha dia por dia, que é
   * trabalho proporcional à consequência.
   *
   * ⚠ Não grava: mexe no estado da tela, e quem grava continua sendo o botão de salvar. O usuário
   * vê o resultado antes de confirmar — atalho que envia sozinho é atalho que não dá para desfazer.
   */
  function replicar(deIndice: number, paraIndices: number[]) {
    const molde = doDia(deIndice);

    if (!molde.abre) return;

    setHorarios((atuais) =>
      DIAS.map(({ indice }) => {
        const atual = atuais.find((h) => h.diaDaSemana === indice) ?? { diaDaSemana: indice };

        return paraIndices.includes(indice)
          ? { ...atual, abre: molde.abre, fecha: molde.fecha }
          : atual;
      }),
    );
  }

  /**
   * ViaCEP — preenche o que der e **nunca bloqueia**.
   *
   * ⚠ Se o serviço estiver fora do ar, o cadastro continua: os campos seguem editáveis e o aviso
   * explica por que nada foi preenchido. Formulário que depende de terceiro para gravar endereço
   * para de funcionar sem avisar no dia em que o terceiro cai — e quem cadastra não tem como saber
   * que o problema não é ele.
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
        // Só o que veio: sobrescrever com vazio apagaria o que a pessoa já digitou.
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
        // A semana inteira, sempre: a virada é derivada do conjunto, e mandar um dia só deixaria
        // a régua dependente da ordem das chamadas.
        horarios: DIAS.map(({ indice }) => {
          const dia = doDia(indice);

          return { diaDaSemana: indice, abre: dia.abre || null, fecha: dia.fecha || null };
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
    <form className="placa form operacao-cadastro" onSubmit={salvar}>
      <fieldset disabled={!podeEditar}>
        <legend>Endereço</legend>

        {/* Grade nomeada, e não `.linha` de duas colunas: CEP, número e UF têm largura própria —
            um campo de UF com a largura da rua parece defeito, e forçar tudo em duas colunas
            iguais foi o que embaralhou a coluna estreita da plataforma. */}
        <div className="grade-endereco">
          <label className="campo-cep">
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

          <label className="campo-rua">
            Rua
            <input
              value={endereco.logradouro ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, logradouro: e.target.value }))}
            />
          </label>

          <label className="campo-numero">
            Número
            <input
              className="dado"
              value={endereco.numero ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, numero: e.target.value }))}
            />
          </label>

          <label className="campo-complemento">
            Complemento
            <input
              value={endereco.complemento ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, complemento: e.target.value }))}
            />
          </label>

          <label className="campo-bairro">
            Bairro
            <input
              value={endereco.bairro ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, bairro: e.target.value }))}
            />
          </label>

          <label className="campo-cidade">
            Cidade
            <input
              value={endereco.cidade ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, cidade: e.target.value }))}
            />
          </label>

          <label className="campo-uf">
            UF
            <input
              className="dado"
              value={endereco.uf ?? ''}
              onChange={(e) => setEndereco((a) => ({ ...a, uf: e.target.value.toUpperCase() }))}
              maxLength={2}
            />
          </label>
        </div>

        {avisoCep && <p className="erro">{avisoCep}</p>}

        <p className="ajuda">
          O CEP consulta os Correios e preenche o resto. Se a consulta falhar, os campos continuam
          editáveis — o cadastro não depende dela.
        </p>
      </fieldset>

      <fieldset disabled={!podeEditar}>
        <legend>Horário de funcionamento</legend>

        <ul className="semana">
          {DIAS.map(({ indice, nome, curto }) => {
            const dia = doDia(indice);
            const fechado = !dia.abre;

            return (
              <li key={indice} className={fechado ? 'dia fechado' : 'dia'}>
                {/* CHAPA-02 — uma barra âmbar marca o dia em que a casa abre. */}
                <span className="dia-marca">
                  <Grelha etapa={fechado ? -1 : 0} de={1} />
                </span>

                <span className="dia-nome">
                  <span className="longo">{nome}</span>
                  <span className="curto">{curto}</span>
                </span>

                <CampoDeHora
                  rotulo={`Abre ${nome}`}
                  valor={dia.abre ?? ''}
                  aoMudar={(hora) => alterarDia(indice, { abre: hora || null })}
                  desabilitado={!podeEditar}
                />

                <span className="ate">às</span>

                <CampoDeHora
                  rotulo={`Fecha ${nome}`}
                  valor={dia.fecha ?? ''}
                  aoMudar={(hora) => alterarDia(indice, { fecha: hora || null })}
                  desabilitado={!podeEditar}
                />

                {/* Alternador, e não botão de ação: `aria-pressed` diz o estado, e o chip mostra
                    qual dos dois vale. Um botão "Fechado" que não muda de aparência deixa a
                    pessoa sem saber se já clicou. */}
                <button
                  type="button"
                  className="chip"
                  aria-pressed={fechado}
                  onClick={() =>
                    alterarDia(
                      indice,
                      fechado ? { abre: '18:00', fecha: '23:00' } : { abre: null, fecha: null },
                    )
                  }
                >
                  {fechado ? 'Fechado' : 'Abre'}
                </button>
              </li>
            );
          })}
        </ul>

        {/* Os atalhos. O molde é sempre SEGUNDA: é o dia que a casa preenche primeiro, e tomar o
            primeiro dia que abre como molde faria o botão significar coisas diferentes conforme o
            estado da semana — atalho tem de ser previsível. */}
        {doDia(1).abre && (
          <div className="atalhos-de-semana">
            <span className="rotulo">Copiar segunda para</span>

            <button
              type="button"
              className="chip"
              onClick={() => replicar(1, [2, 3, 4, 5])}
              disabled={!podeEditar}
            >
              Terça a sexta
            </button>

            <button
              type="button"
              className="chip"
              onClick={() => replicar(1, [0, 2, 3, 4, 5, 6])}
              disabled={!podeEditar}
            >
              Todos os dias
            </button>
          </div>
        )}

        {/* RN-GER-006 ⚠⚠ — a consequência que ninguém adivinha olhando para os horários. */}
        <p className="virada">
          O dia operacional desta casa vira às{' '}
          <strong className="dado">{(virada ?? '04:00:00').slice(0, 5)}</strong>. Pedido feito antes
          desse horário conta no dia anterior — é assim que a noite de sábado não vira faturamento
          de domingo.
        </p>

        <p className="ajuda">
          A virada sai do fechamento que passa da meia-noite. Quem fecha antes dela tem dia
          operacional igual ao dia do calendário.
        </p>
      </fieldset>

      {erro && <p className="erro">{erro}</p>}

      {podeEditar ? (
        <button type="submit" className="btn primario" disabled={salvando}>
          {salvando ? 'Salvando…' : salvo ? 'Salvo' : 'Salvar endereço e horário'}
        </button>
      ) : (
        // RN-GER-010 — sem permissão de alterar, nada de botão que responderia 403.
        <p className="ajuda">
          Você pode <strong>ver</strong> este cadastro. Alterar endereço e horário exige o perfil de
          gerência.
        </p>
      )}
    </form>
  );
}
