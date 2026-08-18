using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Features.Gerais.Parametros;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-003</c> — CA-1 a CA-5 e CA-7: parâmetros com vigência.
/// <para>
/// Além dos parâmetros em si, este arquivo prova o <b>padrão de vigência</b> que
/// `preco_produto` (<c>RN-VEN-010</c>) e `ficha_tecnica` (<c>RN-PRO-002</c>) vão reusar: o
/// intervalo semiaberto e a `EXCLUDE` que impede sobreposição mesmo por escrita fora da
/// aplicação.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ParametrosTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    private const string TaxaDeServico = "PAR-VEN-002";

    [Fact(DisplayName = "SPEC-GER-003 CA-7 — a carga inicial cria todos os parâmetros documentados")]
    public async Task CA7_CargaInicialCriaTodosOsParametros()
    {
        var restaurante = await CadastrarRestauranteAsync("Parametro Burger", "11111111000191");

        // Fonte 1: o documento. Fonte 2: o catálogo do C#. Fonte 3: o banco.
        // Se qualquer uma sair de linha, este teste fica vermelho — que é o ponto do CA-7:
        // parâmetro documentado que ninguém semeou só apareceria como exceção no serviço.
        var noDocumento = CodigosNoDocumento();
        var noCatalogo = CatalogoDeParametros.Todos.Select(d => d.Codigo).OrderBy(c => c, StringComparer.Ordinal).ToList();

        noCatalogo.Should().BeEquivalentTo(
            noDocumento,
            "CatalogoDeParametros transcreve docs/04-parametros-do-sistema.md §1");

        await using var db = Contexto(restaurante);

        var naTabela = await db.Parametros.AsNoTracking()
            .Select(p => p.Codigo).OrderBy(c => c).ToListAsync();

        naTabela.Should().BeEquivalentTo(noDocumento, "a migration semeia a definição de cada parâmetro");

        // E cada um precisa ter VALOR na unidade nova — definição sem valor falha no serviço.
        var comValor = await db.ValoresDeParametro.AsNoTracking()
            .Select(v => v.Parametro!.Codigo).OrderBy(c => c).ToListAsync();

        comValor.Should().BeEquivalentTo(noDocumento, "toda definição recebe valor inicial na carga");

        // Projeta no SQL antes de montar o dicionário: `ToDictionaryAsync` avalia o seletor no
        // cliente, e a navegação `Parametro` não vem carregada num `AsNoTracking` sem `Include`.
        var iniciais = (await db.ValoresDeParametro.AsNoTracking()
                .Select(v => new { Codigo = v.Parametro!.Codigo, v.Valor })
                .ToListAsync())
            .ToDictionary(linha => linha.Codigo, linha => linha.Valor, StringComparer.Ordinal);

        foreach (var definicao in CatalogoDeParametros.Todos)
        {
            iniciais[definicao.Codigo].Should().Be(
                definicao.ValorInicial, $"{definicao.Codigo} precisa nascer com o valor documentado");
        }
    }

    [Fact(DisplayName = "SPEC-GER-003 CA-1 — a leitura é na data do evento, não no valor de agora")]
    public async Task CA1_LeituraNaDataDoEvento()
    {
        var restaurante = await CadastrarRestauranteAsync("Parametro Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var viradaDaTaxa = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        var alteracao = await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/parametros/{TaxaDeServico}", UriKind.Relative),
            new
            {
                NovoValor = "12.00",
                VigenciaInicio = viradaDaTaxa,
                Justificativa = "Reajuste combinado com a equipe de salão",
            });

        alteracao.EnsureSuccessStatusCode();

        // Antes da virada: o valor antigo. Recalcular um pedido de junho com a taxa de julho
        // reescreveria o passado — o mesmo erro que RN-VEN-008 evita para preço.
        var emJunho = await gestao.GetFromJsonAsync<ParametroResponse>(new Uri(
            $"/api/v1/parametros/{TaxaDeServico}?vigenteEm=2026-06-15T12:00:00Z", UriKind.Relative));

        emJunho!.Valor.Should().Be("10.00");

        var agora = await gestao.GetFromJsonAsync<ParametroResponse>(
            new Uri($"/api/v1/parametros/{TaxaDeServico}", UriKind.Relative));

        agora!.Valor.Should().Be("12.00", "sem data, vale o corrente");
    }

    [Fact(DisplayName = "SPEC-GER-003 CA-2 — alterar fecha a vigência anterior e preserva o histórico")]
    public async Task CA2_AlteracaoPreservaOHistorico()
    {
        var restaurante = await CadastrarRestauranteAsync("Parametro Burger", "11111111000191");

        Guid idOriginal;
        string valorOriginal;

        await using (var antes = Contexto(restaurante))
        {
            var corrente = await antes.ValoresDeParametro.AsNoTracking()
                .SingleAsync(v => v.Parametro!.Codigo == TaxaDeServico);

            idOriginal = corrente.Id;
            valorOriginal = corrente.Valor;
            corrente.VigenciaFim.Should().BeNull("a vigência da carga nasce aberta");
        }

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var inicio = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        var alteracao = await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/parametros/{TaxaDeServico}", UriKind.Relative),
            new { NovoValor = "12.00", VigenciaInicio = inicio, Justificativa = "Reajuste de setembro" });

        alteracao.EnsureSuccessStatusCode();

        await using var db = Contexto(restaurante);

        var vigencias = await db.ValoresDeParametro.AsNoTracking()
            .Where(v => v.Parametro!.Codigo == TaxaDeServico)
            .OrderBy(v => v.VigenciaInicio)
            .ToListAsync();

        vigencias.Should().HaveCount(2, "alterar INSERE, não sobrescreve");

        var anterior = vigencias[0];
        anterior.Id.Should().Be(idOriginal, "a linha antiga é a mesma");
        anterior.Valor.Should().Be(valorOriginal, "e nenhum outro campo dela mudou");
        anterior.VigenciaFim.Should().Be(inicio, "fechada exatamente onde a nova começa");

        vigencias[1].Valor.Should().Be("12.00");
        vigencias[1].VigenciaFim.Should().BeNull();
        vigencias[1].Justificativa.Should().Be("Reajuste de setembro");
    }

    [Fact(DisplayName = "SPEC-GER-003 CA-3 — o banco recusa vigências sobrepostas, mesmo por escrita direta")]
    public async Task CA3_VigenciasNaoSeSobrepoem()
    {
        var restaurante = await CadastrarRestauranteAsync("Parametro Burger", "11111111000191");

        await using var db = Contexto(restaurante);
        await db.Database.OpenConnectionAsync();

        var parametroId = await db.Parametros.AsNoTracking()
            .Where(p => p.Codigo == TaxaDeServico).Select(p => p.Id).SingleAsync();

        var conexao = db.Database.GetDbConnection();
        using var comando = conexao.CreateCommand();

        // A vigência da carga é aberta — [MinValue, ∞). Qualquer início conflita com ela.
        // Escrita direta no banco, sem passar pela aplicação: é justamente o caminho por onde a
        // sobreposição nasceria se a garantia fosse só no C#.
        comando.CommandText = """
            INSERT INTO parametro_valor (id, parametro_id, unidade_id, valor, vigencia_inicio, dh_criacao)
            VALUES (gen_random_uuid(), @parametro, @unidade, '15.00', @inicio, now())
            """;

        Adicionar(comando, "parametro", parametroId);
        Adicionar(comando, "unidade", restaurante.UnidadeId);
        Adicionar(comando, "inicio", new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));

        var escrita = async () => await comando.ExecuteNonQueryAsync();

        (await escrita.Should().ThrowAsync<DbException>())
            .Which.Message.Should().Contain("ex_parametro_valor_vigencia");
    }

    [Fact(DisplayName = "SPEC-GER-003 CA-3 — vigência que começa onde a outra termina é aceita")]
    public async Task CA3_IntervaloSemiabertoNaoConflita()
    {
        var restaurante = await CadastrarRestauranteAsync("Parametro Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Duas alterações encadeadas: a segunda começa exatamente onde a primeira passa a
        // valer. `tstzrange` é semiaberto, então não há conflito — e é disso que o fechamento
        // encadeado depende.
        foreach (var (valor, mes) in new[] { ("11.00", 7), ("12.00", 8), ("13.00", 9) })
        {
            var resposta = await gestao.PutAsJsonAsync(
                new Uri($"/api/v1/parametros/{TaxaDeServico}", UriKind.Relative),
                new
                {
                    NovoValor = valor,
                    VigenciaInicio = new DateTimeOffset(2026, mes, 1, 0, 0, 0, TimeSpan.Zero),
                    Justificativa = $"Reajuste programado do mes {mes}",
                });

            resposta.EnsureSuccessStatusCode();
        }

        await using var db = Contexto(restaurante);

        var vigencias = await db.ValoresDeParametro.AsNoTracking()
            .Where(v => v.Parametro!.Codigo == TaxaDeServico)
            .OrderBy(v => v.VigenciaInicio)
            .ToListAsync();

        vigencias.Should().HaveCount(4, "carga inicial + três reajustes");

        // Cadeia sem vão nem sobreposição: o fim de cada uma é o início da seguinte.
        for (var i = 0; i < vigencias.Count - 1; i++)
        {
            vigencias[i].VigenciaFim.Should().Be(
                vigencias[i + 1].VigenciaInicio, "sem vão entre vigências, nem sobreposição");
        }

        vigencias[^1].VigenciaFim.Should().BeNull("só a última fica aberta");
    }

    [Fact(DisplayName = "SPEC-GER-003 CA-4 — alteração com efeito retroativo é recusada")]
    public async Task CA4_AlteracaoRetroativaEhRecusada()
    {
        var restaurante = await CadastrarRestauranteAsync("Parametro Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var emAgosto = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        (await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/parametros/{TaxaDeServico}", UriKind.Relative),
            new { NovoValor = "12.00", VigenciaInicio = emAgosto, Justificativa = "Reajuste de agosto" }))
            .EnsureSuccessStatusCode();

        var retroativa = await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/parametros/{TaxaDeServico}", UriKind.Relative),
            new
            {
                NovoValor = "20.00",
                VigenciaInicio = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
                Justificativa = "Tentativa de reescrever o passado",
            });

        retroativa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await retroativa.Content.ReadAsStringAsync()).Should().Contain("retroativo");

        await using var db = Contexto(restaurante);

        var vigencias = await db.ValoresDeParametro.AsNoTracking()
            .CountAsync(v => v.Parametro!.Codigo == TaxaDeServico);

        vigencias.Should().Be(2, "nada foi gravado pela tentativa recusada");
    }

    [Theory(DisplayName = "SPEC-GER-003 CA-5 — valor fora do domínio é recusado com a mensagem do domínio")]
    [InlineData("PAR-VEN-002", "150", "0..100")]
    [InlineData("PAR-VEN-002", "-1", "0..100")]
    [InlineData("PAR-VEN-003", "silencioso", "bloquear|avisar|permitir")]
    [InlineData("PAR-EST-001", "tres", ">= 0")]
    public async Task CA5_ValorForaDoDominioEhRecusado(string codigo, string valor, string dominio)
    {
        var restaurante = await CadastrarRestauranteAsync("Parametro Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        var resposta = await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/parametros/{codigo}", UriKind.Relative),
            new
            {
                NovoValor = valor,
                VigenciaInicio = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                Justificativa = "Tentativa de valor invalido",
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // A mensagem cita o domínio: dizer só "valor inválido" obriga o usuário a adivinhar.
        (await resposta.Content.ReadAsStringAsync()).Should().Contain(dominio);
    }

    [Fact(DisplayName = "SPEC-GER-003 — o cache é invalidado na escrita e a leitura não serve valor velho")]
    public async Task Cache_EhInvalidadoNaEscrita()
    {
        var restaurante = await CadastrarRestauranteAsync("Parametro Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Primeira leitura popula o cache.
        var antes = await gestao.GetFromJsonAsync<ParametroResponse>(
            new Uri($"/api/v1/parametros/{TaxaDeServico}", UriKind.Relative));
        antes!.Valor.Should().Be("10.00");

        (await gestao.PutAsJsonAsync(
            new Uri($"/api/v1/parametros/{TaxaDeServico}", UriKind.Relative),
            new
            {
                NovoValor = "12.00",
                VigenciaInicio = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                Justificativa = "Reajuste que precisa aparecer na leitura",
            }))
            .EnsureSuccessStatusCode();

        // Leitura pelo IParametros, que é o caminho com cache — a consulta HTTP vai ao banco.
        using var escopo = Ambiente.CriarEscopo();

        var contexto = escopo.ServiceProvider.GetRequiredService<IContextoDeExecucao>();
        contexto.DefinirUnidadePublica(restaurante.UnidadeId);

        var parametros = escopo.ServiceProvider.GetRequiredService<IParametros>();

        var depois = await parametros.ObterAsync<decimal>(
            TaxaDeServico, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

        depois.Should().Be(12.00m, "a escrita invalidou o cache");

        // E a leitura em data anterior continua devolvendo o valor antigo, do mesmo cache.
        var emJunho = await parametros.ObterAsync<decimal>(
            TaxaDeServico, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

        emJunho.Should().Be(10.00m, "o cache guarda todas as vigências, não só a corrente");
    }

    /// <summary>
    /// Lê os códigos `PAR-*` da tabela §1 de `docs/04-parametros-do-sistema.md`.
    /// <para>
    /// É o documento que manda — Document First. Um parâmetro acrescentado ao documento e não
    /// ao código faz este teste falhar, que é exatamente o alerta que se quer.
    /// </para>
    /// </summary>
    private static List<string> CodigosNoDocumento()
    {
        var documento = LocalizarDocumento();

        var codigos = System.Text.RegularExpressions.Regex
            .Matches(File.ReadAllText(documento), @"^\| `(PAR-[A-Z]{3}-\d{3})`",
                System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(correspondencia => correspondencia.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(codigo => codigo, StringComparer.Ordinal)
            .ToList();

        codigos.Should().NotBeEmpty(
            $"a tabela de parâmetros precisa ser legível em {documento}");

        return codigos;
    }

    private static string LocalizarDocumento()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (diretorio is not null)
        {
            var candidato = Path.Combine(diretorio.FullName, "docs", "04-parametros-do-sistema.md");

            if (File.Exists(candidato))
            {
                return candidato;
            }

            diretorio = diretorio.Parent;
        }

        throw new FileNotFoundException(
            "docs/04-parametros-do-sistema.md não encontrado subindo de " + AppContext.BaseDirectory);
    }

    private static void Adicionar(DbCommand comando, string nome, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}
