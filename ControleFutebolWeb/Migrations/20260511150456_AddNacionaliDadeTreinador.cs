using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddNacionaliDadeTreinador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "nacionalidadeid",
                table: "treinadores",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_treinadores_nacionalidadeid",
                table: "treinadores",
                column: "nacionalidadeid");

            migrationBuilder.AddForeignKey(
                name: "FK_treinadores_nacionalidades_nacionalidadeid",
                table: "treinadores",
                column: "nacionalidadeid",
                principalTable: "nacionalidades",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_treinadores_nacionalidades_nacionalidadeid",
                table: "treinadores");

            migrationBuilder.DropIndex(
                name: "IX_treinadores_nacionalidadeid",
                table: "treinadores");

            migrationBuilder.DropColumn(
                name: "nacionalidadeid",
                table: "treinadores");
        }
    }
}
