import { useState } from 'react';
import { Grelha } from '../../components/Grelha';
import { api, mensagemDeErro } from '../../services/api';
import type { Estabelecimento, SessaoDeSuporte } from './tipos';

/**
 * `RN-GER-015` — abrir e encerrar a sessão de suporte de uma casa.
 *
 * **É a peça que faltava na primeira versão desta área.** O gate existia no pipeline desde a
 * `SPEC-GER-004` e a tela citava a regra na mensagem de erro — sem oferecer onde abrir a sessão.
 * Regra imposta sem caminho na interface não é rigor, é beco sem saída: o operador lê o
 * identificador da regra e não tem o que fazer com ele.
 *
 * **Ler não passa por aqui.** A sessão guarda a **escrita**: consultar pedidos, usuários e cardápio
 * de um cliente é rotina de diagnóstico e funciona sem ela.
 */

/** O comando exige dez caracteres. Menos que isso não explica atendimento nenhum. */
const MOTIVO_MINIMO = 10;

export function SessaoDeSuporteDaCasa({
  casa,
  sessao,
  aoMudar,
}: {
  casa: Estabelecimento;
  sessao: SessaoDeSuporte | null;
  aoMudar: () => Promise<void>;
}) {
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  async function abrir() {
    setOcupado(true);
    setErro(null);

    try {
      await api.post('/v1/plataforma/sessoes-de-suporte', { unidadeId: casa.id, motivo });
      setMotivo('');
      await aoMudar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  async function encerrar() {
    if (!sessao) return;

    setOcupado(true);
    setErro(null);

    try {
      await api.post(`/v1/plataforma/sessoes-de-suporte/${sessao.id}/encerrar`);
      await aoMudar();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  if (sessao) {
    return (
      <div className="sessao-suporte aberta">
        <span className="sessao-marca">
          {/* CHAPA-02 — a barra âmbar marca o estado ativo do grupo. */}
          <Grelha etapa={0} de={1} alta />
          <span className="rotulo">Sessão de suporte aberta</span>
        </span>

        <p className="secundario">
          <strong>{sessao.motivo}</strong>
          <br />
          Válida até{' '}
          <span className="dado">{new Date(sessao.validaAte).toLocaleString('pt-BR')}</span>
        </p>

        <button type="button" className="btn calada" disabled={ocupado} onClick={encerrar}>
          Encerrar sessão
        </button>

        {erro && <p className="erro">{erro}</p>}
      </div>
    );
  }

  const falta = MOTIVO_MINIMO - motivo.trim().length;

  return (
    <div className="sessao-suporte">
      <span className="sessao-marca">
        <Grelha etapa={-1} de={1} alta />
        <span className="rotulo">Sem sessão de suporte</span>
      </span>

      <p className="secundario">
        Ler o dado de um restaurante é rotina; <strong>alterá-lo exige dizer por quê</strong>. O
        motivo fica registrado com o seu nome e vale por 2 horas (<code>RN-GER-015</code>).
      </p>

      <label>
        Motivo do atendimento
        <input
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          placeholder="Cliente pediu por telefone para criar o acesso do gerente"
        />
      </label>

      <button type="button" className="btn" disabled={ocupado || falta > 0} onClick={abrir}>
        {ocupado ? 'Abrindo…' : 'Abrir sessão de suporte'}
      </button>

      {motivo.length > 0 && falta > 0 && (
        <p className="secundario">
          Faltam {falta} caracteres — o motivo precisa explicar o atendimento, não só preencher o
          campo.
        </p>
      )}

      {erro && <p className="erro">{erro}</p>}
    </div>
  );
}
