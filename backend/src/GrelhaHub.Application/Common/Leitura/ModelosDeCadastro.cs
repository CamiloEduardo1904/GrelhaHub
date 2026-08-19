namespace GrelhaHub.Application.Common.Leitura;

/// <summary>
/// Os modelos de leitura do <b>cadastro</b> — ADR-0019, segunda leva.
/// <para>
/// A primeira leva cobriu venda (pedido, item, operação do dia). Esta cobre o que a tela de
/// gestão lê: a casa, o cardápio, os módulos, os perfis, os usuários e os parâmetros. Mesma
/// doutrina — sem chave, sem comportamento, mapeados a visões.
/// </para>
/// <para>
/// ⚠⚠ <b>Nem todo modelo é recortado por unidade, e a decisão está em cada um.</b> Marcar
/// <see cref="IRecortadoPorUnidade"/> por reflexo quebraria as rotas anônimas; não marcar por
/// esquecimento vazaria dado entre restaurantes. A regra prática: se a linha DESCREVE uma unidade
/// (a própria casa) ou é catálogo compartilhado por todas (módulo, parâmetro), não recorta; se ela
/// PERTENCE a uma unidade, recorta.
/// </para>
/// </summary>

/// <summary>
/// A casa — <c>vw_unidade</c>.
/// <para>
/// ⚠ <b>NÃO é recortado, e é a mesma decisão do lado da escrita:</b> <c>Unidade</c> não implementa
/// <c>IPertenceAUnidade</c> porque ela É a unidade. Além disso o cardápio público a encontra
/// <b>pelo slug</b>, antes de existir unidade no contexto — recortar aqui devolveria zero linhas e
/// o cliente leria "restaurante não encontrado" para uma casa que existe.
/// </para>
/// </summary>
/// <param name="TotalDeUsuarios">
/// ⚠ <c>RN-GER-016</c> — <b>sem</b> os usuários de plataforma: eles não são do restaurante. Contado
/// pela visão, e não por consulta correlacionada no handler.
/// </param>
public sealed record UnidadeLeitura(
    Guid Id,
    string Slug,
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string? Telefone,
    string? UrlLogo,
    bool Ativa,
    DateTimeOffset CriadoEm,
    string FusoHorario,
    TimeSpan HoraDeViradaOperacional,
    string? Cep,
    string? Logradouro,
    string? NumeroDoEndereco,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf,
    int TotalDeUsuarios,
    int TotalDeProdutosAtivos);

/// <summary>
/// <c>RN-GER-036</c> — um dia da semana da casa, <c>vw_unidade_horario</c>.
/// <para>
/// Recortado: quem lê já resolveu a unidade (o público, pelo slug; a gestão, pela sessão).
/// </para>
/// </summary>
public sealed record HorarioLeitura(
    Guid UnidadeId, DayOfWeek DiaDaSemana, TimeOnly? Abre, TimeOnly? Fecha) : IRecortadoPorUnidade;

/// <summary>
/// <c>vw_cardapio_categoria</c> — a categoria com a contagem de produtos que o CLIENTE veria.
/// </summary>
/// <param name="ProdutosNaVitrine">
/// ⚠ Quantos produtos estão <b>ativos e disponíveis</b>. É o que decide se a categoria aparece no
/// cardápio público — <c>RN-VEN-044</c>: categoria vazia na vitrine é um título sem nada embaixo.
/// A tela de GESTÃO ignora este número e mostra a categoria de qualquer forma, porque lá o
/// problema é o oposto (<c>SPEC-VEN-003</c> §4: categoria invisível não podia ser reativada).
/// </param>
public sealed record CategoriaLeitura(
    Guid UnidadeId,
    Guid Id,
    string Nome,
    int Ordem,
    bool Ativa,
    int ProdutosNaVitrine) : IRecortadoPorUnidade;

/// <summary><c>vw_cardapio_produto</c> — o produto, com o preço já em decimal.</summary>
public sealed record ProdutoDoCardapioLeitura(
    Guid UnidadeId,
    Guid Id,
    Guid CategoriaId,
    string Nome,
    string? Descricao,
    decimal Preco,
    bool Ativo,
    bool Disponivel,
    string? UrlImagem) : IRecortadoPorUnidade;

/// <summary>
/// <c>vw_modulo</c> — o catálogo de módulos. <b>Não recortado</b>: é o mesmo para todas as casas.
/// </summary>
public sealed record ModuloLeitura(Guid Id, string Codigo, string Nome, bool Nucleo, int Ordem);

