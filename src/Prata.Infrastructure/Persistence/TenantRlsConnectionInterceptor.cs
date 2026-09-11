using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prata.Application.Abstractions;

namespace Prata.Infrastructure.Persistence;

/// <summary>
/// Define <c>prata.tenant_id</c> na conexao aberta para as policies RLS (RN-TEN-001).
/// </summary>
public sealed class TenantRlsConnectionInterceptor(ITenantContext tenantContext)
    : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        await ApplyAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        base.ConnectionOpened(connection, eventData);
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (tenantContext.IsResolved && tenantContext.TenantId is Guid tenantId)
        {
            command.CommandText = "SELECT set_config('prata.tenant_id', @tid, false)";
            var p = command.CreateParameter();
            p.ParameterName = "tid";
            p.Value = tenantId.ToString();
            command.Parameters.Add(p);
        }
        else
        {
            // Limpa GUC em conexoes do pool reutilizadas fora de tenant.
            command.CommandText = "SELECT set_config('prata.tenant_id', '', false)";
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
