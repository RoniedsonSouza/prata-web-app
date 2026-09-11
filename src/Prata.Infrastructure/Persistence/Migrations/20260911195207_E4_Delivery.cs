using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prata.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class E4_Delivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "download_job",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    bytes = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_download_job", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gallery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    photo_limit = table.Column<int>(type: "integer", nullable: false),
                    selection_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    portfolio_consent = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    has_minor = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gallery", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "share_link",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_access_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    access_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_share_link", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "photo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gallery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_bytes = table.Column<long>(type: "bigint", nullable: false),
                    original_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_favorite_by_studio = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo", x => x.id);
                    table.ForeignKey(
                        name: "fk_photo_gallery_gallery_id",
                        column: x => x.gallery_id,
                        principalTable: "gallery",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "selection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gallery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_selection", x => x.id);
                    table.ForeignKey(
                        name: "fk_selection_gallery_gallery_id",
                        column: x => x.gallery_id,
                        principalTable: "gallery",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_variant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_variant", x => x.id);
                    table.ForeignKey(
                        name: "fk_photo_variant_photo_photo_id",
                        column: x => x.photo_id,
                        principalTable: "photo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_download_job_tenant_id",
                table: "download_job",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_gallery_tenant_id",
                table: "gallery",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_gallery_tenant_id_expires_at",
                table: "gallery",
                columns: new[] { "tenant_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gallery_tenant_id_order_id",
                table: "gallery",
                columns: new[] { "tenant_id", "order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_gallery_id_original_hash",
                table: "photo",
                columns: new[] { "gallery_id", "original_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_tenant_id",
                table: "photo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_photo_tenant_id_gallery_id_sort_order",
                table: "photo",
                columns: new[] { "tenant_id", "gallery_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_photo_variant_photo_id_kind",
                table: "photo_variant",
                columns: new[] { "photo_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_variant_tenant_id",
                table: "photo_variant",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_selection_gallery_id_photo_id",
                table: "selection",
                columns: new[] { "gallery_id", "photo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_selection_tenant_id",
                table: "selection",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_link_tenant_id",
                table: "share_link",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_link_token_hash",
                table: "share_link",
                column: "token_hash",
                unique: true);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                  r record;
                BEGIN
                  FOR r IN
                    SELECT unnest(ARRAY[
                      'gallery', 'photo', 'photo_variant', 'selection', 'share_link', 'download_job'
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
                name: "download_job");

            migrationBuilder.DropTable(
                name: "photo_variant");

            migrationBuilder.DropTable(
                name: "selection");

            migrationBuilder.DropTable(
                name: "share_link");

            migrationBuilder.DropTable(
                name: "photo");

            migrationBuilder.DropTable(
                name: "gallery");
        }
    }
}
