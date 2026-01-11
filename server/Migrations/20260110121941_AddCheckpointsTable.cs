using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckpointsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Checkpoints",
                columns: table => new
                {
                    CheckpointId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CheckpointName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    X = table.Column<float>(type: "REAL", nullable: false),
                    Y = table.Column<float>(type: "REAL", nullable: false),
                    EnemyPool = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    MaxEnemies = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkpoints", x => x.CheckpointId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_CheckpointName",
                table: "Checkpoints",
                column: "CheckpointName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Checkpoints");
        }
    }
}
