using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notas_usuarioid",
                table: "notas");

            migrationBuilder.DropIndex(
                name: "IX_escalacoes_jogoid",
                table: "escalacoes");

            migrationBuilder.CreateIndex(
                name: "IX_notas_usuarioid_jogoid_jogadorid",
                table: "notas",
                columns: new[] { "usuarioid", "jogoid", "jogadorid" });

            migrationBuilder.CreateIndex(
                name: "IX_jogos_temporada",
                table: "jogos",
                column: "temporada");

            migrationBuilder.CreateIndex(
                name: "IX_jogadores_posicao",
                table: "jogadores",
                column: "posicao");

            migrationBuilder.CreateIndex(
                name: "IX_escalacoes_jogoid_usuarioid",
                table: "escalacoes",
                columns: new[] { "jogoid", "usuarioid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notas_usuarioid_jogoid_jogadorid",
                table: "notas");

            migrationBuilder.DropIndex(
                name: "IX_jogos_temporada",
                table: "jogos");

            migrationBuilder.DropIndex(
                name: "IX_jogadores_posicao",
                table: "jogadores");

            migrationBuilder.DropIndex(
                name: "IX_escalacoes_jogoid_usuarioid",
                table: "escalacoes");

            migrationBuilder.CreateIndex(
                name: "IX_notas_usuarioid",
                table: "notas",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "IX_escalacoes_jogoid",
                table: "escalacoes",
                column: "jogoid");
        }
    }
}
