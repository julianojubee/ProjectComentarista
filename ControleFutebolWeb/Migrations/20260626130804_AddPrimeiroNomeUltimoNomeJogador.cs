using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimeiroNomeUltimoNomeJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "primeironome",
                table: "jogadores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ultimonome",
                table: "jogadores",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "primeironome",
                table: "jogadores");

            migrationBuilder.DropColumn(
                name: "ultimonome",
                table: "jogadores");
        }
    }
}
