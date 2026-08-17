export type Perfil = 'Administrador' | 'Gerente' | 'Atendente' | 'Cozinha';

export interface Sessao {
  token: string;
  expiraEm: string;
  usuarioId: string;
  nome: string;
  perfil: Perfil;
  unidadeId: string;
  restaurante: string;
  slug: string;
}

export interface MeuRestaurante {
  id: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  telefone?: string;
  slug: string;
  linkDoCardapio: string;
  totalDeProdutos: number;
  pedidosDoDia: number;
}

export interface ProdutoInterno {
  id: string;
  nome: string;
  descricao?: string;
  preco: number;
  ativo: boolean;
  disponivel: boolean;
  categoriaId: string;
}

export interface CategoriaInterna {
  id: string;
  nome: string;
  ordem: number;
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
  categorias: CategoriaPublica[];
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
