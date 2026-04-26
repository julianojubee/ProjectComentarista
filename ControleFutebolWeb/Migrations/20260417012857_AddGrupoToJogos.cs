using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupoToJogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "competicoes",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "competicoes",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "competicoes",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "grupo",
                table: "jogos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "competicoes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grupo",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "competicoes");

            migrationBuilder.InsertData(
                table: "competicoes",
                columns: new[] { "id", "nome", "regiao" },
                values: new object[,]
                {
                    { 1, "Copa Libertadores", "América do Sul" },
                    { 2, "Brasileirão Série A", "Brasil" },
                    { 3, "Copa do Brasil", "Brasil" }
                });
        }
    }
}
