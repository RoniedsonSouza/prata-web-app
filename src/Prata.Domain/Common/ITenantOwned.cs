namespace Prata.Domain.Common;

/// <summary>
/// Marca entidade de negocio com tenant. Toda tabela de negocio implementa
/// isto (RN-TEN-001).
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
