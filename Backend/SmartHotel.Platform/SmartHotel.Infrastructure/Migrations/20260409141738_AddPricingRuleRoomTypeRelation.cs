using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHotel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingRuleRoomTypeRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_RoomTypeId_Date",
                table: "PricingRules",
                columns: new[] { "RoomTypeId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_PricingRules_RoomTypes_RoomTypeId",
                table: "PricingRules",
                column: "RoomTypeId",
                principalTable: "RoomTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PricingRules_RoomTypes_RoomTypeId",
                table: "PricingRules");

            migrationBuilder.DropIndex(
                name: "IX_PricingRules_RoomTypeId_Date",
                table: "PricingRules");
        }
    }
}
