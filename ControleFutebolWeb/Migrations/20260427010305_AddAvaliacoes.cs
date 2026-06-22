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
            // formacaoid nunca foi adicionada nas migrações anteriores num BD limpo — adiciona aqui
            migrationBuilder.Sql(@"
                ALTER TABLE timeescalacaopadrao ADD COLUMN IF NOT EXISTS formacaoid integer NOT NULL DEFAULT 0;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_constraint WHERE conname = 'FK_times_formacoes_formacaopadraoid') THEN
                        ALTER TABLE times DROP CONSTRAINT ""FK_times_formacoes_formacaopadraoid"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE times ALTER COLUMN formacaopadraoid SET NOT NULL;
                ALTER TABLE times ALTER COLUMN formacaopadraoid SET DEFAULT 0;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS notadetalhes (
                    id serial NOT NULL,
                    notaid integer NOT NULL,
                    acaoid text NOT NULL,
                    acaolabel text NOT NULL,
                    quantidade integer NOT NULL,
                    peso integer NOT NULL,
                    CONSTRAINT ""PK_notadetalhes"" PRIMARY KEY (id),
                    CONSTRAINT ""FK_notadetalhes_notas_notaid"" FOREIGN KEY (notaid) REFERENCES notas (id) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_indexes WHERE tablename = 'timeescalacaopadrao' AND indexname = 'IX_timeescalacaopadrao_formacaoid') THEN
                        CREATE INDEX ""IX_timeescalacaopadrao_formacaoid"" ON timeescalacaopadrao (formacaoid);
                    END IF;
                    IF NOT EXISTS (SELECT FROM pg_indexes WHERE tablename = 'notadetalhes' AND indexname = 'IX_notadetalhes_notaid') THEN
                        CREATE INDEX ""IX_notadetalhes_notaid"" ON notadetalhes (notaid);
                    END IF;
                    IF NOT EXISTS (SELECT FROM pg_constraint WHERE conname = 'FK_timeescalacaopadrao_formacoes_formacaoid') THEN
                        ALTER TABLE timeescalacaopadrao ADD CONSTRAINT ""FK_timeescalacaopadrao_formacoes_formacaoid""
                            FOREIGN KEY (formacaoid) REFERENCES formacoes (id) ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT FROM pg_constraint WHERE conname = 'FK_times_formacoes_formacaopadraoid') THEN
                        ALTER TABLE times ADD CONSTRAINT ""FK_times_formacoes_formacaopadraoid""
                            FOREIGN KEY (formacaopadraoid) REFERENCES formacoes (id) ON DELETE CASCADE;
                    END IF;
                END $$;
            ");
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
