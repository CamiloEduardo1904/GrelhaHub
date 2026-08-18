using System.Reflection;
using FluentValidation;
using GrelhaHub.Application.Common.Behaviors;
using GrelhaHub.Application.Common.Modulos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GrelhaHub.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra o pipeline CQRS da camada Application — ADR-0002.
    /// </summary>
    public static IServiceCollection AdicionarApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // RN-GER-017 / ADR-0014 — recusa a partida se alguma operação não declarar o seu
        // módulo. Vem ANTES de registrar qualquer coisa: se a declaração falta, não há por que
        // subir um pipeline que deixaria a operação passar sem gate.
        ValidadorDeDeclaracaoDeModulo.Validar(assembly);

        // RN-GER-024 — e recusa a partida se algum perfil do catálogo não declarar o que pode
        // delegar. Sem isto, a lista fixa de delegação escolhida pelo time falharia em silêncio
        // a cada perfil novo (SPEC-GER-007 §4).
        ValidadorDeDelegacao.Validar();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        // RN-GER-033 / ADR-0016 — a emissão de link é usada por três handlers (esqueci a senha,
        // criar usuário de restaurante, criar usuário de plataforma). Serviço e não handler, para
        // que a invalidação dos tokens anteriores exista em um lugar só.
        services.AddScoped<Features.Gerais.Acesso.EmissorDeLinkDeAcesso>();

        // A ordem importa e é esta:
        //
        // 1. MÓDULO — o restaurante contratou isto? Não contratou, não existe (RN-GER-017).
        //    Vem primeiro porque é a pergunta mais externa: não faz sentido perguntar se o
        //    usuário pode fazer algo que o restaurante não comprou.
        // 2. PERMISSÃO — o usuário pode? (RN-GER-010). Depois do módulo e antes da validação:
        //    não faz sentido validar o corpo de uma operação que o usuário não pode executar, e
        //    validar primeiro entregaria a ele quais campos existem.
        // 3. VALIDAÇÃO — o corpo faz sentido? Antes de qualquer coisa tocar o banco.
        // 2. SENHA PENDENTE — RF-SEG-005. Vem antes da permissao porque nao interessa o que o
        //    usuario pode fazer enquanto a senha ainda e a que outra pessoa escolheu.
        // 3. SESSAO DE SUPORTE — RN-GER-015. Escrita de plataforma exige motivo registrado.
        // 4. PERMISSAO — RN-GER-010.
        // 5. VALIDACAO — o corpo faz sentido? Antes de qualquer coisa tocar o banco.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ModuloBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TrocaDeSenhaBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissaoBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
