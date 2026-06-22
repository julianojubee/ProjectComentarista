using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddSubstituicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "substituicoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    jogadorentroud = table.Column<int>(type: "integer", nullable: false),
                    jogadorsaiuid = table.Column<int>(type: "integer", nullable: true),
                    minuto = table.Column<int>(type: "integer", nullable: false),
                    istimecasa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_substituicoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_substituicoes_jogadores_jogadorentroud",
                        column: x => x.jogadorentroud,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_substituicoes_jogadores_jogadorsaiuid",
                        column: x => x.jogadorsaiuid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_substituicoes_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_substituicoes_jogadorentroud",
                table: "substituicoes",
                column: "jogadorentroud");

            migrationBuilder.CreateIndex(
                name: "IX_substituicoes_jogadorsaiuid",
                table: "substituicoes",
                column: "jogadorsaiuid");

            migrationBuilder.CreateIndex(
                name: "IX_substituicoes_jogoid",
                table: "substituicoes",
                column: "jogoid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "substituicoes");
        }
    }
}
