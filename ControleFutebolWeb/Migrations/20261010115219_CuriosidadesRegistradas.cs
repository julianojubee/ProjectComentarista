using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class CuriosidadesRegistradas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "curiosidadesregistradas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    chave = table.Column<string>(type: "text", nullable: false),
                    dtinc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curiosidadesregistradas", x => x.id);
                    table.ForeignKey(
                        name: "FK_curiosidadesregistradas_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_curiosidadesregistradas_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_curiosidadesregistradas_jogoid",
                table: "curiosidadesregistradas",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_curiosidadesregistradas_usuarioid_jogoid_chave",
                table: "curiosidadesregistradas",
                columns: new[] { "usuarioid", "jogoid", "chave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "curiosidadesregistradas");
        }
    }
}
