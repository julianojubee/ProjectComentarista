using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class BackfillJogadorPosicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Jogadores importados via api-football ficaram sem "jogadores.posicao"
            // preenchido (só "escalacoes.posicao" era gravado). Copia a posição da
            // escalação mais recente de cada jogador para corrigir os já existentes.
            migrationBuilder.Sql(@"
                UPDATE jogadores j
                SET posicao = sub.posicao
                FROM (
                    SELECT DISTINCT ON (e.jogadorid) e.jogadorid, e.posicao
                    FROM escalacoes e
                    WHERE e.jogadorid IS NOT NULL
                      AND e.posicao IS NOT NULL
                      AND e.posicao <> ''
                    ORDER BY e.jogadorid, e.id DESC
                ) sub
                WHERE j.id = sub.jogadorid
                  AND (j.posicao IS NULL OR j.posicao = '');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
