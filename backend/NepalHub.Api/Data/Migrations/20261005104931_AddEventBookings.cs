using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "capacity",
                table: "events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "registration_count",
                table: "events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "event_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    event_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    attendee_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Confirmed")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_bookings", x => x.id);
                    table.ForeignKey(
                        name: "event_bookings_fk_attendee_user_id",
                        column: x => x.attendee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "event_bookings_fk_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_event_bookings_attendee_user_id",
                table: "event_bookings",
                column: "attendee_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_bookings_event_attendee",
                table: "event_bookings",
                columns: new[] { "event_id", "attendee_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_bookings_event_id_status",
                table: "event_bookings",
                columns: new[] { "event_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_bookings");

            migrationBuilder.DropColumn(
                name: "capacity",
                table: "events");

            migrationBuilder.DropColumn(
                name: "registration_count",
                table: "events");
        }
    }
}
