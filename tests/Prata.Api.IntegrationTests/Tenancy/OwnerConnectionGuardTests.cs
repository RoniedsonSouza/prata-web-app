using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Prata.Infrastructure;

namespace Prata.Api.IntegrationTests.Tenancy;

public class OwnerConnectionGuardTests
{
    [Fact]
    public void RN_TEN_012_recusa_connection_string_com_prata_owner()
    {
        var config = BuildConfig(
            owner: "Host=localhost;Database=prata;Username=prata_owner;Password=x",
            app: "Host=localhost;Database=prata;Username=prata_owner;Password=x"
        );

        var act = () =>
            DependencyInjection.EnsureNotOwnerRole(config.GetConnectionString("PrataApp")!, config);

        act.Should().Throw<InvalidOperationException>().WithMessage("*RN-TEN-012*");
    }

    [Fact]
    public void RN_TEN_012_aceita_prata_app()
    {
        var config = BuildConfig(
            owner: "Host=localhost;Database=prata;Username=prata_owner;Password=x",
            app: "Host=localhost;Database=prata;Username=prata_app;Password=y"
        );

        var act = () =>
            DependencyInjection.EnsureNotOwnerRole(config.GetConnectionString("PrataApp")!, config);

        act.Should().NotThrow();
    }

    private static IConfiguration BuildConfig(string owner, string app)
    {
        var config = new ConfigurationManager();
        config["ConnectionStrings:PrataOwner"] = owner;
        config["ConnectionStrings:PrataApp"] = app;
        return config;
    }
}
