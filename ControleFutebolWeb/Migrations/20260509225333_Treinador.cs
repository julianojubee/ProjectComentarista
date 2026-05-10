using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class Treinador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "treinadores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    nome = table.Column<string>(type: "text", nullable: false),
                    datanascimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    timeid = table.Column<int>(type: "integer", nullable: false),
                    dtinc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dtalt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treinadores", x => x.id);
                    table.ForeignKey(
                        name: "FK_treinadores_times_timeid",
                        column: x => x.timeid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "treinadoreshistorico",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    treinadorid = table.Column<int>(type: "integer", nullable: false),
                    timeid = table.Column<int>(type: "integer", nullable: false),
                    dtinicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dtfim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treinadoreshistorico", x => x.id);
                    table.ForeignKey(
                        name: "FK_treinadoreshistorico_times_timeid",
                        column: x => x.timeid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_treinadoreshistorico_treinadores_treinadorid",
                        column: x => x.treinadorid,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_treinadores_timeid",
                table: "treinadores",
                column: "timeid");

            migrationBuilder.CreateIndex(
                name: "IX_treinadoreshistorico_timeid",
                table: "treinadoreshistorico",
                column: "timeid");

            migrationBuilder.CreateIndex(
                name: "IX_treinadoreshistorico_treinadorid",
                table: "treinadoreshistorico",
                column: "treinadorid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "treinadoreshistorico");

            migrationBuilder.DropTable(
                name: "treinadores");
        }
    }
}
