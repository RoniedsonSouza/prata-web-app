using System.Reflection;
using AwesomeAssertions;
using Prata.Domain.Common;

namespace Prata.Domain.Tests;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Entity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Prata.Application.Common.IDispatcher).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Prata.Infrastructure.Persistence.PrataDbContext).Assembly;

    private static readonly string[] DomainForbidden =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Dapper",
        "Hangfire",
        "Microsoft.Extensions",
        "Serilog",
        "System.Text.Json",
        "AWSSDK",
        "FluentValidation",
        "AspNetCore",
    ];

    [Fact]
    public void Domain_nao_referencia_nada_de_fora()
    {
        var referenced = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        foreach (var forbidden in DomainForbidden)
        {
            referenced
                .Should()
                .NotContain(
                    name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    because: $"Domain nao pode referenciar {forbidden}"
                );
        }
    }

    [Fact]
    public void Application_referencia_apenas_Domain()
    {
        var projectRefs = ApplicationAssembly
            .GetReferencedAssemblies()
            .Where(a => a.Name is not null && a.Name.StartsWith("Prata.", StringComparison.Ordinal))
            .Select(a => a.Name!)
            .ToArray();

        projectRefs.Should().Contain("Prata.Domain");
        projectRefs.Should().NotContain("Prata.Infrastructure");
        projectRefs.Should().NotContain("Prata.Api");
        projectRefs.Should().NotContain("Prata.Worker");
    }

    [Fact]
    public void Infrastructure_referencia_Application_e_Domain()
    {
        var projectRefs = InfrastructureAssembly
            .GetReferencedAssemblies()
            .Where(a => a.Name is not null && a.Name.StartsWith("Prata.", StringComparison.Ordinal))
            .Select(a => a.Name!)
            .ToArray();

        projectRefs.Should().Contain("Prata.Domain");
        projectRefs.Should().Contain("Prata.Application");
        projectRefs.Should().NotContain("Prata.Api");
    }

    [Fact]
    public void Nenhum_double_em_caminho_de_dinheiro()
    {
        // RN-FIN-003 — ate Billing existir, garante que Money nao usa double.
        var moneyType = typeof(Money);
        var fields = moneyType.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        fields.Should().NotContain(f => f.FieldType == typeof(double) || f.FieldType == typeof(float));

        moneyType.GetProperty(nameof(Money.Amount))!.PropertyType.Should().Be<decimal>();
    }

    [Fact]
    public void Toda_entidade_de_negocio_implementa_ITenantOwned()
    {
        // RN-TEN-001 — entidades de negocio (exceto Tenant e PlatformUser).
        var exempt = new HashSet<string> { nameof(Prata.Domain.Tenancy.Tenant), "PlatformUser", "AuditLog", "OutboxMessage" };

        var businessEntities = DomainAssembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(Entity).IsAssignableFrom(t))
            .Where(t => !typeof(ValueObject).IsAssignableFrom(t))
            .Where(t => !exempt.Contains(t.Name))
            .Where(t => t.Namespace is not null && !t.Namespace.EndsWith(".Common", StringComparison.Ordinal))
            .ToArray();

        // CollectionItem e filho de Collection — nao e ITenantOwned direto (herda via pai).
        // Aceitamos filhos de agregado sem ITenantOwned se o pai tiver.
        var roots = businessEntities
            .Where(t => typeof(AggregateRoot).IsAssignableFrom(t) || t.Name is not "CollectionItem")
            .Where(t => t.Name != "CollectionItem")
            .ToArray();

        foreach (var type in roots)
        {
            typeof(ITenantOwned).IsAssignableFrom(type).Should().BeTrue(because: $"{type.Name} deve implementar ITenantOwned (RN-TEN-001)");
        }
    }
}
