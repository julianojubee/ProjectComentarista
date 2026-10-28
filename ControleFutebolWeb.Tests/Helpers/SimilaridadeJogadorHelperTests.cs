using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Aba Similares do Scout: perfil por 90 minutos, percentil no grupo da função e
    // semelhança = 100 − distância quadrática média entre os percentis.
    public class SimilaridadeJogadorHelperTests
    {
        private static EstatisticaJogador Linha(int minutos, string fonte = FonteEstatistica.ApiFootball,
            int desarmes = 0, int passes = 0, int passesCertos = 0, int gols = 0) => new()
        {
            Fonte = fonte,
            Minutos = minutos,
            Desarmes = desarmes,
            PassesTotal = passes,
            PassesCertos = passesCertos,
            Gols = gols,
        };

        [Fact]
        public void Perfil_NormalizaPor90Minutos()
        {
            var perfil = SimilaridadeJogadorHelper.Perfil(
                new[] { Linha(180, desarmes: 4), Linha(180, desarmes: 2) },
                SimilaridadeJogadorHelper.MetricasLinha);

            Assert.Equal(1.5, perfil["desarmes"], 6);
        }

        // ESPN não publica desarmes: os minutos dela não podem diluir a média, senão
        // o volante pareceria um jogador que não marca.
        [Fact]
        public void Perfil_IgnoraMinutosDeFonteQueNaoCobreAMetrica()
        {
            var perfil = SimilaridadeJogadorHelper.Perfil(
                new[] { Linha(360, desarmes: 8), Linha(900, FonteEstatistica.Espn, gols: 1) },
                SimilaridadeJogadorHelper.MetricasLinha);

            Assert.Equal(2.0, perfil["desarmes"], 6);
            Assert.Equal(90.0 / 1260, perfil["gols"], 6);
        }

        [Fact]
        public void Perfil_SemMinutosSuficientes_DeixaAMetricaDeFora()
        {
            var perfil = SimilaridadeJogadorHelper.Perfil(
                new[] { Linha(200, desarmes: 5) }, SimilaridadeJogadorHelper.MetricasLinha);

            Assert.False(perfil.ContainsKey("desarmes"));
        }

        // Import anterior a PassesCertos: tentou passe e acertou zero. Não é 0%.
        [Fact]
        public void Perfil_PrecisaoDePasse_DescartaLinhaSemPassesCertos()
        {
            var perfil = SimilaridadeJogadorHelper.Perfil(
                new[] { Linha(300, passes: 100, passesCertos: 80), Linha(300, passes: 100, passesCertos: 0) },
                SimilaridadeJogadorHelper.MetricasLinha);

            Assert.Equal(80.0, perfil["passes_precisao"], 6);
        }

        [Fact]
        public void Percentil_EmpatesContamPelaMetade()
        {
            var ordenados = new List<double> { 0, 0, 0, 1 };

            Assert.Equal(37.5, SimilaridadeJogadorHelper.Percentil(0, ordenados), 6);
            Assert.Equal(87.5, SimilaridadeJogadorHelper.Percentil(1, ordenados), 6);
        }

        [Fact]
        public void Ranquear_OrdenaPeloMaisParecidoEExcluiOAlvo()
        {
            var percentis = new Dictionary<int, Dictionary<string, double>>
            {
                [1] = new() { ["a"] = 90, ["b"] = 10, ["c"] = 50 },
                [2] = new() { ["a"] = 85, ["b"] = 15, ["c"] = 50 },
                [3] = new() { ["a"] = 10, ["b"] = 90, ["c"] = 50 },
            };

            var r = SimilaridadeJogadorHelper.Ranquear(1, percentis, minimoMetricas: 3);

            Assert.Equal(new[] { 2, 3 }, r.Select(s => s.JogadorId));
            Assert.Equal(100 - Math.Round(Math.Sqrt((25 + 25 + 0) / 3.0), 1), r[0].Similaridade, 1);
            Assert.Equal("c", r[0].Diferencas[0].Id); // a mais próxima vem primeiro
        }

        [Fact]
        public void Ranquear_ExigeMetricasEmComum()
        {
            var percentis = new Dictionary<int, Dictionary<string, double>>
            {
                [1] = new() { ["a"] = 50, ["b"] = 50, ["c"] = 50, ["d"] = 50, ["e"] = 50 },
                [2] = new() { ["a"] = 50, ["b"] = 50 }, // idêntico, mas medido em duas
                [3] = new() { ["a"] = 70, ["b"] = 30, ["c"] = 60, ["d"] = 40, ["e"] = 50 },
            };

            var r = SimilaridadeJogadorHelper.Ranquear(1, percentis);

            Assert.Equal(new[] { 3 }, r.Select(s => s.JogadorId));
        }
    }
}
