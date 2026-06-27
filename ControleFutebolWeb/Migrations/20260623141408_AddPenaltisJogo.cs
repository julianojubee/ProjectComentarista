using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltisJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "penaltiscasa",
                table: "jogos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "penaltisvisitante",
                table: "jogos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "penaltiscasa",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "penaltisvisitante",
                table: "jogos");
        }
    }
}
