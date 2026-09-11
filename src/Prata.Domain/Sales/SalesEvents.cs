using Prata.Domain.Common;

namespace Prata.Domain.Sales;

public sealed record PedidoCriado(Guid TenantId, Guid OrderId, Guid ClientId) : DomainEvent;

public sealed record PedidoEnviado(Guid TenantId, Guid OrderId) : DomainEvent;

public sealed record PedidoEmAnalise(Guid TenantId, Guid OrderId) : DomainEvent;

public sealed record PedidoRecusado(Guid TenantId, Guid OrderId, string Motivo) : DomainEvent;

public sealed record OrcamentoEnviadoEvent(Guid TenantId, Guid OrderId, int Version, DateTimeOffset ValidoAte)
    : DomainEvent;

public sealed record OrcamentoRevisado(Guid TenantId, Guid OrderId, int Version, DateTimeOffset ValidoAte)
    : DomainEvent;

public sealed record OrcamentoAprovado(Guid TenantId, Guid OrderId) : DomainEvent;

public sealed record PedidoExpirado(Guid TenantId, Guid OrderId, string Motivo) : DomainEvent;

public sealed record PedidoEmEspera(Guid TenantId, Guid OrderId, string Motivo, OrderStatus Origem)
    : DomainEvent;

public sealed record PedidoRetomado(Guid TenantId, Guid OrderId, OrderStatus Destino) : DomainEvent;

public sealed record PedidoCancelado(Guid TenantId, Guid OrderId, string Motivo, bool PeloCliente)
    : DomainEvent;

public sealed record PedidoConfirmado(Guid TenantId, Guid OrderId) : DomainEvent;
