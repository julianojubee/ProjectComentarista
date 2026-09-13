using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class IndisponiveisDoJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jogosindisponiveis",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    timeid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: true),
                    idapijogador = table.Column<long>(type: "bigint", nullable: true),
                    nome = table.Column<string>(type: "text", nullable: false),
                    fotourl = table.Column<string>(type: "text", nullable: true),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: false),
                    atualizadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogosindisponiveis", x => x.id);
                    table.ForeignKey(
                        name: "FK_jogosindisponiveis_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_jogosindisponiveis_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jogosindisponiveis_times_timeid",
                        column: x => x.timeid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_jogosindisponiveis_jogadorid",
                table: "jogosindisponiveis",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_jogosindisponiveis_jogoid",
                table: "jogosindisponiveis",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_jogosindisponiveis_timeid",
                table: "jogosindisponiveis",
                column: "timeid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jogosindisponiveis");
        }
    }
}
