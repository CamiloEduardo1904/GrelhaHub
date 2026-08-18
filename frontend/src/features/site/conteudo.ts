/**
 * O texto da vitrine, separado da marcação.
 *
 * Está aqui e não dentro do JSX por dois motivos práticos: quem escreve a copy não precisa
 * abrir componente React, e a revisão de honestidade (CHAPA-09 — a vitrine não promete o que
 * a API não faz) se faz lendo um arquivo só.
 *
 * ⚠ **CHAPA-09 — não escreva aqui recurso que o sistema não faz.** A vitrine lista apenas o que
 * está no ar; o bloco de roteiro foi removido em 17/08/2026, então não existe mais lugar
 * "marcado como futuro" onde uma promessa possa se esconder.
 */

/**
 * O recurso não tem mais campo de estado.
 *
 * A vitrine tinha dois blocos — "no ar agora" e "o que vem depois" —, e o segundo saiu por decisão
 * do dono do produto (17/08/2026): **cliente final não precisa do roteiro de engenharia.** Sem o
 * bloco, `estado` e `onda` viraram dado que ninguém lê, e dado que ninguém lê é dívida esperando
 * divergir da página.
 */
export interface Recurso {
  titulo: string;
  descricao: string;
}

// ════════════════════════════════════════════════════════════════════════════════════
// CAPA
// ════════════════════════════════════════════════════════════════════════════════════

export const CAPA = {
  /** Honestidade de fase, e não modéstia: o produto está em construção declarada. */
  faixa: 'Em desenvolvimento aberto',
  titulo: 'O pedido entra pelo link. A comanda sai na chapa.',
  subtitulo:
    'GrelhaHub é um ERP completo para o seu restaurante. Seu cardápio ganha um link próprio, o ' +
    'cliente pede sem baixar aplicativo nenhum, e o pedido percorre o ciclo até Finalizado no ' +
    'painel da loja — com quem fez, quando e por qual preço registrado.',
  acaoPrimaria: 'Falar com a gente',
  acaoSecundaria: 'Ver como funciona',
  /** Fatos do produto. Nenhuma cláusula comercial: preço e suporte não estão decididos. */
  garantias: [
    'Sem aplicativo para o cliente baixar',
    'Link próprio do seu restaurante',
    'Roda no tablet da cozinha',
  ],
} as const;

// ════════════════════════════════════════════════════════════════════════════════════
// AS DORES — reconhecimento do cenário
// ════════════════════════════════════════════════════════════════════════════════════

export const DORES = [
  {
    titulo: 'O pedido chega em áudio',
    texto:
      'Áudio, print e “o de sempre”. Alguém transcreve na correria do sábado, e o que vai para ' +
      'a chapa não é exatamente o que o cliente pediu. O erro só aparece na entrega.',
  },
  {
    titulo: 'A comanda se perde no caminho',
    texto:
      'Entre o balcão e a chapa, o papel desaparece. Quando reaparece, ninguém sabe dizer se ' +
      'aquele lanche já foi montado ou se está esperando desde o começo da fila.',
  },
  {
    titulo: 'O preço de ontem virou outro',
    texto:
      'Você reajustou o combo na segunda. O pedido de domingo precisa continuar sendo explicado ' +
      'pelo preço de domingo — senão o fechamento do mês não fecha e ninguém descobre por quê.',
  },
  {
    titulo: 'A noite que vira madrugada',
    texto:
      'O lanche das duas da manhã é do movimento de ontem, não do dia que começou. Quem agrupa ' +
      'por data de criação erra o faturamento de todo fim de semana.',
  },
] as const;

// ════════════════════════════════════════════════════════════════════════════════════
// BENEFÍCIOS — cada um tem regra por trás, e a regra está no catálogo
// ════════════════════════════════════════════════════════════════════════════════════

export const BENEFICIOS = [
  {
    titulo: 'O pedido chega escrito',
    texto:
      'O cliente monta no cardápio e confirma. Não há transcrição no meio, então não há o que ' +
      'transcrever errado.',
  },
  {
    titulo: 'A cozinha vê a fila',
    texto:
      'Cada pedido tem selo de estado, do Aberto ao Finalizado. Quem está na chapa sabe o que ' +
      'entra agora e o que já saiu.',
  },
  {
    titulo: 'O preço fica gravado na venda',
    texto:
      'O valor praticado é copiado no pedido no momento em que ele nasce. Reajuste de hoje não ' +
      'reescreve a venda de ontem.',
  },
  {
    titulo: 'O dia fecha às quatro da manhã',
    texto:
      'O dia operacional vai das 04h00 às 03h59. A venda da madrugada entra no dia a que ela ' +
      'pertence, sem ninguém corrigir planilha depois.',
  },
] as const;

// ════════════════════════════════════════════════════════════════════════════════════
// RECURSOS — CHAPA-09 vive aqui
// ════════════════════════════════════════════════════════════════════════════════════

