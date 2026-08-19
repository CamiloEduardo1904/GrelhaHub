/**
 * CPF na interface — `RN-GER-023`, `RN-GER-013`.
 *
 * **A validação aqui NÃO é a validação do sistema.** Quem decide é o servidor: o value object
 * `Cpf` do domínio recusa dígito verificador errado e sequência repetida, e nenhum campo de tela
 * pode ser tratado como garantia. O que existe aqui é conveniência — dizer ao usuário que o
 * número está errado antes de ele apertar o botão, em vez de depois de uma ida ao servidor.
 *
 * A máscara é **só apresentação**: o que vai no corpo da requisição são os 11 dígitos
 * (`RN-GER-013`). Enviar com pontuação também funcionaria — o backend limpa —, mas mandar limpo
 * deixa explícito de que lado a formatação vive.
 */

const DIGITOS = 11;

/** Só os dígitos, no máximo 11. É isto que vai para a API. */
export function apenasDigitos(texto: string): string {
  return texto.replace(/\D/g, '').slice(0, DIGITOS);
}

/**
 * `52998224725` → `529.982.247-25`, aplicando parcialmente enquanto o usuário digita.
 *
 * Formatar em cada tecla e não só no fim: máscara que aparece ao sair do campo faz o número
 * "pular" na frente de quem está conferindo o documento na mão.
 */
export function formatarCpf(texto: string): string {
  const d = apenasDigitos(texto);

  if (d.length <= 3) return d;
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`;
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`;

  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

/**
 * Mesmo algoritmo do domínio: módulo 11 nos dois dígitos verificadores, e recusa de sequência
 * repetida.
 *
 * A sequência repetida **passa** no cálculo — `111.111.111-11` fecha os dois verificadores. A
 * checagem separada existe por isso, aqui e no servidor.
 */
export function cpfValido(texto: string): boolean {
  const d = apenasDigitos(texto);

  if (d.length !== DIGITOS) return false;
  if (d.split('').every((c) => c === d[0])) return false;

  return d[9] === verificador(d, 9, 10) && d[10] === verificador(d, 10, 11);
}

function verificador(digitos: string, ate: number, pesoInicial: number): string {
  let soma = 0;

  for (let i = 0; i < ate; i++) {
    soma += Number(digitos[i]) * (pesoInicial - i);
  }

  const resto = (soma * 10) % 11;

  return String(resto === 10 ? 0 : resto);
}

/**
 * `RN-GER-039` ⚠ — o CPF como ele aparece em **listagem**: `•••.752.818-••`.
 *
 * Guarda os dois grupos do meio e esconde o primeiro e o verificador. É o suficiente para quem
 * está conferindo um documento na mão reconhecer a pessoa, e insuficiente para quem passou pela
 * mesa levar o número.
 *
 * ⚠⚠ **Isto reduz exposição de TELA, não acesso.** O número vem na resposta da API e quem abre o
 * inspetor o lê. A minimização de verdade é a listagem parar de devolvê-lo — dívida declarada na
 * regra e na `SPEC-GER-007`. Chamar isto de proteção sem dizer o resto seria o cadeado em campo
 * que a API devolve, que a `CHAPA-05` recusa por mentir sobre o modelo.
 */
export function mascararCpf(cpf: string | null | undefined): string {
  const d = apenasDigitos(cpf ?? '');

  // Documento incompleto não é mascarado: mascarar o que já está parcial esconde que ele está
  // parcial. O usuário de plataforma não tem CPF (`RN-GER-023`), e ali o valor é nulo.
  if (d.length !== DIGITOS) return d;

  return `•••.${d.slice(3, 6)}.${d.slice(6, 9)}-••`;
}
