using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "businesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    slug = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    category = table.Column<int>(type: "int", nullable: false),
                    district_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    address = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    website = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_published = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    is_verified = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_businesses", x => x.id);
                    table.ForeignKey(
                        name: "businesses_fk_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "businesses_fk_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "business_hours",
                columns: table => new
                {
                    business_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    day_of_week = table.Column<int>(type: "int", nullable: false),
                    is_closed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    opens_at = table.Column<TimeOnly>(type: "time(6)", nullable: true),
                    closes_at = table.Column<TimeOnly>(type: "time(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_hours", x => new { x.business_id, x.day_of_week });
                    table.ForeignKey(
                        name: "business_hours_fk_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_businesses_created_by_user_id",
                table: "businesses",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_businesses_district_id_is_published",
                table: "businesses",
                columns: new[] { "district_id", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_businesses_is_published_category",
                table: "businesses",
                columns: new[] { "is_published", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_businesses_is_published_created_at",
                table: "businesses",
                columns: new[] { "is_published", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_businesses_is_published_name",
                table: "businesses",
                columns: new[] { "is_published", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_businesses_is_published_verified",
                table: "businesses",
                columns: new[] { "is_published", "is_verified" });

            migrationBuilder.CreateIndex(
                name: "ix_businesses_slug",
                table: "businesses",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_hours");

            migrationBuilder.DropTable(
                name: "businesses");
        }
    }
}
