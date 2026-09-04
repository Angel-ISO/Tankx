using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Matches",
                type: "varchar",
                maxLength: 20,
                nullable: false,
                defaultValue: "NorthAmerica");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Region",
                table: "Matches",
                column: "Region");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_Region",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Matches");
        }
    }
}
