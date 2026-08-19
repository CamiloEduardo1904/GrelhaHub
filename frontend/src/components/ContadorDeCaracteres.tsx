/**
 * Quantos caracteres couberam — `docs/08-design-system.md` §3.
 *
 * Dado, e não prosa: mono com `tabular-nums` (`CHAPA-03`), para que o número não dance enquanto
 * a pessoa digita.
 *
 * ⚠ **O estado excedido não é hipotético.** Ele existe para o cadastro feito quando o limite era
 * outro: `maxLength` impede digitar, não encurta o que já estava gravado — e truncar o texto de
 * alguém ao abrir o formulário seria perda silenciosa de dado (`RN-VEN-053`).
 */
export function ContadorDeCaracteres({ texto, maximo }: { texto: string; maximo: number }) {
  const excedido = texto.length > maximo;

  return (
    <span className="contador-de-caracteres" data-excedido={excedido ? '' : undefined}>
      {/* `aria-live` fora do excedido seria tagarela: o leitor de tela anunciaria cada tecla. */}
      <span aria-hidden="true">
        {texto.length}/{maximo}
      </span>

      {excedido && (
        <span className="somente-leitor" role="status">
          {texto.length - maximo} caractere{texto.length - maximo === 1 ? '' : 's'} além do limite
          de {maximo}.
        </span>
      )}
    </span>
  );
}
