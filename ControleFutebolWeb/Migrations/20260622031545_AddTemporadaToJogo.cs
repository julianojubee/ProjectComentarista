using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTemporadaToJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "temporada",
                table: "jogos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: usa a temporada do link da competição (apifoot:LEAGUE:SEASON)
            migrationBuilder.Sql(@"
                UPDATE jogos j
                SET temporada = CAST(split_part(c.linktransfermarket, ':', 3) AS INTEGER)
                FROM competicoes c
                WHERE j.competicaoid = c.id
                  AND c.linktransfermarket LIKE 'apifoot:%:%'
                  AND split_part(c.linktransfermarket, ':', 3) ~ '^[0-9]+$';
            ");

            // Para jogos sem link válido na competição, usa o ano da data do jogo
            migrationBuilder.Sql(@"
                UPDATE jogos
                SET temporada = EXTRACT(YEAR FROM data)::int
                WHERE temporada = 0 AND data IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "temporada",
                table: "jogos");
        }
    }
}
