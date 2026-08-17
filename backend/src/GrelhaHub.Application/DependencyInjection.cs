using System.Reflection;
using FluentValidation;
using GrelhaHub.Application.Common.Behaviors;
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

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        // A ordem importa: valida antes de qualquer coisa tocar o banco.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
