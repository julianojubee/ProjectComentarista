using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddCoresUniformeJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "corcamisacasa",
                table: "jogos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "corcamisavisitante",
                table: "jogos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cornumerocasa",
                table: "jogos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cornumerovisitante",
                table: "jogos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "corcamisacasa",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "corcamisavisitante",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "cornumerocasa",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "cornumerovisitante",
                table: "jogos");
        }
    }
}
