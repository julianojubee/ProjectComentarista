using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class TimeHeroImagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "heroimagemopacidade",
                table: "times",
                type: "integer",
                nullable: false,
                defaultValue: 35);

            migrationBuilder.AddColumn<int>(
                name: "heroimagemposicaoy",
                table: "times",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<string>(
                name: "heroimagemurl",
                table: "times",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "heroimagemopacidade",
                table: "times");

            migrationBuilder.DropColumn(
                name: "heroimagemposicaoy",
                table: "times");

            migrationBuilder.DropColumn(
                name: "heroimagemurl",
                table: "times");
        }
    }
}