/// <summary>
/// <c>vw_modulo_dependencia</c> — <c>RN-GER-020</c>, uma aresta do grafo. Também não recortado.
/// </summary>
public sealed record ModuloDependenciaLeitura(
    Guid ModuloId, string Codigo, Guid DependeDeId, string DependeDeCodigo);

/// <summary>
/// <c>vw_unidade_modulo</c> — <c>RN-GER-021</c>, uma habilitação com a sua vigência.
/// <para>
/// ⚠ A visão devolve <b>todas</b> as habilitações, inclusive as vencidas: quem decide o que vigora
/// é o instante que o handler recebe (<c>ListarModulosDaUnidade</c> aceita <c>Em</c>), e uma visão
/// que já filtrasse por <c>now()</c> tornaria essa pergunta impossível.
/// </para>
/// </summary>
public sealed record ModuloDaUnidadeLeitura(
    Guid UnidadeId,
    Guid ModuloId,
    string Codigo,
    DateTimeOffset VigenciaInicio,
    DateTimeOffset? VigenciaFim) : IRecortadoPorUnidade;

/// <summary><c>vw_perfil</c> — o perfil da unidade.</summary>
public sealed record PerfilLeitura(
    Guid UnidadeId,
    Guid Id,
    string Nome,
    string? Descricao,
    bool Sistema,
    int TotalDeUsuarios) : IRecortadoPorUnidade;

/// <summary>
/// <c>vw_perfil_permissao</c> — <c>RN-GER-010</c>, o que cada perfil concede.
/// <para>
/// ⚠ Só o <b>código</b> — ADR-0022 §3: a concessão é claim de papel
/// (<c>perfil_permissao.valor</c>), e a descrição legível vive no <c>CatalogoDeAcesso</c>, em
/// código. Uma coluna de descrição aqui seria a segunda fonte do mesmo texto.
/// </para>
/// </summary>
public sealed record PerfilPermissaoLeitura(
    Guid UnidadeId, Guid PerfilId, string Codigo) : IRecortadoPorUnidade;

/// <summary>
/// <c>vw_usuario</c> — quem opera.
/// <para>
/// ⚠⚠ <b>Recortado, e isso é <c>RN-GER-016</c> virando estrutura.</b> No lado da escrita
/// <c>Usuario</c> NÃO implementa <c>IPertenceAUnidade</c>, porque o usuário de plataforma não tem
/// unidade — e a consequência era que todo handler precisava lembrar de escrever o recorte. Aqui o
/// filtro global cobre: o restaurante não enxerga usuário de plataforma nem de outra casa, e a
/// plataforma atravessa (ADR-0013).
/// </para>
/// </summary>
/// <param name="Cpf">Cru, como está no banco. A formatação é do domínio e acontece no handler.</param>
/// <param name="Sobrenome">
/// ⚠ <b>Anulável, e não por descuido:</b> <c>RN-GER-030</c> — o usuário de PLATAFORMA é criado só
/// com nome. Declarar <c>string</c> aqui derrubava a listagem do suporte com
/// <c>InvalidCastException: Column 'sobrenome' is null</c>, que chega ao usuário como 500 e manda
/// procurar a falha na consulta em vez de no contrato.
/// </param>
public sealed record UsuarioLeitura(
    Guid UnidadeId,
    Guid Id,
    string Nome,
    string? Sobrenome,
    string? Cpf,
    string Email,
    Guid? PerfilId,
    string? PerfilNome,
    bool Ativo,
    bool DeveTrocarSenha,
    bool EhPlataforma,
    DateTimeOffset CriadoEm) : IRecortadoPorUnidade;

/// <summary>
/// <c>vw_parametro</c> — o catálogo, <c>SPEC-GER-003</c>. Não recortado: é global.
/// </summary>
public sealed record ParametroLeitura(
    Guid Id, string Codigo, string Descricao, string TipoValor, string? Dominio);

/// <summary>
/// <c>vw_parametro_valor</c> — o valor por unidade, com vigência.
/// <para>
/// ⚠ A visão não escolhe o vigente. Quem escolhe é o handler, com o instante que recebeu — a mesma
/// razão de <see cref="ModuloDaUnidadeLeitura"/>.
/// </para>
/// </summary>
public sealed record ParametroValorLeitura(
    Guid UnidadeId,
    Guid ParametroId,
    string Codigo,
    string Valor,
    DateTimeOffset VigenciaInicio,
    DateTimeOffset? VigenciaFim) : IRecortadoPorUnidade;
