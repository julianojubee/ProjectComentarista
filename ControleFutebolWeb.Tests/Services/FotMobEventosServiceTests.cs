using System.Text.Json;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using static ControleFutebolWeb.Services.FotMobEventosService;

namespace ControleFutebolWeb.Tests.Services
{
    /// <summary>
    /// Leitura dos lances, do placar e do relógio do matchDetails do FotMob, e a regra
    /// de "quem tem mais dado vence" da conferência ao vivo.
    ///
    /// Os JSONs são sintéticos: reproduzem a ESTRUTURA observada na API, com nomes e
    /// valores inventados — ver o comentário sobre exposição em FotMobService.
    /// </summary>
    public class FotMobEventosServiceTests
    {
        private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement;

        private const string Partida = """
        {
          "header": {
            "teams": [ { "name": "Alvinegro", "score": 2 }, { "name": "Tricolor", "score": 1 } ],
            "status": { "started": true, "finished": false, "cancelled": false,
                        "liveTime": { "short": "78‎’‎", "long": "77:15", "maxTime": 90, "addedTime": 0 } }
          },
          "content": {
            "lineup": {
              "homeTeam": { "name": "Alvinegro",
                "starters": [ { "id": 10, "name": "Fulano", "shirtNumber": "8" },
                              { "id": 11, "name": "Beltrano", "shirtNumber": "10" } ],
                "subs":     [ { "id": 12, "name": "Fulano", "shirtNumber": "5" } ] },
              "awayTeam": { "name": "Tricolor",
                "starters": [ { "id": 20, "name": "Sicrano", "shirtNumber": "6" } ],
                "subs": [] }
            },
            "matchFacts": { "events": { "events": [
              { "type": "Goal", "time": 30, "isHome": true, "playerId": 10,
                "player": { "id": 10, "name": "Fulano" }, "ownGoal": null,
                "assistPlayerId": 11, "assistInput": "Beltrano", "isPenaltyShootoutEvent": false },
              { "type": "Half", "time": 45, "player": { "id": null } },
              { "type": "Substitution", "time": 46, "isHome": true, "player": { "id": null },
                "swap": [ { "name": "Fulano", "id": "12" }, { "name": "Beltrano", "id": "11" } ] },
              { "type": "Goal", "time": 60, "isHome": true, "playerId": 20,
                "player": { "id": 20, "name": "Sicrano" }, "ownGoal": true },
              { "type": "Card", "time": 90, "overloadTime": 4, "isHome": false, "playerId": 20,
                "player": { "id": 20, "name": "Sicrano" }, "card": "YellowRed" },
              { "type": "VAR", "time": 90, "isHome": false, "playerId": 20, "player": { "id": 20, "name": "Sicrano" } },
              { "type": "Goal", "time": 120, "isHome": false, "playerId": 20,
                "player": { "id": 20, "name": "Sicrano" }, "isPenaltyShootoutEvent": true }
            ] } }
          }
        }
        """;

        [Fact]
        public void LerLances_TraduzGolAssistenciaSubstituicaoECartao()
        {
            var lances = LerLances(Json(Partida));

            // Half e VAR não são lance; a cobrança da disputa de pênaltis fica de fora.
            Assert.Equal(4, lances.Count);

            var gol = lances[0];
            Assert.Equal(TipoLance.Gol, gol.Tipo);
            Assert.Equal(30, gol.Minuto);
            Assert.Equal(10, gol.IdJogador);
            Assert.Equal(11, gol.IdOutro);
            Assert.False(gol.Contra);

            // swap[0] entrou, swap[1] saiu; os ids vêm como texto.
            var sub = lances[1];
            Assert.Equal(TipoLance.Substituicao, sub.Tipo);
            Assert.Equal(12, sub.IdJogador);
            Assert.Equal(11, sub.IdOutro);

            var contra = lances[2];
            Assert.True(contra.Contra);
            Assert.Equal(0, contra.IdOutro);

            // Minuto sem o acréscimo, como na api-football.
            var cartao = lances[3];
            Assert.Equal(TipoLance.Cartao, cartao.Tipo);
            Assert.Equal(90, cartao.Minuto);
            Assert.Equal("YellowRed", cartao.Cartao);
        }

        [Fact]
        public void LerEscalacao_GuardaCamisaParaSepararHomonimos()
        {
            var atletas = LerEscalacao(Json(Partida), "Alvinegro", "Tricolor");

            Assert.Equal(8, atletas[10].Camisa);
            Assert.Equal(5, atletas[12].Camisa);
            Assert.True(atletas[12].EhCasa);
            Assert.False(atletas[20].EhCasa);
        }

