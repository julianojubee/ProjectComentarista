using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class ModuloCampeonatosProprios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "modulos",
                table: "aspnetusers",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "campeonatos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    modalidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    plataforma = table.Column<string>(type: "text", nullable: true),
                    logourl = table.Column<string>(type: "text", nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criteriosdesempate = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    datainicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    encerradoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tokenpublico = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campeonatos", x => x.id);
                    table.ForeignKey(
                        name: "FK_campeonatos_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jogadoresproprios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    apelido = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    posicao = table.Column<string>(type: "text", nullable: true),
                    numerocamisa = table.Column<int>(type: "integer", nullable: true),
                    datanascimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fotourl = table.Column<string>(type: "text", nullable: true),
                    nacionalidadeid = table.Column<int>(type: "integer", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jogadoresproprios", x => x.id);
                    table.ForeignKey(
                        name: "FK_jogadoresproprios_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jogadoresproprios_nacionalidades_nacionalidadeid",
                        column: x => x.nacionalidadeid,
                        principalTable: "nacionalidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "timesproprios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    usuarioid = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    sigla = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    cidade = table.Column<string>(type: "text", nullable: true),
                    escudourl = table.Column<string>(type: "text", nullable: true),
                    corprincipal = table.Column<string>(type: "text", nullable: true),
                    corsecundaria = table.Column<string>(type: "text", nullable: true),
                    criadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    arquivadoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timesproprios", x => x.id);
                    table.ForeignKey(
                        name: "FK_timesproprios_aspnetusers_usuarioid",
                        column: x => x.usuarioid,
                        principalTable: "aspnetusers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campeonatofases",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    campeonatoid = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    idaevolta = table.Column<bool>(type: "boolean", nullable: false),
                    classificados = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campeonatofases", x => x.id);
                    table.ForeignKey(
                        name: "FK_campeonatofases_campeonatos_campeonatoid",
                        column: x => x.campeonatoid,
                        principalTable: "campeonatos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campeonatoparticipantes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    campeonatoid = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: true),
                    timeproprioid = table.Column<int>(type: "integer", nullable: true),
                    timeid = table.Column<int>(type: "integer", nullable: true),
                    nometimesnapshot = table.Column<string>(type: "text", nullable: true),
                    escudourlsnapshot = table.Column<string>(type: "text", nullable: true),
                    grupo = table.Column<string>(type: "text", nullable: true),
                    semente = table.Column<int>(type: "integer", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campeonatoparticipantes", x => x.id);
                    table.CheckConstraint("ck_campeonatoparticipantes_no_maximo_um_time", "num_nonnulls(timeproprioid, timeid) <= 1");
                    table.ForeignKey(
                        name: "FK_campeonatoparticipantes_campeonatos_campeonatoid",
                        column: x => x.campeonatoid,
                        principalTable: "campeonatos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_campeonatoparticipantes_times_timeid",
                        column: x => x.timeid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_campeonatoparticipantes_timesproprios_timeproprioid",
                        column: x => x.timeproprioid,
                        principalTable: "timesproprios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "itenselenco",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    timeproprioid = table.Column<int>(type: "integer", nullable: false),
                    jogadorid = table.Column<int>(type: "integer", nullable: true),
                    jogadorproprioid = table.Column<int>(type: "integer", nullable: true),
                    nomesnapshot = table.Column<string>(type: "text", nullable: false),
                    fotourlsnapshot = table.Column<string>(type: "text", nullable: true),
                    posicao = table.Column<string>(type: "text", nullable: true),
                    numerocamisa = table.Column<int>(type: "integer", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    incluidoem = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itenselenco", x => x.id);
                    table.CheckConstraint("ck_itenselenco_um_jogador", "num_nonnulls(jogadorid, jogadorproprioid) = 1");
                    table.ForeignKey(
                        name: "FK_itenselenco_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_itenselenco_jogadoresproprios_jogadorproprioid",
                        column: x => x.jogadorproprioid,
                        principalTable: "jogadoresproprios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_itenselenco_timesproprios_timeproprioid",
                        column: x => x.timeproprioid,
                        principalTable: "timesproprios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "partidascampeonato",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    campeonatoid = table.Column<int>(type: "integer", nullable: false),
                    faseid = table.Column<int>(type: "integer", nullable: true),
                    rodada = table.Column<int>(type: "integer", nullable: false),
                    grupo = table.Column<string>(type: "text", nullable: true),
                    chaveordem = table.Column<int>(type: "integer", nullable: true),
                    participantecasaid = table.Column<int>(type: "integer", nullable: true),
                    participantevisitanteid = table.Column<int>(type: "integer", nullable: true),
                    timecasausadoid = table.Column<int>(type: "integer", nullable: true),
                    timevisitanteusadoid = table.Column<int>(type: "integer", nullable: true),
                    placarcasa = table.Column<int>(type: "integer", nullable: true),
                    placarvisitante = table.Column<int>(type: "integer", nullable: true),
                    penaltiscasa = table.Column<int>(type: "integer", nullable: true),
                    penaltisvisitante = table.Column<int>(type: "integer", nullable: true),
                    wo = table.Column<bool>(type: "boolean", nullable: false),
                    data = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    local = table.Column<string>(type: "text", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partidascampeonato", x => x.id);
                    table.ForeignKey(
                        name: "FK_partidascampeonato_campeonatofases_faseid",
                        column: x => x.faseid,
                        principalTable: "campeonatofases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_partidascampeonato_campeonatoparticipantes_participantecasa~",
                        column: x => x.participantecasaid,
                        principalTable: "campeonatoparticipantes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_partidascampeonato_campeonatoparticipantes_participantevisi~",
                        column: x => x.participantevisitanteid,
                        principalTable: "campeonatoparticipantes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_partidascampeonato_campeonatos_campeonatoid",
                        column: x => x.campeonatoid,
                        principalTable: "campeonatos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_partidascampeonato_times_timecasausadoid",
                        column: x => x.timecasausadoid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_partidascampeonato_times_timevisitanteusadoid",
                        column: x => x.timevisitanteusadoid,
                        principalTable: "times",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "eventospartidacampeonato",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    partidaid = table.Column<int>(type: "integer", nullable: false),
                    participanteid = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    minuto = table.Column<int>(type: "integer", nullable: true),
                    jogadorid = table.Column<int>(type: "integer", nullable: true),
                    jogadorproprioid = table.Column<int>(type: "integer", nullable: true),
                    nomesnapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    golid = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventospartidacampeonato", x => x.id);
                    table.CheckConstraint("ck_eventospartidacampeonato_no_maximo_um_jogador", "num_nonnulls(jogadorid, jogadorproprioid) <= 1");
                    table.ForeignKey(
                        name: "FK_eventospartidacampeonato_campeonatoparticipantes_participan~",
                        column: x => x.participanteid,
                        principalTable: "campeonatoparticipantes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_eventospartidacampeonato_eventospartidacampeonato_golid",
                        column: x => x.golid,
                        principalTable: "eventospartidacampeonato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_eventospartidacampeonato_jogadores_jogadorid",
                        column: x => x.jogadorid,
                        principalTable: "jogadores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_eventospartidacampeonato_jogadoresproprios_jogadorproprioid",
                        column: x => x.jogadorproprioid,
                        principalTable: "jogadoresproprios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_eventospartidacampeonato_partidascampeonato_partidaid",
                        column: x => x.partidaid,
                        principalTable: "partidascampeonato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campeonatofases_campeonatoid_ordem",
                table: "campeonatofases",
                columns: new[] { "campeonatoid", "ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_campeonatoparticipantes_campeonatoid",
                table: "campeonatoparticipantes",
                column: "campeonatoid");

            migrationBuilder.CreateIndex(
                name: "IX_campeonatoparticipantes_timeid",
                table: "campeonatoparticipantes",
                column: "timeid");

            migrationBuilder.CreateIndex(
                name: "IX_campeonatoparticipantes_timeproprioid",
                table: "campeonatoparticipantes",
                column: "timeproprioid");

            migrationBuilder.CreateIndex(
                name: "IX_campeonatos_tokenpublico",
                table: "campeonatos",
                column: "tokenpublico",
                unique: true,
                filter: "tokenpublico IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_campeonatos_usuarioid",
                table: "campeonatos",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "IX_eventospartidacampeonato_golid",
                table: "eventospartidacampeonato",
                column: "golid");

            migrationBuilder.CreateIndex(
                name: "IX_eventospartidacampeonato_jogadorid",
                table: "eventospartidacampeonato",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_eventospartidacampeonato_jogadorproprioid",
                table: "eventospartidacampeonato",
                column: "jogadorproprioid");

            migrationBuilder.CreateIndex(
                name: "IX_eventospartidacampeonato_participanteid",
                table: "eventospartidacampeonato",
                column: "participanteid");

            migrationBuilder.CreateIndex(
                name: "IX_eventospartidacampeonato_partidaid",
                table: "eventospartidacampeonato",
                column: "partidaid");

            migrationBuilder.CreateIndex(
                name: "IX_itenselenco_jogadorid",
                table: "itenselenco",
                column: "jogadorid");

            migrationBuilder.CreateIndex(
                name: "IX_itenselenco_jogadorproprioid",
                table: "itenselenco",
                column: "jogadorproprioid");

            migrationBuilder.CreateIndex(
                name: "IX_itenselenco_timeproprioid",
                table: "itenselenco",
                column: "timeproprioid");

            migrationBuilder.CreateIndex(
                name: "IX_itenselenco_timeproprioid_jogadorid",
                table: "itenselenco",
                columns: new[] { "timeproprioid", "jogadorid" },
                unique: true,
                filter: "jogadorid IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_itenselenco_timeproprioid_jogadorproprioid",
                table: "itenselenco",
                columns: new[] { "timeproprioid", "jogadorproprioid" },
                unique: true,
                filter: "jogadorproprioid IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_jogadoresproprios_nacionalidadeid",
                table: "jogadoresproprios",
                column: "nacionalidadeid");

            migrationBuilder.CreateIndex(
                name: "IX_jogadoresproprios_usuarioid",
                table: "jogadoresproprios",
                column: "usuarioid");

            migrationBuilder.CreateIndex(
                name: "IX_partidascampeonato_campeonatoid_rodada",
                table: "partidascampeonato",
                columns: new[] { "campeonatoid", "rodada" });

            migrationBuilder.CreateIndex(
                name: "IX_partidascampeonato_faseid",
                table: "partidascampeonato",
                column: "faseid");

            migrationBuilder.CreateIndex(
                name: "IX_partidascampeonato_participantecasaid",
                table: "partidascampeonato",
                column: "participantecasaid");

            migrationBuilder.CreateIndex(
                name: "IX_partidascampeonato_participantevisitanteid",
                table: "partidascampeonato",
                column: "participantevisitanteid");

            migrationBuilder.CreateIndex(
                name: "IX_partidascampeonato_timecasausadoid",
                table: "partidascampeonato",
                column: "timecasausadoid");

            migrationBuilder.CreateIndex(
                name: "IX_partidascampeonato_timevisitanteusadoid",
                table: "partidascampeonato",
                column: "timevisitanteusadoid");

            migrationBuilder.CreateIndex(
                name: "IX_timesproprios_usuarioid",
                table: "timesproprios",
                column: "usuarioid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventospartidacampeonato");

            migrationBuilder.DropTable(
                name: "itenselenco");

            migrationBuilder.DropTable(
                name: "partidascampeonato");

            migrationBuilder.DropTable(
                name: "jogadoresproprios");

            migrationBuilder.DropTable(
                name: "campeonatofases");

            migrationBuilder.DropTable(
                name: "campeonatoparticipantes");

            migrationBuilder.DropTable(
                name: "campeonatos");

            migrationBuilder.DropTable(
                name: "timesproprios");

            migrationBuilder.DropColumn(
                name: "modulos",
                table: "aspnetusers");
        }
    }
}
