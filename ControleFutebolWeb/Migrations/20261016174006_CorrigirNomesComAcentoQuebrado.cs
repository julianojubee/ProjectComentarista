using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFutebolWeb.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirNomesComAcentoQuebrado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A api-football mandou alguns nomes com o acento estragado ("JoÃ£o Pedro
            // Chermont") e eles foram gravados assim. A importação agora corrige na
            // leitura (MojibakeHelper, via AfHtmlDecodeStringConverter); aqui conserta o
            // que já estava no banco. Mesma regra do helper, par a par: cada Ã/Â +
            // byte de continuação volta para Latin-1 e é relido como UTF-8. Par a par
            // porque um nome pode ter um caractere estragado e outro certo
            // ("SnÃ¦hólm"), e a conversão do texto inteiro falharia no que está certo.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    alvo record;
                    linha record;
                    novo text;
                    par text;
                    padrao text := '[' || chr(195) || chr(194) || '][' || chr(128) || '-' || chr(191) || ']';
                BEGIN
                    FOR alvo IN SELECT * FROM (VALUES
                        ('jogadores', 'nome'), ('jogadores', 'primeironome'), ('jogadores', 'ultimonome'),
                        ('jogosindisponiveis', 'nome'), ('treinadores', 'nome'), ('times', 'nome'),
                        ('times', 'estadionome'), ('jogos', 'estadio'), ('jogos', 'arbitro')
                    ) AS t(tabela, coluna) LOOP
                        FOR linha IN EXECUTE format('SELECT id, %I AS valor FROM %I WHERE %I ~ $1', alvo.coluna, alvo.tabela, alvo.coluna)
                                     USING padrao LOOP
                            novo := linha.valor;
                            LOOP
                                par := substring(novo from padrao);
                                EXIT WHEN par IS NULL;
                                BEGIN
                                    novo := replace(novo, par, convert_from(convert_to(par, 'LATIN1'), 'UTF8'));
                                EXCEPTION WHEN others THEN
                                    EXIT;
                                END;
                            END LOOP;
                            IF novo <> linha.valor THEN
                                EXECUTE format('UPDATE %I SET %I = $1 WHERE id = $2', alvo.tabela, alvo.coluna)
                                    USING novo, linha.id;
                            END IF;
                        END LOOP;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: estragar de novo os nomes não serve para nada.
        }
    }
}
