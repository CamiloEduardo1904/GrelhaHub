using System.Reflection;
using System.Text.Json;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrelhaHub.Infrastructure.Persistence.Interceptors;

/// <summary>
/// <c>SPEC-GER-005</c> — a trilha de auditoria, e as três garantias que o interceptor impõe:
/// <c>RN-GER-002</c> (nada é excluído fisicamente), <c>RN-GER-001</c> (autor e instante
/// automáticos) e <c>RN-GER-011</c> (operação sensível registra antes e depois).
/// <para>
/// <b>Roda dentro da mesma transação da operação.</b> As linhas de trilha entram no
/// <c>ChangeTracker</c> antes do <c>SaveChanges</c>, então ou tudo grava ou nada grava — auditoria
/// que grava depois, fora da transação, perde o registro justamente quando a operação foi anômala.
/// </para>
/// <para>
/// <b>Interceptor e não chamada em cada handler:</b> o que depende de o desenvolvedor lembrar é o
/// que falta justamente quando importa — e aqui o esquecimento produz uma operação sensível sem
/// rastro, descoberta só quando alguém procura por ela e não acha.
/// </para>
/// </summary>
public sealed class InterceptorDeAuditoria(IContextoDeExecucao contexto, IRelogio relogio)
    : SaveChangesInterceptor
{
    private const string Ocultado = "***";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Auditar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Auditar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Auditar(DbContext? db)
    {
        if (db is null)
        {
            return;
        }

        db.ChangeTracker.DetectChanges();

        RecusarExclusaoFisica(db);
        ProtegerATrilha(db);

        var agora = relogio.Agora;
        var usuarioId = contexto.UsuarioId;

        PreencherAutoria(db, agora, usuarioId);

        // Materializa antes de acrescentar linhas: mexer no ChangeTracker enquanto se itera sobre
        // ele lança InvalidOperationException.
        var auditaveis = db.ChangeTracker.Entries()
            .Where(entrada => entrada.Entity is IAuditavelDetalhado)
            .Where(entrada => entrada.State is EntityState.Added or EntityState.Modified)
            .ToList();

        foreach (var entrada in auditaveis)
        {
            if (Montar(entrada, agora, usuarioId) is { } registro)
            {
                // ⚠⚠ VAI PARA A OUTBOX, e não para a trilha — ADR-0017.
                //
                // A trilha mudou de banco. O que NÃO pode mudar é a propriedade que a torna
                // confiável: ou a operação e o registro gravam juntos, ou nenhum grava. Bancos
                // diferentes não têm transação em comum, então a linha é gravada AQUI, dentro
                // desta mesma transação, e um worker a copia para o banco de log depois.
                //
                // ⚠ Escrever direto no banco de log seria mais simples e teria um custo que não
                // se aceita numa trilha: a operação grava, a escrita do log falha, e ninguém
                // fica sabendo.
                db.Add(new OutboxDeAuditoria(registro));
            }
        }
    }

    /// <summary>
    /// <c>RN-GER-002</c> — CA-5. Falha ruidosa em desenvolvimento vale mais do que descobrir a
    /// exclusão pela ausência do dado em produção.
    /// <para>
    /// Complementa o <c>ON DELETE RESTRICT</c> do banco (<c>SPEC-GER-001</c>): o banco impede a
    /// exclusão que tem dependente, e isto impede também a que não tem.
    /// </para>
    /// </summary>
    private static void RecusarExclusaoFisica(DbContext db)
    {
        var exclusoes = db.ChangeTracker.Entries()
            .Where(entrada => entrada.State == EntityState.Deleted)
            .Where(entrada => entrada.Entity is Entidade)
            .Select(entrada => entrada.Entity.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (exclusoes.Count > 0)
        {
            throw new InvalidOperationException(
                $"RN-GER-002 — registro transacional não é excluído fisicamente: " +
                $"{string.Join(", ", exclusoes)}. " +
                "Use cancelamento, inativação, exclusão lógica ou movimento contrário.");
        }
    }

    /// <summary>
    /// CA-7 — a trilha é imutável. Só <c>INSERT</c>: qualquer <c>Modified</c> é recusado.
    /// <para>
    /// <c>Deleted</c> já cai em <see cref="RecusarExclusaoFisica"/>; esta checagem cobre a
    /// alteração, que passaria por lá.
    /// </para>
    /// </summary>
    private static void ProtegerATrilha(DbContext db)
    {
        var alterada = db.ChangeTracker.Entries<LogAuditoria>()
            .Any(entrada => entrada.State == EntityState.Modified);

        if (alterada)
        {
            throw new InvalidOperationException(
                "RN-GER-011 — a trilha de auditoria é imutável: admite apenas inserção. " +
                "Uma trilha alterável não é trilha.");
        }
    }

    /// <summary>
    /// <c>RN-GER-001</c> — CA-1 e CA-2: preenche autor e instante na criação, e <b>bloqueia</b> a
    /// alteração deles depois.
    /// </summary>
    private static void PreencherAutoria(DbContext db, DateTimeOffset agora, Guid? usuarioId)
    {
        foreach (var entrada in db.ChangeTracker.Entries<IAuditavel>())
        {
            if (entrada.State == EntityState.Added)
            {
                entrada.Property(nameof(IAuditavel.CriadoEm)).CurrentValue = agora;

                // Nulo em operação anônima — pedido do cardápio público (RN-VEN-038) é criado por
                // um cliente que não é usuário do sistema.
                entrada.Property(nameof(IAuditavel.CriadoPorUsuarioId)).CurrentValue = usuarioId;

                continue;
            }

            if (entrada.State != EntityState.Modified)
            {
                continue;
            }

            // CA-2 — quem criou e quando não mudam. Restaurar em vez de lançar: a alteração aqui é
            // quase sempre acidental (um `Update` de objeto inteiro), e falhar a operação por isso
            // seria desproporcional. O que não pode é o valor MUDAR.
            foreach (var nome in new[] { nameof(IAuditavel.CriadoEm), nameof(IAuditavel.CriadoPorUsuarioId) })
            {
                var propriedade = entrada.Property(nome);

                if (propriedade.IsModified)
                {
                    propriedade.CurrentValue = propriedade.OriginalValue;
                    propriedade.IsModified = false;
                }
            }
        }
    }

    private static LogAuditoria? Montar(EntityEntry entrada, DateTimeOffset agora, Guid? usuarioId)
    {
        var entidade = (IAuditavelDetalhado)entrada.Entity;
        var novo = entrada.State == EntityState.Added;

        var alteradas = entrada.Properties
            .Where(propriedade => novo
                ? propriedade.CurrentValue is not null
                : propriedade.IsModified)
            .Where(propriedade => !propriedade.Metadata.IsPrimaryKey())
            .ToList();

        if (alteradas.Count == 0)
        {
            // Nada mudou de fato — um `SaveChanges` sobre entidade rastreada sem alteração real.
            // Gravar aqui encheria a trilha de linhas vazias.
            return null;
        }

        var nomes = alteradas
            .Select(propriedade => propriedade.Metadata.Name)
            .ToHashSet(StringComparer.Ordinal);

        var tipoClr = entrada.Entity.GetType();

        var anteriores = novo ? null : Serializar(alteradas, tipoClr, atual: false);
        var novos = Serializar(alteradas, tipoClr, atual: true);

        return new LogAuditoria(
            unidadeId: UnidadeDe(entrada.Entity),
            entidade: entrada.Metadata.GetTableName() ?? tipoClr.Name,
            registroId: entrada.Entity is Entidade comId ? comId.Id : Guid.Empty,
            operacao: entidade.ClassificarAuditoria(
                novo ? TipoDeMudanca.Insercao : TipoDeMudanca.Atualizacao, nomes),
            valoresAnteriores: anteriores,
            valoresNovos: novos,
            justificativa: entidade.JustificativaDaAuditoria,
            usuarioId: usuarioId,
            ocorridoEm: agora);
    }

    /// <summary>
    /// A unidade do registro auditado. Nula para quem não pertence a unidade — o usuário de
    /// plataforma (<c>RN-GER-014</c>) e os catálogos globais.
    /// </summary>
    private static Guid? UnidadeDe(object entidade) => entidade switch
    {
        IPertenceAUnidade daUnidade => daUnidade.UnidadeId,

        // `unidade` não pertence a unidade: ELA é a unidade.
        Unidade unidade => unidade.Id,

        // `usuario` tem unidade nullable desde RN-GER-014.
        Usuario usuario => usuario.UnidadeId,

        _ => null,
    };

    /// <summary>
    /// Serializa só as propriedades que mudaram, ocultando as marcadas com
    /// <see cref="NaoAuditarAttribute"/> — CA-6.
    /// </summary>
    private static string Serializar(
        IEnumerable<PropertyEntry> propriedades, Type tipoClr, bool atual)
    {
        var valores = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var propriedade in propriedades)
        {
            var nome = propriedade.Metadata.Name;

            valores[nome] = Oculta(tipoClr, nome)
                ? Ocultado
                : Legivel(atual ? propriedade.CurrentValue : propriedade.OriginalValue);
        }

        return JsonSerializer.Serialize(valores, OpcoesDeJson);
    }

    private static bool Oculta(Type tipoClr, string propriedade) =>
        tipoClr.GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)
            ?.GetCustomAttribute<NaoAuditarAttribute>() is not null;

    /// <summary>
    /// Converte para algo legível no <c>jsonb</c>. Value object serializado cru sairia como
    /// <c>{"valor": 32.00}</c>, e a trilha é para ser lida por gente.
    /// </summary>
    private static object? Legivel(object? valor) => valor switch
    {
        null => null,
        Dinheiro dinheiro => dinheiro.Valor,
        Quantidade quantidade => quantidade.Valor,
        Enum enumeracao => enumeracao.ToString(),
        _ => valor,
    };

    private static readonly JsonSerializerOptions OpcoesDeJson = new()
    {
        // A trilha é lida por gente, e acento é parte do português. Sem isto, "Observação" sai
        // como "Observação" no jsonb.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
