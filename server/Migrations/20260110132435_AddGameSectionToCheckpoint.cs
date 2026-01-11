using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSectionToCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "Checkpoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_SectionId",
                table: "Checkpoints",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Checkpoints_GameSections_SectionId",
                table: "Checkpoints",
                column: "SectionId",
                principalTable: "GameSections",
                principalColumn: "SectionId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Checkpoints_GameSections_SectionId",
                table: "Checkpoints");

            migrationBuilder.DropIndex(
                name: "IX_Checkpoints_SectionId",
                table: "Checkpoints");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Checkpoints");
        }
    }
}
