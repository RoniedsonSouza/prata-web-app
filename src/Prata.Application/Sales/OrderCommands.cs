using Prata.Application.Abstractions;
using Prata.Application.Billing;
using Prata.Application.Common;
using Prata.Domain.Catalog;
using Prata.Domain.Common;
using Prata.Domain.Notifications;
using Prata.Domain.Sales;

namespace Prata.Application.Sales;

public sealed record CreateOrderCommand(
    string ClientName,
    string ClientEmail,
    string? ClientWhatsApp,
    PreferredChannel PreferredChannel,
    Guid ServiceTypeId,
    DateOnly IntendedDate,
    Guid? PackageId
) : ICommand<Guid>;

public sealed class CreateOrderHandler(
    ITenantContext tenantContext,
    IDateTimeProvider clock,
    IClientRepository clients,
    IOrderRepository orders,
    IUnitOfWork uow,
    ICatalogReader catalog
) : ICommandHandler<CreateOrderCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var serviceType = await catalog.GetServiceTypeAsync(tenantId, command.ServiceTypeId, cancellationToken);
        if (serviceType is null)
            return Error.Validation("SERVICO_NAO_ENCONTRADO", "Tipo de servico nao encontrado.");

        var existing = await clients.GetByEmailAsync(tenantId, command.ClientEmail, cancellationToken);
        Client client;
        if (existing is null)
        {
            var created = Client.Create(
                tenantId,
                command.ClientName,
                command.ClientEmail,
                command.ClientWhatsApp,
                command.PreferredChannel
            );
            if (created.IsFailure)
                return Result.Failure<Guid>(created.Error!.Value);

            client = created.Value;
            await clients.AddAsync(client, cancellationToken);
        }
        else
        {
            client = existing;
        }

        var orderResult = Order.Create(tenantId, client.Id, command.ServiceTypeId, command.IntendedDate, clock.UtcNow);
        if (orderResult.IsFailure)
            return Result.Failure<Guid>(orderResult.Error!.Value);

        var order = orderResult.Value;

        if (command.PackageId is Guid packageId)
        {
            var package = await catalog.GetPackageAsync(tenantId, packageId, cancellationToken);
            if (package is null || package.Status != PackageStatus.Publicado || package.Price is null)
                return Error.Validation("PACOTE_INDISPONIVEL", "Pacote nao disponivel para pedido.");

            var add = order.AdicionarItem(OrderItemKind.Package, package.Id, package.Name, package.Price.Value, quantity: 1);
            if (add.IsFailure)
                return Result.Failure<Guid>(add.Error!.Value);
        }

        await orders.AddAsync(order, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return order.Id;
    }
}

public sealed record SubmitOrderCommand(Guid OrderId) : ICommand<Unit>;

public sealed class SubmitOrderHandler(
    ITenantContext tenantContext,
    IOrderRepository orders,
    IClientRepository clients,
    INotifier notifier,
    IUnitOfWork uow,
    IBriefingCompletenessChecker briefing
) : ICommandHandler<SubmitOrderCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var pendencias = await briefing.GetRequiredPendingAsync(tenantId, order.Id, cancellationToken);
        var result = order.Enviar(pendencias);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);

        // Apos commit — falha de e-mail nao desfaz o pedido (RN-NOT-003).
        await CommercialNotification.TrySendAsync(
            notifier,
            clients,
            tenantId,
            order.ClientId,
            NotificationTypes.PedidoRecebido,
            order.Id.ToString("N"),
            "Recebemos o seu pedido",
            "Seu pedido foi enviado ao estudio. Em breve voce recebe o orcamento.",
            cancellationToken
        );

        return Unit.Value;
    }
}

public sealed record AnalyzeOrderCommand(Guid OrderId) : ICommand<Unit>;

public sealed class AnalyzeOrderHandler(ITenantContext tenantContext, IOrderRepository orders, IUnitOfWork uow)
    : ICommandHandler<AnalyzeOrderCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AnalyzeOrderCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.Analisar();
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RefuseOrderCommand(Guid OrderId, string Motivo) : ICommand<Unit>;

