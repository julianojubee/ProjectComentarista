using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "formacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formacoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nacionalidades",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nacionalidades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "times",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    nome = table.Column<string>(type: "text", nullable: false),
                    cidade = table.Column<string>(type: "text", nullable: false),
                    escudourl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_times", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "posicoesformacao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    nomeposicao = table.Column<string>(type: "text", nullable: false),
                    posicaox = table.Column<double>(type: "double precision", nullable: false),
                    posicaoy = table.Column<double>(type: "double precision", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    formacaoid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posicoesformacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_posicoesformacao_formacoes_formacaoid",
                        column: x => x.formacaoid,
                        principalTable: "formacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jogadores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    nome = table.Column<string>(type: "text", nullable: false),
                    posicao = table.Column<string>(type: "text", nullable: false),
                    datanascimento = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    numerocamisa = table.Column<int>(type: "integer", nullable: true),
                    nacionalidadeid = table.Column<int>(type: "integer", nullable: true),
                    timeid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogadores", x => x.id);
                    table.ForeignKey(
                        name: "FK_jogadores_nacionalidades_nacionalidadeid",
                        column: x => x.nacionalidadeid,
                        principalTable: "nacionalidades",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_jogadores_times_timeid",
                        column: x => x.timeid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jogos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    rodada = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    timecasaid = table.Column<int>(type: "integer", nullable: false),
                    placarcasa = table.Column<int>(type: "integer", nullable: false),
                    placarvisitante = table.Column<int>(type: "integer", nullable: false),
                    timevisitanteid = table.Column<int>(type: "integer", nullable: false),
                    formacaocasaid = table.Column<int>(type: "integer", nullable: false),
                    formacaovisitanteid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogos", x => x.id);
                    table.ForeignKey(
                        name: "FK_jogos_formacoes_formacaocasaid",
                        column: x => x.formacaocasaid,
                        principalTable: "formacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jogos_formacoes_formacaovisitanteid",
                        column: x => x.formacaovisitanteid,
                        principalTable: "formacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jogos_times_timecasaid",
                        column: x => x.timecasaid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jogos_times_timevisitanteid",
                        column: x => x.timevisitanteid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cartoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    minuto = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cartoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_cartoes_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cartoes_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "escalacoes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    titular = table.Column<bool>(type: "boolean", nullable: false),
                    posicao = table.Column<string>(type: "text", nullable: false),
                    istimecasa = table.Column<bool>(type: "boolean", nullable: false),
                    posicaox = table.Column<double>(type: "double precision", nullable: false),
                    posicaoy = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_escalacoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_escalacoes_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_escalacoes_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gols",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    jogoid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    minuto = table.Column<int>(type: "integer", nullable: false),
                    contra = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gols", x => x.id);
                    table.ForeignKey(
                        name: "FK_gols_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gols_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    valor = table.Column<int>(type: "integer", nullable: false),
                    comentario = table.Column<string>(type: "text", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: false),
                    jogoid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notas", x => x.id);
                    table.ForeignKey(
                        name: "FK_notas_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notas_jogos_jogoid",
                        column: x => x.jogoid,
                        principalTable: "jogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "nacionalidades",
                columns: new[] { "id", "nome" },
                values: new object[,]
                {
                    { 1, "Brasil" },
                    { 2, "Argentina" },
                    { 3, "França" },
                    { 4, "Alemanha" },
                    { 5, "Itália" },
                    { 6, "Espanha" },
                    { 7, "Portugal" },
                    { 8, "Uruguai" },
                    { 9, "Chile" },
                    { 10, "Paraguai" },
                    { 11, "Bolívia" },
                    { 12, "Peru" },
                    { 13, "Equador" },
                    { 14, "Colômbia" },
                    { 15, "Venezuela" },
                    { 16, "Guiana" },
                    { 17, "Suriname" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_cartoes_jogadorid",
                table: "cartoes",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_cartoes_jogoid",
                table: "cartoes",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_escalacoes_jogadorid",
                table: "escalacoes",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_escalacoes_jogoid",
                table: "escalacoes",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_gols_jogadorid",
                table: "gols",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_gols_jogoid",
                table: "gols",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_jogadores_nacionalidadeid",
                table: "jogadores",
                column: "nacionalidadeid");

            migrationBuilder.CreateIndex(
                name: "IX_jogadores_timeid",
                table: "jogadores",
                column: "timeid");

            migrationBuilder.CreateIndex(
                name: "IX_jogos_formacaocasaid",
                table: "jogos",
                column: "formacaocasaid");

            migrationBuilder.CreateIndex(
                name: "IX_jogos_formacaovisitanteid",
                table: "jogos",
                column: "formacaovisitanteid");

            migrationBuilder.CreateIndex(
                name: "IX_jogos_timecasaid",
                table: "jogos",
                column: "timecasaid");

            migrationBuilder.CreateIndex(
                name: "IX_jogos_timevisitanteid",
                table: "jogos",
                column: "timevisitanteid");

            migrationBuilder.CreateIndex(
                name: "IX_notas_jogadorid",
                table: "notas",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_notas_jogoid",
                table: "notas",
                column: "jogoid");

            migrationBuilder.CreateIndex(
                name: "IX_posicoesformacao_formacaoid",
                table: "posicoesformacao",
                column: "formacaoid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cartoes");

            migrationBuilder.DropTable(
                name: "escalacoes");

            migrationBuilder.DropTable(
                name: "gols");

            migrationBuilder.DropTable(
                name: "notas");

            migrationBuilder.DropTable(
                name: "posicoesformacao");

            migrationBuilder.DropTable(
                name: "jogadores");

            migrationBuilder.DropTable(
                name: "jogos");

            migrationBuilder.DropTable(
                name: "nacionalidades");

            migrationBuilder.DropTable(
                name: "formacoes");

            migrationBuilder.DropTable(
                name: "times");
        }
    }
}
