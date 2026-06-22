using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioIdToCriterioAnotacaoAndCompeticaoTopTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "usuarioid",
                table: "criterionotas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usuarioid",
                table: "anotacoestime",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "competicoestoptierusuario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    competicaoid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competicoestoptierusuario", x => x.id);
                    table.ForeignKey(
                        name: "FK_competicoestoptierusuario_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_competicoestoptierusuario_competicoes_competicaoid",
                        column: x => x.competicaoid,
                        principalTable: "competicoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_criterionotas_usuarioid",
                table: "criterionotas",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "IX_anotacoestime_usuarioid",
                table: "anotacoestime",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "IX_competicoestoptierusuario_competicaoid",
                table: "competicoestoptierusuario",
                column: "competicaoid");

            migrationBuilder.CreateIndex(
                name: "IX_competicoestoptierusuario_usuarioid",
                table: "competicoestoptierusuario",
                column: "usuarioid");

            migrationBuilder.AddForeignKey(
                name: "FK_anotacoestime_aspnetusers_usuarioid",
                table: "anotacoestime",
                column: "usuarioid",
                principalTable: "aspnetusers",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_criterionotas_aspnetusers_usuarioid",
                table: "criterionotas",
                column: "usuarioid",
                principalTable: "aspnetusers",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_anotacoestime_aspnetusers_usuarioid",
                table: "anotacoestime");

            migrationBuilder.DropForeignKey(
                name: "FK_criterionotas_aspnetusers_usuarioid",
                table: "criterionotas");

            migrationBuilder.DropTable(
                name: "competicoestoptierusuario");

            migrationBuilder.DropIndex(
                name: "IX_criterionotas_usuarioid",
                table: "criterionotas");

            migrationBuilder.DropIndex(
                name: "IX_anotacoestime_usuarioid",
                table: "anotacoestime");

            migrationBuilder.DropColumn(
                name: "usuarioid",
                table: "criterionotas");

            migrationBuilder.DropColumn(
                name: "usuarioid",
                table: "anotacoestime");
        }
    }
}
