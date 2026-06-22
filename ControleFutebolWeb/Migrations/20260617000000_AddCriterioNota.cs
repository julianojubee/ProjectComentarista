using ControleFutebolWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    [DbContext(typeof(FutebolContext))]
    [Migration("20260617000000_AddCriterioNota")]
    public partial class AddCriterioNota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS criterionotas (
                    id      SERIAL PRIMARY KEY,
                    acaoid  TEXT NOT NULL DEFAULT '',
                    label   TEXT NOT NULL DEFAULT '',
                    peso    DOUBLE PRECISION NOT NULL DEFAULT 0,
                    ativo   BOOLEAN NOT NULL DEFAULT TRUE,
                    ordem   INTEGER NOT NULL DEFAULT 0
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO criterionotas (acaoid, label, peso, ativo, ordem) VALUES
                ('offside',           'Impedimento',         -0.1,  TRUE,  1),
                ('finalizacao',       'Finalização',          0.1,  TRUE,  2),
                ('finalizacao_gol',   'Finalização no alvo',  0.2,  TRUE,  3),
                ('gol',               'Gol',                  2.0,  TRUE,  4),
                ('gol_sofrido',       'Gol sofrido',         -1.0,  TRUE,  5),
                ('assistencia',       'Assistência',          1.0,  TRUE,  6),
                ('defesa',            'Defesa (goleiro)',      0.5,  TRUE,  7),
                ('passe_chave',       'Passe-chave',          0.5,  TRUE,  8),
                ('desarme',           'Desarme',              0.1,  TRUE,  9),
                ('bloqueio',          'Bloqueio',             0.1,  TRUE, 10),
                ('interceptacao',     'Interceptação',        0.1,  TRUE, 11),
                ('duelo_vencido',     'Duelo vencido',        0.1,  TRUE, 12),
                ('drible_certo',      'Drible certo',         0.1,  TRUE, 13),
                ('drible_sofrido',    'Drible sofrido',      -0.1,  TRUE, 14),
                ('falta_sofrida',     'Falta sofrida',        0.1,  TRUE, 15),
                ('falta_cometida',    'Falta cometida',      -0.1,  TRUE, 16),
                ('cartao_amarelo',    'Cartão amarelo',      -0.5,  TRUE, 17),
                ('cartao_vermelho',   'Cartão vermelho',     -1.0,  TRUE, 18),
                ('penalti_sofrido',   'Pênalti sofrido',      0.5,  TRUE, 19),
                ('penalti_cometido',  'Pênalti cometido',    -0.5,  TRUE, 20),
                ('penalti_perdido',   'Pênalti perdido',     -0.5,  TRUE, 21),
                ('penalti_defendido', 'Pênalti defendido',    0.5,  TRUE, 22)
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS criterionotas;");
        }
    }
}
