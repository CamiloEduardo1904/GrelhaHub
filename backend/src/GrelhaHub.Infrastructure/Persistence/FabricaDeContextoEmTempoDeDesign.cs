using GrelhaHub.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// Usada pelo <c>dotnet ef</c> — <c>migrations add</c> e <c>database update</c>.
/// <para>
/// Existe porque o <c>GrelhaHubDbContext</c> depende de <see cref="IContextoDeExecucao"/>,
/// que em tempo de execução vem do JWT — e em tempo de design não há requisição HTTP.
/// </para>
/// <para>
/// ⚠ <b>A conexão precisa ser válida para <c>database update</c>.</b> O comentário anterior aqui
/// dizia que ela "não precisa ser válida, porque migrations são geradas a partir do modelo" — o que
/// vale para <c>migrations add</c> e é falso para <c>database update</c>. O literal apontava para a
/// porta <b>5432</b>, e o contêiner de desenvolvimento publica em <b>5433</b>: o comando falhava com
/// erro de <i>autenticação</i>, porque na 5432 havia outro PostgreSQL atendendo — mensagem que manda
/// procurar senha em vez de porta.
/// </para>
/// </summary>
public sealed class FabricaDeContextoEmTempoDeDesign : IDesignTimeDbContextFactory<GrelhaHubDbContext>
{
    /// <summary>
    /// A porta é a do <c>docker-compose.yml</c> de desenvolvimento (5433 no host → 5432 no
    /// contêiner). Sobreponha com a variável de ambiente <c>ConnectionStrings__GrelhaHub</c>.
    /// </summary>
    private const string ConexaoDeDesenvolvimento =
        "Host=localhost;Port=5433;Database=grelhahub;Username=grelhahub;Password=grelhahub";

    public GrelhaHubDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__GrelhaHub");

        var options = new DbContextOptionsBuilder<GrelhaHubDbContext>()
            .UseNpgsql(string.IsNullOrWhiteSpace(conexao) ? ConexaoDeDesenvolvimento : conexao)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new GrelhaHubDbContext(options, new ContextoDeDesign());
    }

    private sealed class ContextoDeDesign : IContextoDeExecucao
    {
        public Guid? UsuarioId => null;
        public Guid UnidadeId => Guid.Empty;
        public bool Autenticado => false;
        public System.Security.Claims.ClaimsPrincipal? Principal => null;
        public bool PodeVerCusto => false;
        public IReadOnlySet<string> Permissoes => new HashSet<string>();
        public bool Pode(string permissao) => false;
        public bool DeveTrocarSenha => false;
        public bool EhPlataforma => false;
        public void DefinirUnidadePublica(Guid unidadeId) { }
    }
}
