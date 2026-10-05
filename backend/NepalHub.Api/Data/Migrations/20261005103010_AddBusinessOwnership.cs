using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "businesses",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "business_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    business_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    claimant_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    evidence = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    reviewed_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    review_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_claims", x => x.id);
                    table.ForeignKey(
                        name: "business_claims_fk_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "business_claims_fk_claimant_user_id",
                        column: x => x.claimant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "business_claims_fk_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_businesses_owner_user_id",
                table: "businesses",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_claims_business_claimant_status",
                table: "business_claims",
                columns: new[] { "business_id", "claimant_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_business_claims_claimant_user_id",
                table: "business_claims",
                column: "claimant_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_claims_reviewed_by_user_id",
                table: "business_claims",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_claims_status_created_at",
                table: "business_claims",
                columns: new[] { "status", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "businesses_fk_owner_user_id",
                table: "businesses",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "businesses_fk_owner_user_id",
                table: "businesses");

            migrationBuilder.DropTable(
                name: "business_claims");

            migrationBuilder.DropIndex(
                name: "ix_businesses_owner_user_id",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "businesses");
        }
    }
}
