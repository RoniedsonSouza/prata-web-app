using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Prata.Infrastructure.Persistence;

#nullable disable

namespace Prata.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PrataDbContext))]
    [Migration("20260912210000_E5_AgendaContrato")]
    public partial class E5_AgendaContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_starts_at",
                table: "order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_ends_at",
                table: "order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "travel_buffer_minutes",
                table: "order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "custom_domain",
                table: "tenant",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "custom_domain_status",
                table: "tenant",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Nenhum");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "custom_domain_verified_at",
                table: "tenant",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "whats_app_cloud_enabled",
                table: "tenant",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "availability",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekday = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    starts_at_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    ends_at_time = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_availability", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blackout_date",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blackout_date", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    template_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pdf_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pdf_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    viewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaces_contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clause_codes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signature",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    signer_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    pdf_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signature", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_availability_tenant_id",
                table: "availability",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_availability_tenant_id_weekday",
                table: "availability",
                columns: new[] { "tenant_id", "weekday" });

            migrationBuilder.CreateIndex(
                name: "ix_blackout_date_tenant_id_date",
                table: "blackout_date",
                columns: new[] { "tenant_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contract_tenant_id",
                table: "contract",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_contract_tenant_id_order_id",
                table: "contract",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_signature_contract_id",
                table: "signature",
                column: "contract_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signature_tenant_id",
                table: "signature",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_custom_domain",
                table: "tenant",
                column: "custom_domain",
                unique: true,
                filter: "custom_domain IS NOT NULL");

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                  r record;
                BEGIN
                  FOR r IN
                    SELECT unnest(ARRAY[
                      'availability', 'blackout_date', 'contract', 'signature'
                    ]) AS table_name
                  LOOP
                    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', r.table_name);
                    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', r.table_name);
                    EXECUTE format('DROP POLICY IF EXISTS %I ON %I', r.table_name || '_tenant_isolation', r.table_name);
                    EXECUTE format(
                      'CREATE POLICY %I ON %I
                         USING (tenant_id = nullif(current_setting(''prata.tenant_id'', true), '''')::uuid)
                         WITH CHECK (tenant_id = nullif(current_setting(''prata.tenant_id'', true), '''')::uuid)',
                      r.table_name || '_tenant_isolation',
                      r.table_name
                    );
                  END LOOP;
                END $$;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "availability");
            migrationBuilder.DropTable(name: "blackout_date");
            migrationBuilder.DropTable(name: "signature");
            migrationBuilder.DropTable(name: "contract");

            migrationBuilder.DropIndex(name: "ix_tenant_custom_domain", table: "tenant");

            migrationBuilder.DropColumn(name: "scheduled_starts_at", table: "order");
            migrationBuilder.DropColumn(name: "scheduled_ends_at", table: "order");
            migrationBuilder.DropColumn(name: "travel_buffer_minutes", table: "order");
            migrationBuilder.DropColumn(name: "custom_domain", table: "tenant");
            migrationBuilder.DropColumn(name: "custom_domain_status", table: "tenant");
            migrationBuilder.DropColumn(name: "custom_domain_verified_at", table: "tenant");
            migrationBuilder.DropColumn(name: "whats_app_cloud_enabled", table: "tenant");
        }
    }
}
