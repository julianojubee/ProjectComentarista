using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <summary>
    /// Correção de dados + trava: o mesmo fixture da api-football gravado duas vezes.
    ///
    /// O anti-duplicata da sincronização (ApiFootballService.IncluirOuAtualizarJogo)
    /// procura o jogo no banco antes de inserir, mas duas sincronizações rodando ao mesmo
    /// tempo não enxergam a linha uma da outra: as duas consultam, as duas não acham nada
    /// e as duas inserem. Foi assim que a Ligue 1 2025/26 ficou com 32 partidas repetidas —
    /// o PSG aparecia com 40 jogos na tabela contra 37 dos outros, e como a tabela só
    /// decide o campeão quando todos os times jogaram a mesma quantidade de partidas
    /// (TitulosHelper), o título do PSG simplesmente não era contado.
    ///
    /// Aqui as cópias são apagadas (fica a de menor id; escalações, gols e estatísticas
    /// da cópia saem junto por cascade) e o índice único passa a impedir a recorrência —
    /// numa corrida, o segundo INSERT falha em vez de duplicar a partida.
    /// </summary>
    public partial class RemoveJogosDuplicadosEIndiceUnicoFixture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM jogos
                WHERE id IN (
                    SELECT id FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (PARTITION BY linkdetalhes ORDER BY id) AS copia
                        FROM jogos
                        WHERE linkdetalhes IS NOT NULL AND linkdetalhes <> ''
                    ) ranqueado
                    WHERE copia > 1
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_jogos_linkdetalhes",
                table: "jogos",
                column: "linkdetalhes",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // As partidas apagadas não voltam: eram cópias idênticas de jogos que
            // continuam no banco, e recriá-las seria repor exatamente o dado errado.
            migrationBuilder.DropIndex(
                name: "IX_jogos_linkdetalhes",
                table: "jogos");
        }
    }
}
