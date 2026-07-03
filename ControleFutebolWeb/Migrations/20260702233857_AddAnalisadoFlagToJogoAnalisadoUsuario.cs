using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalisadoFlagToJogoAnalisadoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true (não false) — sob a semântica antiga, toda linha
            // que já existia em produção representava "analisado". Sem isso, o
            // backfill desmarcaria como não-analisados todos os jogos já analisados.
            migrationBuilder.AddColumn<bool>(
                name: "analisado",
                table: "jogosanalisadosusuario",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "analisado",
                table: "jogosanalisadosusuario");
        }
    }
}
