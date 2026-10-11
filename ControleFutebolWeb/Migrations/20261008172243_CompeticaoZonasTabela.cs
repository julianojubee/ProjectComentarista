using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class CompeticaoZonasTabela : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "zonaplayoffvagas",
                table: "competicoes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "zonarebaixamentovagas",
                table: "competicoes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zonatopo2rotulo",
                table: "competicoes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "zonatopo2vagas",
                table: "competicoes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zonatoporotulo",
                table: "competicoes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "zonatopovagas",
                table: "competicoes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "zonaplayoffvagas",
                table: "competicoes");

            migrationBuilder.DropColumn(
                name: "zonarebaixamentovagas",
                table: "competicoes");

            migrationBuilder.DropColumn(
                name: "zonatopo2rotulo",
                table: "competicoes");

            migrationBuilder.DropColumn(
                name: "zonatopo2vagas",
                table: "competicoes");

            migrationBuilder.DropColumn(
                name: "zonatoporotulo",
                table: "competicoes");

            migrationBuilder.DropColumn(
                name: "zonatopovagas",
                table: "competicoes");
        }
    }
}
