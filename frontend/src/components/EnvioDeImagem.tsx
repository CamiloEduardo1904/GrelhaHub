import { useRef, useState } from 'react';
import { api, mensagemDeErro } from '../services/api';

/**
 * Envio de imagem — `RN-VEN-042`, `RN-GER-026`, `SPEC-VEN-002`.
 *
 * **A validação aqui é só para não gastar a viagem.** Quem decide o formato é o servidor, lendo a
 * assinatura do arquivo: extensão e `Content-Type` são declarados por quem envia, e o `accept` do
 * `<input>` é uma sugestão que qualquer um contorna. Recusar 6 MB antes de subir 6 MB é cortesia
 * com a conexão do restaurante, não segurança.
 */

/** `RN-VEN-042` — 5 MB. O mesmo número do domínio, e o comentário diz de onde ele vem. */
const TAMANHO_MAXIMO = 5 * 1024 * 1024;

const ACEITOS = ['image/jpeg', 'image/png'];

export function EnvioDeImagem({
  rota,
  urlAtual,
  rotulo,
  aoEnviar,
  aoRemover,
}: {
  /** Rota da API, relativa a `/api`. */
  rota: string;
  urlAtual?: string | null;
  rotulo: string;
  aoEnviar: (url: string) => void | Promise<void>;
  /** Quando informado, exibe o botão de remover. `RN-GER-002` — limpa a referência, não o arquivo. */
  aoRemover?: () => void | Promise<void>;
}) {
  const entrada = useRef<HTMLInputElement>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  /**
   * O arquivo não carregou.
   *
   * ADR-0015 — banco e volume são **duas** cópias de backup, e podem dessincronizar: restaurar o
   * dump sem o volume deixa colunas apontando para arquivos que não existem. Sem este estado, o
   * navegador mostra o seu ícone de imagem quebrada, que o usuário lê como "o sistema está com
   * defeito" — e não como "esta foto sumiu". A moldura vazia diz a segunda coisa.
   */
  const [semArquivo, setSemArquivo] = useState(false);

  async function escolher(arquivo: File | undefined) {
    if (!arquivo) return;

    setErro(null);

    if (arquivo.size > TAMANHO_MAXIMO) {
      setErro('A imagem não pode passar de 5 MB.');
      return;
    }

    // O tipo declarado pelo navegador. Confere aqui para dar retorno imediato; o servidor confere
    // os bytes, que é o que vale.
    if (!ACEITOS.includes(arquivo.type)) {
      setErro('Aceitamos apenas JPEG e PNG.');
      return;
    }

    setEnviando(true);

    try {
      const corpo = new FormData();
      corpo.append('arquivo', arquivo);

      const { data } = await api.post<{ url: string }>(rota, corpo);

      // O caminho novo precisa de uma chance: sem isto, uma falha anterior deixaria a moldura
      // vazia para sempre, mesmo com o arquivo recém-gravado.
      setSemArquivo(false);

      await aoEnviar(data.url);
    } catch (e) {
      // ADR-0009 — a mensagem vem do servidor. Se a assinatura não corresponder, é ele quem diz.
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);

      // Limpa o input para que escolher o MESMO arquivo de novo dispare `onChange` outra vez.
      if (entrada.current) entrada.current.value = '';
    }
  }

  async function remover() {
    if (!aoRemover) return;

    setEnviando(true);

    try {
      await api.delete(rota);
      await aoRemover();
    } catch (e) {
      setErro(mensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="envio-imagem">
      {urlAtual && !semArquivo ? (
        <img
          className="miniatura"
          src={urlAtual}
          alt={rotulo}
          onError={() => setSemArquivo(true)}
        />
      ) : (
        // Moldura vazia com traço tracejado: diz "cabe uma imagem aqui" sem inventar um
        // placeholder de "sem foto", que é ruído (CHAPA-06 — e não é emoji).
        <span className="miniatura vazia" aria-hidden="true" />
      )}

      <div className="envio-imagem-acoes">
        <button
          type="button"
          className="btn calada"
          disabled={enviando}
          onClick={() => entrada.current?.click()}
        >
          {enviando ? 'Enviando…' : urlAtual ? 'Trocar' : 'Enviar foto'}
        </button>

        {urlAtual && aoRemover && (
          <button type="button" className="btn perigo" disabled={enviando} onClick={remover}>
            Remover
          </button>
        )}
      </div>

      <input
        ref={entrada}
        type="file"
        accept="image/jpeg,image/png"
        hidden
        onChange={(e) => void escolher(e.target.files?.[0])}
      />

      {erro && <p className="erro">{erro}</p>}
    </div>
  );
}
