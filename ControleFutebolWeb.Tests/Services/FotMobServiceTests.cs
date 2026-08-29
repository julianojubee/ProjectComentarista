using System.Text.Json;
using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Tests.Services
{
    /// <summary>
    /// Leitura do payload do FotMob (matchDetails). O que se testa aqui é o ponto em
    /// que o serviço mais pode errar em silêncio: o formato deles mistura número puro,
    /// fração em texto e campo ausente na MESMA estrutura, e um mapeamento errado não
    /// estoura exceção nenhuma — só grava a coluna errada.
    ///
    /// Os JSONs abaixo são sintéticos: reproduzem a ESTRUTURA observada na API, com
    /// valores inventados. O payload real do FotMob não entra no repositório de
    /// propósito — ver o comentário sobre exposição em FotMobService.
    /// </summary>
    public class FotMobServiceTests
    {
        private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement;

        // ── Estatísticas por jogador ──────────────────────────────────────────

        // O FotMob agrupa as estatísticas do jogador em "Top stats", "Attack",
        // "Defense" e "Duels". O casamento é pela key (id interno, estável), nunca pelo
        // título, que muda com o idioma da resposta.
        private const string JogadorDeLinha = """
        {
          "name": "Fulano de Tal",
          "stats": [
            {
              "title": "Top stats",
              "stats": {
                "FotMob rating":   { "key": "rating_title",    "stat": { "value": 7.98, "type": "double" } },
                "Minutes played":  { "key": "minutes_played",  "stat": { "value": 77,   "type": "integer" } },
                "Goals":           { "key": "goals",           "stat": { "value": 1,    "type": "integer" } },
                "Accurate passes": { "key": "accurate_passes", "stat": { "value": 34, "total": 40, "type": "fractionWithPercentage" } },
                "Shotmap":         { "key": null,              "stat": { "value": 1,    "type": "boolean" } }
              }
            },
            {
              "title": "Duels",
              "stats": {
                "Successful dribbles": { "key": "dribbles_succeeded", "stat": { "value": 2, "total": 5, "type": "fractionWithPercentage" } },
                "Was fouled":          { "key": "was_fouled",         "stat": { "value": null, "type": "integer" } }
              }
            }
          ]
        }
        """;

        [Fact]
        public void LerEstatisticasDoJogador_AchataOsGruposNumaChaveSo()
        {
            var lido = FotMobService.LerEstatisticasDoJogador(Json(JogadorDeLinha));

            // Grupos diferentes ("Top stats" e "Duels") caem no mesmo dicionário.
            Assert.Equal(77, lido["minutes_played"].Valor);
            Assert.Equal(2, lido["dribbles_succeeded"].Valor);
        }

        // O par (value, total) é o que separa "passes certos" de "passes tentados": o
        // FotMob não manda o total como campo próprio. Trocar um pelo outro daria uma
        // precisão de passe de 100% em todo mundo.
        [Fact]
        public void LerEstatisticasDoJogador_SeparaOAcertoDoTentado()
        {
            var lido = FotMobService.LerEstatisticasDoJogador(Json(JogadorDeLinha));

            Assert.Equal(34, lido["accurate_passes"].Valor);
            Assert.Equal(40, lido["accurate_passes"].Total);

            Assert.Equal(2, lido["dribbles_succeeded"].Valor);
            Assert.Equal(5, lido["dribbles_succeeded"].Total);
        }

        // Estatística sem total é contagem simples e não pode inventar um denominador.
        [Fact]
        public void LerEstatisticasDoJogador_ContagemSimplesNaoTemTotal()
        {
            var lido = FotMobService.LerEstatisticasDoJogador(Json(JogadorDeLinha));

            Assert.Equal(1, lido["goals"].Valor);
            Assert.Null(lido["goals"].Total);
        }

        // "Shotmap" vem com key null (é um marcador de UI, não uma estatística). Entrar
        // no dicionário com chave vazia atropelaria alguma outra leitura.
        [Fact]
        public void LerEstatisticasDoJogador_IgnoraLinhaSemChave()
        {
            var lido = FotMobService.LerEstatisticasDoJogador(Json(JogadorDeLinha));

            Assert.DoesNotContain(lido, x => string.IsNullOrEmpty(x.Key));
        }

        // O FotMob manda a linha com value null quando o jogador não registrou aquilo.
        // Precisa chegar como null e não como 0: quem lê decide o que fazer com a
        // ausência, e FonteEstatistica existe justamente para isso.
        [Fact]
        public void LerEstatisticasDoJogador_ValorAusenteChegaNulo()
        {
            var lido = FotMobService.LerEstatisticasDoJogador(Json(JogadorDeLinha));

            Assert.True(lido.ContainsKey("was_fouled"));
            Assert.Null(lido["was_fouled"].Valor);
        }

        [Fact]
        public void LerEstatisticasDoJogador_SemBlocoDeStats_NaoEstoura()
        {
            Assert.Empty(FotMobService.LerEstatisticasDoJogador(Json("""{ "name": "Sem stats" }""")));
        }

        // ── Estatísticas de time ──────────────────────────────────────────────

        // A tabela de time mistura três formatos na mesma coluna.
        [Fact]
        public void LerValorTime_LeNumeroPuro()
        {
            Assert.Equal(21, FotMobService.LerValorTime(Json("21"))!.Value.Valor);
        }

        // "291 (80%)" = 291 passes certos, 80% de acerto. O percentual entre parênteses
        // vem arredondado pelo FotMob; o absoluto permite recalcular com precisão.
        [Fact]
        public void LerValorTime_LeFracaoEmTextoPeloValorAbsoluto()
        {
            Assert.Equal(291, FotMobService.LerValorTime(Json("\"291 (80%)\""))!.Value.Valor);
        }

        // As linhas de cabeçalho de seção ("Shots", "Passes") vêm com [null, null].
        [Fact]
        public void LerValorTime_CabecalhoDeSecaoNaoEhValor()
        {
            Assert.Null(FotMobService.LerValorTime(Json("null")));
        }

        [Fact]
        public void LerValorTime_TextoQueNaoEhNumero_NaoViraZero()
        {
            Assert.Null(FotMobService.LerValorTime(Json("\"-\"")));
        }

        // ── Cartões ───────────────────────────────────────────────────────────

        // Cartão não vem em playerStats — o campo simplesmente não existe lá. Sai da
        // linha do tempo da partida.
        private const string PartidaComCartoes = """
        {
          "content": {
            "matchFacts": {
              "events": {
                "events": [
                  { "type": "Card", "time": 6,  "card": "Yellow",    "playerId": 110169, "isHome": false },
                  { "type": "Goal", "time": 18, "card": null,        "playerId": 110169, "isHome": false },
                  { "type": "Card", "time": 44, "card": "Yellow",    "playerId": 0,      "isHome": true  },
                  { "type": "Card", "time": 70, "card": "Yellow",    "playerId": 110169, "isHome": false },
                  { "type": "Card", "time": 70, "card": "YellowRed", "playerId": 110169, "isHome": false },
                  { "type": "Card", "time": 88, "card": "Red",       "playerId": 555,    "isHome": true  },
                  { "type": "AddedTime", "time": 90, "card": null, "playerId": null, "isHome": null }
                ]
              }
            }
          }
        }
        """;

        [Fact]
        public void MapearCartoes_SomaOsAmarelosDoMesmoJogador()
        {
            var cartoes = FotMobService.MapearCartoes(Json(PartidaComCartoes));

            Assert.Equal(2, cartoes[110169].Amarelos);
        }

        // O segundo amarelo vem como evento "YellowRed" à parte, e é expulsão.
        [Fact]
        public void MapearCartoes_SegundoAmareloContaComoVermelho()
        {
            var cartoes = FotMobService.MapearCartoes(Json(PartidaComCartoes));

            Assert.Equal(1, cartoes[110169].Vermelhos);
        }

        // playerId 0 = jogador que o FotMob não tem cadastrado (acontece com técnico e
        // com reserva de clube pequeno). Não dá para atribuir a ninguém.
        [Fact]
        public void MapearCartoes_IgnoraCartaoSemJogadorIdentificado()
        {
            var cartoes = FotMobService.MapearCartoes(Json(PartidaComCartoes));

            Assert.DoesNotContain(0L, cartoes.Keys);
        }

        [Fact]
        public void MapearCartoes_EventoQueNaoEhCartao_NaoEntra()
        {
            var cartoes = FotMobService.MapearCartoes(Json(PartidaComCartoes));

            // O gol do 110169 não pode ter virado cartão.
            Assert.Equal((2, 1), cartoes[110169]);
        }

        // No total do TIME o cartão do jogador não identificado ainda conta: ele
        // aconteceu na partida, só não dá para dizer de quem foi.
        [Fact]
        public void ContarCartoesDoTime_IncluiCartaoDeJogadorSemId()
        {
            var (amarelos, vermelhos) = FotMobService.ContarCartoesDoTime(Json(PartidaComCartoes), ehCasa: true);

            Assert.Equal(1, amarelos);
            Assert.Equal(1, vermelhos);
        }

        [Fact]
        public void ContarCartoesDoTime_SeparaMandanteDeVisitante()
        {
            var casa = FotMobService.ContarCartoesDoTime(Json(PartidaComCartoes), ehCasa: true);
            var fora = FotMobService.ContarCartoesDoTime(Json(PartidaComCartoes), ehCasa: false);

            Assert.Equal((1, 1), casa);
            Assert.Equal((2, 1), fora);
        }

        // Partida sem cartão nenhum: o bloco de eventos pode nem existir. Zero cartões
        // é informação (e é o que vai para o EstatisticasJson), não erro.
        [Fact]
        public void ContarCartoesDoTime_PartidaSemEventos_DaZero()
        {
            var vazio = Json("""{ "content": { } }""");

            Assert.Equal((0, 0), FotMobService.ContarCartoesDoTime(vazio, ehCasa: true));
            Assert.Empty(FotMobService.MapearCartoes(vazio));
        }
    }
}
