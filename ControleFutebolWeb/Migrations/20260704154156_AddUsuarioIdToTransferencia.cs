using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioIdToTransferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "usuarioid",
                table: "transferencias",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_usuarioid",
                table: "transferencias",
                column: "usuarioid");

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_aspnetusers_usuarioid",
                table: "transferencias",
                column: "usuarioid",
                principalTable: "aspnetusers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_aspnetusers_usuarioid",
                table: "transferencias");

            migrationBuilder.DropIndex(
                name: "IX_transferencias_usuarioid",
                table: "transferencias");

            migrationBuilder.DropColumn(
                name: "usuarioid",
                table: "transferencias");
        }
    }
}
