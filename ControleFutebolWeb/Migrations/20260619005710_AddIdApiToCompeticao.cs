using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddIdApiToCompeticao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Usa SQL direto para ser idempotente (coluna pode já ter sido adicionada antes de um crash)
            migrationBuilder.Sql("ALTER TABLE competicoes ADD COLUMN IF NOT EXISTS idapi integer;");

            // criterionotas já existia no banco — criação omitida intencionalmente
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "idapi",
                table: "competicoes");
        }
    }
}
