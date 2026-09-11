using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prata.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class E2_Briefing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "briefing_answer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    prompt_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    type_snapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_briefing_answer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "briefing_consent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    consented_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    purpose_text_snapshot = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_briefing_consent", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "briefing_template",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_briefing_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "briefing_question",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    block = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    visible_when = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    options_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_briefing_question", x => x.id);
                    table.ForeignKey(
                        name: "fk_briefing_question_briefing_template_template_id",
                        column: x => x.template_id,
                        principalTable: "briefing_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_briefing_answer_tenant_id",
                table: "briefing_answer",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_briefing_answer_tenant_id_is_sensitive",
                table: "briefing_answer",
                columns: new[] { "tenant_id", "is_sensitive" });

            migrationBuilder.CreateIndex(
                name: "ix_briefing_answer_tenant_id_order_id_question_code",
                table: "briefing_answer",
                columns: new[] { "tenant_id", "order_id", "question_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_briefing_consent_tenant_id",
                table: "briefing_consent",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_briefing_consent_tenant_id_order_id_scope",
                table: "briefing_consent",
                columns: new[] { "tenant_id", "order_id", "scope" });

            migrationBuilder.CreateIndex(
                name: "ix_briefing_question_template_id",
                table: "briefing_question",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_briefing_question_tenant_id",
                table: "briefing_question",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_briefing_question_tenant_id_template_id_code",
                table: "briefing_question",
                columns: new[] { "tenant_id", "template_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_briefing_template_tenant_id",
                table: "briefing_template",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_briefing_template_tenant_id_service_type_id_version",
                table: "briefing_template",
                columns: new[] { "tenant_id", "service_type_id", "version" },
                unique: true);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                  r record;
                BEGIN
                  FOR r IN
                    SELECT unnest(ARRAY[
                      'briefing_template',
                      'briefing_question',
                      'briefing_answer',
                      'briefing_consent'
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
            migrationBuilder.DropTable(
                name: "briefing_answer");

            migrationBuilder.DropTable(
                name: "briefing_consent");

            migrationBuilder.DropTable(
                name: "briefing_question");

            migrationBuilder.DropTable(
                name: "briefing_template");
        }
    }
}
