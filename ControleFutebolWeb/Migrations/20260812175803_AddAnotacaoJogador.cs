using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAnotacaoJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anotacoesjogador",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    categoria = table.Column<string>(type: "text", nullable: true),
                    dtinc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dtalt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuarioid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anotacoesjogador", x => x.id);
                    table.ForeignKey(
                        name: "FK_anotacoesjogador_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_anotacoesjogador_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anotacoesjogador_jogadorid_usuarioid",
                table: "anotacoesjogador",
                columns: new[] { "jogadorid", "usuarioid" });

            migrationBuilder.CreateIndex(
                name: "IX_anotacoesjogador_usuarioid",
                table: "anotacoesjogador",
                column: "usuarioid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anotacoesjogador");
        }
    }
}
