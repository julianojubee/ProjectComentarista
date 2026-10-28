using System.Net;
using System.Text;
using ControleFutebolWeb.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControleFutebolWeb.Tests.Services
{
    /// <summary>
    /// Leitura do game log e da carreira do jogador (página /NBA/Jogador/{id}). O
    /// formato da ESPN muda de um endpoint para o outro sem avisar: no /overview o
    /// adversário é objeto com logo, no /gamelog é só a sigla; o placar vem do ponto
    /// de vista do mandante; e a pré-temporada vem misturada com a temporada regular.
    /// Errar qualquer um desses não estoura exceção — só mostra o dado errado.
    ///
    /// JSONs sintéticos com a estrutura observada na API.
    /// </summary>
    public class EspnNbaJogadorTests
    {
        private const string GameLog = """
        {
          "filters": [{"name": "season", "options": [{"value": "2026", "displayValue": "2025-26"}, {"value": "2025", "displayValue": "2024-25"}]}],
          "labels": ["MIN", "REB", "V", "PTS"],
          "events": {
            "10": {"id": "10", "atVs": "x",  "gameDate": "2026-04-13T00:30:00.000+00:00", "homeTeamId": "13", "awayTeamId": "26", "homeTeamScore": "131", "awayTeamScore": "107", "gameResult": "V", "opponent": "UTAH", "team": "LAL"},
            "11": {"id": "11", "atVs": "em", "gameDate": "2026-04-10T02:00:00.000+00:00", "homeTeamId": "9",  "awayTeamId": "13", "homeTeamScore": "120", "awayTeamScore": "110", "gameResult": "D", "opponent": "GS",   "team": "LAL"},
            "90": {"id": "90", "atVs": "x",  "gameDate": "2025-10-10T00:00:00.000+00:00", "homeTeamId": "13", "awayTeamId": "1",  "homeTeamScore": "99",  "awayTeamScore": "98",  "gameResult": "V", "opponent": "ATL",  "team": "LAL"}
          },
          "seasonTypes": [
            {"displayName": "Pós-temporada 2025-26", "categories": [{"displayName": "Quartas de Final", "type": "event"}],
             "summary": {"stats": [{"displayName": "Totais", "stats": ["384", "67", "73", "200"]}]}},
            {"displayName": "Temporada Regular 2025-26",
             "categories": [{"displayName": "april", "type": "event", "events": [{"eventId": "11", "stats": ["30", "5", "6", "18"]}, {"eventId": "10", "stats": ["17", "4", "6", "18"]}], "totals": ["23.5", "4.5", "6.0", "18.0"]}],
             "summary": {"stats": [{"displayName": "Médias", "stats": ["33.2", "6.1", "7.2", "20.9"]}]}},
            {"displayName": "Pré-temporada 2025-26",
             "categories": [{"displayName": "october", "type": "event", "events": [{"eventId": "90", "stats": ["20", "3", "2", "10"]}]}]}
          ]
        }
        """;

        private const string Carreira = """
        {
          "teams": {
            "cleveland-cavaliers": {"id": "5", "abbreviation": "CLE", "displayName": "Cleveland Cavaliers", "logos": [{"href": "https://x/cle.png"}]},
            "los-angeles-lakers": {"id": "13", "abbreviation": "LAL", "displayName": "Los Angeles Lakers", "logos": [{"href": "https://x/lal.png"}]}
          },
          "categories": [
            {"name": "averages", "labels": ["J", "GS", "PTS"], "totals": ["2", "2", "24.0"],
             "statistics": [
               {"teamSlug": "cleveland-cavaliers", "season": {"year": 2004, "displayName": "2003-04"}, "stats": ["79", "79", "20.9"]},
               {"teamSlug": "los-angeles-lakers",  "season": {"year": 2019, "displayName": "2018-19"}, "stats": ["55", "55", "27.4"]}
             ]},
            {"name": "miscellaneous", "labels": ["DD2"], "statistics": []}
          ]
        }
        """;

        private sealed class EspnFalsa : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var url = request.RequestUri!.PathAndQuery;
                var corpo = url.Contains("/gamelog") ? GameLog : url.Contains("/stats") ? Carreira : "{}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
                });
            }
        }

        private static EspnNbaService Criar() => new(
            new HttpClient(new EspnFalsa()),
            NullLogger<EspnNbaService>.Instance,
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000_000 }));

        [Fact]
        public async Task GameLog_ignora_a_pre_temporada_e_traduz_o_mes()
        {
            var log = await Criar().ObterGameLogAsync("1966", 2026);

            Assert.NotNull(log);
            var mes = Assert.Single(log!.Meses);
            Assert.Equal("Abril", mes.Nome);
            Assert.DoesNotContain(mes.Jogos, j => j.EventoId == "90");
            Assert.Equal(new[] { "2025-26", "2024-25" }, log.Temporadas.Select(t => t.Rotulo));
        }

        [Fact]
        public async Task GameLog_orienta_o_placar_e_acha_o_adversario_pelo_mando()
        {
            var log = await Criar().ObterGameLogAsync("1966", 2026);
            var jogos = log!.Meses[0].Jogos;

            // Mais recente primeiro.
            Assert.Equal(new[] { "10", "11" }, jogos.Select(j => j.EventoId));

            var emCasa = jogos[0];
            Assert.True(emCasa.EmCasa);
            Assert.Equal("26", emCasa.AdversarioId);      // em casa → o adversário é o visitante
            Assert.Equal("131-107", emCasa.Placar);
            Assert.EndsWith("/utah.png", emCasa.AdversarioLogo);

            var fora = jogos[1];
            Assert.False(fora.EmCasa);
            Assert.Equal("9", fora.AdversarioId);         // fora → o adversário é o mandante
            Assert.Equal("110-120", fora.Placar);         // placar do jogador primeiro
            Assert.Equal("D", fora.Resultado);
        }

        [Fact]
        public async Task GameLog_corrige_o_rotulo_de_assistencias_e_guarda_o_resumo_dos_playoffs()
        {
            var log = await Criar().ObterGameLogAsync("1966", 2026);

            Assert.Equal(new[] { "MIN", "REB", "AST", "PTS" }, log!.Colunas);
            Assert.Equal("Totais", Assert.Single(log.ResumoPlayoffs).Rotulo);
            Assert.Equal("Médias", Assert.Single(log.ResumoTemporada).Rotulo);
        }

        [Fact]
        public async Task Carreira_liga_cada_temporada_ao_time_e_descarta_bloco_vazio()
        {
            var carreira = await Criar().ObterCarreiraAsync("1966", playoffs: false);

            var medias = Assert.Single(carreira!);
            Assert.Equal("Médias", medias.Titulo);
            Assert.Equal(new[] { "J", "JT", "PTS" }, medias.Colunas);
            // Mais recente primeiro, com o time daquele ano.
            Assert.Equal("2018-19", medias.Linhas[0].Temporada);
            Assert.Equal("LAL", medias.Linhas[0].Time?.Sigla);
            Assert.Equal("CLE", medias.Linhas[1].Time?.Sigla);
        }
    }
}
