using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    slug = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    organization = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    category = table.Column<int>(type: "int", nullable: false),
                    employment_type = table.Column<int>(type: "int", nullable: false),
                    work_mode = table.Column<int>(type: "int", nullable: false),
                    district_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    application_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    application_deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    is_published = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                    table.ForeignKey(
                        name: "jobs_fk_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "jobs_fk_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_created_by_user_id",
                table: "jobs",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_district_id_is_published",
                table: "jobs",
                columns: new[] { "district_id", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_is_published_application_deadline",
                table: "jobs",
                columns: new[] { "is_published", "application_deadline" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_is_published_category",
                table: "jobs",
                columns: new[] { "is_published", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_is_published_created_at",
                table: "jobs",
                columns: new[] { "is_published", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_is_published_employment_type",
                table: "jobs",
                columns: new[] { "is_published", "employment_type" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_is_published_work_mode",
                table: "jobs",
                columns: new[] { "is_published", "work_mode" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_slug",
                table: "jobs",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jobs");
        }
    }
}
