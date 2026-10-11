using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class DecodificarEntidadesHtmlNomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A api-football mandava alguns textos com entidade HTML ("N. O&apos;Reilly",
            // "Stadio Renato Dall&apos;Ara") e eles foram gravados assim. A importação
            // agora decodifica na leitura (AfHtmlDecodeStringConverter); aqui corrige o
            // que já estava no banco.
            migrationBuilder.Sql("""
                UPDATE jogadores SET nome = replace(nome, '&apos;', '''') WHERE nome LIKE '%&apos;%';
                UPDATE jogos SET estadio = replace(estadio, '&apos;', '''') WHERE estadio LIKE '%&apos;%';
                UPDATE jogos SET arbitro = replace(arbitro, '&apos;', '''') WHERE arbitro LIKE '%&apos;%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: não dá para saber quais apóstrofos vieram codificados.
        }
    }
}
