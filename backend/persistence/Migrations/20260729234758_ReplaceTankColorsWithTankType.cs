using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTankColorsWithTankType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TankBodyColor",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "TankTrailColor",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "TankTurretColor",
                table: "Profiles");

            migrationBuilder.AddColumn<string>(
                name: "TankType",
                table: "Profiles",
                type: "varchar",
                maxLength: 50,
                nullable: false,
                defaultValue: "tank_green");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TankType",
                table: "Profiles");

            migrationBuilder.AddColumn<string>(
                name: "TankBodyColor",
                table: "Profiles",
                type: "varchar",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TankTrailColor",
                table: "Profiles",
                type: "varchar",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TankTurretColor",
                table: "Profiles",
                type: "varchar",
                maxLength: 7,
                nullable: false,
                defaultValue: "");
        }
    }
}