        [Fact]
        public void LerEscalacao_LadoComNomeQueNaoConfereFicaDeFora()
        {
            var atletas = LerEscalacao(Json(Partida), "Outro Clube", "Tricolor");

            Assert.False(atletas.ContainsKey(10));
            Assert.True(atletas.ContainsKey(20));
        }

        [Fact]
        public void LerSituacao_EmAndamento_IgnoraMarcasDeDirecaoDoTexto()
        {
            var s = LerSituacao(Json(Partida), "Alvinegro", "Tricolor");

            Assert.True(s.Iniciada);
            Assert.False(s.Finalizada);
            Assert.Equal("2H", s.Codigo);
            Assert.Equal(78, s.Minuto);
            Assert.Null(s.Acrescimo);
            Assert.Equal(2, s.PlacarCasa);
            Assert.Equal(1, s.PlacarVisitante);
        }

        private static string Status(string status) => $$"""
        { "header": { "teams": [ { "name": "A", "score": 1 }, { "name": "B", "score": 1 } ],
                      "status": {{status}} } }
        """;

        [Fact]
        public void LerSituacao_Acrescimo()
        {
            var s = LerSituacao(Json(Status("""
                { "started": true, "finished": false, "liveTime": { "short": "45+2’", "maxTime": 45 } }
                """)), "A", "B");

            Assert.Equal("1H", s.Codigo);
            Assert.Equal(45, s.Minuto);
            Assert.Equal(2, s.Acrescimo);
        }

        [Fact]
        public void LerSituacao_Intervalo()
        {
            var s = LerSituacao(Json(Status("""
                { "started": true, "finished": false, "liveTime": { "short": "HT", "maxTime": 45 } }
                """)), "A", "B");

            Assert.Equal("HT", s.Codigo);
            Assert.Null(s.Minuto);
        }

        [Theory]
        [InlineData("FT", "FT")]
        [InlineData("AET", "AET")]
        [InlineData("Pen", "PEN")]
        public void LerSituacao_Encerrada(string motivo, string esperado)
        {
            var s = LerSituacao(Json(Status($$"""
                { "started": true, "finished": true, "reason": { "short": "{{motivo}}" } }
                """)), "A", "B");

            Assert.True(s.Finalizada);
            Assert.Equal(esperado, s.Codigo);
        }

        [Fact]
        public void LerSituacao_MandanteInvertido_NaoDevolvePlacar()
        {
            var s = LerSituacao(Json(Status("""{ "started": true, "finished": true }""")), "B", "A");

            Assert.Null(s.PlacarCasa);
            Assert.Null(s.PlacarVisitante);
        }

        [Fact]
        public void CasarPorNomeECamisa_HomonimoNoElenco_ACamisaDecide()
        {
            // Nome idêntico ("Danilo") ganhava no desempate por nome e levava o gol do
            // titular "Danilo Santos" para o reserva.
            var elenco = new List<Jogador>
            {
                new() { Id = 1, Nome = "Danilo Santos", NumeroCamisa = 8 },
                new() { Id = 2, Nome = "Danilo", NumeroCamisa = 90 },
            };

            Assert.Equal(1, EspnEscalacaoService.CasarPorNomeECamisa(elenco, "Danilo", 8)?.Id);
            Assert.Equal(2, EspnEscalacaoService.CasarPorNomeECamisa(elenco, "Danilo", 90)?.Id);
            Assert.Null(EspnEscalacaoService.CasarPorNomeECamisa(elenco, "Danilo", null));
        }

        // ── Placar na conferência ─────────────────────────────────────────────

        private static SituacaoPartida Situacao(bool finalizada, string codigo, int casa, int fora) =>
            new(true, finalizada, codigo, finalizada ? null : 70, null, casa, fora);

        [Fact]
        public void AplicarPlacar_EncerradoSemPlacarGravado_ViraPlacarFinal()
        {
            var jogo = new Jogo { PlacarParcialCasa = 1, PlacarParcialVisitante = 0, StatusParcial = "2H" };

            var r = ComplementoFotMobService.AplicarPlacar(jogo, Situacao(true, "FT", 3, 2));

            Assert.NotNull(r);
            Assert.Equal(3, jogo.PlacarCasa);
            Assert.Equal(2, jogo.PlacarVisitante);
            Assert.Equal("Finalizado", jogo.Status);
            Assert.Null(jogo.PlacarParcialCasa);
        }

