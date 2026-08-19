/**
 * Datas do dia civil, para valor inicial de campo e para os atalhos de período.
 *
 * ⚠ **Nada aqui sabe de `RN-GER-006`.** Estes são dias de calendário; o recorte por **dia
 * operacional** — 04h00 às 03h59 — é feito no servidor, pela coluna gerada `data_operacional`.
 * Quem quiser calcular dia operacional no cliente estará escrevendo a segunda implementação de uma
 * regra estrutural, que é exatamente o que `DiaOperacionalParidadeTests` existe para impedir.
 *
 * Arquivo próprio, e não dentro de `CampoDeData.tsx`, porque arquivo de componente exporta
 * componente: o `react-refresh` avisa, e ele está certo — trocar uma função utilitária não deve
 * derrubar o estado da tela no recarregamento.
 */

/** `AAAA-MM-DD` de hoje, no fuso de quem está olhando. */
export function hojeIso(): string {
  return isoDe(new Date());
}

/** `AAAA-MM-DD` de N dias atrás. Negativo não é caso de uso, e por isso não é tratado. */
export function diasAtras(dias: number): string {
  const data = new Date();
  data.setDate(data.getDate() - dias);

  return isoDe(data);
}

function isoDe(data: Date): string {
  const mes = String(data.getMonth() + 1).padStart(2, '0');
  const dia = String(data.getDate()).padStart(2, '0');

  return `${data.getFullYear()}-${mes}-${dia}`;
}

/**
 * "há 22 min" — quanto tempo passou desde um instante ISO.
 *
 * ⚠ **É a informação que faltava na comanda, e ela vale mais que o número do pedido.** Uma revisão
 * de UX registrou assim: *"para um sistema cujo argumento central é 'a cozinha vê a fila', saber
 * que o pedido #3 está aberto há 22 minutos é mais importante que saber que ele é o número 3"*. O
 * painel não dizia isso em lugar nenhum.
 *
 * **Tempo de RELÓGIO, não de dia operacional.** Aqui a distinção de `RN-GER-006` não se aplica: a
 * pergunta é "há quanto tempo esta pessoa está esperando", e a resposta são minutos corridos. Usar
 * dia operacional para isso seria aplicar a regra fora do lugar dela.
 *
 * Devolve `null` quando o instante é inválido — string vazia, data que não parseia. Rótulo de tempo
 * é apoio: some quando não dá para calcular, em vez de escrever "NaN" na comanda.
 */
export function tempoDecorrido(desde: string, agora: Date = new Date()): string | null {
  const instante = new Date(desde);

  if (Number.isNaN(instante.getTime())) {
    return null;
  }

  const minutos = Math.floor((agora.getTime() - instante.getTime()) / 60_000);

  // Relógio adiantado no cliente, ou pedido criado neste segundo: "agora" é a leitura honesta.
  // "há -3 min" seria pior que impreciso — leria como defeito.
  if (minutos < 1) return 'agora';
  if (minutos < 60) return `há ${minutos} min`;

  const horas = Math.floor(minutos / 60);

  if (horas < 24) {
    const resto = minutos % 60;

    // "há 2 h 05" e não "há 2 horas": na cozinha, o resto de minuto ainda importa na segunda hora.
    return resto === 0 ? `há ${horas} h` : `há ${horas} h ${String(resto).padStart(2, '0')}`;
  }

  const dias = Math.floor(horas / 24);

  return `há ${dias} ${dias === 1 ? 'dia' : 'dias'}`;
}
