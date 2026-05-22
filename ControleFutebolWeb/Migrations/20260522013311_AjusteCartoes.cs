using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AjusteCartoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "jogoid1",
                table: "gols",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "jogoid1",
                table: "cartoes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_gols_jogoid1",
                table: "gols",
                column: "jogoid1");

            migrationBuilder.CreateIndex(
                name: "IX_cartoes_jogoid1",
                table: "cartoes",
                column: "jogoid1");

            migrationBuilder.AddForeignKey(
                name: "FK_cartoes_jogos_jogoid1",
                table: "cartoes",
                column: "jogoid1",
                principalTable: "jogos",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_gols_jogos_jogoid1",
                table: "gols",
                column: "jogoid1",
                principalTable: "jogos",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cartoes_jogos_jogoid1",
                table: "cartoes");

            migrationBuilder.DropForeignKey(
                name: "FK_gols_jogos_jogoid1",
                table: "gols");

            migrationBuilder.DropIndex(
                name: "IX_gols_jogoid1",
                table: "gols");

            migrationBuilder.DropIndex(
                name: "IX_cartoes_jogoid1",
                table: "cartoes");

            migrationBuilder.DropColumn(
                name: "jogoid1",
                table: "gols");

            migrationBuilder.DropColumn(
                name: "jogoid1",
                table: "cartoes");
        }
    }
}
