/**
 * Nome do perfil — RÓTULO, para mostrar na tela.
 *
 * RN-GER-010 — nunca decida o que mostrar a partir dele. A pergunta é sobre PERMISSÃO
 * (`pode(sessao, 'pedido.cancelar')`), porque o perfil Operador (RN-GER-022) foi criado depois
 * e um `perfil === 'Gerente'` espalhado pela interface obrigaria a caçar cada condicional.
 */
export type Perfil =
  | 'Administrador'
  | 'Gerente'
  | 'Atendente'
  | 'Cozinha'
  | 'Operador'
  | 'Entregador'
  | 'Plataforma';

export interface Sessao {
  token: string;
  expiraEm: string;
  usuarioId: string;
  nome: string;

  /**
   * `RF-SEG-005` — usado pela tela de troca de senha no primeiro acesso.
   *
   * Trocar a senha **não** derruba a pendência do token que já está na mão: o claim
   * `trocar_senha` viaja nele. A tela reautentica com a senha nova, e para isso precisa do e-mail.
   */
  email: string;

  perfil: Perfil;

  /** Nulo para o usuário de plataforma, que não pertence a unidade — RN-GER-014. */
  unidadeId: string | null;
  restaurante: string;
  slug: string;

  /** RN-GER-014 — suporte do GrelhaHub, enxerga todas as unidades (ADR-0013). */
  ehPlataforma: boolean;

  /** RN-GER-010 — códigos `recurso.acao`. Esconder na tela é conveniência; o gate está na API. */
  permissoes: string[];

  /** RF-SEG-005 — se verdadeiro, só a troca de senha responde. */
  deveTrocarSenha: boolean;
}

export interface MeuRestaurante {
  id: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  telefone?: string;
  slug: string;
  linkDoCardapio: string;
  fusoHorario: string;
  totalDeProdutos: number;
  pedidosDoDia: number;

  /** RN-GER-026 — a logo exibida no cardápio público. Nula é estado válido. */
  urlLogo?: string | null;
}

/** SPEC-GER-007 — o usuário da unidade, na tela de administração. */
export interface UsuarioDaUnidade {
  id: string;

  /** `RN-GER-009` — de qual unidade. Nulo só no de plataforma, que não aparece nas listas. */
  unidadeId: string | null;
  nome: string;
  sobrenome?: string | null;

  /** RN-GER-023 — chega FORMATADO da API. O banco guarda 11 dígitos. */
  cpf?: string | null;
  email: string;
  perfil: Perfil;
  perfilId: string;
  ativo: boolean;
  deveTrocarSenha: boolean;
}

export interface ProdutoInterno {
  id: string;
  nome: string;
  descricao?: string;
  preco: number;
  ativo: boolean;
  disponivel: boolean;
  categoriaId: string;

  /** RN-VEN-042 — caminho da imagem. Nulo quando o produto não tem foto. */
  urlImagem?: string | null;
}

export interface CategoriaInterna {
  id: string;
  nome: string;
  ordem: number;

  /**
   * RN-VEN-043 — a gestão vê categoria inativa; o cliente não.
   *
   * O campo é o que permite a tela oferecer "Reativar". Antes da SPEC-VEN-003 a consulta interna
   * filtrava `Ativa` no servidor: a categoria escondida desaparecia da tela e não havia como
   * trazê-la de volta.
   */
  ativa: boolean;

  produtos: ProdutoInterno[];
}

/**
 * RN-GER-012 — não existe campo de custo, CMV ou margem aqui, e isso é intencional:
 * a API não os envia para o cardápio público.
 */
export interface ProdutoPublico {
  id: string;
  nome: string;
  descricao?: string;
  preco: number;

  /** RN-VEN-042 — a foto do lanche. Nula é comum e é estado válido. */
  urlImagem?: string | null;
}

export interface CategoriaPublica {
  id: string;
  nome: string;
  produtos: ProdutoPublico[];
}

export interface CardapioPublico {
  slug: string;
  nomeFantasia: string;
  telefone?: string;

  /** RN-GER-026 — a logo da casa. Nula: a vitrine exibe a marca do GrelhaHub. */
  urlLogo?: string | null;

  /** RN-GER-037 — o endereço em uma linha. Nulo quando a casa não cadastrou. */
  endereco?: string | null;

  /** RN-VEN-048 — a casa está aberta agora. Falso fecha a vitrine. */
  aberta: boolean;

  /** RN-GER-036 — a semana, para dizer quando abre. */
  horarios: HorarioPublico[];

  categorias: CategoriaPublica[];
}

/** RN-GER-036 — um dia da semana. `abre` nulo é dia fechado. */
export interface HorarioPublico {
  diaDaSemana: number;
  abre?: string | null;
  fecha?: string | null;
}

/** RN-VEN-018 — ciclo de vida do pedido. */
export type StatusPedido =
  | 'Aberto'
  | 'Confirmado'
  | 'EmProducao'
  | 'Pronto'
  | 'Entregue'
  | 'Finalizado'
  | 'Cancelado';

export interface ItemResumo {
  produto: string;
  quantidade: number;
  precoPraticado: number;
  observacao?: string;
}

export interface PedidoResumo {
  id: string;

  /**
   * `RN-GER-009` — de qual unidade este pedido é.
   *
   * Sem ele a lista da base inteira é ilegível: `numero` é identificador **humano**, sequencial
   * por dia operacional e **repetido entre unidades** — duas casas têm o pedido nº 1.
   */
  unidadeId: string;

  numero: number;
  canal: string;
  status: StatusPedido;
  cliente?: string;
  telefone?: string;
  total: number;
  quantidadeDeItens: number;
  criadoEm: string;
  dataOperacional: string;
  observacao?: string;
  itens: ItemResumo[];
}

/**
 * `RN-VEN-054` ⚠⚠ — o pedido como o **painel de senha** o recebe.
 *
 * **Cinco campos, e a magreza é o cumprimento da regra.** O painel exibe o **número** e nada mais,
 * então nome, telefone, observação, itens, canal e valor **não existem neste contrato** — não são
 * nulos, não são opcionais que a tela esconde: a API não os envia (`SPEC-VEN-004` §3). É `CHAPA-05`
 * do lado do dado.
 *
 * ⚠⚠ **A versão anterior deste tipo trazia a comanda inteira**, e era estruturalmente compatível
 * com {@link PedidoResumo} justamente para a `<Comanda>` aceitar os dois. Com a revisão de
 * 19/08/2026 essa compatibilidade **acabou de propósito**: o painel não desenha comanda, e um tipo
 * que ainda coubesse lá convidaria alguém a passá-lo para ela de novo.
 */
export interface PedidoNaFila {
  /** Chave do React. ⚠ Nunca `numero`: ele reinicia a cada virada e repete entre unidades. */
  id: string;

  /** `RN-GER-009` — de qual unidade. */
  unidadeId: string;

  /** O que o painel exibe. Único por (unidade, dia operacional), e **reinicia**. */
  numero: number;

  /** Só para escolher a coluna — preparando ou pronto. O painel não escreve estado por pedido. */
  status: StatusPedido;

  /**
   * `RN-GER-006` ⚠⚠ — o dia operacional do pedido.
   *
   * A fila **não tem recorte de período**, então ela traz o pedido que atravessou a virada. Num
   * painel de senha isso é mais perigoso do que era na comanda: o #1 de ontem e o #1 de hoje são
   * **o mesmo algarismo na parede**. A tela carrega a data no número de dia anterior.
   */
  dataOperacional: string;
}
