using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddObservacaoJogoTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "observacoesjogotag",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: true),
                    texto = table.Column<string>(type: "text", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observacoesjogotag", x => x.id);
                    table.ForeignKey(
                        name: "FK_observacoesjogotag_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_observacoesjogotag_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_observacoesjogotag_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_observacoesjogotag_jogadorid_usuarioid",
                table: "observacoesjogotag",
                columns: new[] { "jogadorid", "usuarioid" });

            migrationBuilder.CreateIndex(
                name: "IX_observacoesjogotag_jogoid",
                table: "observacoesjogotag",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_observacoesjogotag_usuarioid",
                table: "observacoesjogotag",
                column: "usuarioid");

            // Migra os dados do esquema antigo: texto serializado "[CASA] .../[VISITANTE] ..."
            // em jogosanalisadosusuario.observacoes vira uma linha por tag na tabela nova.
            migrationBuilder.Sql(@"
                INSERT INTO observacoesjogotag (jogoid, usuarioid, tipo, texto, jogadorid, ordem)
                SELECT jau.jogoid, jau.usuarioid,
                       CASE WHEN upper(m[1]) = 'CASA' THEN 'MANDANTE' ELSE 'VISITANTE' END,
                       trim(m[2]),
                       NULL,
                       ln.ordinality
                FROM jogosanalisadosusuario jau
                CROSS JOIN LATERAL unnest(string_to_array(jau.observacoes, E'\n')) WITH ORDINALITY AS ln(linha, ordinality)
                CROSS JOIN LATERAL regexp_match(ln.linha, '^\[(CASA|VISITANTE)\]\s*(.*)$', 'i') AS m
                WHERE jau.observacoes IS NOT NULL AND jau.observacoes <> '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observacoesjogotag");
        }
    }
}
