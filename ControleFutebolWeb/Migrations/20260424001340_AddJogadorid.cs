using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddJogadorid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Cria a tabela se não existir (pode ter sido criada fora desta migração)
                    CREATE TABLE IF NOT EXISTS timesescalacoespadrao (
                        id serial NOT NULL,
                        timeid integer NOT NULL,
                        CONSTRAINT ""PK_timesescalacoespadrao"" PRIMARY KEY (id),
                        CONSTRAINT ""FK_timesescalacoespadrao_times_timeid"" FOREIGN KEY (timeid) REFERENCES times (id) ON DELETE CASCADE
                    );
                    IF NOT EXISTS (SELECT FROM pg_indexes WHERE tablename = 'timesescalacoespadrao' AND indexname = 'IX_timesescalacoespadrao_timeid') THEN
                        CREATE INDEX ""IX_timesescalacoespadrao_timeid"" ON timesescalacoespadrao (timeid);
                    END IF;
                    -- Adiciona jogadorid
                    ALTER TABLE timesescalacoespadrao ADD COLUMN IF NOT EXISTS jogadorid integer;
                    IF NOT EXISTS (SELECT FROM pg_indexes WHERE tablename = 'timesescalacoespadrao' AND indexname = 'IX_timesescalacoespadrao_jogadorid') THEN
                        CREATE INDEX ""IX_timesescalacoespadrao_jogadorid"" ON timesescalacoespadrao (jogadorid);
                    END IF;
                    IF NOT EXISTS (SELECT FROM pg_constraint WHERE conname = 'FK_timesescalacoespadrao_jogadores_jogadorid') THEN
                        ALTER TABLE timesescalacoespadrao ADD CONSTRAINT ""FK_timesescalacoespadrao_jogadores_jogadorid""
                            FOREIGN KEY (jogadorid) REFERENCES jogadores (id);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_timesescalacoespadrao_jogadores_jogadorid",
                table: "timesescalacoespadrao");

            migrationBuilder.DropIndex(
                name: "IX_timesescalacoespadrao_jogadorid",
                table: "timesescalacoespadrao");

            migrationBuilder.DropColumn(
                name: "jogadorid",
                table: "timesescalacoespadrao");
        }
    }
}
