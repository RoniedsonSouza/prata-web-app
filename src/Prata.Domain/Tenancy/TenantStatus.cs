namespace Prata.Domain.Tenancy;

/// <summary>
/// Estado operacional do tenant. Membros em portugues (ADR-0009).
/// </summary>
public enum TenantStatus
{
    Rascunho = 0,
    Ativo = 1,
    Suspenso = 2,
}