        [Fact]
        public void AplicarPlacar_PenaltisComPlacarDaDisputa_GravaOsDois()
        {
            var jogo = new Jogo();

            ComplementoFotMobService.AplicarPlacar(jogo,
                new SituacaoPartida(true, true, "PEN", null, null, 3, 2, 3, 4));

            Assert.Equal(3, jogo.PlacarCasa);
            Assert.Equal(2, jogo.PlacarVisitante);
            Assert.Equal(3, jogo.PenaltisCasa);
            Assert.Equal(4, jogo.PenaltisVisitante);
        }

        [Fact]
        public void LerDisputa_PlacarECobrancasNaOrdem()
        {
            var raiz = Json("""
            { "header": { "teams": [ { "name": "A", "score": 3 }, { "name": "B", "score": 2 } ],
                          "status": { "started": true, "finished": true,
                                      "reason": { "short": "Pen", "penalties": [3, 4] } } },
              "content": { "matchFacts": { "events": {
                "events": [ { "type": "PenaltyShootout", "time": 121, "penaltyScore": [3, 4] } ],
                "penaltyShootoutEvents": [
                  { "type": "Goal", "isHome": false, "playerId": 1, "player": { "id": 1, "name": "Batedor B" }, "isPenaltyShootoutEvent": true },
                  { "type": "MissedPenalty", "isHome": true, "playerId": null, "player": { "id": 2, "name": "Batedor A" } }
                ] } } } }
            """);

            var s = LerSituacao(raiz, "A", "B");
            Assert.Equal("PEN", s.Codigo);
            Assert.Equal(3, s.PenaltisCasa);
            Assert.Equal(4, s.PenaltisVisitante);

            // O marcador PenaltyShootout não é lance do tempo de jogo.
            Assert.Empty(LerLances(raiz));

            var cobrancas = LerCobrancasDisputa(raiz);
            Assert.Equal(2, cobrancas.Count);
            Assert.Equal(new CobrancaDisputa(1, false, 1, "Batedor B", true), cobrancas[0]);
            Assert.Equal(new CobrancaDisputa(2, true, 2, "Batedor A", false), cobrancas[1]);
        }

        [Fact]
        public void AplicarPlacar_PenaltisSemPlacarDaDisputa_FicaNoParcial()
        {
            var jogo = new Jogo();

            ComplementoFotMobService.AplicarPlacar(jogo, Situacao(true, "PEN", 1, 1));

            Assert.Null(jogo.PlacarCasa);
            Assert.Equal(1, jogo.PlacarParcialCasa);
            Assert.Equal("P", jogo.StatusParcial);
        }

        [Fact]
        public void AplicarPlacar_NaoMexeNoPlacarQueJaEstaGravado()
        {
            var jogo = new Jogo { PlacarCasa = 0, PlacarVisitante = 0 };

            Assert.Null(ComplementoFotMobService.AplicarPlacar(jogo, Situacao(true, "FT", 3, 2)));
            Assert.Equal(0, jogo.PlacarCasa);
        }

        [Fact]
        public void AplicarPlacar_ParcialDaApiAtrasado_FotMobAssume()
        {
            var jogo = new Jogo { PlacarParcialCasa = 1, PlacarParcialVisitante = 0, StatusParcial = "2H" };

            ComplementoFotMobService.AplicarPlacar(jogo, Situacao(false, "2H", 2, 1));

            Assert.Equal(2, jogo.PlacarParcialCasa);
            Assert.Equal(70, jogo.MinutoParcial);
        }

        [Fact]
        public void AplicarPlacar_ParcialDaApiEmDia_Mantido()
        {
            var jogo = new Jogo { PlacarParcialCasa = 2, PlacarParcialVisitante = 1, StatusParcial = "2H", MinutoParcial = 80 };

            Assert.Null(ComplementoFotMobService.AplicarPlacar(jogo, Situacao(false, "2H", 2, 1)));
            Assert.Equal(80, jogo.MinutoParcial);
        }

        // ── Volume das estatísticas ───────────────────────────────────────────

        [Fact]
        public void VolumeEstatisticasTime_SomaSoOsContadoresComuns()
        {
            // "Free Kicks" só a api-football publica e posse é percentual: nenhum dos
            // dois entra, senão a comparação pesaria para um lado.
            const string json = """
            [ { "TimeId": 1, "Stats": { "Total Shots": "10", "Fouls": 5, "Free Kicks": "20", "Ball Possession": "40%" } },
              { "TimeId": 2, "Stats": { "Total Shots": "3", "Corner Kicks": null } } ]
            """;

            Assert.Equal(18, ComplementoFotMobService.VolumeEstatisticasTime(json));
            Assert.Equal(0, ComplementoFotMobService.VolumeEstatisticasTime(null));
            Assert.Equal(0, ComplementoFotMobService.VolumeEstatisticasTime("não é json"));
        }
    }
}
