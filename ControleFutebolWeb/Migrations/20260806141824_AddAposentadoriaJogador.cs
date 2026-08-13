using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAposentadoriaJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_times_timedestinoid",
                table: "transferencias");

            migrationBuilder.AlterColumn<int>(
                name: "timedestinoid",
                table: "transferencias",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "aposentado",
                table: "jogadores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "aposentadoem",
                table: "jogadores",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_times_timedestinoid",
                table: "transferencias",
                column: "timedestinoid",
                principalTable: "times",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_times_timedestinoid",
                table: "transferencias");

            migrationBuilder.DropColumn(
                name: "aposentado",
                table: "jogadores");

            migrationBuilder.DropColumn(
                name: "aposentadoem",
                table: "jogadores");

            migrationBuilder.AlterColumn<int>(
                name: "timedestinoid",
                table: "transferencias",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_times_timedestinoid",
                table: "transferencias",
                column: "timedestinoid",
                principalTable: "times",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
