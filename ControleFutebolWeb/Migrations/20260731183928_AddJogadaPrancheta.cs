using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddJogadaPrancheta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jogadas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    timeid = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    passosjson = table.Column<string>(type: "text", nullable: false),
                    criadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogadas", x => x.id);
                    table.ForeignKey(
                        name: "FK_jogadas_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jogadas_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jogadas_times_timeid",
                        column: x => x.timeid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_jogadas_jogoid",
                table: "jogadas",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_jogadas_timeid",
                table: "jogadas",
                column: "timeid");

            migrationBuilder.CreateIndex(
                name: "IX_jogadas_usuarioid_jogoid",
                table: "jogadas",
                columns: new[] { "usuarioid", "jogoid" });

            migrationBuilder.CreateIndex(
                name: "IX_jogadas_usuarioid_timeid",
                table: "jogadas",
                columns: new[] { "usuarioid", "timeid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jogadas");
        }
    }
}
