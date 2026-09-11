using Microsoft.Extensions.Logging;
using Prata.Application.Abstractions;
using Prata.Application.Common;
using Prata.Domain.Common;

namespace Prata.Application.Behaviors;

public sealed class TenantGuardBehavior<TRequest, TResponse>(ITenantContext tenantContext) : IPipelineBehavior<TRequest, TResponse>
{
    public Task<Result<TResponse>> Handle(
        TRequest request,
        Func<CancellationToken, Task<Result<TResponse>>> next,
        CancellationToken cancellationToken
    )
    {
        if (!tenantContext.IsResolved)
        {
            return Task.FromResult(Result.Failure<TResponse>(new Error("TENANT_NAO_RESOLVIDO", "Tenant nao foi resolvido no request.")));
        }

        return next(cancellationToken);
    }
}

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<FluentValidation.IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        Func<CancellationToken, Task<Result<TResponse>>> next,
        CancellationToken cancellationToken
    )
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new FluentValidation.ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
            return await next(cancellationToken);

        var message = string.Join("; ", failures.Select(f => f.ErrorMessage));
        return Result.Failure<TResponse>(new Error("VALIDACAO_FALHOU", message));
    }
}

public sealed class RequestLoggingBehavior<TRequest, TResponse>(
    ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger,
    ITenantContext tenantContext
) : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        Func<CancellationToken, Task<Result<TResponse>>> next,
        CancellationToken cancellationToken
    )
    {
        var name = typeof(TRequest).Name;
        // Nunca logar payload: briefing sensivel e dado de cartao (RN-BRF-031, RN-FIN-002).
        logger.LogInformation("Handling {Request} tenant={TenantId}", name, tenantContext.TenantId);

        var result = await next(cancellationToken);

        logger.LogInformation("Handled {Request} success={Success} code={Code}", name, result.IsSuccess, result.Error?.Code);

        return result;
    }
}
