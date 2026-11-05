using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_claimed",
                table: "jobs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_claimed",
                table: "events",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_claimed",
                table: "deals",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "content_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    job_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    event_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    deal_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    claimant_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    review_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_claims", x => x.id);
                    table.ForeignKey(
                        name: "content_claims_fk_claimant_user_id",
                        column: x => x.claimant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "content_claims_fk_deal_id",
                        column: x => x.deal_id,
                        principalTable: "deals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "content_claims_fk_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "content_claims_fk_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_content_claims_claimant_user_id",
                table: "content_claims",
                column: "claimant_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_claims_deal_claimant_status",
                table: "content_claims",
                columns: new[] { "deal_id", "claimant_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_content_claims_event_claimant_status",
                table: "content_claims",
                columns: new[] { "event_id", "claimant_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_content_claims_job_claimant_status",
                table: "content_claims",
                columns: new[] { "job_id", "claimant_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_content_claims_status_created_at",
                table: "content_claims",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_claims");

            migrationBuilder.DropColumn(
                name: "is_claimed",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "is_claimed",
                table: "events");

            migrationBuilder.DropColumn(
                name: "is_claimed",
                table: "deals");
        }
    }
}
