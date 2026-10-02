using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDealsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    slug = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    summary = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    discount_text = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    business_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    district_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    redemption_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_published = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deals", x => x.id);
                    table.ForeignKey(
                        name: "deals_fk_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "deals_fk_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "deals_fk_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    slug = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    summary = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    category = table.Column<int>(type: "int", nullable: false),
                    organiser = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    business_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    district_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    venue = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    registration_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_published = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.ForeignKey(
                        name: "events_fk_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "events_fk_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "events_fk_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_deals_business_id",
                table: "deals",
                column: "business_id");

            migrationBuilder.CreateIndex(
                name: "ix_deals_created_by_user_id",
                table: "deals",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_deals_district_id_is_published",
                table: "deals",
                columns: new[] { "district_id", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_deals_is_published_ends_on",
                table: "deals",
                columns: new[] { "is_published", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "ix_deals_is_published_starts_on",
                table: "deals",
                columns: new[] { "is_published", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "ix_deals_slug",
                table: "deals",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_business_id",
                table: "events",
                column: "business_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_created_by_user_id",
                table: "events",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_district_id_is_published",
                table: "events",
                columns: new[] { "district_id", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_events_is_published_category",
                table: "events",
                columns: new[] { "is_published", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_events_is_published_ends_at",
                table: "events",
                columns: new[] { "is_published", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_events_is_published_starts_at",
                table: "events",
                columns: new[] { "is_published", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_events_slug",
                table: "events",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deals");

            migrationBuilder.DropTable(
                name: "events");
        }
    }
}
