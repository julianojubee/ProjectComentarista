using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class CampeonatosCascataParticipante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_eventospartidacampeonato_campeonatoparticipantes_participan~",
                table: "eventospartidacampeonato");

            migrationBuilder.DropForeignKey(
                name: "FK_partidascampeonato_campeonatoparticipantes_participantecasa~",
                table: "partidascampeonato");

            migrationBuilder.DropForeignKey(
                name: "FK_partidascampeonato_campeonatoparticipantes_participantevisi~",
                table: "partidascampeonato");

            migrationBuilder.AddForeignKey(
                name: "FK_eventospartidacampeonato_campeonatoparticipantes_participan~",
                table: "eventospartidacampeonato",
                column: "participanteid",
                principalTable: "campeonatoparticipantes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_partidascampeonato_campeonatoparticipantes_participantecasa~",
                table: "partidascampeonato",
                column: "participantecasaid",
                principalTable: "campeonatoparticipantes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_partidascampeonato_campeonatoparticipantes_participantevisi~",
                table: "partidascampeonato",
                column: "participantevisitanteid",
                principalTable: "campeonatoparticipantes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_eventospartidacampeonato_campeonatoparticipantes_participan~",
                table: "eventospartidacampeonato");

            migrationBuilder.DropForeignKey(
                name: "FK_partidascampeonato_campeonatoparticipantes_participantecasa~",
                table: "partidascampeonato");

            migrationBuilder.DropForeignKey(
                name: "FK_partidascampeonato_campeonatoparticipantes_participantevisi~",
                table: "partidascampeonato");

            migrationBuilder.AddForeignKey(
                name: "FK_eventospartidacampeonato_campeonatoparticipantes_participan~",
                table: "eventospartidacampeonato",
                column: "participanteid",
                principalTable: "campeonatoparticipantes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_partidascampeonato_campeonatoparticipantes_participantecasa~",
                table: "partidascampeonato",
                column: "participantecasaid",
                principalTable: "campeonatoparticipantes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_partidascampeonato_campeonatoparticipantes_participantevisi~",
                table: "partidascampeonato",
                column: "participantevisitanteid",
                principalTable: "campeonatoparticipantes",
                principalColumn: "id");
        }
    }
}
