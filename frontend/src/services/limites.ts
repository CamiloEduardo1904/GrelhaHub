/**
 * Tetos de digitação que a tela conhece — e que o servidor decide.
 *
 * **O número vive nos dois lados de propósito**, como o mínimo do `motivo` no `<Dialogo />`: a
 * tela evita a viagem, o validator recusa quem não passou pela tela. O valor de verdade está no
 * catálogo de regras; aqui e no domínio C# ficam as duas cópias que o cumprem.
 *
 * ⚠ Mexer num destes números sem mexer na constante correspondente do backend produz o pior
 * defeito possível para o usuário: um campo que aceita digitar e recusa ao salvar.
 */

/**
 * `RN-VEN-052` — a observação que o cliente escreve no pedido.
 *
 * O teto existe para que a comanda mostre o texto **inteiro**: instrução de preparo cortada pela
 * metade é pior que instrução ausente, porque quem lê não sabe que falta pedaço.
 *
 * Espelha `Pedido.MaximoDeCaracteresNaObservacao`.
 */
export const MAXIMO_OBSERVACAO_DO_PEDIDO = 80;

/**
 * `RN-VEN-053` — a descrição do produto, o texto de vitrine que acompanha o nome.
 *
 * Espelha `Produto.MaximoDeCaracteresNaDescricao`.
 *
 * ⚠ O campo aceitava 500 até 18/08/2026. Cadastro antigo acima do teto continua no banco, e a
 * tela mostra quanto excedeu em vez de truncar o texto de alguém ao abrir o formulário.
 */
export const MAXIMO_DESCRICAO_DO_PRODUTO = 200;
