using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prata.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class E2_Sales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    whats_app = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    preferred_channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intended_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status_antes_da_espera = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    hold_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    refusal_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    discount_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    discount_fixed_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    subtotal_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_item_order_order_id",
                        column: x => x.order_id,
                        principalTable: "order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    emitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valido_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_order_order_id",
                        column: x => x.order_id,
                        principalTable: "order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_client_tenant_id",
                table: "client",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_tenant_id_email",
                table: "client",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_tenant_id",
                table: "order",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_tenant_id_intended_date",
                table: "order",
                columns: new[] { "tenant_id", "intended_date" });

            migrationBuilder.CreateIndex(
                name: "ix_order_tenant_id_status",
                table: "order",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_order_item_order_id",
                table: "order_item",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_tenant_id",
                table: "order_item",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_tenant_id_order_id",
                table: "order_item",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_order_id",
                table: "quote",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_tenant_id",
                table: "quote",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_tenant_id_order_id_version",
                table: "quote",
                columns: new[] { "tenant_id", "order_id", "version" },
                unique: true);

            // ENABLE + FORCE RLS + policy nas tabelas Sales (RN-TEN-001).
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                  r record;
                BEGIN
                  FOR r IN
                    SELECT unnest(ARRAY['client', 'order', 'order_item', 'quote']) AS table_name
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
            migrationBuilder.DropTable(
                name: "client");

            migrationBuilder.DropTable(
                name: "order_item");

            migrationBuilder.DropTable(
                name: "quote");

            migrationBuilder.DropTable(
                name: "order");
        }
    }
}
