CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE formacoes (
        id serial NOT NULL,
        nome text NOT NULL,
        CONSTRAINT "PK_formacoes" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE nacionalidades (
        id serial NOT NULL,
        nome text NOT NULL,
        CONSTRAINT "PK_nacionalidades" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE times (
        id serial NOT NULL,
        nome text NOT NULL,
        cidade text NOT NULL,
        escudourl text,
        CONSTRAINT "PK_times" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE posicoesformacao (
        id serial NOT NULL,
        nomeposicao text NOT NULL,
        posicaox double precision NOT NULL,
        posicaoy double precision NOT NULL,
        ordem integer NOT NULL,
        formacaoid integer NOT NULL,
        CONSTRAINT "PK_posicoesformacao" PRIMARY KEY (id),
        CONSTRAINT "FK_posicoesformacao_formacoes_formacaoid" FOREIGN KEY (formacaoid) REFERENCES formacoes (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE jogadores (
        id serial NOT NULL,
        nome text NOT NULL,
        posicao text NOT NULL,
        datanascimento timestamp without time zone NOT NULL,
        numerocamisa integer,
        nacionalidadeid integer,
        timeid integer NOT NULL,
        CONSTRAINT "PK_jogadores" PRIMARY KEY (id),
        CONSTRAINT "FK_jogadores_nacionalidades_nacionalidadeid" FOREIGN KEY (nacionalidadeid) REFERENCES nacionalidades (id),
        CONSTRAINT "FK_jogadores_times_timeid" FOREIGN KEY (timeid) REFERENCES times (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE jogos (
        id serial NOT NULL,
        rodada integer NOT NULL,
        data timestamp without time zone NOT NULL,
        timecasaid integer NOT NULL,
        placarcasa integer NOT NULL,
        placarvisitante integer NOT NULL,
        timevisitanteid integer NOT NULL,
        formacaocasaid integer NOT NULL,
        formacaovisitanteid integer NOT NULL,
        CONSTRAINT "PK_jogos" PRIMARY KEY (id),
        CONSTRAINT "FK_jogos_formacoes_formacaocasaid" FOREIGN KEY (formacaocasaid) REFERENCES formacoes (id) ON DELETE CASCADE,
        CONSTRAINT "FK_jogos_formacoes_formacaovisitanteid" FOREIGN KEY (formacaovisitanteid) REFERENCES formacoes (id) ON DELETE CASCADE,
        CONSTRAINT "FK_jogos_times_timecasaid" FOREIGN KEY (timecasaid) REFERENCES times (id) ON DELETE CASCADE,
        CONSTRAINT "FK_jogos_times_timevisitanteid" FOREIGN KEY (timevisitanteid) REFERENCES times (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE cartoes (
        id serial NOT NULL,
        jogoid integer NOT NULL,
        jogadorid integer NOT NULL,
        minuto integer NOT NULL,
        tipo text NOT NULL,
        CONSTRAINT "PK_cartoes" PRIMARY KEY (id),
        CONSTRAINT "FK_cartoes_jogadores_jogadorid" FOREIGN KEY (jogadorid) REFERENCES jogadores (id) ON DELETE CASCADE,
        CONSTRAINT "FK_cartoes_jogos_jogoid" FOREIGN KEY (jogoid) REFERENCES jogos (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE escalacoes (
        id serial NOT NULL,
        jogoid integer NOT NULL,
        jogadorid integer NOT NULL,
        titular boolean NOT NULL,
        posicao text NOT NULL,
        istimecasa boolean NOT NULL,
        posicaox double precision NOT NULL,
        posicaoy double precision NOT NULL,
        CONSTRAINT "PK_escalacoes" PRIMARY KEY (id),
        CONSTRAINT "FK_escalacoes_jogadores_jogadorid" FOREIGN KEY (jogadorid) REFERENCES jogadores (id) ON DELETE CASCADE,
        CONSTRAINT "FK_escalacoes_jogos_jogoid" FOREIGN KEY (jogoid) REFERENCES jogos (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE gols (
        id serial NOT NULL,
        jogoid integer NOT NULL,
        jogadorid integer NOT NULL,
        minuto integer NOT NULL,
        contra boolean NOT NULL,
        CONSTRAINT "PK_gols" PRIMARY KEY (id),
        CONSTRAINT "FK_gols_jogadores_jogadorid" FOREIGN KEY (jogadorid) REFERENCES jogadores (id) ON DELETE CASCADE,
        CONSTRAINT "FK_gols_jogos_jogoid" FOREIGN KEY (jogoid) REFERENCES jogos (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE TABLE notas (
        id serial NOT NULL,
        valor integer NOT NULL,
        comentario text NOT NULL,
        jogadorid integer NOT NULL,
        jogoid integer NOT NULL,
        CONSTRAINT "PK_notas" PRIMARY KEY (id),
        CONSTRAINT "FK_notas_jogadores_jogadorid" FOREIGN KEY (jogadorid) REFERENCES jogadores (id) ON DELETE CASCADE,
        CONSTRAINT "FK_notas_jogos_jogoid" FOREIGN KEY (jogoid) REFERENCES jogos (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    INSERT INTO nacionalidades (id, nome)
    VALUES (1, 'Brasil');
    INSERT INTO nacionalidades (id, nome)
    VALUES (2, 'Argentina');
    INSERT INTO nacionalidades (id, nome)
    VALUES (3, 'França');
    INSERT INTO nacionalidades (id, nome)
    VALUES (4, 'Alemanha');
    INSERT INTO nacionalidades (id, nome)
    VALUES (5, 'Itália');
    INSERT INTO nacionalidades (id, nome)
    VALUES (6, 'Espanha');
    INSERT INTO nacionalidades (id, nome)
    VALUES (7, 'Portugal');
    INSERT INTO nacionalidades (id, nome)
    VALUES (8, 'Uruguai');
    INSERT INTO nacionalidades (id, nome)
    VALUES (9, 'Chile');
    INSERT INTO nacionalidades (id, nome)
    VALUES (10, 'Paraguai');
    INSERT INTO nacionalidades (id, nome)
    VALUES (11, 'Bolívia');
    INSERT INTO nacionalidades (id, nome)
    VALUES (12, 'Peru');
    INSERT INTO nacionalidades (id, nome)
    VALUES (13, 'Equador');
    INSERT INTO nacionalidades (id, nome)
    VALUES (14, 'Colômbia');
    INSERT INTO nacionalidades (id, nome)
    VALUES (15, 'Venezuela');
    INSERT INTO nacionalidades (id, nome)
    VALUES (16, 'Guiana');
    INSERT INTO nacionalidades (id, nome)
    VALUES (17, 'Suriname');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_cartoes_jogadorid" ON cartoes (jogadorid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_cartoes_jogoid" ON cartoes (jogoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_escalacoes_jogadorid" ON escalacoes (jogadorid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_escalacoes_jogoid" ON escalacoes (jogoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_gols_jogadorid" ON gols (jogadorid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_gols_jogoid" ON gols (jogoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_jogadores_nacionalidadeid" ON jogadores (nacionalidadeid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_jogadores_timeid" ON jogadores (timeid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_jogos_formacaocasaid" ON jogos (formacaocasaid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_jogos_formacaovisitanteid" ON jogos (formacaovisitanteid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_jogos_timecasaid" ON jogos (timecasaid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_jogos_timevisitanteid" ON jogos (timevisitanteid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_notas_jogadorid" ON notas (jogadorid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_notas_jogoid" ON notas (jogoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    CREATE INDEX "IX_posicoesformacao_formacaoid" ON posicoesformacao (formacaoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    PERFORM setval(
        pg_get_serial_sequence('nacionalidades', 'id'),
        GREATEST(
            (SELECT MAX(id) FROM nacionalidades) + 1,
            nextval(pg_get_serial_sequence('nacionalidades', 'id'))),
        false);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260405175508_AddInitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260405175508_AddInitialCreate', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412215357_AddIdApiToTime') THEN
    ALTER TABLE jogos DROP CONSTRAINT "FK_jogos_formacoes_formacaocasaid";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412215357_AddIdApiToTime') THEN
    ALTER TABLE jogos DROP CONSTRAINT "FK_jogos_formacoes_formacaovisitanteid";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412215357_AddIdApiToTime') THEN
    ALTER TABLE times ADD idapi integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412215357_AddIdApiToTime') THEN
    ALTER TABLE jogos ALTER COLUMN formacaovisitanteid DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412215357_AddIdApiToTime') THEN
    ALTER TABLE jogos ALTER COLUMN formacaocasaid DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412215357_AddIdApiToTime') THEN
    ALTER TABLE jogos ADD CONSTRAINT "FK_jogos_formacoes_formacaocasaid" FOREIGN KEY (formacaocasaid) REFERENCES formacoes (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412215357_AddIdApiToTime') THEN
    ALTER TABLE jogos ADD CONSTRAINT "FK_jogos_formacoes_formacaovisitanteid" FOREIGN KEY (formacaovisitanteid) REFERENCES formacoes (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260412215357_AddIdApiToTime') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260412215357_AddIdApiToTime', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260413000427_AddPartidaApiIdToJogos') THEN
    ALTER TABLE jogos ADD partidaapiid integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260413000427_AddPartidaApiIdToJogos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260413000427_AddPartidaApiIdToJogos', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260413010729_ConvertDateTimeToTimestamptz') THEN
    ALTER TABLE jogos ALTER COLUMN data TYPE timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260413010729_ConvertDateTimeToTimestamptz') THEN
    ALTER TABLE jogadores ALTER COLUMN datanascimento TYPE timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260413010729_ConvertDateTimeToTimestamptz') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260413010729_ConvertDateTimeToTimestamptz', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260415222810_AddCoresClube') THEN
    ALTER TABLE times ADD corprincipal text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260415222810_AddCoresClube') THEN
    ALTER TABLE times ADD corsecundaria text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260415222810_AddCoresClube') THEN
    ALTER TABLE jogos ALTER COLUMN data TYPE timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260415222810_AddCoresClube') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260415222810_AddCoresClube', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260415223732_AddBackground') THEN
    ALTER TABLE times ADD backgroundurl text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260415223732_AddBackground') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260415223732_AddBackground', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417012857_AddGrupoToJogos') THEN
    DELETE FROM competicoes
    WHERE id = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417012857_AddGrupoToJogos') THEN
    DELETE FROM competicoes
    WHERE id = 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417012857_AddGrupoToJogos') THEN
    DELETE FROM competicoes
    WHERE id = 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417012857_AddGrupoToJogos') THEN
    ALTER TABLE jogos ADD grupo text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417012857_AddGrupoToJogos') THEN
    ALTER TABLE competicoes ADD tipo text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417012857_AddGrupoToJogos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260417012857_AddGrupoToJogos', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417013236_MakeGrupoNullable') THEN
    ALTER TABLE jogos ALTER COLUMN grupo DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417013236_MakeGrupoNullable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260417013236_MakeGrupoNullable', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417022008_addClassificacaoViewModel') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260417022008_addClassificacaoViewModel', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417022128_addCompetitionad') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260417022128_addCompetitionad', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417202020_addCorrecaoPlacarCasaNull') THEN
    ALTER TABLE jogos ALTER COLUMN placarvisitante DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417202020_addCorrecaoPlacarCasaNull') THEN
    ALTER TABLE jogos ALTER COLUMN placarcasa DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417202020_addCorrecaoPlacarCasaNull') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260417202020_addCorrecaoPlacarCasaNull', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419053100_addElencoTime') THEN
    ALTER TABLE jogadores ADD timeid1 integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419053100_addElencoTime') THEN
    CREATE INDEX "IX_jogadores_timeid1" ON jogadores (timeid1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419053100_addElencoTime') THEN
    ALTER TABLE jogadores ADD CONSTRAINT "FK_jogadores_times_timeid1" FOREIGN KEY (timeid1) REFERENCES times (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419053100_addElencoTime') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260419053100_addElencoTime', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420010253_addUniformesTimes') THEN
    ALTER TABLE times ADD camisaurl text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420010253_addUniformesTimes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260420010253_addUniformesTimes', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423214952_AddFormacaoPadraoRelationToTimes') THEN
    ALTER TABLE times ADD formacaopadraoid integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423214952_AddFormacaoPadraoRelationToTimes') THEN
    CREATE INDEX "IX_times_formacaopadraoid" ON times (formacaopadraoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423214952_AddFormacaoPadraoRelationToTimes') THEN
    ALTER TABLE times ADD CONSTRAINT "FK_times_formacoes_formacaopadraoid" FOREIGN KEY (formacaopadraoid) REFERENCES formacoes (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423214952_AddFormacaoPadraoRelationToTimes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260423214952_AddFormacaoPadraoRelationToTimes', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424001340_AddJogadorid') THEN
    ALTER TABLE timesescalacoespadrao ADD jogadorid integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424001340_AddJogadorid') THEN
    CREATE INDEX "IX_timesescalacoespadrao_jogadorid" ON timesescalacoespadrao (jogadorid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424001340_AddJogadorid') THEN
    ALTER TABLE timesescalacoespadrao ADD CONSTRAINT "FK_timesescalacoespadrao_jogadores_jogadorid" FOREIGN KEY (jogadorid) REFERENCES jogadores (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424001340_AddJogadorid') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260424001340_AddJogadorid', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424212012_AddPosicaoid') THEN
    ALTER TABLE timesescalacoespadrao ADD posicaoid integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424212012_AddPosicaoid') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260424212012_AddPosicaoid', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424230808_AddPosicaoIdPosicaoFormacao') THEN
    ALTER TABLE posicoesformacao ADD posicaoid integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424230808_AddPosicaoIdPosicaoFormacao') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260424230808_AddPosicaoIdPosicaoFormacao', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER TABLE timesescalacoespadrao DROP CONSTRAINT "FK_timesescalacoespadrao_jogadores_jogadorid";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER TABLE timesescalacoespadrao DROP CONSTRAINT "FK_timesescalacoespadrao_times_timeid";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER TABLE timesescalacoespadrao DROP CONSTRAINT "PK_timesescalacoespadrao";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER TABLE timesescalacoespadrao RENAME TO timeescalacaopadrao;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER INDEX "IX_timesescalacoespadrao_timeid" RENAME TO "IX_timeescalacaopadrao_timeid";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER INDEX "IX_timesescalacoespadrao_jogadorid" RENAME TO "IX_timeescalacaopadrao_jogadorid";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER TABLE timeescalacaopadrao ADD CONSTRAINT "PK_timeescalacaopadrao" PRIMARY KEY (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER TABLE timeescalacaopadrao ADD CONSTRAINT "FK_timeescalacaopadrao_jogadores_jogadorid" FOREIGN KEY (jogadorid) REFERENCES jogadores (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    ALTER TABLE timeescalacaopadrao ADD CONSTRAINT "FK_timeescalacaopadrao_times_timeid" FOREIGN KEY (timeid) REFERENCES times (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260424233422_AddTimeescalacaoPadrao') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260424233422_AddTimeescalacaoPadrao', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427010305_AddAvaliacoes') THEN
    ALTER TABLE times DROP CONSTRAINT "FK_times_formacoes_formacaopadraoid";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427010305_AddAvaliacoes') THEN
    UPDATE times SET formacaopadraoid = 0 WHERE formacaopadraoid IS NULL;
    ALTER TABLE times ALTER COLUMN formacaopadraoid SET NOT NULL;
    ALTER TABLE times ALTER COLUMN formacaopadraoid SET DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427010305_AddAvaliacoes') THEN
    CREATE TABLE notadetalhes (
        id serial NOT NULL,
        notaid integer NOT NULL,
        acaoid text NOT NULL,
        acaolabel text NOT NULL,
        quantidade integer NOT NULL,
        peso integer NOT NULL,
        CONSTRAINT "PK_notadetalhes" PRIMARY KEY (id),
        CONSTRAINT "FK_notadetalhes_notas_notaid" FOREIGN KEY (notaid) REFERENCES notas (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427010305_AddAvaliacoes') THEN
    CREATE INDEX "IX_timeescalacaopadrao_formacaoid" ON timeescalacaopadrao (formacaoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427010305_AddAvaliacoes') THEN
    CREATE INDEX "IX_notadetalhes_notaid" ON notadetalhes (notaid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427010305_AddAvaliacoes') THEN
    ALTER TABLE timeescalacaopadrao ADD CONSTRAINT "FK_timeescalacaopadrao_formacoes_formacaoid" FOREIGN KEY (formacaoid) REFERENCES formacoes (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427010305_AddAvaliacoes') THEN
    ALTER TABLE times ADD CONSTRAINT "FK_times_formacoes_formacaopadraoid" FOREIGN KEY (formacaopadraoid) REFERENCES formacoes (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427010305_AddAvaliacoes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260427010305_AddAvaliacoes', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260429195830_AddFaseEscalacaoToEscalacoes') THEN
    ALTER TABLE escalacoes ADD faseescalacao text NOT NULL DEFAULT 'INICIAL';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260429195830_AddFaseEscalacaoToEscalacoes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260429195830_AddFaseEscalacaoToEscalacoes', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260429223918_AddObservacoesToJogos') THEN
    ALTER TABLE jogos ADD observacoes text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260429223918_AddObservacoesToJogos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260429223918_AddObservacoesToJogos', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430214507_AddnullnoCampoCor') THEN
    ALTER TABLE times ALTER COLUMN corsecundaria DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430214507_AddnullnoCampoCor') THEN
    ALTER TABLE times ALTER COLUMN corprincipal DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430214507_AddnullnoCampoCor') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260430214507_AddnullnoCampoCor', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501135134_Addmigrationnova') THEN
    ALTER TABLE jogos ADD eventkey integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501135134_Addmigrationnova') THEN
    ALTER TABLE jogos ADD status text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501135134_Addmigrationnova') THEN
    CREATE TABLE importacaologs (
        id serial NOT NULL,
        dataimportacao timestamp with time zone NOT NULL,
        competicaoid integer NOT NULL,
        nometimeapi text NOT NULL,
        nometimebanco text,
        acao text NOT NULL,
        observacao text,
        CONSTRAINT "PK_importacaologs" PRIMARY KEY (id),
        CONSTRAINT "FK_importacaologs_competicoes_competicaoid" FOREIGN KEY (competicaoid) REFERENCES competicoes (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501135134_Addmigrationnova') THEN
    CREATE INDEX "IX_importacaologs_competicaoid" ON importacaologs (competicaoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501135134_Addmigrationnova') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260501135134_Addmigrationnova', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502215549_AddmigrationCampoIdade') THEN
    DROP TABLE importacaologs;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502215549_AddmigrationCampoIdade') THEN
    ALTER TABLE jogadores ADD atualizado boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502215549_AddmigrationCampoIdade') THEN
    ALTER TABLE jogadores ADD idadetransfermarkt integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502215549_AddmigrationCampoIdade') THEN
    ALTER TABLE jogadores ADD idapi text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502215549_AddmigrationCampoIdade') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260502215549_AddmigrationCampoIdade', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502224611_AlterarIdApiParaLong') THEN
    ALTER TABLE jogadores ALTER COLUMN idapi TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502224611_AlterarIdApiParaLong') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260502224611_AlterarIdApiParaLong', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502230808_IncluirDtInc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260502230808_IncluirDtInc', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502231000_IncluirDtIncDrop') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260502231000_IncluirDtIncDrop', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502231150_IncluirDtIncajustado') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260502231150_IncluirDtIncajustado', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502231253_AddCamposControleJogador') THEN
    ALTER TABLE jogadores ADD dtalt timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502231253_AddCamposControleJogador') THEN
    ALTER TABLE jogadores ADD dtinc timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502231253_AddCamposControleJogador') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260502231253_AddCamposControleJogador', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502231922_Ajustardtalt') THEN
    ALTER TABLE jogadores ALTER COLUMN dtalt DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502231922_Ajustardtalt') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260502231922_Ajustardtalt', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503035223_AjusteCompeticaoId') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260503035223_AjusteCompeticaoId', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503040903_AddAssistencia') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260503040903_AddAssistencia', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503204651_AddCamisaVisitante') THEN
    ALTER TABLE times ADD camisavisitanteurl text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503204651_AddCamisaVisitante') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260503204651_AddCamisaVisitante', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260506012404_Addatualziadonojogo') THEN
    ALTER TABLE jogos ADD atualizado integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260506012404_Addatualziadonojogo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260506012404_Addatualziadonojogo', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509224927_CriarTabelaTreinador') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260509224927_CriarTabelaTreinador', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509225333_Treinador') THEN
    CREATE TABLE treinadores (
        id serial NOT NULL,
        nome text NOT NULL,
        datanascimento timestamp with time zone NOT NULL,
        timeid integer NOT NULL,
        dtinc timestamp with time zone NOT NULL,
        dtalt timestamp with time zone,
        CONSTRAINT "PK_treinadores" PRIMARY KEY (id),
        CONSTRAINT "FK_treinadores_times_timeid" FOREIGN KEY (timeid) REFERENCES times (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509225333_Treinador') THEN
    CREATE TABLE treinadoreshistorico (
        id serial NOT NULL,
        treinadorid integer NOT NULL,
        timeid integer NOT NULL,
        dtinicio timestamp with time zone NOT NULL,
        dtfim timestamp with time zone,
        CONSTRAINT "PK_treinadoreshistorico" PRIMARY KEY (id),
        CONSTRAINT "FK_treinadoreshistorico_times_timeid" FOREIGN KEY (timeid) REFERENCES times (id) ON DELETE CASCADE,
        CONSTRAINT "FK_treinadoreshistorico_treinadores_treinadorid" FOREIGN KEY (treinadorid) REFERENCES treinadores (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509225333_Treinador') THEN
    CREATE INDEX "IX_treinadores_timeid" ON treinadores (timeid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509225333_Treinador') THEN
    CREATE INDEX "IX_treinadoreshistorico_timeid" ON treinadoreshistorico (timeid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509225333_Treinador') THEN
    CREATE INDEX "IX_treinadoreshistorico_treinadorid" ON treinadoreshistorico (treinadorid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260509225333_Treinador') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260509225333_Treinador', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510021845_AddFotos') THEN
    ALTER TABLE jogos ADD fotourl text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510021845_AddFotos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510021845_AddFotos', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510022057_AddFotosjogadores') THEN
    ALTER TABLE jogadores ADD fotourl text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510022057_AddFotosjogadores') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260510022057_AddFotosjogadores', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511150456_AddNacionaliDadeTreinador') THEN
    ALTER TABLE treinadores ADD nacionalidadeid integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511150456_AddNacionaliDadeTreinador') THEN
    CREATE INDEX "IX_treinadores_nacionalidadeid" ON treinadores (nacionalidadeid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511150456_AddNacionaliDadeTreinador') THEN
    ALTER TABLE treinadores ADD CONSTRAINT "FK_treinadores_nacionalidades_nacionalidadeid" FOREIGN KEY (nacionalidadeid) REFERENCES nacionalidades (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511150456_AddNacionaliDadeTreinador') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260511150456_AddNacionaliDadeTreinador', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511151342_AddFotosTreinador') THEN
    ALTER TABLE treinadores ADD fotourl text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511151342_AddFotosTreinador') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260511151342_AddFotosTreinador', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260518113558_AddCampoAnalisadoJogos') THEN
    ALTER TABLE jogos ADD analisado integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260518113558_AddCampoAnalisadoJogos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260518113558_AddCampoAnalisadoJogos', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260519172625_AddCampoLinktranfermarket') THEN
    ALTER TABLE jogadores ADD linktransfermarket text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260519172625_AddCampoLinktranfermarket') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260519172625_AddCampoLinktranfermarket', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260519183651_AjsuteCamposData') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260519183651_AjsuteCamposData', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260519215642_AddUrlCompeticoes') THEN
    ALTER TABLE competicoes ADD linktransfermarket text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260519215642_AddUrlCompeticoes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260519215642_AddUrlCompeticoes', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520021625_AddcamposLink') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260520021625_AddcamposLink', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520154620_AddAssistencias') THEN
    CREATE TABLE assistencias (
        id serial NOT NULL,
        jogoid integer NOT NULL,
        jogadorid integer NOT NULL,
        minuto integer NOT NULL,
        CONSTRAINT "PK_assistencias" PRIMARY KEY (id),
        CONSTRAINT "FK_assistencias_jogadores_jogadorid" FOREIGN KEY (jogadorid) REFERENCES jogadores (id) ON DELETE CASCADE,
        CONSTRAINT "FK_assistencias_jogos_jogoid" FOREIGN KEY (jogoid) REFERENCES jogos (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520154620_AddAssistencias') THEN
    CREATE INDEX "IX_assistencias_jogadorid" ON assistencias (jogadorid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520154620_AddAssistencias') THEN
    CREATE INDEX "IX_assistencias_jogoid" ON assistencias (jogoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520154620_AddAssistencias') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260520154620_AddAssistencias', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520181748_Addnulldatajogo') THEN
    ALTER TABLE jogos ALTER COLUMN data DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520181748_Addnulldatajogo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260520181748_Addnulldatajogo', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521225308_Addlindetelhas') THEN
    ALTER TABLE jogos ADD linkdetalhes text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521225308_Addlindetelhas') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260521225308_Addlindetelhas', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522013311_AjusteCartoes') THEN
    ALTER TABLE gols ADD jogoid1 integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522013311_AjusteCartoes') THEN
    ALTER TABLE cartoes ADD jogoid1 integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522013311_AjusteCartoes') THEN
    CREATE INDEX "IX_gols_jogoid1" ON gols (jogoid1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522013311_AjusteCartoes') THEN
    CREATE INDEX "IX_cartoes_jogoid1" ON cartoes (jogoid1);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522013311_AjusteCartoes') THEN
    ALTER TABLE cartoes ADD CONSTRAINT "FK_cartoes_jogos_jogoid1" FOREIGN KEY (jogoid1) REFERENCES jogos (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522013311_AjusteCartoes') THEN
    ALTER TABLE gols ADD CONSTRAINT "FK_gols_jogos_jogoid1" FOREIGN KEY (jogoid1) REFERENCES jogos (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522013311_AjusteCartoes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260522013311_AjusteCartoes', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260526235918_DataNascimentoNullable') THEN
    ALTER TABLE jogadores ALTER COLUMN datanascimento DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260526235918_DataNascimentoNullable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260526235918_DataNascimentoNullable', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613023907_AddSelecaoIdToJogador') THEN
    ALTER TABLE jogadores ADD selecaoid integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613023907_AddSelecaoIdToJogador') THEN
    CREATE INDEX "IX_jogadores_selecaoid" ON jogadores (selecaoid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613023907_AddSelecaoIdToJogador') THEN
    ALTER TABLE jogadores ADD CONSTRAINT "FK_jogadores_times_selecaoid" FOREIGN KEY (selecaoid) REFERENCES times (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613023907_AddSelecaoIdToJogador') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260613023907_AddSelecaoIdToJogador', '9.0.2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613032052_AddLinkOgolToTreinador') THEN
    ALTER TABLE treinadores ADD linkogol text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613032052_AddLinkOgolToTreinador') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260613032052_AddLinkOgolToTreinador', '9.0.2');
    END IF;
END $EF$;
COMMIT;

