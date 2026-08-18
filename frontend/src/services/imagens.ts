import { api } from './api';

/**
 * As imagens vindas do banco de arquivo — **ADR-0018**.
 *
 * Até 18/08/2026 a foto era um arquivo no disco da VPS, servido pelo Nginx. Hoje ela é um binário
 * no banco `grelhahub_arquivo`, e a referência gravada na coluna é uma **rota**:
 * `/api/v1/arquivos/{id}`.
 *
 * ## Duas formas de exibir, e a escolha não é indiferente
 *
 * | Forma | Quando |
 * |-------|--------|
 * | `<img src={referencia}>` | A vitrine pública. O navegador **cacheia** a imagem e não passa pelo JavaScript |
 * | `carregarBase64(referencia)` | O painel. Poucas miniaturas, e o conteúdo chega embutido na resposta |
 *
 * ⚠⚠ **Base64 infla o conteúdo em um terço e não entra no cache de imagem do navegador.** Numa
 * vitrine de vinte produtos aberta no celular do cliente, isso são megabytes a mais antes do
 * primeiro preço aparecer na tela — por isso o cardápio público **não** usa esta função, e a
 * decisão está escrita aqui em vez de virar folclore.
 */

/** Uma imagem já carregada, como `data:` URI. */
const cache = new Map<string, Promise<string>>();

type RespostaDeArquivo = {
  id: string;
  tipoMime: string;
  tamanhoEmBytes: number;
  conteudoBase64: string;
};

/**
 * Devolve a imagem como `data:` URI, pronta para ir em `src`.
 *
 * ⚠ O prefixo `data:image/png;base64,` **vem do servidor**. Montá-lo aqui exigiria adivinhar o tipo
 * a partir da extensão — e é justamente o que `RN-VEN-042` proíbe, porque extensão é declaração de
 * quem envia.
 *
 * A promessa é guardada em cache **antes** de resolver: duas miniaturas do mesmo arquivo montadas
 * no mesmo quadro fazem uma requisição, não duas.
 */
export function carregarBase64(referencia: string): Promise<string> {
  const guardada = cache.get(referencia);

  if (guardada) return guardada;

  const promessa = api
    .get<RespostaDeArquivo>(`${semPrefixoDaApi(referencia)}/base64`)
    .then((resposta) => resposta.data.conteudoBase64)
    .catch((erro: unknown) => {
      // Falha não fica no cache: uma queda de rede não deve condenar a miniatura pelo resto da
      // sessão. A próxima montagem tenta de novo.
      cache.delete(referencia);
      throw erro;
    });

  cache.set(referencia, promessa);

  return promessa;
}

/**
 * `true` quando a referência aponta para o banco de arquivo.
 *
 * ⚠ Existem referências **antigas** em base — as do ADR-0015, no formato `/arquivos/...`. Elas
 * continuam sendo servidas pelo Nginx e não têm rota `/base64`. Perguntar antes de chamar é o que
 * impede a miniatura de um cadastro velho de quebrar.
 */
export function ehDoBancoDeArquivo(referencia: string | null | undefined): referencia is string {
  return typeof referencia === 'string' && referencia.startsWith('/api/v1/arquivos/');
}

/** O cliente HTTP já tem `/api` como base; repetir o prefixo daria `/api/api/...`. */
function semPrefixoDaApi(referencia: string): string {
  return referencia.startsWith('/api/') ? referencia.slice('/api'.length) : referencia;
}
