using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAnaliseCompartilhada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analisescompartilhadas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expiraem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revogadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    visualizacoes = table.Column<int>(type: "integer", nullable: false),
                    ultimoacessoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analisescompartilhadas", x => x.id);
                    table.ForeignKey(
                        name: "FK_analisescompartilhadas_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analisescompartilhadas_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analisescompartilhadas_jogoid",
                table: "analisescompartilhadas",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_analisescompartilhadas_token",
                table: "analisescompartilhadas",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analisescompartilhadas_usuarioid_jogoid",
                table: "analisescompartilhadas",
                columns: new[] { "usuarioid", "jogoid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analisescompartilhadas");
        }
    }
}
