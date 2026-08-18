using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddFonteAEstatisticaJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fonte",
                table: "estatisticasjogador",
                type: "text",
                nullable: false,
                defaultValue: "apifootball");

            // As linhas que já existiam vieram todas da api-football, exceto as poucas
            // que o fallback da ESPN gravou antes desta coluna existir. Essas são
            // reconhecíveis pela assinatura do que a ESPN não publica: o jogo inteiro
            // sem nenhum minuto e com passes, duelos, desarmes e passes-chave zerados.
            // A api-football pode não mandar minutos, mas nunca deixa os quatro em zero
            // no time todo — quem sofre falta, toca na bola.
            migrationBuilder.Sql("""
                UPDATE estatisticasjogador
                SET fonte = 'espn'
                WHERE jogoid IN (
                    SELECT jogoid FROM estatisticasjogador
                    GROUP BY jogoid
                    HAVING COUNT(minutos) = 0
                       AND SUM(passestotal) = 0
                       AND SUM(duelostotal) = 0
                       AND SUM(desarmes) = 0
                       AND SUM(passeschave) = 0
                );
                """);

            // O default existiu só para preencher as linhas antigas; o modelo já nasce
            // com FonteEstatistica.ApiFootball. Deixá-lo no banco divergiria do snapshot
            // e viraria migração fantasma na próxima alteração da tabela.
            migrationBuilder.Sql("ALTER TABLE estatisticasjogador ALTER COLUMN fonte DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fonte",
                table: "estatisticasjogador");
        }
    }
}
