using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddEstatisticaJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "estatisticasjogador",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    minutos = table.Column<int>(type: "integer", nullable: true),
                    rating = table.Column<double>(type: "double precision", nullable: true),
                    offsides = table.Column<int>(type: "integer", nullable: false),
                    finalizacoestotal = table.Column<int>(type: "integer", nullable: false),
                    finalizacoesnogol = table.Column<int>(type: "integer", nullable: false),
                    gols = table.Column<int>(type: "integer", nullable: false),
                    golssofridos = table.Column<int>(type: "integer", nullable: false),
                    assistencias = table.Column<int>(type: "integer", nullable: false),
                    defesas = table.Column<int>(type: "integer", nullable: false),
                    passestotal = table.Column<int>(type: "integer", nullable: false),
                    passeschave = table.Column<int>(type: "integer", nullable: false),
                    desarmes = table.Column<int>(type: "integer", nullable: false),
                    bloqueios = table.Column<int>(type: "integer", nullable: false),
                    interceptacoes = table.Column<int>(type: "integer", nullable: false),
                    duelostotal = table.Column<int>(type: "integer", nullable: false),
                    duelosvencidos = table.Column<int>(type: "integer", nullable: false),
                    driblestentados = table.Column<int>(type: "integer", nullable: false),
                    driblescertos = table.Column<int>(type: "integer", nullable: false),
                    driblessofridos = table.Column<int>(type: "integer", nullable: false),
                    faltassofridas = table.Column<int>(type: "integer", nullable: false),
                    faltascometidas = table.Column<int>(type: "integer", nullable: false),
                    cartoesamarelos = table.Column<int>(type: "integer", nullable: false),
                    cartoesvermelhos = table.Column<int>(type: "integer", nullable: false),
                    penaltisofrido = table.Column<int>(type: "integer", nullable: false),
                    penalticometido = table.Column<int>(type: "integer", nullable: false),
                    penaltiperdido = table.Column<int>(type: "integer", nullable: false),
                    penaltidefendido = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estatisticasjogador", x => x.id);
                    table.ForeignKey(
                        name: "FK_estatisticasjogador_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_estatisticasjogador_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_estatisticasjogador_jogadorid",
                table: "estatisticasjogador",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_estatisticasjogador_jogoid",
                table: "estatisticasjogador",
                column: "jogoid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estatisticasjogador");
        }
    }
}
