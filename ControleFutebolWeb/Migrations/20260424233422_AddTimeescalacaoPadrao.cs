using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeescalacaoPadrao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_timesescalacoespadrao_jogadores_jogadorid",
                table: "timesescalacoespadrao");

            migrationBuilder.DropForeignKey(
                name: "FK_timesescalacoespadrao_times_timeid",
                table: "timesescalacoespadrao");

            migrationBuilder.DropPrimaryKey(
                name: "PK_timesescalacoespadrao",
                table: "timesescalacoespadrao");

            migrationBuilder.RenameTable(
                name: "timesescalacoespadrao",
                newName: "timeescalacaopadrao");

            migrationBuilder.RenameIndex(
                name: "IX_timesescalacoespadrao_timeid",
                table: "timeescalacaopadrao",
                newName: "IX_timeescalacaopadrao_timeid");

            migrationBuilder.RenameIndex(
                name: "IX_timesescalacoespadrao_jogadorid",
                table: "timeescalacaopadrao",
                newName: "IX_timeescalacaopadrao_jogadorid");

            migrationBuilder.AddPrimaryKey(
                name: "PK_timeescalacaopadrao",
                table: "timeescalacaopadrao",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_timeescalacaopadrao_jogadores_jogadorid",
                table: "timeescalacaopadrao",
                column: "jogadorid",
                principalTable: "jogadores",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_timeescalacaopadrao_times_timeid",
                table: "timeescalacaopadrao",
                column: "timeid",
                principalTable: "times",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_timeescalacaopadrao_jogadores_jogadorid",
                table: "timeescalacaopadrao");

            migrationBuilder.DropForeignKey(
                name: "FK_timeescalacaopadrao_times_timeid",
                table: "timeescalacaopadrao");

            migrationBuilder.DropPrimaryKey(
                name: "PK_timeescalacaopadrao",
                table: "timeescalacaopadrao");

            migrationBuilder.RenameTable(
                name: "timeescalacaopadrao",
                newName: "timesescalacoespadrao");

            migrationBuilder.RenameIndex(
                name: "IX_timeescalacaopadrao_timeid",
                table: "timesescalacoespadrao",
                newName: "IX_timesescalacoespadrao_timeid");

            migrationBuilder.RenameIndex(
                name: "IX_timeescalacaopadrao_jogadorid",
                table: "timesescalacoespadrao",
                newName: "IX_timesescalacoespadrao_jogadorid");

            migrationBuilder.AddPrimaryKey(
                name: "PK_timesescalacoespadrao",
                table: "timesescalacoespadrao",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_timesescalacoespadrao_jogadores_jogadorid",
                table: "timesescalacoespadrao",
                column: "jogadorid",
                principalTable: "jogadores",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_timesescalacoespadrao_times_timeid",
                table: "timesescalacoespadrao",
                column: "timeid",
                principalTable: "times",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
