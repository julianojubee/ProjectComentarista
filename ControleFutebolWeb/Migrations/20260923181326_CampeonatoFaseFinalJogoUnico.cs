using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class CampeonatoFaseFinalJogoUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "finaljogounico",
                table: "campeonatofases",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "finaljogounico",
                table: "campeonatofases");
        }
    }
}
