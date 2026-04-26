using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddFormacaoPadraoRelationToTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "formacaopadraoid",
                table: "times",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_times_formacaopadraoid",
                table: "times",
                column: "formacaopadraoid");

            migrationBuilder.AddForeignKey(
                name: "FK_times_formacoes_formacaopadraoid",
                table: "times",
                column: "formacaopadraoid",
                principalTable: "formacoes",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_times_formacoes_formacaopadraoid",
                table: "times");

            migrationBuilder.DropIndex(
                name: "IX_times_formacaopadraoid",
                table: "times");

            migrationBuilder.DropColumn(
                name: "formacaopadraoid",
                table: "times");
        }
    }
}
