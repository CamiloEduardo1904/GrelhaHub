using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Infraestrutura;

/// <summary>
/// Sobe a API real contra o PostgreSQL do contêiner — ADR-0009.
/// <para>
/// <b>Nada é substituído por dublê.</b> Controller, pipeline do MediatR, validators,
/// <c>TratadorDeExcecoes</c>, autenticação JWT, filtro global e EF Core são os de produção.
/// O único ponto trocado é a string de conexão: é isso que faz o teste provar a regra em vez
/// de provar a configuração do teste.
/// </para>
/// </summary>
internal sealed class ApiDeTestes(string stringDeConexao, string chaveJwt) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" e não "Development" de propósito: em Development o Program aplica
        // migrations na partida (ADR-0006). Aqui quem migra é o ambiente, uma vez só —
        // se cada host migrasse, dois testes em paralelo competiriam pelo mesmo lock.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:GrelhaHub"] = stringDeConexao,
                ["Jwt:Chave"] = chaveJwt,

                // O log de requisição do Serilog polui a saída do runner e esconde a
                // asserção que falhou, que é o que se quer ler.
                ["Serilog:MinimumLevel:Default"] = "Warning",

                // ADR-0015 / SPEC-VEN-002 — a raiz dos arquivos vai para uma pasta temporária
                // POR EXECUÇÃO. Sem isto, o upload dos testes escreve dentro de `bin/Release` e o
                // diretório de build cresce a cada rodada, com a rodada seguinte encontrando o
                // resíduo da anterior — o mesmo problema que a lista de tabelas do
                // `AmbienteDeIntegracao` resolve para o banco.
                ["Arquivos:Raiz"] = RaizDeArquivos,

                // RN-GER-014 — credenciais do usuário de plataforma, pelo mesmo mecanismo de
                // configuração que o .env da VPS usa em produção.
                ["Plataforma:Nome"] = "Suporte de Teste",
                ["Plataforma:Email"] = AmbienteDeIntegracao.EmailDePlataforma,
                ["Plataforma:SenhaInicial"] = AmbienteDeIntegracao.SenhaDePlataforma,
            }));

        builder.UseSetting("ConnectionStrings:GrelhaHub", stringDeConexao);
        builder.UseSetting("Jwt:Chave", chaveJwt);
    }

    /// <summary>
    /// Pasta de arquivos desta execução. Estática para que todos os hosts da suíte compartilhem a
    /// mesma raiz — o `Existe` conferido em um teste precisa achar o que outro gravou pela API.
    /// </summary>
    private static readonly string RaizDeArquivos = Path.Combine(
        Path.GetTempPath(), "grelhahub-testes", Guid.CreateVersion7().ToString());

    /// <summary>
    /// Escopo de serviços da API em execução — para o teste que precisa de um serviço real
    /// (o <see cref="Application.Common.Interfaces.IRelogio"/>, por exemplo) e não de uma
    /// reimplementação sua.
    /// </summary>
    public IServiceScope CriarEscopo() => Services.CreateScope();
}
