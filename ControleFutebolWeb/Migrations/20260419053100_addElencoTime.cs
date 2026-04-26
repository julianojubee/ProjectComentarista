using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class addElencoTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "timeid1",
                table: "jogadores",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_jogadores_timeid1",
                table: "jogadores",
                column: "timeid1");

            migrationBuilder.AddForeignKey(
                name: "FK_jogadores_times_timeid1",
                table: "jogadores",
                column: "timeid1",
                principalTable: "times",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_jogadores_times_timeid1",
                table: "jogadores");

            migrationBuilder.DropIndex(
                name: "IX_jogadores_timeid1",
                table: "jogadores");

            migrationBuilder.DropColumn(
                name: "timeid1",
                table: "jogadores");
        }
    }
}
