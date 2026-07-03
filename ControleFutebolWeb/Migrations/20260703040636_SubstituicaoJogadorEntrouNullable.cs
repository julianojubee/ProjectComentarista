using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class SubstituicaoJogadorEntrouNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_substituicoes_jogadores_jogadorentroud",
                table: "substituicoes");

            migrationBuilder.AlterColumn<int>(
                name: "jogadorentroud",
                table: "substituicoes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_substituicoes_jogadores_jogadorentroud",
                table: "substituicoes",
                column: "jogadorentroud",
                principalTable: "jogadores",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Limpa linhas corrompidas pelo fallback antigo do import: quando o
            // jogador que entrou não era resolvido, gravava-se o próprio jogador
            // que saiu como "entrou" (fulano entrou no lugar de fulano). Essas
            // linhas quebravam as setas ↑ da tela Analisar. Um "reimportar dados"
            // depois desta migration regrava a substituição corretamente.
            migrationBuilder.Sql(@"
UPDATE substituicoes
   SET jogadorentroud = NULL
 WHERE jogadorsaiuid IS NOT NULL
   AND jogadorentroud = jogadorsaiuid;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_substituicoes_jogadores_jogadorentroud",
                table: "substituicoes");

            migrationBuilder.AlterColumn<int>(
                name: "jogadorentroud",
                table: "substituicoes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_substituicoes_jogadores_jogadorentroud",
                table: "substituicoes",
                column: "jogadorentroud",
                principalTable: "jogadores",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
