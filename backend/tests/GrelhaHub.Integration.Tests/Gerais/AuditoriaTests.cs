using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Application.Features.Gerais.Auditoria;
using GrelhaHub.Domain.Gerais;
using GrelhaHub.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Integration.Tests.Gerais;

/// <summary>
/// <c>SPEC-GER-005</c> — CA-1 a CA-8: a trilha de auditoria.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class AuditoriaTests(AmbienteDeIntegracao ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact(DisplayName = "RN-GER-001 — a criação registra autor e instante sem o handler atribuir (CA-1)")]
    public async Task CA1_CriacaoRegistraAutorEInstante()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // Uma escrita autenticada: alterar parâmetro cria uma ParametroValor nova.
        (await gestao.PutAsJsonAsync(
            new Uri("/api/v1/parametros/PAR-VEN-002", UriKind.Relative),
            new
            {
                NovoValor = "12.00",
                VigenciaInicio = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                Justificativa = "Reajuste para conferir a trilha",
            }))
            .EnsureSuccessStatusCode();

        await using var db = Contexto(restaurante);

        var novo = await db.ValoresDeParametro
            .AsNoTracking()
            .Where(v => v.Parametro!.Codigo == "PAR-VEN-002" && v.Valor == "12.00")
            .SingleAsync();

        novo.CriadoEm.Should().NotBe(default);
        novo.CriadoPorUsuarioId.Should().NotBeNull(
            "o interceptor preenche o autor — nenhum handler o atribui");

        // E o pedido do cardápio público NÃO tem autor: quem o criou foi o cliente, que não é
        // usuário do sistema (RN-VEN-038). É a nulidade que a §6 original proibia e a operação
        // exige.
        var produtoId = await CriarProdutoAsync(restaurante, "Anonimo", 30.00m);
        await CriarPedidoAnonimoAsync(restaurante.Slug, produtoId);

        await using var conferencia = Contexto(restaurante);

        var pedido = await conferencia.Pedidos.AsNoTracking().SingleAsync();

        pedido.CriadoPorUsuarioId.Should().BeNull("pedido público não tem usuário responsável");
        pedido.CriadoEm.Should().NotBe(default);
    }

    [Fact(DisplayName = "RN-GER-001 — autor e instante de criação são imutáveis (CA-2)")]
    public async Task CA2_CamposDeCriacaoSaoImutaveis()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Imutavel", 30.00m);

        var pedidoId = await CriarPedidoAnonimoAsync(restaurante.Slug, produtoId);

        DateTimeOffset criadoOriginal;

        await using (var db = Contexto(restaurante))
        {
            var pedido = await db.Pedidos.SingleAsync(p => p.Id == pedidoId);
            criadoOriginal = pedido.CriadoEm;

            // Tenta mexer nos campos de criação por baixo do domínio — é assim que a alteração
            // acidental acontece de verdade: um `Update` de objeto inteiro.
            db.Entry(pedido).Property(nameof(Domain.Vendas.Pedido.CriadoEm)).CurrentValue =
                criadoOriginal.AddYears(-1);
            db.Entry(pedido).Property("CriadoPorUsuarioId").CurrentValue = Guid.CreateVersion7();

            pedido.Cancelar("Cancelamento para forcar um UPDATE");

            await db.SaveChangesAsync();
        }

        await using var conferencia = Contexto(restaurante);

        var depois = await conferencia.Pedidos.AsNoTracking().SingleAsync(p => p.Id == pedidoId);

        depois.CriadoEm.Should().BeCloseTo(criadoOriginal, TimeSpan.FromMilliseconds(1),
            "o interceptor restaura o valor original");
        depois.CriadoPorUsuarioId.Should().BeNull("continua sem autor, como nasceu");
        depois.Status.Should().Be(Domain.Vendas.StatusPedido.Cancelado,
            "o resto da operação passou: só os campos de criação foram protegidos");
    }

    [Fact(DisplayName = "RN-GER-011 — alteração sensível registra só o que mudou, antes e depois (CA-3)")]
    public async Task CA3_AlteracaoRegistraAntesEDepois()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Cancelavel", 45.00m);
        var pedidoId = await CriarPedidoAnonimoAsync(restaurante.Slug, produtoId);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/cancelar", UriKind.Relative),
            new { Motivo = "Cliente desistiu na porta" }))
            .EnsureSuccessStatusCode();

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var registro = await db.Trilha
            .AsNoTracking()
            .Where(l => l.Entidade == "pedido" && l.RegistroId == pedidoId)
            .OrderByDescending(l => l.OcorridoEm)
            .FirstAsync();

        // O pedido guarda o motivo, e ele entra na trilha.
        registro.Operacao.Should().Be(OperacaoDeAuditoria.Cancelamento,
            "quem classifica é o Pedido, não o interceptor");
        registro.Justificativa.Should().Be("Cliente desistiu na porta");
        registro.UsuarioId.Should().NotBeNull("a operação foi autenticada");
        registro.UnidadeId.Should().Be(restaurante.UnidadeId);

        // APENAS as propriedades que mudaram — auditar 40 colunas para registrar a alteração de
        // uma torna a trilha ilegível, e a trilha existe para ser lida.
        var antes = Chaves(registro.ValoresAnteriores);
        var depois = Chaves(registro.ValoresNovos);

        depois.Should().Contain("Status").And.Contain("MotivoCancelamento");
        depois.Should().NotContain("Numero", "o número não mudou e não entra na trilha");
        depois.Should().NotContain("Canal");

        antes.Should().BeEquivalentTo(depois, "antes e depois cobrem as mesmas propriedades");

        Valor(registro.ValoresAnteriores, "Status").Should().Be("Aberto");
        Valor(registro.ValoresNovos, "Status").Should().Be("Cancelado");
    }

    [Fact(DisplayName = "RN-GER-011 — a trilha grava na MESMA transação da operação (CA-4)")]
    public async Task CA4_AuditoriaEhAtomicaComAOperacao()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Atomico", 41.00m);
        var pedidoId = await CriarPedidoAnonimoAsync(restaurante.Slug, produtoId);

        await using var db = Contexto(restaurante);

        var pedido = await db.Pedidos.SingleAsync(p => p.Id == pedidoId);
        pedido.Cancelar("Cancelamento que vai falhar por causa da trilha");

        // Força a auditoria a falhar: uma linha de trilha inválida na mesma transação. O `CHECK`
        // ck_log_operacao recusa operação fora do domínio, e é o banco que derruba tudo.
        db.Trilha.Add(new LogAuditoria(
            restaurante.UnidadeId, "pedido", pedidoId,
            (OperacaoDeAuditoria)99, null, "{}", null, null, DateTimeOffset.UtcNow));

        var gravar = async () => await db.SaveChangesAsync();

        await gravar.Should().ThrowAsync<DbUpdateException>();

        // NADA foi gravado — nem a operação, nem a auditoria.
        await using var conferencia = Contexto(restaurante);

        (await conferencia.Pedidos.AsNoTracking().SingleAsync(p => p.Id == pedidoId))
            .Status.Should().Be(Domain.Vendas.StatusPedido.Aberto,
                "o cancelamento foi desfeito junto com a trilha que falhou");

        (await conferencia.Trilha.AsNoTracking()
            .CountAsync(l => l.RegistroId == pedidoId && l.Operacao == OperacaoDeAuditoria.Cancelamento))
            .Should().Be(0);
    }

    [Fact(DisplayName = "RN-GER-002 — exclusão física é recusada pelo interceptor (CA-5)")]
    public async Task CA5_ExclusaoFisicaEhRecusada()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Perene", 33.00m);
        var pedidoId = await CriarPedidoAnonimoAsync(restaurante.Slug, produtoId);

        await using var db = Contexto(restaurante);

        // SEM `Include`: com os itens carregados, o change tracker do EF derruba o próprio
        // `Remove()` (`HandleConceptualNulls`, porque `ItemPedido.PedidoId` é obrigatório e o
        // delete é `Restrict`) — e o teste passaria medindo a guarda do EF, não a nossa. É o
        // caminho SEM dependente carregado que chega ao interceptor, e é justamente o que
        // `ON DELETE RESTRICT` não cobre.
        var pedido = await db.Pedidos.SingleAsync(p => p.Id == pedidoId);

        db.Pedidos.Remove(pedido);

        var gravar = async () => await db.SaveChangesAsync();

        // Falha ruidosa em desenvolvimento vale mais do que descobrir a exclusão pela ausência do
        // dado em produção. Complementa o ON DELETE RESTRICT: o banco impede a exclusão que tem
        // dependente, e isto impede também a que não tem.
        (await gravar.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*RN-GER-002*");

        await using var conferencia = Contexto(restaurante);

        (await conferencia.Pedidos.AsNoTracking().CountAsync(p => p.Id == pedidoId))
            .Should().Be(1, "o registro permanece no banco");
    }

    [Fact(DisplayName = "SPEC-GER-005 — o hash de senha nunca aparece na trilha (CA-6)")]
    public async Task CA6_SenhaNuncaNaTrilha()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        Guid perfilId;
        await using (var db = Contexto(restaurante))
        {
            perfilId = await db.Perfis.Where(p => p.Nome == "Cozinha").Select(p => p.Id).SingleAsync();
        }

        (await gestao.PostAsJsonAsync(
            new Uri("/api/v1/usuarios", UriKind.Relative),
            new
            {
                Nome = "Auditado",
                Sobrenome = "Da Trilha",
                Cpf = CpfDeTeste.Gerar(4242),
                Email = "auditado@trilha.teste",
                PerfilId = perfilId,
                SenhaInicial = "senha-que-nao-pode-vazar-1",
            }))
            .EnsureSuccessStatusCode();

        await using var db2 = Ambiente.CriarContexto(Guid.Empty);

        var registros = await db2.Trilha.AsNoTracking()
            .Where(l => l.Entidade == "usuario")
            .ToListAsync();

        registros.Should().NotBeEmpty("a criação de usuário é auditada");

        foreach (var registro in registros)
        {
            var corpo = (registro.ValoresAnteriores ?? "") + (registro.ValoresNovos ?? "");

            corpo.Should().NotContainEquivalentOf("senha-que-nao-pode-vazar",
                "a senha em claro nunca chega nem perto da trilha");

            // O hash é substituído por "***": um hash antigo na trilha é um hash a mais em
            // circulação, e a trilha é lida pela gerência.
            if (Chaves(registro.ValoresNovos).Contains("SenhaHash"))
            {
                Valor(registro.ValoresNovos, "SenhaHash").Should().Be("***");
            }
        }
    }

    [Fact(DisplayName = "SPEC-GER-005 — a trilha é imutável: alteração é recusada (CA-7)")]
    public async Task CA7_TrilhaEhImutavel()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Registro", 37.00m);
        var pedidoId = await CriarPedidoAnonimoAsync(restaurante.Slug, produtoId);

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        (await gestao.PostAsJsonAsync(
            new Uri($"/api/v1/pedidos/{pedidoId}/cancelar", UriKind.Relative),
            new { Motivo = "Cancelamento a ser auditado" }))
            .EnsureSuccessStatusCode();

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var registro = await db.Trilha.FirstAsync(l => l.RegistroId == pedidoId);

        // Alterar: recusado por ProtegerATrilha.
        db.Entry(registro).Property(nameof(LogAuditoria.Justificativa)).CurrentValue = "reescrito";

        var alterar = async () => await db.SaveChangesAsync();

        (await alterar.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*imutável*");

        db.Entry(registro).State = EntityState.Unchanged;

        // Excluir: recusado por RecusarExclusaoFisica.
        db.Trilha.Remove(registro);

        var excluir = async () => await db.SaveChangesAsync();

        (await excluir.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*RN-GER-002*");
    }

    [Fact(DisplayName = "SPEC-GER-005 — o BANCO recusa UPDATE e DELETE na trilha, não só a aplicação (CA-7)")]
    public async Task CA7_BancoRecusaAlteracaoDaTrilha()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");
        var produtoId = await CriarProdutoAsync(restaurante, "Blindado", 39.00m);
        var pedidoId = await CriarPedidoAnonimoAsync(restaurante.Slug, produtoId);

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        (await db.Trilha.AsNoTracking().CountAsync(l => l.RegistroId == pedidoId))
            .Should().BeGreaterThan(0, "o pedido gerou trilha");

        // SQL cru: é o caminho que o interceptor NÃO vê — psql na mão, script de correção, um ORM
        // futuro. ADR-0009 prefere TRG a APP justamente aqui.
        var alterar = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE log_auditoria SET justificativa = 'reescrito por fora'");

        (await alterar.Should().ThrowAsync<Npgsql.PostgresException>())
            .WithMessage("*log_auditoria e imutavel*");

        var excluir = async () => await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM log_auditoria");

        (await excluir.Should().ThrowAsync<Npgsql.PostgresException>())
            .WithMessage("*log_auditoria e imutavel*");

        // TRUNCATE não dispara gatilho FOR EACH ROW — e é o comando que apagaria tudo de uma vez.
        var truncar = async () => await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE log_auditoria");

        (await truncar.Should().ThrowAsync<Npgsql.PostgresException>())
            .WithMessage("*log_auditoria e imutavel*");

        await using var conferencia = Ambiente.CriarContexto(Guid.Empty);

        (await conferencia.Trilha.AsNoTracking().CountAsync(l => l.RegistroId == pedidoId))
            .Should().BeGreaterThan(0, "nada saiu da trilha");
    }

    [Fact(DisplayName = "RN-GER-012 — atendente não consulta a trilha (CA-8)")]
    public async Task CA8_ConsultaRestritaAGerencia()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        // O administrador consulta.
        var doAdmin = await gestao.GetFromJsonAsync<TrilhaResponse>(
            new Uri("/api/v1/auditoria", UriKind.Relative));

        doAdmin.Should().NotBeNull();
        doAdmin!.Registros.Should().NotBeEmpty("a carga do restaurante já gerou trilha");
        doAdmin.Tamanho.Should().BeLessThanOrEqualTo(50, "RNF-002 — teto de página");

        // O atendente, não: a trilha guarda valores anteriores de campos sensíveis, e abri-la a
        // ele contornaria RN-GER-012 por outra porta.
        var atendente = await CriarAtendenteAsync(restaurante);

        using var comoAtendente = Ambiente.CriarCliente(atendente);

        var recusado = await comoAtendente.GetAsync(new Uri("/api/v1/auditoria", UriKind.Relative));

        recusado.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await recusado.Content.ReadAsStringAsync()).Should().Contain("RN-GER-010");
    }

    [Fact(DisplayName = "SPEC-GER-003 CA-6 — a alteração de parâmetro fica na trilha com a justificativa")]
    public async Task AlteracaoDeParametroEntraNaTrilha()
    {
        var restaurante = await CadastrarRestauranteAsync("Trilha Burger", "11111111000191");

        using var gestao = Ambiente.CriarCliente(restaurante.Token);

        (await gestao.PutAsJsonAsync(
            new Uri("/api/v1/parametros/PAR-VEN-002", UriKind.Relative),
            new
            {
                NovoValor = "12.00",
                VigenciaInicio = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                Justificativa = "Reajuste combinado com a equipe de salao",
            }))
            .EnsureSuccessStatusCode();

        await using var db = Ambiente.CriarContexto(Guid.Empty);

        var registros = await db.Trilha.AsNoTracking()
            .Where(l => l.Entidade == "parametro_valor")
            .OrderByDescending(l => l.OcorridoEm)
            .ToListAsync();

        // Duas linhas: a vigência antiga FECHADA (Alteracao) e a nova ABERTA (Criacao).
        registros.Should().HaveCountGreaterThanOrEqualTo(2);

        var criacao = registros.First(l => l.Operacao == OperacaoDeAuditoria.Criacao);
        criacao.Justificativa.Should().Be("Reajuste combinado com a equipe de salao");
        criacao.UsuarioId.Should().NotBeNull("é o CA-6 da SPEC-GER-003: quem alterou fica registrado");

        var fechamento = registros.First(l => l.Operacao == OperacaoDeAuditoria.Alteracao);
        Chaves(fechamento.ValoresNovos).Should().Contain("VigenciaFim");
    }

    private static HashSet<string> Chaves(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var documento = JsonDocument.Parse(json);

        return documento.RootElement.EnumerateObject()
            .Select(propriedade => propriedade.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? Valor(string? json, string chave)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var documento = JsonDocument.Parse(json);

        return documento.RootElement.TryGetProperty(chave, out var propriedade)
            ? propriedade.ToString()
            : null;
    }

    private async Task<Guid> CriarPedidoAnonimoAsync(string slug, Guid produtoId)
    {
        using var anonimo = Ambiente.CriarCliente();

        var criado = await anonimo.PostAsJsonAsync(
            new Uri($"/api/publico/{slug}/pedidos", UriKind.Relative),
            new
            {
                NomeCliente = "Cliente Anonimo",
                Telefone = "11988887777",
                Observacao = (string?)null,
                Itens = new[] { new { ProdutoId = produtoId, Quantidade = 1, Observacao = (string?)null } },
            });

        criado.EnsureSuccessStatusCode();

        return (await criado.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();
    }

    private async Task<string> CriarAtendenteAsync(RestauranteDeTeste restaurante)
    {
        const string senha = "senha-de-atendente-123";

        using var escopo = Ambiente.CriarEscopo();
        var hash = escopo.ServiceProvider.GetRequiredService<IHashDeSenha>();

        await using (var db = Contexto(restaurante))
        {
            var perfilId = await db.Perfis
                .Where(p => p.Nome == "Atendente").Select(p => p.Id).SingleAsync();

            db.Usuarios.Add(new Usuario(
                restaurante.UnidadeId, "Atendente da Trilha", "atendente@trilha.teste",
                hash.Gerar(senha), perfilId, deveTrocarSenha: false));

            await db.SaveChangesAsync();
        }

        return await AutenticarAsync("atendente@trilha.teste", senha);
    }
}
