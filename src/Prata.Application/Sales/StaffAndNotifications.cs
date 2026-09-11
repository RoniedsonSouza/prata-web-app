using Prata.Application.Abstractions;
using Prata.Application.Common;
using Prata.Domain.Common;
using Prata.Domain.Sales;

namespace Prata.Application.Sales;

public sealed record DesignateSensitiveStaffCommand(Guid OrderId, Guid UserId) : ICommand<Unit>;

public sealed class DesignateSensitiveStaffHandler(
    ITenantContext tenantContext,
    IOrderRepository orders,
    IUnitOfWork uow
) : ICommandHandler<DesignateSensitiveStaffCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DesignateSensitiveStaffCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.DesignarStaffSensivel(command.UserId);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RemoveSensitiveStaffCommand(Guid OrderId, Guid UserId) : ICommand<Unit>;

public sealed class RemoveSensitiveStaffHandler(
    ITenantContext tenantContext,
    IOrderRepository orders,
    IUnitOfWork uow
) : ICommandHandler<RemoveSensitiveStaffCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RemoveSensitiveStaffCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.RemoverStaffSensivel(command.UserId);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Dispara e-mail transacional apos commit de negocio (RN-NOT-003).</summary>
public static class CommercialNotification
{
    public static async Task TrySendAsync(
        INotifier notifier,
        IClientRepository clients,
        Guid tenantId,
        Guid clientId,
        string type,
        string idempotencyKey,
        string subject,
        string body,
        CancellationToken cancellationToken
    )
    {
        var client = await clients.GetByIdAsync(tenantId, clientId, cancellationToken);
        if (client is null || string.IsNullOrWhiteSpace(client.Email))
            return;

        await notifier.SendTransactionalEmailAsync(
            tenantId,
            client.Email,
            type,
            idempotencyKey,
            subject,
            body,
            cancellationToken
        );
    }
}
