using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prata.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class E2_NotificationsAndStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sensitive_staff_user_ids",
                table: "order",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "notification_message",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_message", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_message_tenant_id",
                table: "notification_message",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_message_tenant_id_recipient_type_idempotency_k",
                table: "notification_message",
                columns: new[] { "tenant_id", "recipient", "type", "idempotency_key" },
                unique: true);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                  r record;
                BEGIN
                  FOR r IN
                    SELECT unnest(ARRAY['notification_message']) AS table_name
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
                name: "notification_message");

            migrationBuilder.DropColumn(
                name: "sensitive_staff_user_ids",
                table: "order");
        }
    }
}
