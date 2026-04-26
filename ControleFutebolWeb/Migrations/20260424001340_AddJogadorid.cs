using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddJogadorid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "jogadorid",
                table: "timesescalacoespadrao",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_timesescalacoespadrao_jogadorid",
                table: "timesescalacoespadrao",
                column: "jogadorid");

            migrationBuilder.AddForeignKey(
                name: "FK_timesescalacoespadrao_jogadores_jogadorid",
                table: "timesescalacoespadrao",
                column: "jogadorid",
                principalTable: "jogadores",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_timesescalacoespadrao_jogadores_jogadorid",
                table: "timesescalacoespadrao");

            migrationBuilder.DropIndex(
                name: "IX_timesescalacoespadrao_jogadorid",
                table: "timesescalacoespadrao");

            migrationBuilder.DropColumn(
                name: "jogadorid",
                table: "timesescalacoespadrao");
        }
    }
}
