using FluentAssertions;

namespace GrelhaHub.Application.Tests.Arquitetura;

/// <summary>
/// <c>RN-GER-002</c> / ADR-0022 — <c>UserManager.DeleteAsync</c> existe e <b>nunca pode ser
/// chamado</b>: nenhum registro transacional é excluído fisicamente, e usuário é o registro que
/// mais importa preservar (a trilha, o pedido e o token de acesso apontam para ele).
/// <para>
/// <b>Por que varredura de fonte e não reflexão:</b> uma chamada de método não aparece em
/// metadado de tipo — reflexão enxerga o que um assembly DECLARA, não o que ele INVOCA. As
/// alternativas seriam ler IL (peso desproporcional) ou confiar na revisão (que é exatamente o
/// que "um teste de arquitetura recusa a build" existe para não fazer). A varredura pega
/// qualquer <c>.DeleteAsync(</c> em código de produção — do <c>UserManager</c>, do
/// <c>RoleManager</c> ou de um <c>DbSet</c> — e quem tiver um uso legítimo um dia vai precisar
/// vir aqui dizer por quê, que é o custo certo.
/// </para>
/// <para>
/// ⚠ A defesa em tempo de execução é outra, e continua de pé: <c>InterceptorDeAuditoria</c>
/// recusa <c>EntityState.Deleted</c> de <c>Usuario</c> e <c>Perfil</c> no <c>SaveChanges</c>.
/// Este teste fecha o caminho que NÃO passa pelo interceptor — <c>DeleteAsync</c> do
/// <c>UserManager</c> usa o mesmo contexto, mas um store futuro poderia não usar.
/// </para>
/// </summary>
public sealed class ExclusaoFisicaDeIdentityTests
{
    [Fact(DisplayName = "RN-GER-002 — nenhum código de produção chama DeleteAsync")]
    public void NenhumaChamadaADeleteAsync()
    {
        var fontes = DiretorioDosFontes();

        var infratores = Directory
            .EnumerateFiles(fontes, "*.cs", SearchOption.AllDirectories)
            .Where(caminho => !caminho.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !caminho.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(caminho => File.ReadLines(caminho)
                .Select((linha, indice) => (Linha: linha, Numero: indice + 1))
                .Where(l => !EhComentario(l.Linha))
                .Where(l => l.Linha.Contains(".DeleteAsync(", StringComparison.Ordinal))
                .Select(l => $"{Path.GetRelativePath(fontes, caminho)}:{l.Numero}"))
            .ToList();

        infratores.Should().BeEmpty(
            "RN-GER-002 — registro transacional não se exclui fisicamente; usuário e perfil "
            + "se INATIVAM. Se este uso é legítimo (e não é do UserManager), justifique aqui");
    }

    private static bool EhComentario(string linha)
    {
        var aparada = linha.TrimStart();

        return aparada.StartsWith("//", StringComparison.Ordinal)
            || aparada.StartsWith('*');
    }

    /// <summary>
    /// Sobe do diretório do assembly de teste até achar <c>backend/src</c> — o teste roda de
    /// <c>bin/&lt;config&gt;/&lt;tfm&gt;</c>, e um caminho relativo fixo quebraria na primeira
    /// mudança de layout. Falha com mensagem própria se não achar: teste de arquitetura que
    /// silenciosamente varre um diretório vazio é pior que teste nenhum.
    /// </summary>
    private static string DiretorioDosFontes()
    {
        for (var atual = new DirectoryInfo(AppContext.BaseDirectory);
             atual is not null;
             atual = atual.Parent)
        {
            var candidato = Path.Combine(atual.FullName, "src", "GrelhaHub.Domain");

            if (Directory.Exists(candidato))
            {
                return Path.Combine(atual.FullName, "src");
            }
        }

        throw new InvalidOperationException(
            "Não encontrei backend/src acima do assembly de teste — a varredura de fonte de "
            + "RN-GER-002 não tem o que varrer, e passar sem varrer seria mentir.");
    }
}
