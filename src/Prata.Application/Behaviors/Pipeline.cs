using Microsoft.Extensions.DependencyInjection;
using Prata.Application.Common;
using Prata.Domain.Common;

namespace Prata.Application.Behaviors;

/// <summary>
/// Pipeline behavior. Ordem de registro: Logging → TenantGuard → Idempotency →
/// Validation → Transaction (docs/02 §5.2).
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<Result<TResponse>> Handle(
        TRequest request,
        Func<CancellationToken, Task<Result<TResponse>>> next,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Decorator que envolve o handler com a cadeia de behaviors.
/// </summary>
public sealed class PipelineDispatcher(IServiceProvider serviceProvider, IDispatcher inner) : IDispatcher
{
    public async Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        var behaviors =
            serviceProvider.GetService(typeof(IEnumerable<IPipelineBehavior<ICommand<TResponse>, TResponse>>))
            as IEnumerable<IPipelineBehavior<ICommand<TResponse>, TResponse>>;

        Func<CancellationToken, Task<Result<TResponse>>> next = ct => inner.Send(command, ct);

        if (behaviors is not null)
        {
            foreach (var behavior in behaviors.Reverse())
            {
                var current = next;
                var captured = behavior;
                next = ct => captured.Handle(command, current, ct);
            }
        }

        return await next(cancellationToken);
    }

    public Task<Result<TResponse>> Ask<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        inner.Ask(query, cancellationToken);
}
