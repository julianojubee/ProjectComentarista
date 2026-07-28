using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddObservacaoJogoTagMencao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "observacoesjogotagmencoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    observacaojogotagid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observacoesjogotagmencoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_observacoesjogotagmencoes_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_observacoesjogotagmencoes_observacoesjogotag_observacaojogo~",
                        column: x => x.observacaojogotagid,
                        principalTable: "observacoesjogotag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_observacoesjogotagmencoes_jogadorid",
                table: "observacoesjogotagmencoes",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_observacoesjogotagmencoes_observacaojogotagid",
                table: "observacoesjogotagmencoes",
                column: "observacaojogotagid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observacoesjogotagmencoes");
        }
    }
}
