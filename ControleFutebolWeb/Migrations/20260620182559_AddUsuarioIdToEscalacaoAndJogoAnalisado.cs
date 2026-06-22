using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioIdToEscalacaoAndJogoAnalisado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "usuarioid",
                table: "escalacoes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "jogosanalisadosusuario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogosanalisadosusuario", x => x.id);
                    table.ForeignKey(
                        name: "FK_jogosanalisadosusuario_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jogosanalisadosusuario_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_escalacoes_usuarioid",
                table: "escalacoes",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "IX_jogosanalisadosusuario_jogoid",
                table: "jogosanalisadosusuario",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_jogosanalisadosusuario_usuarioid",
                table: "jogosanalisadosusuario",
                column: "usuarioid");

            migrationBuilder.AddForeignKey(
                name: "FK_escalacoes_aspnetusers_usuarioid",
                table: "escalacoes",
                column: "usuarioid",
                principalTable: "aspnetusers",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_escalacoes_aspnetusers_usuarioid",
                table: "escalacoes");

            migrationBuilder.DropTable(
                name: "jogosanalisadosusuario");

            migrationBuilder.DropIndex(
                name: "IX_escalacoes_usuarioid",
                table: "escalacoes");

            migrationBuilder.DropColumn(
                name: "usuarioid",
                table: "escalacoes");
        }
    }
}
