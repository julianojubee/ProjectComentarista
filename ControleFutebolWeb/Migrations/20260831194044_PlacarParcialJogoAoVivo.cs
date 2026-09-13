using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class PlacarParcialJogoAoVivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "placarparcialcasa",
                table: "jogos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "placarparcialem",
                table: "jogos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "placarparcialvisitante",
                table: "jogos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "placarparcialcasa",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "placarparcialem",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "placarparcialvisitante",
                table: "jogos");
        }
    }
}
