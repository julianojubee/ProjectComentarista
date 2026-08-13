using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltiConvertidoEEntrouDoBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "entroudobanco",
                table: "estatisticasjogador",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "penalticonvertido",
                table: "estatisticasjogador",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "entroudobanco",
                table: "estatisticasjogador");

            migrationBuilder.DropColumn(
                name: "penalticonvertido",
                table: "estatisticasjogador");
        }
    }
}
