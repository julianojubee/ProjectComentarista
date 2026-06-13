using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddSelecaoIdToJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "selecaoid",
                table: "jogadores",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_jogadores_selecaoid",
                table: "jogadores",
                column: "selecaoid");

            migrationBuilder.AddForeignKey(
                name: "FK_jogadores_times_selecaoid",
                table: "jogadores",
                column: "selecaoid",
                principalTable: "times",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_jogadores_times_selecaoid",
                table: "jogadores");

            migrationBuilder.DropIndex(
                name: "IX_jogadores_selecaoid",
                table: "jogadores");

            migrationBuilder.DropColumn(
                name: "selecaoid",
                table: "jogadores");
        }
    }
}
