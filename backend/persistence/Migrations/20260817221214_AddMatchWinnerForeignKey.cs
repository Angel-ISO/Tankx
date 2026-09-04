using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchWinnerForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Matches_WinnerId",
                table: "Matches",
                column: "WinnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Profiles_WinnerId",
                table: "Matches",
                column: "WinnerId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Profiles_WinnerId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_WinnerId",
                table: "Matches");
        }
    }
}
