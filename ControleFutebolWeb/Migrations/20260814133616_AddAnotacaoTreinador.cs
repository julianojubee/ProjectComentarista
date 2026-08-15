using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAnotacaoTreinador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anotacoestreinador",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    treinadorid = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    categoria = table.Column<string>(type: "text", nullable: true),
                    dtinc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dtalt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuarioid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anotacoestreinador", x => x.id);
                    table.ForeignKey(
                        name: "FK_anotacoestreinador_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_anotacoestreinador_treinadores_treinadorid",
                        column: x => x.treinadorid,
                        principalTable: "treinadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anotacoestreinador_treinadorid_usuarioid",
                table: "anotacoestreinador",
                columns: new[] { "treinadorid", "usuarioid" });

            migrationBuilder.CreateIndex(
                name: "IX_anotacoestreinador_usuarioid",
                table: "anotacoestreinador",
                column: "usuarioid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anotacoestreinador");
        }
    }
}
