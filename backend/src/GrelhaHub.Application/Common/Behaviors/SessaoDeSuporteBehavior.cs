using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Common.Behaviors;

/// <summary>
/// <c>RN-GER-015</c> ⚠ — usuário de plataforma só <b>escreve</b> com sessão de suporte aberta.
/// ADR-0013.
/// <para>
/// É a contrapartida de ter desligado o filtro global para ele: ler para diagnosticar é rotina;
/// alterar o dado de um cliente sem dizer por quê, não.
/// </para>
/// <para>
/// <b>Escrita é identificada pelo sufixo <c>Command</c></b>, e isso só é confiável porque a
/// partida <b>recusa</b> operação cujo nome não termine em <c>Command</c> ou <c>Query</c>
/// (<c>ValidadorDeDeclaracaoDeModulo</c>). Sem essa checagem, a convenção seria suposição — e uma
/// operação chamada <c>ExcluirTudo</c> passaria como leitura.
/// </para>
/// </summary>
public sealed class SessaoDeSuporteBehavior<TRequest, TResponse>(
    IContextoDeExecucao contexto, IAppDbContext db, IRelogio relogio)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Escritas de plataforma que <b>não</b> exigem sessão de suporte, e por quê.
    /// <para>
    /// Lista explícita e curta de propósito: uma heurística — "isenta quem tem propriedade
    /// Justificativa" — pareceria elegante e isentaria por acidente o comando que alguém escrevesse
    /// amanhã com um campo de nome parecido.
    /// </para>
    /// </summary>
    public static readonly string[] OperacoesLiberadas =
    [
        // Abrir a sessão não pode exigir sessão aberta.
        "AbrirSessaoDeSuporteCommand",
        "EncerrarSessaoDeSuporteCommand",

        // Login antecede tudo.
        "AutenticarCommand",

        // RN-GER-015 exige que a escrita de plataforma seja explicável. Habilitar e desabilitar
        // módulo JÁ carrega `Justificativa` obrigatória de dez caracteres no próprio comando —
        // a explicação fica na linha de `unidade_modulo`, junto do que ela explica, o que é MAIS
        // rastreável que uma sessão genérica de duas horas.
        //
        // Além disso é ato COMERCIAL, não de suporte: exigir "abra uma sessão de suporte" para
        // registrar uma venda seria pedir o motivo errado.
        "AlterarModuloDaUnidadeCommand",

        // SPEC-GER-007 §4 — cadastro assistido de estabelecimento.
        //
        // Aqui a isenção não é preferência: é a ÚNICA saída coerente. A sessão de suporte é aberta
        // PARA UMA UNIDADE (`AbrirSessaoDeSuporteCommand` recebe `unidadeId`), e no cadastro a
        // unidade ainda não existe — exigir sessão tornaria a operação impossível de executar, não
        // mais controlada.
        //
        // E o argumento de fundo é o mesmo do módulo: não há dado de cliente sendo tocado. Criar um
        // cliente é ato comercial; RN-GER-015 existe para explicar por que a plataforma mexeu no
        // dado de alguém que já era cliente. A autoria fica na trilha (SPEC-GER-005), com o
        // `usuario_id` da plataforma na linha de criação da unidade.
        "CadastrarEstabelecimentoCommand",

        // Consequência de delegar: `CadastrarEstabelecimentoHandler` despacha o comando interno,
        // e ele chega ao pipeline com o contexto de plataforma. Sem isto, a isenção acima seria
        // anulada pelo próprio comando que ela libera.
        "CadastrarRestauranteCommand",

        // RN-GER-029 — inativar ou reativar a unidade.
        //
        // ⚠ A LINHA DIVISÓRIA desta lista é **comercial × suporte**, e este comando cai do lado
        // comercial pelo mesmo argumento de `AlterarModuloDaUnidadeCommand`: encerrar contrato ou
        // religar cliente é ato de negócio, carrega `Justificativa` obrigatória de dez caracteres
        // no próprio comando, e a explicação fica na trilha junto do que ela explica.
        //
        // `AlterarEstabelecimentoCommand` — mudar razão social, nome fantasia e telefone de um
        // cliente — **NÃO** está aqui de propósito: aquilo é mexer no dado de alguém em nome dele,
        // que é a definição de suporte, e não carrega justificativa própria. Exige sessão.
        "AlternarSituacaoDaUnidadeCommand",

        // RN-GER-030 / RN-GER-031 — a plataforma administrando os SEUS PRÓPRIOS usuários.
        //
        // Cai do lado esquerdo da linha divisória por uma razão mais forte que as anteriores: aqui
        // não existe dado de cliente nem cliente. E a sessão de suporte é aberta PARA UMA UNIDADE
        // (`AbrirSessaoDeSuporteCommand` recebe `unidadeId`) — exigi-la para criar um colega de
        // suporte obrigaria a escolher um restaurante ao acaso e registrar um motivo de suporte
        // falso na conta dele. Seria menos rastreável, não mais.
        //
        // A autoria não se perde: fica na trilha (SPEC-GER-005) com o `usuario_id` de quem criou.
        "CriarUsuarioDeSuporteCommand",
        "AlternarSituacaoDoUsuarioDeSuporteCommand",

        // RF-SEG-005 — trocar a PRÓPRIA senha.
        //
        // ⚠ Sem esta linha, usuário de plataforma com troca pendente fica em DEADLOCK, e o
        // bloqueio é total: `TrocaDeSenhaBehavior` só libera `TrocarSenhaCommand`, e este behavior
        // exigiria sessão de suporte justamente dele — sessão que não pode ser aberta, porque
        // `AbrirSessaoDeSuporteCommand` está bloqueado pela troca pendente. A conta nasceria
        // inutilizável, sem nenhuma operação possível e sem mensagem que explicasse o círculo.
        //
        // Era latente até RN-GER-030: nenhum caminho criava usuário de plataforma com a troca
        // pendente (a carga inicial nasce sem ela, e `RedefinirSenhaCommand` recusa plataforma).
        // Passou a ser alcançável no mesmo commit que criou a porta, e é por isso que está aqui.
        //
        // Trocar a própria senha também não é "escrita sobre dado de unidade": é a conta de quem
        // está pedindo.
        "TrocarSenhaCommand",
    ];

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var nome = typeof(TRequest).Name;

        var precisaDeSessao =
            contexto.EhPlataforma
            && nome.EndsWith("Command", StringComparison.Ordinal)
            && !OperacoesLiberadas.Contains(nome, StringComparer.Ordinal);

        if (!precisaDeSessao)
        {
            return await next();
        }

        if (contexto.UsuarioId is not { } usuarioId)
        {
            throw new NaoAutorizadoException("Sessão inválida.");
        }

        var limite = relogio.Agora - Domain.Gerais.SessaoDeSuporte.Duracao;

        var temSessao = await db.SessoesDeSuporte
            .AsNoTracking()
            .AnyAsync(
                sessao => sessao.UsuarioId == usuarioId
                       && sessao.EncerradaEm == null
                       && sessao.AbertaEm > limite,
                cancellationToken);

        if (!temSessao)
        {
            throw new SessaoDeSuporteExigidaException();
        }

        return await next();
    }
}
