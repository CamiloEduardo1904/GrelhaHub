/**
 * Máscara de moeda na interface — `RN-GER-003`, `SPEC-GER-001` §4.
 *
 * **O campo de preço aceitava texto livre**, e o resultado aparecia na tela: `333333333333`
 * digitado inteiro num campo chamado "Preço", com o formulário se oferecendo para salvar. O
 * servidor recusaria (o validator tem teto de 100 000), mas recusar depois da viagem não é o
 * ponto — o ponto é que o campo não parecia moeda em nenhum momento da digitação.
 *
 * **A máscara é acumuladora de centavos, não formatadora de texto.** Quem digita `3390` vê
 * `33,90`; a vírgula não é digitada, ela **anda**. É o comportamento de terminal de cartão e de
 * caixa registradora, e é o único que não exige que o operador acerte onde fica o separador
 * enquanto atende alguém no balcão.
 *
 * Duas casas decimais, sempre: `RN-GER-003`. Quantidade de insumo tem **quatro**
 * (`RN-GER-004`) — quando existir campo de quantidade, ele precisa da sua própria máscara, e
 * reusar esta seria gravar `0,1563` como `0,16` sem erro nenhum.
 */

/**
 * O teto em dígitos: `9999999` → `99 999,99`.
 *
 * O número casa com o `LessThan(100_000)` do `SalvarProdutoValidator` de propósito. **Não é a
 * regra** — a regra está no servidor; é o campo se recusando a montar um valor que já se sabe
 * que será recusado.
 */
const MAXIMO_DE_DIGITOS = 7;

/** `"33,90"` ou `"3390"` → `"3390"`. Os centavos como inteiro, em texto. */
export function centavosDigitados(texto: string): string {
  // `replace(/^0+/, '')` e não `Number(...)`: o valor pode passar do inteiro seguro se alguém
  // colar uma sequência longa, e aqui só interessa a sequência de dígitos.
  return texto.replace(/\D/g, '').replace(/^0+(?=\d)/, '').slice(0, MAXIMO_DE_DIGITOS);
}

/**
 * `"3390"` → `"33,90"`. Aplicada a cada tecla, e não ao sair do campo: máscara que só aparece no
 * `blur` faz o número pular na frente de quem está conferindo o preço.
 *
 * Campo vazio devolve vazio — e **não** `"0,00"`. Um zero que aparece sozinho no campo faz o
 * usuário apagar antes de digitar, e o preço zero é recusado por `RN-VEN-032` de qualquer forma.
 */
export function formatarDinheiro(texto: string): string {
  const digitos = centavosDigitados(texto);

  if (digitos.length === 0) {
    return '';
  }

  const centavos = digitos.padStart(3, '0');
  const reais = centavos.slice(0, -2);
  const fracao = centavos.slice(-2);

  // Separador de milhar: `12345678` sem ponto é ilegível, e o preço é o campo que mais se
  // confere de longe.
  const comMilhar = reais.replace(/\B(?=(\d{3})+(?!\d))/g, '.');

  return `${comMilhar},${fracao}`;
}

/**
 * O valor para o corpo da requisição — `numeric(12,2)` do outro lado.
 *
 * Divide por 100 em vez de trocar vírgula por ponto: a segunda forma reintroduz o erro de
 * arredondamento que a máscara de centavos evita.
 */
export function valorDeDinheiro(texto: string): number {
  const digitos = centavosDigitados(texto);

  return digitos.length === 0 ? 0 : Number(digitos) / 100;
}

/** O caminho de volta: `32.9` → `"32,90"`, para abrir o formulário de edição já formatado. */
export function paraCampoDeDinheiro(valor: number): string {
  return formatarDinheiro(String(Math.round(valor * 100)));
}
