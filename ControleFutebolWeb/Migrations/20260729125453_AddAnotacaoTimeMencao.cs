using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAnotacaoTimeMencao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anotacoestimemencoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    anotacaotimeid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anotacoestimemencoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_anotacoestimemencoes_anotacoestime_anotacaotimeid",
                        column: x => x.anotacaotimeid,
                        principalTable: "anotacoestime",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_anotacoestimemencoes_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anotacoestimemencoes_anotacaotimeid",
                table: "anotacoestimemencoes",
                column: "anotacaotimeid");

            migrationBuilder.CreateIndex(
                name: "IX_anotacoestimemencoes_jogadorid",
                table: "anotacoestimemencoes",
                column: "jogadorid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anotacoestimemencoes");
        }
    }
}
