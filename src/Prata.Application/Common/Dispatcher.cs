using Microsoft.Extensions.DependencyInjection;
using Prata.Domain.Common;

namespace Prata.Application.Common;

/// <summary>
/// Dispatcher proprio (~40 linhas). Substitui MediatR (ADR-0001).
/// </summary>
public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResponse));
        var handler =
            serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"Handler nao registrado para {commandType.Name}.");

        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResponse>, TResponse>.Handle))!;
        return (Task<Result<TResponse>>)method.Invoke(handler, [command, cancellationToken])!;
    }

    public Task<Result<TResponse>> Ask<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResponse));
        var handler =
            serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"Handler nao registrado para {queryType.Name}.");

        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResponse>, TResponse>.Handle))!;
        return (Task<Result<TResponse>>)method.Invoke(handler, [query, cancellationToken])!;
    }
}

public static class DispatcherServiceCollectionExtensions
{
    public static IServiceCollection AddPrataDispatcher(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        return services;
    }
}
