using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeescalacaoPadrao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'timesescalacoespadrao') THEN
                        ALTER TABLE timesescalacoespadrao DROP CONSTRAINT IF EXISTS ""FK_timesescalacoespadrao_jogadores_jogadorid"";
                        ALTER TABLE timesescalacoespadrao DROP CONSTRAINT IF EXISTS ""FK_timesescalacoespadrao_times_timeid"";
                        ALTER TABLE timesescalacoespadrao DROP CONSTRAINT IF EXISTS ""PK_timesescalacoespadrao"";
                        ALTER TABLE timesescalacoespadrao RENAME TO timeescalacaopadrao;
                    END IF;
                    IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'timeescalacaopadrao') THEN
                        IF EXISTS (SELECT FROM pg_indexes WHERE tablename = 'timeescalacaopadrao' AND indexname = 'IX_timesescalacoespadrao_timeid') THEN
                            ALTER INDEX ""IX_timesescalacoespadrao_timeid"" RENAME TO ""IX_timeescalacaopadrao_timeid"";
                        END IF;
                        IF EXISTS (SELECT FROM pg_indexes WHERE tablename = 'timeescalacaopadrao' AND indexname = 'IX_timesescalacoespadrao_jogadorid') THEN
                            ALTER INDEX ""IX_timesescalacoespadrao_jogadorid"" RENAME TO ""IX_timeescalacaopadrao_jogadorid"";
                        END IF;
                        IF NOT EXISTS (SELECT FROM pg_constraint WHERE conname = 'PK_timeescalacaopadrao') THEN
                            ALTER TABLE timeescalacaopadrao ADD CONSTRAINT ""PK_timeescalacaopadrao"" PRIMARY KEY (id);
                        END IF;
                        IF NOT EXISTS (SELECT FROM pg_constraint WHERE conname = 'FK_timeescalacaopadrao_jogadores_jogadorid') THEN
                            ALTER TABLE timeescalacaopadrao ADD CONSTRAINT ""FK_timeescalacaopadrao_jogadores_jogadorid""
                                FOREIGN KEY (jogadorid) REFERENCES jogadores (id);
                        END IF;
                        IF NOT EXISTS (SELECT FROM pg_constraint WHERE conname = 'FK_timeescalacaopadrao_times_timeid') THEN
                            ALTER TABLE timeescalacaopadrao ADD CONSTRAINT ""FK_timeescalacaopadrao_times_timeid""
                                FOREIGN KEY (timeid) REFERENCES times (id) ON DELETE CASCADE;
                        END IF;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_timeescalacaopadrao_jogadores_jogadorid",
                table: "timeescalacaopadrao");

            migrationBuilder.DropForeignKey(
                name: "FK_timeescalacaopadrao_times_timeid",
                table: "timeescalacaopadrao");

            migrationBuilder.DropPrimaryKey(
                name: "PK_timeescalacaopadrao",
                table: "timeescalacaopadrao");

            migrationBuilder.RenameTable(
                name: "timeescalacaopadrao",
                newName: "timesescalacoespadrao");

            migrationBuilder.RenameIndex(
                name: "IX_timeescalacaopadrao_timeid",
                table: "timesescalacoespadrao",
                newName: "IX_timesescalacoespadrao_timeid");

            migrationBuilder.RenameIndex(
                name: "IX_timeescalacaopadrao_jogadorid",
                table: "timesescalacoespadrao",
                newName: "IX_timesescalacoespadrao_jogadorid");

            migrationBuilder.AddPrimaryKey(
                name: "PK_timesescalacoespadrao",
                table: "timesescalacoespadrao",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_timesescalacoespadrao_jogadores_jogadorid",
                table: "timesescalacoespadrao",
                column: "jogadorid",
                principalTable: "jogadores",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_timesescalacoespadrao_times_timeid",
                table: "timesescalacoespadrao",
                column: "timeid",
                principalTable: "times",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
