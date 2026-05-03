using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddmigrationCampoIdade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "importacaologs");

            migrationBuilder.AddColumn<bool>(
                name: "atualizado",
                table: "jogadores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "idadetransfermarkt",
                table: "jogadores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idapi",
                table: "jogadores",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "atualizado",
                table: "jogadores");

            migrationBuilder.DropColumn(
                name: "idadetransfermarkt",
                table: "jogadores");

            migrationBuilder.DropColumn(
                name: "idapi",
                table: "jogadores");

            migrationBuilder.CreateTable(
                name: "importacaologs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    competicaoid = table.Column<int>(type: "integer", nullable: false),
                    acao = table.Column<string>(type: "text", nullable: false),
                    dataimportacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    nometimeapi = table.Column<string>(type: "text", nullable: false),
                    nometimebanco = table.Column<string>(type: "text", nullable: true),
                    observacao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_importacaologs", x => x.id);
                    table.ForeignKey(
                        name: "FK_importacaologs_competicoes_competicaoid",
                        column: x => x.competicaoid,
                        principalTable: "competicoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_importacaologs_competicaoid",
                table: "importacaologs",
                column: "competicaoid");
        }
    }
}
