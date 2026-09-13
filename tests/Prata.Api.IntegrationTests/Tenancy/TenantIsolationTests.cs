using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prata.Application.Abstractions;
using Prata.Domain.Briefing;
using Prata.Domain.Catalog;
using Prata.Domain.Common;
using Prata.Domain.Contracts;
using Prata.Domain.Delivery;
using Prata.Domain.Sales;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Persistence;

namespace Prata.Api.IntegrationTests.Tenancy;

/// <summary>
/// Isolamento cruzado com Postgres real e papel prata_app (RN-TEN-002).
/// O nome <c>TenantIsolationTests</c> e gate de CI (docs/15).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private string _appConnection = null!;

    public TenantIsolationTests(PostgresFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync() => await ApplySchemaAndRolesAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RN_TEN_002_tenant_nao_le_pacote_do_vizinho()
    {
        Guid tenantA;
        Guid tenantB;

        await using (var owner = CreateOwnerContext())
        {
            (tenantA, tenantB) = await SeedTenantsAndPackagesAsync(owner);
        }

        await using var contextA = CreateAppContext(tenantA);
        await SetTenantGucAsync(contextA, tenantA);

        var visible = await contextA.Packages.ToListAsync();

        visible.Should().ContainSingle();
        visible[0].TenantId.Should().Be(tenantA);
        visible.Should().NotContain(p => p.TenantId == tenantB);
    }

    [Fact]
    public async Task RN_TEN_001_toda_tabela_de_negocio_com_tenant_id_tem_policy_rls()
    {
        await using var conn = new NpgsqlConnection(_appConnection);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.relname AS table_name
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN information_schema.columns col
              ON col.table_schema = n.nspname AND col.table_name = c.relname
            WHERE n.nspname = 'public'
              AND c.relkind = 'r'
              AND col.column_name = 'tenant_id'
              AND NOT EXISTS (
                SELECT 1
                FROM pg_policies p
                WHERE p.schemaname = 'public' AND p.tablename = c.relname
              )
            ORDER BY 1
            """;

        var missing = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            missing.Add(reader.GetString(0));

        missing
            .Should()
            .BeEmpty(
                "toda tabela com tenant_id precisa de policy RLS (E1 §3.3). Sem policy: "
                    + string.Join(", ", missing)
            );
    }

    private async Task ApplySchemaAndRolesAsync()
    {
        var ownerCs = _fixture.OwnerConnectionString;

        await using (var conn = new NpgsqlConnection(ownerCs))
        {
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

        await using (var ownerCtx = CreateOwnerContext(ownerCs))
        {
            await ownerCtx.Database.EnsureDeletedAsync();
            await ownerCtx.Database.MigrateAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(ownerCs) { Username = "prata_app", Password = "prata_app_dev_only" };
        _appConnection = builder.ConnectionString;

        // Idempotente: reforça RLS caso migration antiga nao tenha policy.
        await ApplyInlineRlsAsync(ownerCs);

        await using (var conn = new NpgsqlConnection(ownerCs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                GRANT USAGE ON SCHEMA public TO prata_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO prata_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO prata_app;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO prata_app;
                """;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task ApplyInlineRlsAsync(string ownerCs)
    {
        await using var conn = new NpgsqlConnection(ownerCs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // Aplica ENABLE+FORCE+policy em TODA tabela publica que tenha tenant_id.
        cmd.CommandText = """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN
                SELECT c.relname AS table_name
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN information_schema.columns col
                  ON col.table_schema = n.nspname AND col.table_name = c.relname
                WHERE n.nspname = 'public'
                  AND c.relkind = 'r'
                  AND col.column_name = 'tenant_id'
              LOOP
                EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', r.table_name);
                EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', r.table_name);
                EXECUTE format('DROP POLICY IF EXISTS %I ON %I', r.table_name || '_tenant_isolation', r.table_name);
                EXECUTE format(
                  'CREATE POLICY %I ON %I USING (tenant_id = nullif(current_setting(''prata.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''prata.tenant_id'', true), '''')::uuid)',
                  r.table_name || '_tenant_isolation', r.table_name);
              END LOOP;
            END $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task RN_TEN_002_tenant_nao_le_cliente_do_vizinho()
    {
        Guid tenantA;
        Guid tenantB;

        await using (var owner = CreateOwnerContext())
        {
            (tenantA, tenantB) = await SeedTenantsAndClientsAsync(owner);
        }

        await using var contextA = CreateAppContext(tenantA);
        await SetTenantGucAsync(contextA, tenantA);

        var visible = await contextA.Clients.ToListAsync();

        visible.Should().ContainSingle();
        visible[0].TenantId.Should().Be(tenantA);
        visible.Should().NotContain(c => c.TenantId == tenantB);
    }

    [Fact]
    public async Task RN_TEN_002_tenant_nao_le_pedido_do_vizinho()
    {
        Guid tenantA;
        Guid tenantB;
        Guid orderB;

        await using (var owner = CreateOwnerContext())
        {
            (tenantA, tenantB, orderB) = await SeedTenantsAndOrdersAsync(owner);
        }

        await using var contextA = CreateAppContext(tenantA);
        await SetTenantGucAsync(contextA, tenantA);

        var visible = await contextA.Orders.ToListAsync();
        var vizinho = await contextA.Orders.FirstOrDefaultAsync(o => o.Id == orderB);

        visible.Should().ContainSingle();
        visible[0].TenantId.Should().Be(tenantA);
        vizinho.Should().BeNull();
    }

    [Fact]
    public async Task RN_TEN_002_tenant_nao_le_resposta_briefing_do_vizinho()
    {
        Guid tenantA;
        Guid tenantB;

        await using (var owner = CreateOwnerContext())
        {
            (tenantA, tenantB) = await SeedTenantsAndBriefingAnswersAsync(owner);
        }

        await using var contextA = CreateAppContext(tenantA);
        await SetTenantGucAsync(contextA, tenantA);

        var visible = await contextA.BriefingAnswers.ToListAsync();
        visible.Should().ContainSingle();
        visible[0].TenantId.Should().Be(tenantA);
        visible.Should().NotContain(a => a.TenantId == tenantB);
    }

    private static async Task<(Guid TenantA, Guid TenantB)> SeedTenantsAndPackagesAsync(PrataDbContext db)
    {
        var agora = DateTimeOffset.UtcNow;
        var a = Tenant.Create("A", "estudio-a", agora).Value;
        var b = Tenant.Create("B", "estudio-b", agora).Value;

        db.Tenants.Add(a);
        db.Tenants.Add(b);
        await db.SaveChangesAsync();

        var typeA = ServiceType.Create(a.Id, "studio", "Studio", 1);
        var typeB = ServiceType.Create(b.Id, "studio", "Studio", 1);
        db.ServiceTypes.AddRange(typeA, typeB);

        var pkgA = Package.Create(a.Id, typeA.Id, "Pacote A", Money.Brl(1000m), 50).Value;
        var pkgB = Package.Create(b.Id, typeB.Id, "Pacote B", Money.Brl(2000m), 80).Value;
        db.Packages.AddRange(pkgA, pkgB);
        await db.SaveChangesAsync();

        return (a.Id, b.Id);
    }

    private static async Task<(Guid TenantA, Guid TenantB)> SeedTenantsAndClientsAsync(PrataDbContext db)
    {
        var agora = DateTimeOffset.UtcNow;
        var a = Tenant.Create("A", "estudio-a-cli", agora).Value;
        var b = Tenant.Create("B", "estudio-b-cli", agora).Value;
        db.Tenants.AddRange(a, b);
        await db.SaveChangesAsync();

        db.Clients.Add(Client.Create(a.Id, "Ana", "ana@a.com", null, PreferredChannel.Email).Value);
        db.Clients.Add(Client.Create(b.Id, "Bruno", "bruno@b.com", null, PreferredChannel.WhatsApp).Value);
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    private static async Task<(Guid TenantA, Guid TenantB, Guid OrderB)> SeedTenantsAndOrdersAsync(
        PrataDbContext db
    )
    {
        var agora = DateTimeOffset.UtcNow;
        var a = Tenant.Create("A", "estudio-a-ord", agora).Value;
        var b = Tenant.Create("B", "estudio-b-ord", agora).Value;
        db.Tenants.AddRange(a, b);
        await db.SaveChangesAsync();

        var typeA = ServiceType.Create(a.Id, "studio", "Studio", 1);
        var typeB = ServiceType.Create(b.Id, "studio", "Studio", 1);
        db.ServiceTypes.AddRange(typeA, typeB);

        var clientA = Client.Create(a.Id, "Ana", "ana-ord@a.com", null, PreferredChannel.Email).Value;
        var clientB = Client.Create(b.Id, "Bruno", "bruno-ord@b.com", null, PreferredChannel.Email).Value;
        db.Clients.AddRange(clientA, clientB);

        var data = DateOnly.FromDateTime(agora.UtcDateTime.Date.AddDays(40));
        var orderA = Order.Create(a.Id, clientA.Id, typeA.Id, data, agora).Value;
        var orderB = Order.Create(b.Id, clientB.Id, typeB.Id, data, agora).Value;
        db.Orders.AddRange(orderA, orderB);
        await db.SaveChangesAsync();

        return (a.Id, b.Id, orderB.Id);
    }

    [Fact]
    public async Task RN_TEN_002_tenant_nao_le_galeria_do_vizinho()
    {
        Guid tenantA;
        Guid tenantB;
        Guid galleryB;

        await using (var owner = CreateOwnerContext())
        {
            (tenantA, tenantB, galleryB) = await SeedTenantsAndGalleriesAsync(owner);
        }

        await using var contextA = CreateAppContext(tenantA);
        await SetTenantGucAsync(contextA, tenantA);

        var visible = await contextA.Galleries.ToListAsync();
        var vizinho = await contextA.Galleries.FirstOrDefaultAsync(g => g.Id == galleryB);

        visible.Should().ContainSingle();
        visible[0].TenantId.Should().Be(tenantA);
        vizinho.Should().BeNull();
    }

    [Fact]
    public async Task RN_TEN_002_tenant_nao_le_contrato_do_vizinho()
    {
        Guid tenantA;
        Guid tenantB;
        Guid contractB;

        await using (var owner = CreateOwnerContext())
        {
            (tenantA, tenantB, contractB) = await SeedTenantsAndContractsAsync(owner);
        }

        await using var contextA = CreateAppContext(tenantA);
        await SetTenantGucAsync(contextA, tenantA);

        var visible = await contextA.Contracts.ToListAsync();
        var vizinho = await contextA.Contracts.FirstOrDefaultAsync(c => c.Id == contractB);

        visible.Should().ContainSingle();
        visible[0].TenantId.Should().Be(tenantA);
        vizinho.Should().BeNull();
    }

    private static async Task<(Guid TenantA, Guid TenantB, Guid ContractB)> SeedTenantsAndContractsAsync(
        PrataDbContext db
    )
    {
        var agora = DateTimeOffset.UtcNow;
        var a = Tenant.Create("A", "estudio-a-ctr", agora).Value;
        var b = Tenant.Create("B", "estudio-b-ctr", agora).Value;
        db.Tenants.AddRange(a, b);
        await db.SaveChangesAsync();

        var typeA = ServiceType.Create(a.Id, "studio", "Studio", 1);
        var typeB = ServiceType.Create(b.Id, "studio", "Studio", 1);
        db.ServiceTypes.AddRange(typeA, typeB);

        var clientA = Client.Create(a.Id, "Ana", "ana-ctr@a.com", null, PreferredChannel.Email).Value;
        var clientB = Client.Create(b.Id, "Bruno", "bruno-ctr@b.com", null, PreferredChannel.Email).Value;
        db.Clients.AddRange(clientA, clientB);

        var data = DateOnly.FromDateTime(agora.UtcDateTime.Date.AddDays(40));
        var orderA = Order.Create(a.Id, clientA.Id, typeA.Id, data, agora).Value;
        var orderB = Order.Create(b.Id, clientB.Id, typeB.Id, data, agora).Value;
        db.Orders.AddRange(orderA, orderB);
        await db.SaveChangesAsync();

        var clauses = ContractRequiredClauses.All.ToArray();
        var cA = Contract.Create(a.Id, orderA.Id, "v1", clauses, agora, 7).Value;
        var cB = Contract.Create(b.Id, orderB.Id, "v1", clauses, agora, 7).Value;
        db.Contracts.AddRange(cA, cB);
        await db.SaveChangesAsync();
        return (a.Id, b.Id, cB.Id);
    }

    private static async Task<(Guid TenantA, Guid TenantB)> SeedTenantsAndBriefingAnswersAsync(PrataDbContext db)
    {
        var agora = DateTimeOffset.UtcNow;
        var a = Tenant.Create("A", "estudio-a-brf", agora).Value;
        var b = Tenant.Create("B", "estudio-b-brf", agora).Value;
        db.Tenants.AddRange(a, b);
        await db.SaveChangesAsync();

        var typeA = ServiceType.Create(a.Id, "studio", "Studio", 1);
        var typeB = ServiceType.Create(b.Id, "studio", "Studio", 1);
        db.ServiceTypes.AddRange(typeA, typeB);

        var clientA = Client.Create(a.Id, "Ana", "ana-brf@a.com", null, PreferredChannel.Email).Value;
        var clientB = Client.Create(b.Id, "Bruno", "bruno-brf@b.com", null, PreferredChannel.Email).Value;
        db.Clients.AddRange(clientA, clientB);

        var data = DateOnly.FromDateTime(agora.UtcDateTime.Date.AddDays(40));
        var orderA = Order.Create(a.Id, clientA.Id, typeA.Id, data, agora).Value;
        var orderB = Order.Create(b.Id, clientB.Id, typeB.Id, data, agora).Value;
        db.Orders.AddRange(orderA, orderB);

        var qA = Question.Create(a.Id, Guid.NewGuid(), "A2", "Local", QuestionType.Text, BriefingBlock.A, 1, true, false).Value;
        var qB = Question.Create(b.Id, Guid.NewGuid(), "A2", "Local", QuestionType.Text, BriefingBlock.A, 1, true, false).Value;
        db.BriefingAnswers.Add(
            Answer.Create(a.Id, orderA.Id, qA, """{"text":"SP"}""", true, false).Value
        );
        db.BriefingAnswers.Add(
            Answer.Create(b.Id, orderB.Id, qB, """{"text":"RJ"}""", true, false).Value
        );
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    private static async Task<(Guid TenantA, Guid TenantB, Guid GalleryB)> SeedTenantsAndGalleriesAsync(
        PrataDbContext db
    )
    {
        var agora = DateTimeOffset.UtcNow;
        var a = Tenant.Create("A", "estudio-a-gal", agora).Value;
        var b = Tenant.Create("B", "estudio-b-gal", agora).Value;
        db.Tenants.AddRange(a, b);
        await db.SaveChangesAsync();

        var typeA = ServiceType.Create(a.Id, "studio", "Studio", 1);
        var typeB = ServiceType.Create(b.Id, "studio", "Studio", 1);
        db.ServiceTypes.AddRange(typeA, typeB);
        var clientA = Client.Create(a.Id, "Ana", "ana-gal@a.com", null, PreferredChannel.Email).Value;
        var clientB = Client.Create(b.Id, "Bruno", "bruno-gal@b.com", null, PreferredChannel.Email).Value;
        db.Clients.AddRange(clientA, clientB);
        var data = DateOnly.FromDateTime(agora.UtcDateTime.Date.AddDays(40));
        var orderA = Order.Create(a.Id, clientA.Id, typeA.Id, data, agora).Value;
        var orderB = Order.Create(b.Id, clientB.Id, typeB.Id, data, agora).Value;
        db.Orders.AddRange(orderA, orderB);

        var galA = Gallery
            .Create(a.Id, orderA.Id, 50, PortfolioConsent.Sim, false, agora.AddMonths(12), agora)
            .Value;
        var galB = Gallery
            .Create(b.Id, orderB.Id, 50, PortfolioConsent.Sim, false, agora.AddMonths(12), agora)
            .Value;
        db.Galleries.AddRange(galA, galB);
        await db.SaveChangesAsync();
        return (a.Id, b.Id, galB.Id);
    }

    private PrataDbContext CreateOwnerContext(string? cs = null)
    {
        var options = new DbContextOptionsBuilder<PrataDbContext>()
            .UseNpgsql(cs ?? _fixture.OwnerConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        var tenant = new MutableTenantContext();
        return new PrataDbContext(options, tenant);
    }

    private PrataDbContext CreateAppContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PrataDbContext>().UseNpgsql(_appConnection).UseSnakeCaseNamingConvention().Options;
        var tenant = new MutableTenantContext();
        tenant.Set(tenantId, "slug", TenantStatus.Ativo);
        return new PrataDbContext(options, tenant);
    }

    private static async Task SetTenantGucAsync(PrataDbContext db, Guid tenantId)
    {
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT set_config('prata.tenant_id', {0}, false)", tenantId.ToString());
    }
}
