using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NepalHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessIsClaimedAndClaimStatusRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_claimed",
                table: "businesses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Status is stored as its name. Rename the two closed states to match the
            // content-claim lifecycle (Pending / Accepted / Released).
            migrationBuilder.Sql("UPDATE `business_claims` SET `status` = 'Accepted' WHERE `status` = 'Approved';");
            migrationBuilder.Sql("UPDATE `business_claims` SET `status` = 'Released' WHERE `status` = 'Rejected';");

            // A listing that already has an owner was accepted under the old staff flow and
            // must hide like any other accepted claim.
            migrationBuilder.Sql("UPDATE `businesses` SET `is_claimed` = 1 WHERE `owner_user_id` IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `business_claims` SET `status` = 'Approved' WHERE `status` = 'Accepted';");
            migrationBuilder.Sql("UPDATE `business_claims` SET `status` = 'Rejected' WHERE `status` = 'Released';");
            migrationBuilder.Sql("UPDATE `businesses` SET `is_claimed` = 0;");

            migrationBuilder.DropColumn(
                name: "is_claimed",
                table: "businesses");
        }
    }
}
