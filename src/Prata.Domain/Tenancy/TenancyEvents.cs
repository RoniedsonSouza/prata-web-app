using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

public sealed record TenantCriado(Guid TenantId, string Slug, string Name) : DomainEvent;

public sealed record TenantSuspenso(Guid TenantId, string Reason) : DomainEvent;

public sealed record TenantReativado(Guid TenantId) : DomainEvent;

public sealed record TemaAlterado(Guid TenantId, ThemeId ThemeId) : DomainEvent;

public sealed record PortfolioPublicado(Guid TenantId, string Slug) : DomainEvent;
