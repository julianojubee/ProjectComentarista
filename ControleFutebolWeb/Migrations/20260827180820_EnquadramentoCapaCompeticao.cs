using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class EnquadramentoCapaCompeticao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "posx",
                table: "competicoesherousuario",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "posy",
                table: "competicoesherousuario",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "zoom",
                table: "competicoesherousuario",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "posx",
                table: "competicoesherousuario");

            migrationBuilder.DropColumn(
                name: "posy",
                table: "competicoesherousuario");

            migrationBuilder.DropColumn(
                name: "zoom",
                table: "competicoesherousuario");
        }
    }
}
