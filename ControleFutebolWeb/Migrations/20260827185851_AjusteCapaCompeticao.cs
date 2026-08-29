using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AjusteCapaCompeticao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ajuste",
                table: "competicoesherousuario",
                type: "text",
                nullable: false,
                // Capas já salvas foram enquadradas no modo antigo (preencher a faixa).
                defaultValue: "COBRIR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ajuste",
                table: "competicoesherousuario");
        }
    }
}
