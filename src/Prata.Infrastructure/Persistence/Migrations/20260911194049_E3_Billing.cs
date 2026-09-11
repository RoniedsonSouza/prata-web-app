using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prata.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class E3_Billing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    travel_buffer_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    split_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    split_fixed_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    split_platform_fee_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    platform_fee_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    external_charge_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    process_error = table.Column<string>(type: "text", nullable: true),
                    discarded_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payout",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_payout_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payout", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payout_account",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_recipient_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kyc_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    holder_document_masked = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    pix_key_masked = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    bank_masked = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    kyc_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payout_account", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_issue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expected_json = table.Column<string>(type: "jsonb", nullable: false),
                    found_json = table.Column<string>(type: "jsonb", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reconciliation_issue", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "split_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    fixed_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    vigente_de = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vigente_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_split_rule", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "installment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    external_installment_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installment", x => x.id);
                    table.ForeignKey(
                        name: "fk_installment_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_tenant_id",
                table: "booking",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_tenant_id_order_id",
                table: "booking",
                columns: new[] { "tenant_id", "order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_installment_payment_id_sequence",
                table: "installment",
                columns: new[] { "payment_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_installment_tenant_id",
                table: "installment",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_tenant_id",
                table: "payment",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_tenant_id_order_id",
                table: "payment",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_tenant_id_status",
                table: "payment",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_event_external_event_id",
                table: "payment_event",
                column: "external_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_event_tenant_id",
                table: "payment_event",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payout_tenant_id",
                table: "payout",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payout_tenant_id_status",
                table: "payout",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payout_account_tenant_id",
                table: "payout_account",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_issue_tenant_id",
                table: "reconciliation_issue",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_issue_tenant_id_resolved_at",
                table: "reconciliation_issue",
                columns: new[] { "tenant_id", "resolved_at" });

            migrationBuilder.CreateIndex(
                name: "ix_split_rule_tenant_id",
                table: "split_rule",
                column: "tenant_id");

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                  r record;
                BEGIN
                  FOR r IN
                    SELECT unnest(ARRAY[
                      'payment', 'installment', 'split_rule', 'payout_account',
                      'payout', 'payment_event', 'reconciliation_issue', 'booking'
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

            // Booking minimo E3: EXCLUDE impede sobreposicao (docs/11 §5 / RN-AGD-001).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION prata_booking_span(
                  p_starts timestamptz,
                  p_ends timestamptz,
                  p_buffer_minutes integer
                ) RETURNS tstzrange
                LANGUAGE sql
                IMMUTABLE
                STRICT
                AS $fn$
                  SELECT tstzrange(
                    p_starts - (p_buffer_minutes * INTERVAL '1 minute'),
                    p_ends   + (p_buffer_minutes * INTERVAL '1 minute')
                  );
                $fn$;

                ALTER TABLE booking ADD CONSTRAINT ex_booking_no_overlap
                  EXCLUDE USING gist (
                    tenant_id WITH =,
                    prata_booking_span(starts_at, ends_at, travel_buffer_minutes) WITH &&
                  ) WHERE (status = 'Ativo');
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking");

            migrationBuilder.DropTable(
                name: "installment");

            migrationBuilder.DropTable(
                name: "payment_event");

            migrationBuilder.DropTable(
                name: "payout");

            migrationBuilder.DropTable(
                name: "payout_account");

            migrationBuilder.DropTable(
                name: "reconciliation_issue");

            migrationBuilder.DropTable(
                name: "split_rule");

            migrationBuilder.DropTable(
                name: "payment");
        }
    }
}
