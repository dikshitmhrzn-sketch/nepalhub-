using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDealsAndEventsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_events_is_published_created_at",
                table: "events",
                columns: new[] { "is_published", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_deals_is_published_created_at",
                table: "deals",
                columns: new[] { "is_published", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_events_is_published_created_at",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_deals_is_published_created_at",
                table: "deals");
        }
    }
}
