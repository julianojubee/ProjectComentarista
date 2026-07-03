-- Aplicar manualmente via psql quando dotnet ef database update falhar
-- psql -U root -d projectcomentarista

-- 1. UsuarioId em Escalacoes
ALTER TABLE "Escalacoes" ADD COLUMN "UsuarioId" text NULL;

-- 2. Nova tabela JogosAnalisadosUsuario
CREATE TABLE "JogosAnalisadosUsuario" (
    "Id" serial NOT NULL,
    "JogoId" integer NOT NULL,
    "UsuarioId" text NOT NULL,
    CONSTRAINT "PK_JogosAnalisadosUsuario" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_JogosAnalisadosUsuario_Jogos_JogoId" FOREIGN KEY ("JogoId") REFERENCES "Jogos" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_JogosAnalisadosUsuario_AspNetUsers_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_JogosAnalisadosUsuario_JogoId" ON "JogosAnalisadosUsuario" ("JogoId");
CREATE INDEX "IX_JogosAnalisadosUsuario_UsuarioId" ON "JogosAnalisadosUsuario" ("UsuarioId");
CREATE UNIQUE INDEX "IX_JogosAnalisadosUsuario_JogoId_UsuarioId" ON "JogosAnalisadosUsuario" ("JogoId", "UsuarioId");

CREATE INDEX "IX_Escalacoes_UsuarioId" ON "Escalacoes" ("UsuarioId");

-- 3. Registrar migration no historico do EF
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260620182559_AddUsuarioIdToEscalacaoAndJogoAnalisado', '9.0.4');
