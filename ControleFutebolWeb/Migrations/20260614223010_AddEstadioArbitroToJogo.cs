using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadioArbitroToJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "arbitro",
                table: "jogos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estadio",
                table: "jogos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "arbitro",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "estadio",
                table: "jogos");
        }
    }
}
