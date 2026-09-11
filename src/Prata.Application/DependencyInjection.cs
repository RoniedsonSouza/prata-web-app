using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Prata.Application.Briefing;
using Prata.Application.Common;
using Prata.Application.Sales;
using Prata.Domain.Common;

namespace Prata.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPrataApplication(this IServiceCollection services)
    {
        services.AddPrataDispatcher();
        services.AddScoped<IBriefingCompletenessChecker, BriefingCompletenessChecker>();
        RegisterHandlers(services, typeof(CreateOrderHandler).Assembly);
        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;

                var def = iface.GetGenericTypeDefinition();
                if (def == typeof(ICommandHandler<,>) || def == typeof(IQueryHandler<,>))
                    services.AddScoped(iface, type);
            }
        }
    }
}
