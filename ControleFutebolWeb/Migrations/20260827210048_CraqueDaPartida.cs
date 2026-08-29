using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class CraqueDaPartida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "craquesdapartida",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    nota = table.Column<double>(type: "double precision", nullable: false),
                    gols = table.Column<int>(type: "integer", nullable: false),
                    assistencias = table.Column<int>(type: "integer", nullable: false),
                    chancescriadas = table.Column<int>(type: "integer", nullable: false),
                    empatados = table.Column<int>(type: "integer", nullable: false),
                    calculadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_craquesdapartida", x => x.id);
                    table.ForeignKey(
                        name: "FK_craquesdapartida_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_craquesdapartida_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_craquesdapartida_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_craquesdapartida_jogadorid",
                table: "craquesdapartida",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_craquesdapartida_jogoid_usuarioid",
                table: "craquesdapartida",
                columns: new[] { "jogoid", "usuarioid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_craquesdapartida_usuarioid_jogadorid",
                table: "craquesdapartida",
                columns: new[] { "usuarioid", "jogadorid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "craquesdapartida");
        }
    }
}
