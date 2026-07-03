using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class IndicesDesempenhoBusca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_jogos_data",
                table: "jogos",
                column: "data");

            // Índices trigram (pg_trgm) para a busca do autocomplete (Home/Buscar),
            // que usa ILIKE '%q%' — B-tree não atende curinga à esquerda; GIN+trgm sim.
            // Criar extensão exige privilégio; se o usuário do banco não tiver, apenas
            // registra um NOTICE e segue sem os índices (a busca continua funcionando,
            // só não acelera).
            migrationBuilder.Sql(@"
DO $$
BEGIN
    CREATE EXTENSION IF NOT EXISTS pg_trgm;
EXCEPTION WHEN insufficient_privilege THEN
    RAISE NOTICE 'Sem privilégio para criar a extensão pg_trgm; índices trigram não serão criados.';
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
        CREATE INDEX IF NOT EXISTS ix_times_nome_trgm       ON times       USING gin (nome gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_jogadores_nome_trgm   ON jogadores   USING gin (nome gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_competicoes_nome_trgm ON competicoes USING gin (nome gin_trgm_ops);
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_jogos_data",
                table: "jogos");

            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ix_times_nome_trgm;
DROP INDEX IF EXISTS ix_jogadores_nome_trgm;
DROP INDEX IF EXISTS ix_competicoes_nome_trgm;
");
        }
    }
}