export const RECURSOS: Recurso[] = [
  {
    titulo: 'Cardápio com link próprio',
    descricao:
      'Seu endereço público sai pronto no cadastro. Manda no Instagram, no grupo, no cartaz — ' +
      'abre no navegador, sem instalar nada.',
  },
  {
    titulo: 'Pedido sem login e sem app',
    descricao:
      'O cliente escolhe, informa nome e telefone, e pronto. Cadastro obrigatório é onde a ' +
      'maioria dos pedidos morre.',
  },
  {
    titulo: 'Painel com o ciclo do pedido',
    descricao:
      'Aberto, Confirmado, Em produção, Pronto, Entregue, Finalizado. O estado é do sistema, ' +
      'não da memória de quem está no turno.',
  },
  {
    titulo: 'Cancelamento com motivo',
    descricao:
      'Nenhum pedido é apagado. Cancelar exige motivo, e o motivo fica junto de quem cancelou ' +
      'e de quando.',
  },
  {
    titulo: 'Acompanhamento pelo cliente',
    descricao:
      'O cliente abre o próprio pedido e vê em que etapa ele está. Uma pergunta a menos no ' +
      'WhatsApp por pedido.',
  },
  {
    titulo: 'Perfis por função',
    descricao:
      'Administrador, Gerente, Atendente, Cozinha, Operador e Entregador. Cada um vê o que a ' +
      'função dele precisa, e só.',
  },
  {
    titulo: 'Trilha de auditoria',
    descricao:
      'Quem alterou, quando, e o valor antes e depois. A trilha não pode ser editada nem ' +
      'apagada — nem por quem administra.',
  },
  {
    titulo: 'Mais de uma loja',
    descricao:
      'Cada unidade tem seus dados, seus usuários e seus módulos. Uma não alcança a outra.',
  },
];

// ════════════════════════════════════════════════════════════════════════════════════
// COMO FUNCIONA
// ════════════════════════════════════════════════════════════════════════════════════

/** O título da seção — mais comercial que "o link já está no ar". */
export const COMO_FUNCIONA = {
  sobre: 'Como funciona',
  titulo: 'Três passos e a sua loja já está atendendo',
} as const;

export const ETAPAS = [
  {
    titulo: 'Cadastre o restaurante',
    texto:
      'Razão social, CNPJ, telefone e o seu acesso de administrador. O link do cardápio nasce ' +
      'junto, já com um cardápio de exemplo para você ver funcionando.',
  },
  {
    titulo: 'Monte o cardápio',
    texto:
      'Suas categorias, seus produtos, seus preços. O que está indisponível você desliga sem ' +
      'apagar — amanhã ele volta.',
  },
  {
    titulo: 'Receba e produza',
    texto:
      'O pedido cai no painel. A cozinha avança o estado, o cliente acompanha pelo link, e o ' +
      'histórico fica registrado.',
  },
] as const;

// ════════════════════════════════════════════════════════════════════════════════════
// PLANOS
// ════════════════════════════════════════════════════════════════════════════════════

/**
 * ⚠ **Preço não está decidido, e não é decisão de componente.**
 *
 * Os planos são recortes de MÓDULO — é assim que o sistema já habilita e desabilita
 * funcionalidade por unidade. A estrutura está pronta; para publicar um valor, troque
 * `preco` de `null` para o número e ajuste `periodo`. Enquanto for `null`, a vitrine mostra
 * “sob consulta” e o botão convida a conversar, que é honesto.
 */
export interface Plano {
  nome: string;
  resumo: string;
  preco: number | null;
  periodo: string;
  itens: readonly string[];
  /** CHAPA-02 — exatamente UM plano é âmbar em todo o grupo. */
  indicado?: boolean;
}

/** O cabeçalho da seção de planos. */
export const PLANOS_CABECALHO = {
  sobre: 'Planos',
  titulo: 'Escolha o seu plano e comece a vender',
  apoio:
    'Você paga pelos módulos que a sua operação usa, e liga mais quando crescer — sem trocar de ' +
    'sistema e sem migrar dado.',
} as const;

export const PLANOS: readonly Plano[] = [
  {
    nome: 'Balcão',
    resumo: 'O delivery próprio funcionando, sem intermediário no meio do pedido.',
    preco: null,
    periodo: 'por unidade / mês',
    itens: [
      'Cardápio público com link próprio',
      'Pedido sem app e acompanhamento pelo cliente',
      'Painel com o ciclo do pedido',
      'Perfis, permissões e trilha de auditoria',
      'Dia operacional e parâmetros por unidade',
    ],
  },
  {
    nome: 'Cozinha',
    resumo: 'Tudo do Balcão, mais o que responde “quanto custa o meu lanche”.',
    preco: null,
    periodo: 'por unidade / mês',
    indicado: true,
    itens: [
      'Tudo do plano Balcão',
      'Cadastro completo de produto e insumo',
      'Ficha técnica, custo e margem',
      'Estoque por lote com validade e saída FEFO',
      'Ordem de produção disparada na confirmação',
    ],
  },
  {
    nome: 'Casa completa',
    resumo: 'A operação inteira, do fornecedor à entrega.',
    preco: null,
    periodo: 'por unidade / mês',
    itens: [
      'Tudo do plano Cozinha',
      'Caixa, formas de pagamento e conferência de turno',
      'Compras, fornecedor e entrada de nota',
      'Comanda de mesa e salão',
      'Entrega, entregador e rota',
    ],
  },
];

