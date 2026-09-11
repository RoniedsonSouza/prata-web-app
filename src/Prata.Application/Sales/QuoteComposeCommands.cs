using Prata.Application.Abstractions;
using Prata.Application.Common;
using Prata.Domain.Catalog;
using Prata.Domain.Common;
using Prata.Domain.Sales;

namespace Prata.Application.Sales;

public sealed record AddOrderPackageCommand(Guid OrderId, Guid PackageId) : ICommand<Unit>;

public sealed class AddOrderPackageHandler(
    ITenantContext tenantContext,
    IOrderRepository orders,
    ICatalogReader catalog,
    IUnitOfWork uow
) : ICommandHandler<AddOrderPackageCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AddOrderPackageCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var package = await catalog.GetPackageAsync(tenantId, command.PackageId, cancellationToken);
        if (package is null || package.Status != PackageStatus.Publicado || package.Price is null)
            return Error.Validation("PACOTE_INDISPONIVEL", "Pacote nao disponivel.");

        if (package.ServiceTypeId != order.ServiceTypeId)
            return Error.Validation("PACOTE_SERVICO_DIVERGENTE", "Pacote nao pertence ao tipo de servico do pedido.");

        var add = order.AdicionarItem(
            OrderItemKind.Package,
            package.Id,
            package.Name,
            package.Price.Value,
            quantity: 1
        );
        if (add.IsFailure)
            return add;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record AddOrderAddonCommand(Guid OrderId, Guid AddonId, int Quantity) : ICommand<Unit>;

public sealed class AddOrderAddonHandler(
    ITenantContext tenantContext,
    IOrderRepository orders,
    ICatalogReader catalog,
    IUnitOfWork uow
) : ICommandHandler<AddOrderAddonCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AddOrderAddonCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var addon = await catalog.GetAddonAsync(tenantId, command.AddonId, cancellationToken);
        if (addon is null || !addon.IsActive)
            return Error.Validation("ADICIONAL_INDISPONIVEL", "Adicional nao disponivel.");

        var qty = command.Quantity <= 0 ? 1 : command.Quantity;
        var add = order.AdicionarItem(OrderItemKind.Addon, addon.Id, addon.Name, addon.Price, qty);
        if (add.IsFailure)
            return add;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RemoveOrderItemCommand(Guid OrderId, Guid ItemId) : ICommand<Unit>;

public sealed class RemoveOrderItemHandler(ITenantContext tenantContext, IOrderRepository orders, IUnitOfWork uow)
    : ICommandHandler<RemoveOrderItemCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RemoveOrderItemCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.RemoverItem(command.ItemId);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ApplyOrderDiscountCommand(
    Guid OrderId,
    string Kind,
    decimal? FixedAmount,
    decimal? Percent
) : ICommand<Unit>;

public sealed class ApplyOrderDiscountHandler(ITenantContext tenantContext, IOrderRepository orders, IUnitOfWork uow)
    : ICommandHandler<ApplyOrderDiscountCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ApplyOrderDiscountCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        Result<Discount> discount;
        if (string.Equals(command.Kind, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Percent is not decimal p)
                return SalesErrors.DescontoInvalido;
            discount = Discount.TryPercent(p);
        }
        else
        {
            if (command.FixedAmount is not decimal amount || amount <= 0)
                return SalesErrors.DescontoInvalido;
            discount = Discount.TryFixed(Money.Brl(amount));
        }

        if (discount.IsFailure)
            return Result.Failure<Unit>(discount.Error!.Value);

        var applied = order.AplicarDesconto(discount.Value);
        if (applied.IsFailure)
            return applied;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ClearOrderDiscountCommand(Guid OrderId) : ICommand<Unit>;

public sealed class ClearOrderDiscountHandler(ITenantContext tenantContext, IOrderRepository orders, IUnitOfWork uow)
    : ICommandHandler<ClearOrderDiscountCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ClearOrderDiscountCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.RemoverDesconto();
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
