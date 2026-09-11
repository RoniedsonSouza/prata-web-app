using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Prata.Application.Abstractions;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Persistence;

/// <summary>
/// Factory usada pelo `dotnet ef` sem subir a API completa.
/// </summary>
public sealed class PrataDbContextFactory : IDesignTimeDbContextFactory<PrataDbContext>
{
    public PrataDbContext CreateDbContext(string[] args)
    {
        var connection =
            Environment.GetEnvironmentVariable("ConnectionStrings__PrataOwner")
            ?? "Host=localhost;Port=5432;Database=prata;Username=prata_owner;Password=prata_dev_only";

        var options = new DbContextOptionsBuilder<PrataDbContext>()
            .UseNpgsql(connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PrataDbContext(options, new MutableTenantContext());
    }
}
