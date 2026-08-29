using System.Text.Json;
using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Tests.Services
{
    /// <summary>
    /// Leitura do perfil do jogador no FotMob (playerData / playerStats), que alimenta
    /// a tela de estatísticas avançadas.
    ///
    /// JSONs sintéticos com a estrutura real observada na API — o payload deles não
    /// entra no repositório, mesma regra de FotMobServiceTests.
    /// </summary>
    public class FotMobPerfilServiceTests
    {
        private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement;

        // ── Métricas da temporada ─────────────────────────────────────────────

        // statFormat separa contagem de taxa, e a diferença muda a leitura: "87.6 por 90
        // minutos" não quer dizer nada. O FotMob repete a própria taxa no campo per90,
        // então confiar nele sem olhar o formato produziria essa bobagem na tela.
        private const string Desempenho = """
        {
          "statsSection": {
            "items": [
              {
                "localizedTitleId": "passing",
                "items": [
                  { "localizedTitleId": "successful_passes", "title": "Accurate passes",
                    "statFormat": "number", "statValue": "276", "per90": 35.23, "percentileRank": 96.4 },
                  { "localizedTitleId": "successful_passes_accuracy", "title": "Pass accuracy",
                    "statFormat": "percent", "statValue": "87.6", "per90": 87.62, "percentileRank": 88.1 },
                  { "localizedTitleId": "expected_assists", "title": "xA",
                    "statFormat": "fraction", "statValue": "0.61", "per90": 0.26 }
                ]
              },
              {
                "localizedTitleId": "inventada_pelo_fotmob",
                "items": [
                  { "localizedTitleId": "metrica_novissima", "title": "Brand New Metric",
                    "statFormat": "number", "statValue": "7" }
                ]
              }
            ]
          }
        }
        """;

        [Fact]
        public void Desempenho_TaxaNaoGanhaMediaPor90()
        {
            var g = FotMobPerfilService.LerDesempenho(Json(Desempenho)).First();
            var taxa = g.Metricas.Single(m => m.Nome == "Precisão de passe");

            Assert.True(taxa.EhPercentual);
            Assert.Null(taxa.Por90);
        }

        [Fact]
        public void Desempenho_ContagemMantemMediaPor90()
        {
            var g = FotMobPerfilService.LerDesempenho(Json(Desempenho)).First();
            var contagem = g.Metricas.Single(m => m.Nome == "Passes certos");

            Assert.False(contagem.EhPercentual);
            Assert.Equal(35.23, contagem.Por90);
        }

        // A tela é em português e o statValue vem com ponto decimal. Só o separador
        // muda: reformatar o número poderia discordar do que o FotMob mostra.
        [Fact]
        public void Desempenho_ValorSaiComVirgula()
        {
            var g = FotMobPerfilService.LerDesempenho(Json(Desempenho)).First();

            Assert.Equal("87,6", g.Metricas.Single(m => m.Nome == "Precisão de passe").Valor);
            Assert.Equal("0,61", g.Metricas.Single(m => m.Nome == "Assistências esperadas (xA)").Valor);
        }

        // O percentil é o que a tela tem de mais valioso — é o que responde "isso é bom
        // para esta liga?", pergunta que a nossa base não alcança. Perdê-lo na leitura
        // esvaziaria a funcionalidade sem quebrar nada visível.
        [Fact]
        public void Desempenho_PreservaOPercentil()
        {
            var g = FotMobPerfilService.LerDesempenho(Json(Desempenho)).First();

            Assert.Equal(96.4, g.Metricas.Single(m => m.Nome == "Passes certos").Percentil);
        }

        // Métrica que o FotMob criar depois aparece com o título original em vez de
        // sumir da tela: o de-para é para traduzir, não para filtrar.
        [Fact]
        public void Desempenho_MetricaDesconhecidaApareceComONomeOriginal()
        {
            var grupos = FotMobPerfilService.LerDesempenho(Json(Desempenho)).ToList();
            var nova = grupos.Last().Metricas.Single();

            Assert.Equal("Brand New Metric", nova.Nome);
            Assert.Equal("7", nova.Valor);
        }

        [Fact]
        public void Desempenho_PayloadSemStatsSection_NaoEstoura()
        {
            Assert.Empty(FotMobPerfilService.LerDesempenho(Json("""{ }""")));
        }

        // ── Posição ───────────────────────────────────────────────────────────

        // O casamento é pela key, não pelo label: esta rota responde em inglês mesmo
        // pedindo pt-BR, então o label é instável e a key não.
        [Fact]
        public void Posicao_UsaAMarcadaComoPrincipal()
        {
            var raiz = Json("""
            {
              "positionDescription": { "positions": [
                { "strPos": { "label": "Right Midfielder", "key": "rightmidfielder" }, "occurences": 9, "isMainPosition": false },
                { "strPos": { "label": "Right Winger", "key": "rightwinger" }, "occurences": 3, "isMainPosition": true }
              ]}
            }
            """);

            // A de mais ocorrências é outra: quem manda é a marca de principal.
            Assert.Equal("Ponta direita", FotMobPerfilService.PosicaoPrincipal(raiz));
        }

        [Fact]
        public void Posicao_SemPrincipal_UsaAMaisFrequente()
        {
            var raiz = Json("""
            {
              "positionDescription": { "positions": [
                { "strPos": { "label": "Striker", "key": "striker" }, "occurences": 2, "isMainPosition": false },
                { "strPos": { "label": "Attacking Midfielder", "key": "centerattackingmidfielder" }, "occurences": 11, "isMainPosition": false }
              ]}
            }
            """);

            Assert.Equal("Meia atacante", FotMobPerfilService.PosicaoPrincipal(raiz));
        }

        [Fact]
        public void Posicao_KeyDesconhecida_MantemOOriginal()
        {
            var raiz = Json("""
            {
              "positionDescription": { "positions": [
                { "strPos": { "label": "Sweeper Keeper", "key": "sweeperkeeper" }, "occurences": 1, "isMainPosition": true }
              ]}
            }
            """);

            Assert.Equal("Sweeper Keeper", FotMobPerfilService.PosicaoPrincipal(raiz));
        }

        // ── Temporadas ────────────────────────────────────────────────────────

        // O seletor da tela é em dois níveis (temporada, depois competição) porque a
        // lista crua é longa e repetitiva. O agrupamento vem daqui, e o entryId é o que
        // precisa sobreviver a ele: é o único identificador que a rota de desempenho
        // aceita — nem o ano nem o id da liga servem.
        [Fact]
        public void Temporadas_AgrupamAsCompeticoesPorAno()
        {
            var raiz = Json("""
            {
              "statSeasons": [
                { "seasonName": "2025/2026", "tournaments": [
                    { "name": "Amir of Qatar Cup", "entryId": "1-0", "hasDeepStats": false },
                    { "name": "AFC Champions League Elite", "entryId": "1-1", "hasDeepStats": true }
                ]},
                { "seasonName": "2025", "tournaments": [
                    { "name": "Qatar Cup", "entryId": "2-0", "hasDeepStats": false }
                ]}
              ]
            }
            """);

            var lista = FotMobPerfilService.LerTemporadas(raiz).ToList();

            Assert.Equal(2, lista.Count);
            Assert.Equal("2025/2026", lista[0].Nome);
            Assert.Equal(2, lista[0].Competicoes.Count);
            Assert.Equal("1-1", lista[0].Competicoes[1].Id);
            Assert.Equal("AFC Champions League Elite", lista[0].Competicoes[1].Nome);
        }

        // A fonte já entrega da mais recente para a mais antiga, que é a ordem útil.
        // Reordenar por nome quebraria isso: "2025" viria depois de "2025/2026".
        [Fact]
        public void Temporadas_MantemAOrdemDaFonte()
        {
            var raiz = Json("""
            {
              "statSeasons": [
                { "seasonName": "2026/2027", "tournaments": [ { "name": "A", "entryId": "0-0" } ] },
                { "seasonName": "2025/2026", "tournaments": [ { "name": "B", "entryId": "1-0" } ] },
                { "seasonName": "2015/2016", "tournaments": [ { "name": "C", "entryId": "2-0" } ] }
              ]
            }
            """);

            var nomes = FotMobPerfilService.LerTemporadas(raiz).Select(t => t.Nome).ToList();

            Assert.Equal(new[] { "2026/2027", "2025/2026", "2015/2016" }, nomes);
        }

        // hasDeepStats separa a competição com o quadro completo da que só tem o
        // básico. A tela marca essas para ninguém clicar e achar que quebrou.
        [Fact]
        public void Temporadas_MarcamQuemTemDadoDetalhado()
        {
            var raiz = Json("""
            {
              "statSeasons": [
                { "seasonName": "2025/2026", "tournaments": [
                    { "name": "Rasa", "entryId": "1-0", "hasDeepStats": false },
                    { "name": "Completa", "entryId": "1-1", "hasDeepStats": true }
                ]}
              ]
            }
            """);

            var comps = FotMobPerfilService.LerTemporadas(raiz).Single().Competicoes;

            Assert.False(comps[0].TemDadosDetalhados);
            Assert.True(comps[1].TemDadosDetalhados);
        }

        // Sem entryId não há como pedir as estatísticas; a competição viraria um botão
        // que não leva a lugar nenhum. E a temporada que fica sem nenhuma some junto.
        [Fact]
        public void Temporadas_SemEntryIdSomem_ETemporadaVaziaTambem()
        {
            var raiz = Json("""
            {
              "statSeasons": [
                { "seasonName": "2020", "tournaments": [ { "name": "Sem id" } ] },
                { "seasonName": "2021", "tournaments": [
                    { "name": "Sem id" },
                    { "name": "Com id", "entryId": "1-0" }
                ]}
              ]
            }
            """);

            var lista = FotMobPerfilService.LerTemporadas(raiz).ToList();

            Assert.Single(lista);
            Assert.Equal("2021", lista[0].Nome);
            Assert.Single(lista[0].Competicoes);
        }
        // ── Blocos que a fonte manda como null ────────────────────────

        // O caso real que derrubou a tela: jogador de 19 anos recém-vinculado. O FotMob
        // não manda lista vazia quando não tem o dado, manda o bloco inteiro como null —
        // e descer um nível a partir de um null LANÇA, não devolve vazio. Quem tem menos
        // história (sem título, sem carreira, sem posição consolidada) é exatamente quem
        // recebe esses nulls, então o pior payload é o mais comum entre os novatos.
        private const string PerfilDeNovato = """
        {
          "name": "Yan Diomande",
          "trophies": null,
          "careerHistory": null,
          "positionDescription": null,
          "playerInformation": null,
          "recentMatches": null,
          "statSeasons": null
        }
        """;

        [Fact]
        public void Titulos_BlocoNulo_NaoEstoura()
        {
            Assert.Empty(FotMobPerfilService.LerTitulos(Json(PerfilDeNovato)));
        }

        [Fact]
        public void Carreira_BlocoNulo_NaoEstoura()
        {
            Assert.Empty(FotMobPerfilService.LerCarreira(Json(PerfilDeNovato)));
        }

        [Fact]
        public void Jogos_BlocoNulo_NaoEstoura()
        {
            Assert.Empty(FotMobPerfilService.LerJogos(Json(PerfilDeNovato)));
        }

        [Fact]
        public void Temporadas_BlocoNulo_NaoEstoura()
        {
            Assert.Empty(FotMobPerfilService.LerTemporadas(Json(PerfilDeNovato)));
        }

        [Fact]
        public void Posicao_BlocoNulo_NaoEstoura()
        {
            Assert.Null(FotMobPerfilService.PosicaoPrincipal(Json(PerfilDeNovato)));
        }

        [Fact]
        public void Desempenho_SecaoNula_NaoEstoura()
        {
            Assert.Empty(FotMobPerfilService.LerDesempenho(Json("""{ "statsSection": null }""")));
        }

        // Null também aparece um nível abaixo, dentro de uma lista que existe.
        [Fact]
        public void Titulos_SubBlocoNulo_NaoEstoura()
        {
            var raiz = Json("""
            {
              "trophies": { "playerTrophies": [
                { "teamName": "Real Madrid", "teamId": 8633, "tournaments": null }
              ]}
            }
            """);

            Assert.Empty(FotMobPerfilService.LerTitulos(raiz));
        }

        [Fact]
        public void Jogos_MatchDateNulo_NaoEstoura()
        {
            var raiz = Json("""
            {
              "recentMatches": [
                { "opponentTeamName": "Espanyol", "leagueName": "LaLiga", "matchDate": null,
                  "homeScore": 1, "awayScore": 2, "minutesPlayed": 26 }
              ]
            }
            """);

            var jogo = Assert.Single(FotMobPerfilService.LerJogos(raiz));

            Assert.Null(jogo.Data);
            Assert.Equal("Espanyol", jogo.Adversario);
        }
    }
}
