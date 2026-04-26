using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddIdApiToTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_jogos_formacoes_formacaocasaid",
                table: "jogos");

            migrationBuilder.DropForeignKey(
                name: "FK_jogos_formacoes_formacaovisitanteid",
                table: "jogos");

            migrationBuilder.AddColumn<int>(
                name: "idapi",
                table: "times",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "formacaovisitanteid",
                table: "jogos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "formacaocasaid",
                table: "jogos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_jogos_formacoes_formacaocasaid",
                table: "jogos",
                column: "formacaocasaid",
                principalTable: "formacoes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_jogos_formacoes_formacaovisitanteid",
                table: "jogos",
                column: "formacaovisitanteid",
                principalTable: "formacoes",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_jogos_formacoes_formacaocasaid",
                table: "jogos");

            migrationBuilder.DropForeignKey(
                name: "FK_jogos_formacoes_formacaovisitanteid",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "idapi",
                table: "times");

            migrationBuilder.AlterColumn<int>(
                name: "formacaovisitanteid",
                table: "jogos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "formacaocasaid",
                table: "jogos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_jogos_formacoes_formacaocasaid",
                table: "jogos",
                column: "formacaocasaid",
                principalTable: "formacoes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_jogos_formacoes_formacaovisitanteid",
                table: "jogos",
                column: "formacaovisitanteid",
                principalTable: "formacoes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