// ════════════════════════════════════════════════════════════════════════════════════
// PERGUNTAS
// ════════════════════════════════════════════════════════════════════════════════════

export const PERGUNTAS = [
  {
    pergunta: 'Como faço para começar?',
    resposta:
      'A conta do seu restaurante é criada pela nossa equipe, junto com o contrato: nós cadastramos ' +
      'o estabelecimento, habilitamos os módulos que você contratou e criamos o seu acesso de ' +
      'administrador. A partir daí você cria os acessos da sua equipe sozinho, pelo painel.',
  },
  {
    pergunta: 'Preciso instalar alguma coisa?',
    resposta:
      'Não. O painel abre no navegador do computador ou do tablet, e o cliente abre o cardápio ' +
      'pelo link. Não há aplicativo para baixar de nenhum dos dois lados.',
  },
  {
    pergunta: 'O cliente precisa criar conta para pedir?',
    resposta:
      'Não. O pedido pelo cardápio é anônimo: nome e telefone bastam. Exigir cadastro é onde a ' +
      'maior parte dos pedidos é abandonada.',
  },
  {
    pergunta: 'O que já dá para usar hoje?',
    resposta:
      'Tudo o que está na lista de recursos acima já funciona: cardápio com link próprio, pedido ' +
      'sem app, acompanhamento pelo cliente, painel com o ciclo completo, perfis de acesso e ' +
      'trilha de auditoria. Ficha técnica, estoque, caixa e fiscal entram por módulo, conforme a ' +
      'sua operação crescer — a gente combina com você o que entra e quando.',
  },
  {
    pergunta: 'Por que o dia começa às quatro da manhã?',
    resposta:
      'Porque restaurante fecha depois da meia-noite. O dia operacional vai das 04h00 às ' +
      '03h59, então o pedido das duas da manhã pertence ao movimento da noite anterior. Sem ' +
      'isso, todo relatório de fim de semana sai errado.',
  },
  {
    pergunta: 'Quem consegue ver o custo e a margem?',
    resposta:
      'Só quem tem a permissão. E a restrição é do servidor: para quem não tem, o campo não é ' +
      'enviado — não é um cadeado desenhado na tela sobre um dado que já chegou.',
  },
  {
    pergunta: 'Tenho mais de uma loja. Funciona?',
    resposta:
      'Sim. Cada unidade tem seus próprios dados, usuários, parâmetros e módulos habilitados, e ' +
      'uma unidade não alcança os dados da outra.',
  },
  {
    pergunta: 'Quanto custa?',
    resposta:
      'Os planos são recortes de módulo, e é assim que o sistema habilita funcionalidade por ' +
      'unidade. Os valores estão sendo fechados — fale com a gente e a gente monta o recorte ' +
      'que serve para a sua operação.',
  },
] as const;

export const CHAMADA_FINAL = {
  titulo: 'Seu restaurante pode estar vendendo pelo link ainda esta semana',
  texto:
    'A gente configura a sua loja, monta o cardápio com você e entrega o link pronto para ' +
    'divulgar. Você só precisa dizer como a sua operação funciona.',
  acao: 'Quero uma demonstração',
} as const;

/**
 * O rodapé legal. Razão social e CNPJ de quem assina o produto.
 */
export const RODAPE = {
  empresa: 'CDP TECH SOLUTIONS',
  cnpj: '63.533.872/0001-35',
  descricao:
    'ERP completo para restaurantes e deliveries. Cardápio próprio, pedido sem app e a ' +
    'operação da chapa no mesmo lugar.',
} as const;

/**
 * Como falar com a gente.
 *
 * ⚠ **Nenhum canal está preenchido, e é de propósito.** A vitrine é pública: um e-mail ou telefone
 * inventado aqui é pior do que canal nenhum — quem escreve para um endereço que não existe vai
 * embora achando que a empresa não responde. Enquanto estiver `null`, os botões de contato **não
 * são renderizados** e a chamada da página passa a ser o acesso de quem já é cliente.
 *
 * Preencha um dos dois e a vitrine volta a ter caminho de conversão. `whatsapp` é só dígitos, com
 * DDI: `5511988887777`.
 */
export const CONTATO: { email: string | null; whatsapp: string | null } = {
  email: null,
  whatsapp: null,
};

/** O `href` do contato, ou `null` quando não há canal configurado. */
export function linkDeContato(): string | null {
  if (CONTATO.whatsapp) {
    return `https://wa.me/${CONTATO.whatsapp}`;
  }

  if (CONTATO.email) {
    return `mailto:${CONTATO.email}?subject=Quero%20conhecer%20o%20GrelhaHub`;
  }

  return null;
}
