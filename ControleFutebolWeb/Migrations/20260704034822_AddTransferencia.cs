using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transferencias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    timeorigemid = table.Column<int>(type: "integer", nullable: true),
                    timedestinoid = table.Column<int>(type: "integer", nullable: false),
                    jogoid = table.Column<int>(type: "integer", nullable: true),
                    data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencias", x => x.id);
                    table.ForeignKey(
                        name: "FK_transferencias_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transferencias_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transferencias_times_timedestinoid",
                        column: x => x.timedestinoid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transferencias_times_timeorigemid",
                        column: x => x.timeorigemid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_data",
                table: "transferencias",
                column: "data");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_jogadorid",
                table: "transferencias",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_jogoid",
                table: "transferencias",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_timedestinoid",
                table: "transferencias",
                column: "timedestinoid");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_timeorigemid",
                table: "transferencias",
                column: "timeorigemid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transferencias");
        }
    }
}
