using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
internal sealed class ApiDeTestes(
    string stringDeConexao, string stringDeLog, string stringDeArquivo, string chaveJwt)
    : WebApplicationFactory<Program>
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

                // ADR-0017 — o banco de log. Sem isto a API nem sobe, por decisão: sistema sem
                // onde gravar a trilha não deveria aceitar a primeira requisição.
                ["ConnectionStrings:GrelhaHubLog"] = stringDeLog,

                // ADR-0018 — o banco de arquivo. Mesma regra: sem ele a API não sobe.
                ["ConnectionStrings:GrelhaHubArquivo"] = stringDeArquivo,
                ["Jwt:Chave"] = chaveJwt,

                // O log de requisição do Serilog polui a saída do runner e esconde a
                // asserção que falhou, que é o que se quer ler.
                ["Serilog:MinimumLevel:Default"] = "Warning",

                // RN-GER-014 — credenciais do usuário de plataforma, pelo mesmo mecanismo de
                // configuração que o .env da VPS usa em produção.
                ["Plataforma:Nome"] = "Suporte de Teste",
                ["Plataforma:Email"] = AmbienteDeIntegracao.EmailDePlataforma,
                ["Plataforma:SenhaInicial"] = AmbienteDeIntegracao.SenhaDePlataforma,
            }));

        builder.UseSetting("ConnectionStrings:GrelhaHub", stringDeConexao);
        builder.UseSetting("ConnectionStrings:GrelhaHubLog", stringDeLog);
        builder.UseSetting("ConnectionStrings:GrelhaHubArquivo", stringDeArquivo);
        builder.UseSetting("Jwt:Chave", chaveJwt);

        // ADR-0016 — O CORREIO É O ÚNICO DUBLÊ DA SUÍTE, e a exceção é declarada: a alternativa
        // seria um servidor SMTP no contêiner para provar uma regra que não é sobre SMTP. O que os
        // testes precisam do e-mail é o CONTEÚDO — o link com o token, que só existe na mensagem
        // (RN-GER-033 guarda apenas o hash). O caminho continua real até a fila.
        builder.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<Application.Common.Interfaces.IEnviadorDeEmail>();
            servicos.AddSingleton<Application.Common.Interfaces.IEnviadorDeEmail>(CaixaDeSaida);

            // ⚠⚠ O WORKER DE DRENAGEM NÃO RODA NO HOST DE TESTE — e não é dublê, é remoção.
            //
            // Em produção ele é exatamente o que a ADR-0017 quer: varre a outbox a cada 5 s e
            // copia a trilha para o banco de log. O que estava errado era ele existir num host
            // cujos testes afirmam o estado ANTES e DEPOIS da drenagem: a suíte leva ~2 min, o
            // worker tica ~24 vezes, e quando um tique caía entre a operação e a asserção,
            // `Outbox_GravaNaMesmaTransacao` encontrava a outbox vazia — verde local, vermelho
            // no agente carregado de CI. Com ele ligado, TODA asserção sobre trilha/outbox é uma
            // corrida, não só essa.
            //
            // A drenagem no teste é EXPLÍCITA, por `Ambiente.DrenarOutboxAsync()` — que chama o
            // MESMO código do worker (`ServicoDeDrenagemDaOutbox.CopiarAsync`), então desligar o
            // relógio não reduz o que a suíte cobre; reduz o acaso.
            //
            // Removido pelo DESCRIPTOR, e não `RemoveAll<IHostedService>()`: o serviço de envio
            // de e-mail também é hosted, e é ele que consome a fila até a caixa de saída dublê.
            var drenagem = servicos.SingleOrDefault(descriptor =>
                descriptor.ImplementationType ==
                    typeof(Infrastructure.Persistence.ServicoDeDrenagemDaOutbox));

            if (drenagem is not null)
            {
                servicos.Remove(drenagem);
            }
        });
    }

    /// <summary>
    /// Escopo de serviços da API em execução — para o teste que precisa de um serviço real
    /// (o <see cref="Application.Common.Interfaces.IRelogio"/>, por exemplo) e não de uma
    /// reimplementação sua.
    /// </summary>
    public IServiceScope CriarEscopo() => Services.CreateScope();

    /// <summary>
    /// A caixa de saída desta execução — estática pelo mesmo motivo da raiz de arquivos: um teste
    /// dispara o e-mail por um host e lê a mensagem pelo mesmo lugar.
    /// </summary>
    public static readonly CaixaDeSaidaDeTeste CaixaDeSaida = new();
}
