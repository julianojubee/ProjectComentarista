using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAnotacaoCompeticao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anotacoescompeticao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    competicaoid = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    categoria = table.Column<string>(type: "text", nullable: true),
                    dtinc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dtalt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuarioid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anotacoescompeticao", x => x.id);
                    table.ForeignKey(
                        name: "FK_anotacoescompeticao_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_anotacoescompeticao_competicoes_competicaoid",
                        column: x => x.competicaoid,
                        principalTable: "competicoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anotacoescompeticao_competicaoid_usuarioid",
                table: "anotacoescompeticao",
                columns: new[] { "competicaoid", "usuarioid" });

            migrationBuilder.CreateIndex(
                name: "IX_anotacoescompeticao_usuarioid",
                table: "anotacoescompeticao",
                column: "usuarioid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anotacoescompeticao");
        }
    }
}
