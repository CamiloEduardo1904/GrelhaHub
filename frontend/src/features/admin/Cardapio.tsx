import { useState } from 'react';
import { Dialogo } from '../../components/Dialogo';
import { EnvioDeImagem } from '../../components/EnvioDeImagem';
import { Grelha } from '../../components/Grelha';
import { IconeDesligar, IconeLapis, IconeMais } from '../../components/Icone';
import { api, mensagemDeErro, moeda } from '../../services/api';
import { formatarDinheiro, paraCampoDeDinheiro, valorDeDinheiro } from '../../services/dinheiro';
import type { CategoriaInterna, ProdutoInterno } from '../../services/tipos';

/**
 * Cadastro do cardápio — `SPEC-VEN-003`, `RN-VEN-043`, `RN-VEN-044`, `RN-VEN-045`.
 *
 * **Esta tela não existia, nem para produto.** `SalvarProdutoCommand` estava pronto desde a
 * `SPEC-VEN-001`, com validator e permissão, e sem um único chamador no frontend: a aba listava e
 * alternava disponibilidade, e não criava nada. Um restaurante recém-cadastrado chegava aqui com o
 * cardápio de exemplo e nenhum botão.
 *
 * **A tela é onde `ativo` e `disponível` param de parecer a mesma coisa** (`RN-VEN-044`). O
 * alternador na linha é o "acabou hoje", que é o gesto do dia; sair do cardápio fica no menu de
 * ações, atrás de um diálogo de confirmação, porque é decisão de temporada.
 */

