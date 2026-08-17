using GrelhaHub.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GrelhaHub.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo <c>dotnet ef</c> ao gerar migrations.
/// <para>
/// Existe porque o <c>GrelhaHubDbContext</c> depende de <see cref="IContextoDeExecucao"/>,
/// que em tempo de execução vem do JWT — e em tempo de design não há requisição HTTP.
/// A conexão aqui não precisa ser válida: as migrations são geradas a partir do modelo,
/// não do banco.
/// </para>
/// </summary>
public sealed class FabricaDeContextoEmTempoDeDesign : IDesignTimeDbContextFactory<GrelhaHubDbContext>
{
    public GrelhaHubDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GrelhaHubDbContext>()
            .UseNpgsql("Host=localhost;Database=grelhahub;Username=grelhahub;Password=grelhahub")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new GrelhaHubDbContext(options, new ContextoDeDesign());
    }

    private sealed class ContextoDeDesign : IContextoDeExecucao
    {
        public Guid? UsuarioId => null;
        public Guid UnidadeId => Guid.Empty;
        public bool Autenticado => false;
        public bool PodeVerCusto => false;
        public bool EhGerencia => false;
        public void DefinirUnidadePublica(Guid unidadeId) { }
    }
}
