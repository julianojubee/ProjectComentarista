using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <summary>
    /// Correção de dados: passagens de treinador salvas sem data de fim mesmo já tendo
    /// terminado. A api-football devolve end = null tanto para o trabalho atual quanto,
    /// às vezes, para passagens antigas que ela não fechou — e o sistema mostrava o técnico
    /// dirigindo dois clubes ao mesmo tempo (ex.: Fernando Diniz no Corinthians e no Vasco).
    ///
    /// A importação já não grava mais assim (ver TreinadorHistoricoNormalizador), mas as
    /// linhas gravadas antes disso continuariam erradas — o dedupe da importação nunca
    /// reescreve uma passagem que já existe. Esta migração fecha as que ficaram abertas
    /// indevidamente, usando o início da passagem seguinte como data de saída.
    ///
    /// Só a passagem mais recente de cada treinador pode continuar aberta.
    /// </summary>
    public partial class FechaPassagensTreinadorSobrepostas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE treinadoreshistorico h
                SET dtfim = (
                    SELECT MIN(seguinte.dtinicio)
                    FROM treinadoreshistorico seguinte
                    WHERE seguinte.treinadorid = h.treinadorid
                      AND seguinte.dtinicio > h.dtinicio
                )
                WHERE h.dtfim IS NULL
                  AND EXISTS (
                    SELECT 1
                    FROM treinadoreshistorico seguinte
                    WHERE seguinte.treinadorid = h.treinadorid
                      AND seguinte.dtinicio > h.dtinicio
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: reabrir as passagens recriaria exatamente o dado errado que esta
            // migração veio consertar, e as datas originais (null) não distinguem quais
            // linhas foram tocadas aqui.
        }
    }
}
