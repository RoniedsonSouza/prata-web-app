using Testcontainers.PostgreSql;
using Npgsql;

namespace Prata.Api.IntegrationTests;

/// <summary>
/// Fixture compartilhada: Postgres real para RLS / isolamento.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("prata")
            .WithUsername("prata_owner")
            .WithPassword("prata_dev_only")
            .Build();

    public string OwnerConnectionString => Container.GetConnectionString();

    public string AppConnectionString
    {
        get
        {
            var builder = new NpgsqlConnectionStringBuilder(OwnerConnectionString)
            {
                Username = "prata_app",
                Password = "prata_app_dev_only",
            };
            return builder.ConnectionString;
        }
    }

    public async ValueTask InitializeAsync()
    {
        await Container.StartAsync();
        await EnsureAppRoleAsync();
    }

    public async ValueTask DisposeAsync() => await Container.DisposeAsync();

    public async Task EnsureAppRoleAsync()
    {
        await using var conn = new NpgsqlConnection(OwnerConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
              IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'prata_app') THEN
                CREATE ROLE prata_app LOGIN PASSWORD 'prata_app_dev_only' NOSUPERUSER NOCREATEDB NOCREATEROLE;
              END IF;
            END $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task GrantAppRoleAsync()
    {
        await EnsureAppRoleAsync();
        await using var conn = new NpgsqlConnection(OwnerConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            GRANT USAGE ON SCHEMA public TO prata_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO prata_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO prata_app;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public
              GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO prata_app;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
