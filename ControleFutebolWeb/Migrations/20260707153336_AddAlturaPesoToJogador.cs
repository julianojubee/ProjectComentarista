using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAlturaPesoToJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "altura",
                table: "jogadores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "peso",
                table: "jogadores",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "altura",
                table: "jogadores");

            migrationBuilder.DropColumn(
                name: "peso",
                table: "jogadores");
        }
    }
}
