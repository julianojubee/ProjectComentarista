using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class RelogioJogoAoVivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "acrescimoparcial",
                table: "jogos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "minutoparcial",
                table: "jogos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "statusparcial",
                table: "jogos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "acrescimoparcial",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "minutoparcial",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "statusparcial",
                table: "jogos");
        }
    }
}
