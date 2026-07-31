using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulacaoJogoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "simulacoesjogousuario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    competicaoid = table.Column<int>(type: "integer", nullable: false),
                    temporada = table.Column<int>(type: "integer", nullable: false),
                    placarcasa = table.Column<int>(type: "integer", nullable: false),
                    placarvisitante = table.Column<int>(type: "integer", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulacoesjogousuario", x => x.id);
                    table.ForeignKey(
                        name: "FK_simulacoesjogousuario_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_simulacoesjogousuario_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_simulacoesjogousuario_jogoid",
                table: "simulacoesjogousuario",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_simulacoesjogousuario_usuarioid_competicaoid_temporada",
                table: "simulacoesjogousuario",
                columns: new[] { "usuarioid", "competicaoid", "temporada" });

            migrationBuilder.CreateIndex(
                name: "IX_simulacoesjogousuario_usuarioid_jogoid",
                table: "simulacoesjogousuario",
                columns: new[] { "usuarioid", "jogoid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "simulacoesjogousuario");
        }
    }
}
