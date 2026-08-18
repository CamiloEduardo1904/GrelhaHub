import type { Perfil } from '../../services/tipos';

/**
 * O perfil do usuário e **o que ele concede** — `RN-GER-010`, `RN-GER-024`.
 *
 * **Substitui o `<select>` de perfil.** Escolher perfil é conceder acesso, e um `<option>` com a
 * palavra "Gerente" não diz o que está sendo entregue: quem atribui assina em branco. Aqui cada
 * perfil é um cartão que lista, em português, o que a pessoa vai poder fazer.
 *
 * **As permissões vêm do servidor** (`/v1/usuarios/perfis`), nunca de uma cópia no TypeScript — a
 * cópia divergiria do `CatalogoDeAcesso` na primeira permissão nova, e a tela passaria a prometer
 * o que o servidor recusa. É o mesmo argumento que trouxe `delegavel` para a API
 * (`SPEC-GER-007` §11.1).
 *
 * ⚠ **O que não é delegável não aparece** (`RN-GER-024`): o gerente não vê o cartão de
 * Administrador. Esconder é conveniência — `ValidadorDeDelegacao` recusa no servidor de todo jeito.
 */

export interface PermissaoDoPerfil {
  codigo: string;
  descricao: string;
}

export interface PerfilDaUnidade {
  id: string;
  nome: Perfil;
  descricao: string;
  delegavel: boolean;
  permissoes: PermissaoDoPerfil[];
}

export function MenuDePerfis({
  perfis,
  selecionado,
  aoSelecionar,
  desabilitado = false,
}: {
  perfis: PerfilDaUnidade[];
  selecionado: string;
  aoSelecionar: (perfilId: string) => void;
  desabilitado?: boolean;
}) {
  return (
    <fieldset className="menu-perfis" disabled={desabilitado}>
      <legend>Perfil e acesso</legend>

      <ul className="perfis" role="radiogroup" aria-label="Perfil do usuário">
        {perfis.map((perfil) => {
          const ativo = perfil.id === selecionado;

          return (
            <li key={perfil.id}>
              <button
                type="button"
                role="radio"
                aria-checked={ativo}
                className={ativo ? 'perfil-cartao ativo' : 'perfil-cartao'}
                onClick={() => aoSelecionar(perfil.id)}
              >
                <span className="perfil-topo">
                  <strong>{perfil.nome}</strong>

                  {/* O número é dado: mono, tabular. */}
                  <span className="dado">
                    {perfil.permissoes.length === 0
                      ? 'sem acesso'
                      : `${perfil.permissoes.length} permissões`}
                  </span>
                </span>

                {/* CHAPA-10 — a descrição é prosa e vai na cor do texto. */}
                <span className="perfil-descricao">{perfil.descricao}</span>

                {perfil.permissoes.length > 0 ? (
                  <ul className="perfil-permissoes">
                    {perfil.permissoes.map((permissao) => (
                      <li key={permissao.codigo}>
                        {permissao.descricao}
                        {/* O código fica visível: é ele que aparece no erro do servidor
                            ("falta a permissão pedido.cancelar"), e ligar um ao outro é o que
                            permite ao suporte responder sem adivinhar. */}
                        <span className="chave">{permissao.codigo}</span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <span className="perfil-descricao">
                    Este perfil ainda não concede nada — as permissões dele entram com o módulo
                    correspondente.
                  </span>
                )}
              </button>
            </li>
          );
        })}
      </ul>
    </fieldset>
  );
}
