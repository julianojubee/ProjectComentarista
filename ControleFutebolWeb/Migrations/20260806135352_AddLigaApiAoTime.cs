using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddLigaApiAoTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ligaidapi",
                table: "times",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paisapi",
                table: "times",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ligaidapi",
                table: "times");

            migrationBuilder.DropColumn(
                name: "paisapi",
                table: "times");
        }
    }
}
