using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddIndiceUnicoIdApiJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O indice so pode nascer depois que a base ficar limpa: producao ja tinha
            // pares duplicados vindos de importacoes simultaneas. Funde cada duplicata no
            // cadastro de menor id, repontando todas as FKs que apontam para jogadores;
            // quando o repontamento esbarra num indice unico (a linha equivalente ja existe
            // no cadastro que fica), o registro do duplicado e descartado.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    dup     RECORD;
    fk      RECORD;
    manter  integer;
    remover integer;
BEGIN
    FOR dup IN
        SELECT idapi FROM jogadores
        WHERE idapi IS NOT NULL
        GROUP BY idapi HAVING count(*) > 1
    LOOP
        SELECT min(id) INTO manter FROM jogadores WHERE idapi = dup.idapi;

        FOR remover IN
            SELECT id FROM jogadores WHERE idapi = dup.idapi AND id <> manter
        LOOP
            FOR fk IN
                SELECT c.conrelid::regclass::text AS tabela, a.attname AS coluna
                FROM pg_constraint c
                JOIN pg_attribute a
                  ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
                WHERE c.confrelid = 'jogadores'::regclass
                  AND c.contype = 'f'
                  AND array_length(c.conkey, 1) = 1
            LOOP
                BEGIN
                    EXECUTE format('UPDATE %s SET %I = $1 WHERE %I = $2',
                                   fk.tabela, fk.coluna, fk.coluna)
                    USING manter, remover;
                EXCEPTION WHEN unique_violation THEN
                    EXECUTE format('DELETE FROM %s WHERE %I = $1', fk.tabela, fk.coluna)
                    USING remover;
                END;
            END LOOP;

            DELETE FROM jogadores WHERE id = remover;
            RAISE NOTICE 'jogador duplicado % fundido em % (idapi %)', remover, manter, dup.idapi;
        END LOOP;
    END LOOP;
END $$;
");

            migrationBuilder.CreateIndex(
                name: "IX_jogadores_idapi",
                table: "jogadores",
                column: "idapi",
                unique: true,
                filter: "idapi IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_jogadores_idapi",
                table: "jogadores");
        }
    }
}
