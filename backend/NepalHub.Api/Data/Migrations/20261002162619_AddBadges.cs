using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBadges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    icon = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    criteria = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    threshold = table.Column<int>(type: "int", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_badges", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    badge_definition_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    awarded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_badges", x => x.id);
                    table.ForeignKey(
                        name: "user_badges_fk_badge_id",
                        column: x => x.badge_definition_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_badges_fk_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "badges",
                columns: new[] { "id", "code", "criteria", "description", "icon", "name", "sort_order", "threshold" },
                values: new object[,]
                {
                    { new Guid("1c54e599-6c99-5d50-9744-280d80029e13"), "quiz-enthusiast", "QuizzesCompleted", "Finish ten daily quizzes.", "book", "Quiz Enthusiast", 3, 10 },
                    { new Guid("7c2973f0-77c0-065b-ae5e-f815bd56c381"), "seven-day-streak", "ConsecutiveDayStreak", "Finish a quiz on seven days in a row.", "flame", "7-Day Streak", 2, 7 },
                    { new Guid("c4d90053-a983-a15c-ac90-4ad657b5d6ea"), "first-quiz", "QuizzesCompleted", "Finish your first daily quiz.", "trophy", "First Quiz", 1, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_badges_code",
                table: "badges",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_badges_sort_order",
                table: "badges",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_badge_id",
                table: "user_badges",
                column: "badge_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_user_id_awarded_at",
                table: "user_badges",
                columns: new[] { "user_id", "awarded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_user_id_badge_id",
                table: "user_badges",
                columns: new[] { "user_id", "badge_definition_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_badges");

            migrationBuilder.DropTable(
                name: "badges");
        }
    }
}
