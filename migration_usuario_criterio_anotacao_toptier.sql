-- psql -U root -d projectcomentarista

-- 1. UsuarioId em criterionotas
ALTER TABLE "criterionotas" ADD COLUMN "UsuarioId" text NULL;
CREATE INDEX "IX_criterionotas_UsuarioId" ON "criterionotas" ("UsuarioId");

-- 2. UsuarioId em AnotacoesTime (verificar nome real da tabela)
ALTER TABLE "AnotacoesTime" ADD COLUMN "UsuarioId" text NULL;
CREATE INDEX "IX_AnotacoesTime_UsuarioId" ON "AnotacoesTime" ("UsuarioId");

-- 3. Nova tabela CompeticoesTopTierUsuario
CREATE TABLE "CompeticoesTopTierUsuario" (
    "Id" serial NOT NULL,
    "CompeticaoId" integer NOT NULL,
    "UsuarioId" text NOT NULL,
    CONSTRAINT "PK_CompeticoesTopTierUsuario" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CompeticoesTopTierUsuario_Competicoes_CompeticaoId"
        FOREIGN KEY ("CompeticaoId") REFERENCES "competicoes" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_CompeticoesTopTierUsuario_AspNetUsers_UsuarioId"
        FOREIGN KEY ("UsuarioId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);
CREATE INDEX "IX_CompeticoesTopTierUsuario_CompeticaoId" ON "CompeticoesTopTierUsuario" ("CompeticaoId");
CREATE INDEX "IX_CompeticoesTopTierUsuario_UsuarioId" ON "CompeticoesTopTierUsuario" ("UsuarioId");
CREATE UNIQUE INDEX "IX_CompeticoesTopTierUsuario_CompeticaoId_UsuarioId"
    ON "CompeticoesTopTierUsuario" ("CompeticaoId", "UsuarioId");

-- 4. Registrar migration
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260620205407_AddUsuarioIdToCriterioAnotacaoAndCompeticaoTopTier', '9.0.4');
