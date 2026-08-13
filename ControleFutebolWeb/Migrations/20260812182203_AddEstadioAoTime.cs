using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadioAoTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "estadiocapacidade",
                table: "times",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estadiocidade",
                table: "times",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estadiogramado",
                table: "times",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estadioimagemurl",
                table: "times",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estadionome",
                table: "times",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "estadiocapacidade",
                table: "times");

            migrationBuilder.DropColumn(
                name: "estadiocidade",
                table: "times");

            migrationBuilder.DropColumn(
                name: "estadiogramado",
                table: "times");

            migrationBuilder.DropColumn(
                name: "estadioimagemurl",
                table: "times");

            migrationBuilder.DropColumn(
                name: "estadionome",
                table: "times");
        }
    }
}
