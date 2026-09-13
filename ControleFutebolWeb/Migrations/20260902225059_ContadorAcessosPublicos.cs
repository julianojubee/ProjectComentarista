using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class ContadorAcessosPublicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acessospublicos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    ferramenta = table.Column<string>(type: "text", nullable: false),
                    dia = table.Column<DateOnly>(type: "date", nullable: false),
                    visitas = table.Column<int>(type: "integer", nullable: false),
                    visitantes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acessospublicos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_acessospublicos_ferramenta_dia",
                table: "acessospublicos",
                columns: new[] { "ferramenta", "dia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acessospublicos");
        }
    }
}
