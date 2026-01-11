using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerStatsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "BonusDamagePercent",
                table: "PlayerStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "DamageReductionPercent",
                table: "PlayerStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "KnockbackTime",
                table: "PlayerStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "SpawnX",
                table: "PlayerStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "SpawnY",
                table: "PlayerStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "StunTime",
                table: "PlayerStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "WeaponRange",
                table: "PlayerStats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BonusDamagePercent",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "DamageReductionPercent",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "KnockbackTime",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "SpawnX",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "SpawnY",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "StunTime",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "WeaponRange",
                table: "PlayerStats");
        }
    }
}