public sealed class RefuseOrderHandler(ITenantContext tenantContext, IOrderRepository orders, IUnitOfWork uow)
    : ICommandHandler<RefuseOrderCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RefuseOrderCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.Recusar(command.Motivo);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record SendQuoteCommand(Guid OrderId, int? ValidadeDias) : ICommand<Unit>;

public sealed class SendQuoteHandler(
    ITenantContext tenantContext,
    IDateTimeProvider clock,
    IOrderRepository orders,
    IClientRepository clients,
    INotifier notifier,
    ITenantReader tenants,
    IUnitOfWork uow
) : ICommandHandler<SendQuoteCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SendQuoteCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var validity = command.ValidadeDias ?? await tenants.GetQuoteValidityDaysAsync(tenantId, cancellationToken) ?? 7;

        var result = order.EnviarOrcamento(clock.UtcNow, validity);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);

        var quote = order.CurrentQuote!;
        await CommercialNotification.TrySendAsync(
            notifier,
            clients,
            tenantId,
            order.ClientId,
            NotificationTypes.OrcamentoEnviado,
            $"{order.Id:N}:v{quote.Version}",
            "Seu orcamento esta pronto",
            $"Orcamento v{quote.Version} valido ate {quote.ValidoAte:dd/MM/yyyy}. Acesse o portal para aprovar.",
            cancellationToken
        );

        return Unit.Value;
    }
}

public sealed record ApproveQuoteCommand(Guid OrderId) : ICommand<Unit>;

public sealed class ApproveQuoteHandler(
    ITenantContext tenantContext,
    IDateTimeProvider clock,
    IOrderRepository orders,
    IClientRepository clients,
    INotifier notifier,
    IDispatcher dispatcher,
    IUnitOfWork uow
) : ICommandHandler<ApproveQuoteCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ApproveQuoteCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.Aprovar(clock.UtcNow);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);

        // RN-FIN-012 — cria cobranca de sinal com split.
        var deposit = await dispatcher.Send(new CreateDepositPaymentCommand(order.Id), cancellationToken);
        if (deposit.IsFailure)
            return Result.Failure<Unit>(deposit.Error!.Value);

        await CommercialNotification.TrySendAsync(
            notifier,
            clients,
            tenantId,
            order.ClientId,
            NotificationTypes.OrcamentoAprovado,
            order.Id.ToString("N"),
            "Orcamento aprovado",
            "Recebemos a aprovacao. O sinal foi gerado — acesse o portal para pagar.",
            cancellationToken
        );

        return Unit.Value;
    }
}

public sealed record PutOrderOnHoldCommand(Guid OrderId, string Motivo) : ICommand<Unit>;

public sealed class PutOrderOnHoldHandler(ITenantContext tenantContext, IOrderRepository orders, IUnitOfWork uow)
    : ICommandHandler<PutOrderOnHoldCommand, Unit>
{
    public async Task<Result<Unit>> Handle(PutOrderOnHoldCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.ColocarEmEspera(command.Motivo);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ResumeOrderCommand(Guid OrderId) : ICommand<Unit>;

public sealed class ResumeOrderHandler(ITenantContext tenantContext, IOrderRepository orders, IUnitOfWork uow)
    : ICommandHandler<ResumeOrderCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ResumeOrderCommand command, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not Guid tenantId)
            return Error.Validation("TENANT_AUSENTE", "Tenant nao resolvido.");

        var order = await orders.GetByIdAsync(tenantId, command.OrderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var result = order.Retomar();
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Leitura de catalogo usada pelos handlers comerciais.</summary>
public interface ICatalogReader
{
    Task<ServiceType?> GetServiceTypeAsync(Guid tenantId, Guid serviceTypeId, CancellationToken cancellationToken);

    Task<Package?> GetPackageAsync(Guid tenantId, Guid packageId, CancellationToken cancellationToken);

    Task<Addon?> GetAddonAsync(Guid tenantId, Guid addonId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Package>> ListPublishedPackagesAsync(Guid tenantId, Guid serviceTypeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Addon>> ListActiveAddonsAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface ITenantReader
{
    Task<int?> GetQuoteValidityDaysAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Ate o modulo de Briefing existir, retorna lista vazia (sem pendencias).
/// </summary>
public interface IBriefingCompletenessChecker
{
    Task<IReadOnlyList<string>> GetRequiredPendingAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken);
}
