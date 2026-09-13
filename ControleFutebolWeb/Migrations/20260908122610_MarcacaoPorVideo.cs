using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class MarcacaoPorVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "marcacoesvideo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    acaoid = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    segundovideo = table.Column<int>(type: "integer", nullable: false),
                    minutojogo = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marcacoesvideo", x => x.id);
                    table.ForeignKey(
                        name: "FK_marcacoesvideo_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_marcacoesvideo_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_marcacoesvideo_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_marcacoesvideo_jogadorid",
                table: "marcacoesvideo",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_marcacoesvideo_jogoid_jogadorid",
                table: "marcacoesvideo",
                columns: new[] { "jogoid", "jogadorid" });

            migrationBuilder.CreateIndex(
                name: "IX_marcacoesvideo_usuarioid_jogoid",
                table: "marcacoesvideo",
                columns: new[] { "usuarioid", "jogoid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marcacoesvideo");
        }
    }
}