/** `RN-VEN-045` — não existe excluir. O que existe é inativar, e o vocabulário da tela diz isso. */
export function Cardapio({
  categorias,
  aoMudar,
}: {
  categorias: CategoriaInterna[];
  aoMudar: () => Promise<void>;
}) {
  const [erro, setErro] = useState<string | null>(null);

  /** `null` = fechado; `{}` = criando; com `id` = editando. */
  const [editandoCategoria, setEditandoCategoria] = useState<Partial<CategoriaInterna> | null>(null);
  const [editandoProduto, setEditandoProduto] = useState<Partial<ProdutoInterno> | null>(null);
  const [inativandoProduto, setInativandoProduto] = useState<ProdutoInterno | null>(null);
  const [inativandoCategoria, setInativandoCategoria] = useState<CategoriaInterna | null>(null);

  async function chamar(acao: () => Promise<unknown>) {
    setErro(null);

    try {
      await acao();
      await aoMudar();
    } catch (e) {
      // ADR-0009 — a mensagem é a do servidor. "Já existe um produto com este nome" vem do
      // handler, não de uma cópia da regra escrita aqui.
      setErro(mensagemDeErro(e));
      throw e;
    }
  }

  const ativas = categorias.filter((categoria) => categoria.ativa);

  return (
    <div className="cardapio-gestao">
      {erro && <p className="erro">{erro}</p>}

      <div className="cardapio-acoes">
        <button type="button" className="btn" onClick={() => setEditandoCategoria({})}>
          <IconeMais /> Nova categoria
        </button>

        {/* Sem categoria não há onde pendurar produto: o campo é obrigatório no comando, e
            oferecer o botão para tomar 422 seria gate sem porta em miniatura. */}
        <button
          type="button"
          className="btn calada"
          disabled={ativas.length === 0}
          onClick={() => setEditandoProduto({ categoriaId: ativas[0]?.id })}
        >
          <IconeMais /> Novo produto
        </button>
      </div>

      {categorias.length === 0 && (
        <div className="vazio">
          <Grelha etapa={-1} de={3} alta />
          <p>Seu cardápio está vazio.</p>
          <p className="secundario">
            Comece por uma categoria — "Lanches", "Bebidas" — e depois cadastre os produtos dela.
          </p>
        </div>
      )}

      {categorias.map((categoria) => (
        <section
          key={categoria.id}
          className={categoria.ativa ? 'placa grupo-gestao' : 'placa grupo-gestao escondido'}
        >
          <header className="grupo-cab">
            {/* CHAPA-02 — uma barra âmbar por grupo. Categoria escondida tem a barra morta:
                o estado é visual antes de ser texto. */}
            <Grelha etapa={0} de={1} morta={!categoria.ativa} />

            <h3>{categoria.nome}</h3>

            <span className="ordem-grupo">
              ordem <span className="dado">{categoria.ordem}</span>
            </span>

            {/* RN-VEN-043 — o selo diz que os produtos continuam cadastrados. É o que separa
                "escondido" de "inativado", e a diferença some se a tela não a escrever. */}
            {!categoria.ativa && (
              <span className="selo" data-fase="planejado">
                Escondida do cardápio
              </span>
            )}

            <div className="grupo-cab-acoes">
              <button
                type="button"
                className="btn calada"
                onClick={() => setEditandoCategoria(categoria)}
              >
                <IconeLapis /> Editar
              </button>

              {categoria.ativa ? (
                <button
                  type="button"
                  className="btn calada perigo"
                  onClick={() => setInativandoCategoria(categoria)}
                >
                  <IconeDesligar /> Esconder
                </button>
              ) : (
                // A porta de volta. Sem ela, esconder era caminho de mão única — e era
                // literalmente o que acontecia antes da SPEC-VEN-003 §4.
                <button
                  type="button"
                  className="btn"
                  onClick={() =>
                    void chamar(() =>
                      api.patch(`/v1/cardapio/categorias/${categoria.id}/situacao`, { ativa: true }),
                    )
                  }
                >
                  Reativar
                </button>
              )}
            </div>
          </header>

          {categoria.produtos.length === 0 ? (
            <p className="secundario sem-produto">Nenhum produto nesta categoria.</p>
          ) : (
            <div className="rolagem">
              <table className="tabela-cardapio">
                {/*
                  ⚠ `<colgroup>` com larguras fixas, e não coluna dimensionada pelo conteúdo.

                  Cada categoria é uma tabela própria, e tabela automática mede as colunas pelo
                  que TEM dentro: "Lanches" com descrições longas empurrava PREÇO e AÇÕES para uma
                  posição, "Acompanhamentos" para outra. O resultado era a página inteira
                  desalinhada de seção para seção — e preço é justamente o dado que se confere
                  descendo a coluna com o olho.

                  Com largura declarada, as três tabelas viram a mesma grade.
                */}
                <colgroup>
                  <col className="col-foto" />
                  <col />
                  <col className="col-preco" />
                  <col className="col-turno" />
                  <col className="col-acoes" />
                </colgroup>

                <thead>
                  <tr>
                    <th scope="col">Foto</th>
                    <th scope="col">Produto</th>
                    <th scope="col" className="num">
                      Preço
                    </th>
                    <th scope="col">Tem hoje</th>
                    <th scope="col" className="cel-acoes">
                      Ações
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {/* Cartoon para aqui. Dado é tabulação, contraste e nenhuma surpresa.
                      Sem coluna de custo: RN-GER-012 — a API não devolve o campo, e por
                      CHAPA-05 não há cadeado no que a API não manda. */}
                  {categoria.produtos.map((produto) => (
                    <tr key={produto.id} className={produto.ativo ? '' : 'apagado'}>
                      <td className="cel-foto">
                        {/* RN-VEN-042 — uma imagem por produto. Sem foto é estado válido. */}
                        <EnvioDeImagem
                          rota={`/v1/produtos/${produto.id}/imagem`}
                          urlAtual={produto.urlImagem}
                          rotulo={`Foto de ${produto.nome}`}
                          aoEnviar={aoMudar}
                          aoRemover={aoMudar}
                        />
                      </td>

                      <td>
                        <strong>{produto.nome}</strong>
                        {produto.descricao && <p className="secundario">{produto.descricao}</p>}

                        {/* RN-VEN-044 — "fora do cardápio" é o estado CADASTRAL, e a tela usa
                            palavra diferente da do turno de propósito. */}
                        {!produto.ativo && (
                          <span className="selo" data-fase="morto">
                            Fora do cardápio
                          </span>
                        )}
                      </td>

                      <td className="num">{moeda.format(produto.preco)}</td>

                      <td>
                        {/* O gesto do dia: RN-VEN-044, estado do turno. Fica na linha porque
                            acontece no meio do movimento, com uma mão só. */}
                        <label className="alternador">
                          <input
                            type="checkbox"
                            checked={produto.disponivel}
                            disabled={!produto.ativo}
                            onChange={(e) =>
                              void chamar(() =>
                                api.patch(`/v1/cardapio/produtos/${produto.id}/disponibilidade`, {
                                  disponivel: e.target.checked,
                                }),
                              )
                            }
                          />
                          {produto.disponivel ? 'Sim' : 'Acabou'}
                        </label>
                      </td>

                      <td className="cel-acoes">
                        <div className="linha-acoes">
                          <button
                            type="button"
                            className="btn calada"
                            onClick={() => setEditandoProduto(produto)}
                          >
                            <IconeLapis /> Editar
                          </button>

                          {produto.ativo ? (
                            <button
                              type="button"
                              className="btn calada perigo"
                              onClick={() => setInativandoProduto(produto)}
                            >
                              <IconeDesligar /> Tirar
                            </button>
                          ) : (
                            <button
                              type="button"
                              className="btn calada"
                              onClick={() =>
                                void chamar(() =>
                                  api.patch(`/v1/cardapio/produtos/${produto.id}/situacao`, {
                                    ativo: true,
                                  }),
                                )
                              }
                            >
                              Repor
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ))}

      {editandoCategoria && (
        <FormularioDeCategoria
          categoria={editandoCategoria}
          proximaOrdem={Math.max(0, ...categorias.map((c) => c.ordem)) + 1}
          aoSalvar={async (corpo) => {
            await chamar(() => api.post('/v1/cardapio/categorias', corpo));
            setEditandoCategoria(null);
          }}
          aoFechar={() => setEditandoCategoria(null)}
        />
      )}

      {editandoProduto && (
        <FormularioDeProduto
          produto={editandoProduto}
          categorias={ativas}
          aoSalvar={async (corpo) => {
            await chamar(() => api.post('/v1/cardapio/produtos', corpo));
            setEditandoProduto(null);
          }}
          aoFechar={() => setEditandoProduto(null)}
        />
      )}

      {inativandoCategoria && (
        <Dialogo
          titulo={`Esconder "${inativandoCategoria.nome}" do cardápio?`}
          // RN-VEN-043 — a descrição é o enunciado da regra em português de gente. Quem lê
          // "esconder" precisa saber que os produtos ficam de pé.
          descricao={
            `Os ${inativandoCategoria.produtos.length} produto(s) desta categoria saem do ` +
            'cardápio público e continuam cadastrados. Ao reativar, todos voltam como estavam.'
          }
          rotuloConfirmar="Esconder categoria"
          perigo
          aoConfirmar={async () => {
            await chamar(() =>
              api.patch(`/v1/cardapio/categorias/${inativandoCategoria.id}/situacao`, {
                ativa: false,
              }),
            );
            setInativandoCategoria(null);
          }}
          aoFechar={() => setInativandoCategoria(null)}
        />
      )}

      {inativandoProduto && (
        <Dialogo
          titulo={`Tirar "${inativandoProduto.nome}" do cardápio?`}
          descricao={
            // RN-VEN-045 — a frase existe porque "tirar do cardápio" soa como excluir, e não é.
            'O produto sai do cardápio e continua no sistema: os pedidos antigos seguem ' +
            'mostrando o nome e o preço praticados. Nada é apagado, e você pode repor depois. ' +
            'Se for só "acabou hoje", use o alternador da linha.'
          }
          rotuloConfirmar="Tirar do cardápio"
          perigo
          aoConfirmar={async () => {
            await chamar(() =>
              api.patch(`/v1/cardapio/produtos/${inativandoProduto.id}/situacao`, { ativo: false }),
            );
            setInativandoProduto(null);
          }}
          aoFechar={() => setInativandoProduto(null)}
        />
      )}
    </div>
  );
}

function FormularioDeCategoria({
  categoria,
  proximaOrdem,
  aoSalvar,
  aoFechar,
}: {
  categoria: Partial<CategoriaInterna>;
  proximaOrdem: number;
  aoSalvar: (corpo: { id: string | null; nome: string; ordem: number }) => Promise<void>;
  aoFechar: () => void;
}) {
  const [nome, setNome] = useState(categoria.nome ?? '');
  const [ordem, setOrdem] = useState(String(categoria.ordem ?? proximaOrdem));

  const numero = Number(ordem);
  const ordemValida = Number.isInteger(numero) && numero >= 0 && numero <= 999;

  return (
    <Dialogo
      titulo={categoria.id ? 'Editar categoria' : 'Nova categoria'}
      impedido={nome.trim().length === 0 || !ordemValida}
      rotuloConfirmar="Salvar"
      aoConfirmar={() =>
        aoSalvar({ id: categoria.id ?? null, nome: nome.trim(), ordem: numero })
      }
      aoFechar={aoFechar}
    >
      <label>
        Nome
        <input value={nome} onChange={(e) => setNome(e.target.value)} autoFocus maxLength={100} />
      </label>

      <label>
        Ordem no cardápio
        <input
          type="number"
          inputMode="numeric"
          min={0}
          max={999}
          value={ordem}
          onChange={(e) => setOrdem(e.target.value)}
        />
      </label>

      <p className="secundario">
        A ordem decide em que posição o cliente vê o grupo. Empate é resolvido por ordem
        alfabética.
      </p>
    </Dialogo>
  );
}

function FormularioDeProduto({
  produto,
  categorias,
  aoSalvar,
  aoFechar,
}: {
  produto: Partial<ProdutoInterno>;
  categorias: CategoriaInterna[];
  aoSalvar: (corpo: {
    id: string | null;
    categoriaId: string;
    nome: string;
    descricao: string | null;
    preco: number;
  }) => Promise<void>;
  aoFechar: () => void;
}) {
  const [categoriaId, setCategoriaId] = useState(produto.categoriaId ?? categorias[0]?.id ?? '');
  const [nome, setNome] = useState(produto.nome ?? '');
  const [descricao, setDescricao] = useState(produto.descricao ?? '');

  // Texto mascarado, não número: `<input type="number">` aceita `e`, `+`, `-` e notação
  // científica, e no celular abre um teclado que não é o de moeda. O estado guarda o texto já
  // formatado, e a máscara é acumuladora de centavos — a vírgula anda, ninguém a digita.
  const [preco, setPreco] = useState(
    produto.preco === undefined ? '' : paraCampoDeDinheiro(produto.preco),
  );

  const valor = valorDeDinheiro(preco);

  // RN-VEN-032 — maior que zero. A conferência aqui é usabilidade: o validator do servidor é
  // quem recusa, e é dele que vem a mensagem (ADR-0009).
  const precoValido = valor > 0;

  const impedido = nome.trim().length === 0 || !categoriaId || !precoValido;

  return (
    <Dialogo
      titulo={produto.id ? 'Editar produto' : 'Novo produto'}
      impedido={impedido}
      rotuloConfirmar="Salvar"
      aoConfirmar={() =>
        aoSalvar({
          id: produto.id ?? null,
          categoriaId,
          nome: nome.trim(),
          descricao: descricao.trim() === '' ? null : descricao.trim(),
          preco: valor,
        })
      }
      aoFechar={aoFechar}
    >
      <label>
        Categoria
        <select value={categoriaId} onChange={(e) => setCategoriaId(e.target.value)}>
          {categorias.map((categoria) => (
            <option key={categoria.id} value={categoria.id}>
              {categoria.nome}
            </option>
          ))}
        </select>
      </label>

      <label>
        Nome
        <input value={nome} onChange={(e) => setNome(e.target.value)} autoFocus maxLength={200} />
      </label>

      <label>
        Descrição
        <textarea
          rows={2}
          value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
          maxLength={500}
          placeholder="O que vai no lanche"
        />
      </label>

      <label>
        Preço
        <span className="campo-moeda">
          {/* O prefixo é do CAMPO, não do valor digitado: deixá-lo dentro do texto obrigaria a
              máscara a preservá-lo a cada tecla, e o primeiro `backspace` o levaria embora. */}
          <span className="campo-moeda-prefixo" aria-hidden="true">
            R$
          </span>
          <input
            // `inputMode="numeric"` e não `decimal`: quem digita só dígitos não precisa da
            // vírgula no teclado do celular, e a tecla existindo convida a digitá-la.
            inputMode="numeric"
            className="dado"
            value={preco}
            onChange={(e) => setPreco(formatarDinheiro(e.target.value))}
            placeholder="0,00"
            aria-invalid={preco.length > 0 && !precoValido ? true : undefined}
          />
        </span>
      </label>

      {preco.length > 0 && !precoValido && (
        <p className="erro">O preço deve ser maior que zero.</p>
      )}

      {/* RN-VEN-008 — a frase que evita o susto: reajustar o preço não mexe em pedido emitido. */}
      {produto.id && (
        <p className="secundario">
          Mudar o preço vale para os próximos pedidos. Os já emitidos guardam o preço praticado.
        </p>
      )}
    </Dialogo>
  );
}
