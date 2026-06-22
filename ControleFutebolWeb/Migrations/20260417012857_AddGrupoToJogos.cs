using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupoToJogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grupo",
                table: "jogos",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Cria a tabela competicoes se não existir (pode não ter sido criada em BD limpo)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS competicoes (
                    id serial NOT NULL,
                    nome text NOT NULL DEFAULT '',
                    regiao text NOT NULL DEFAULT '',
                    tipo text NOT NULL DEFAULT '',
                    CONSTRAINT ""PK_competicoes"" PRIMARY KEY (id)
                );
            ");

            // Adiciona coluna tipo se não existir (para BD existente sem essa coluna)
            migrationBuilder.Sql(@"
                ALTER TABLE competicoes ADD COLUMN IF NOT EXISTS tipo text NOT NULL DEFAULT '';
                DELETE FROM competicoes WHERE id IN (1, 2, 3);
            ");

            // competicaoid nunca foi adicionado ao jogos em nenhuma migração anterior
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    ALTER TABLE jogos ADD COLUMN IF NOT EXISTS competicaoid integer NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT FROM pg_indexes WHERE tablename = 'jogos' AND indexname = 'IX_jogos_competicaoid') THEN
                        CREATE INDEX ""IX_jogos_competicaoid"" ON jogos (competicaoid);
                    END IF;
                    IF NOT EXISTS (SELECT FROM pg_constraint WHERE conname = 'FK_jogos_competicoes_competicaoid') THEN
                        ALTER TABLE jogos ADD CONSTRAINT ""FK_jogos_competicoes_competicaoid""
                            FOREIGN KEY (competicaoid) REFERENCES competicoes (id) ON DELETE CASCADE;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grupo",
                table: "jogos");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "competicoes");

            migrationBuilder.InsertData(
                table: "competicoes",
                columns: new[] { "id", "nome", "regiao" },
                values: new object[,]
                {
                    { 1, "Copa Libertadores", "América do Sul" },
                    { 2, "Brasileirão Série A", "Brasil" },
                    { 3, "Copa do Brasil", "Brasil" }
                });
        }
    }
}
