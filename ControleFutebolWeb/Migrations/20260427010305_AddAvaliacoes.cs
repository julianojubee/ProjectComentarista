using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAvaliacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_times_formacoes_formacaopadraoid",
                table: "times");

            migrationBuilder.AlterColumn<int>(
                name: "formacaopadraoid",
                table: "times",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "notadetalhes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    notaid = table.Column<int>(type: "integer", nullable: false),
                    acaoid = table.Column<string>(type: "text", nullable: false),
                    acaolabel = table.Column<string>(type: "text", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    peso = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notadetalhes", x => x.id);
                    table.ForeignKey(
                        name: "FK_notadetalhes_notas_notaid",
                        column: x => x.notaid,
                        principalTable: "notas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_timeescalacaopadrao_formacaoid",
                table: "timeescalacaopadrao",
                column: "formacaoid");

            migrationBuilder.CreateIndex(
                name: "IX_notadetalhes_notaid",
                table: "notadetalhes",
                column: "notaid");

            migrationBuilder.AddForeignKey(
                name: "FK_timeescalacaopadrao_formacoes_formacaoid",
                table: "timeescalacaopadrao",
                column: "formacaoid",
                principalTable: "formacoes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_times_formacoes_formacaopadraoid",
                table: "times",
                column: "formacaopadraoid",
                principalTable: "formacoes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_timeescalacaopadrao_formacoes_formacaoid",
                table: "timeescalacaopadrao");

            migrationBuilder.DropForeignKey(
                name: "FK_times_formacoes_formacaopadraoid",
                table: "times");

            migrationBuilder.DropTable(
                name: "notadetalhes");

            migrationBuilder.DropIndex(
                name: "IX_timeescalacaopadrao_formacaoid",
                table: "timeescalacaopadrao");

            migrationBuilder.DropColumn(
                name: "formacaoid",
                table: "timeescalacaopadrao");

            migrationBuilder.AlterColumn<int>(
                name: "formacaopadraoid",
                table: "times",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_times_formacoes_formacaopadraoid",
                table: "times",
                column: "formacaopadraoid",
                principalTable: "formacoes",
                principalColumn: "id");
        }
    }
}
