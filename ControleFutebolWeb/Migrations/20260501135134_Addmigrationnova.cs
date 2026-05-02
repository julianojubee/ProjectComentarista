using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class Addmigrationnova : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "eventkey",
                table: "jogos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "jogos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "importacaologs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    dataimportacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    competicaoid = table.Column<int>(type: "integer", nullable: false),
                    nometimeapi = table.Column<string>(type: "text", nullable: false),
                    nometimebanco = table.Column<string>(type: "text", nullable: true),
                    acao = table.Column<string>(type: "text", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "importacaologs");

            migrationBuilder.DropColumn(
                name: "eventkey",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "status",
                table: "jogos");
        }
    }
}
