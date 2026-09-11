using Prata.Application.Abstractions;
using Prata.Application.Common;
using Prata.Domain.Common;

namespace Prata.Application.Billing;

public sealed record CreateDepositPaymentCommand(Guid OrderId) : ICommand<Guid>;

public sealed class CreateDepositPaymentHandler(IDepositPaymentService deposits, ITenantContext tenant)
    : ICommandHandler<CreateDepositPaymentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDepositPaymentCommand command, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        return await deposits.CreateForApprovedOrderAsync(tenantId, command.OrderId, cancellationToken);
    }
}

public sealed record ConfirmOrderCommand(Guid OrderId, bool ContratoAssinado) : ICommand<Unit>;

public sealed class ConfirmOrderHandler(IOrderConfirmationService confirmations, ITenantContext tenant)
    : ICommandHandler<ConfirmOrderCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ConfirmOrderCommand command, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        return await confirmations.ConfirmAsync(tenantId, command.OrderId, command.ContratoAssinado, cancellationToken);
    }
}

/// <summary>RN-FIN-012 — cria Payment de sinal apos aprovacao.</summary>
public interface IDepositPaymentService
{
    Task<Result<Guid>> CreateForApprovedOrderAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken = default);
}

public interface IOrderConfirmationService
{
    Task<Result<Unit>> ConfirmAsync(Guid tenantId, Guid orderId, bool contratoAssinado, CancellationToken cancellationToken = default);
}

public interface IWebhookPaymentProcessor
{
    Task ProcessAsync(Guid paymentEventId, CancellationToken cancellationToken = default);
}
