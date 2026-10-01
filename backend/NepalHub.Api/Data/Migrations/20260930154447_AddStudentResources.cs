using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    slug = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    summary = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<int>(type: "int", nullable: false),
                    category = table.Column<int>(type: "int", nullable: false),
                    level = table.Column<int>(type: "int", nullable: false),
                    external_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    academic_year = table.Column<int>(type: "int", nullable: true),
                    provider = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    award_amount_npr = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    eligibility = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_published = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resources", x => x.id);
                    table.ForeignKey(
                        name: "resources_fk_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_resources_created_by_user_id",
                table: "resources",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_resources_is_published_category",
                table: "resources",
                columns: new[] { "is_published", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_resources_is_published_created_at",
                table: "resources",
                columns: new[] { "is_published", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_resources_is_published_education_level",
                table: "resources",
                columns: new[] { "is_published", "level" });

            migrationBuilder.CreateIndex(
                name: "ix_resources_is_published_expires_on",
                table: "resources",
                columns: new[] { "is_published", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "ix_resources_is_published_resource_type",
                table: "resources",
                columns: new[] { "is_published", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_resources_slug",
                table: "resources",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resources");
        }
    }
}
