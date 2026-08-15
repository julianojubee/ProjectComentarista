using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class FaseJogoClassifierTests
    {
        private static CompeticaoFase F(int id, string nome, string tipo, int ordem, string? pattern = null) =>
            new() { Id = id, Nome = nome, Tipo = tipo, Ordem = ordem, RoundsPattern = pattern };

        private static Jogo J(int id, string grupo) => new() { Id = id, Grupo = grupo };

        // Argentino 2026: dois torneios de pontos corridos, cada um com seu mata-mata.
        private static List<CompeticaoFase> FasesArgentino() => new()
        {
            F(1, "Apertura", "PONTOS_CORRIDOS", 1, "Apertura"),
            F(2, "Apertura - Mata-Mata", "MATA_MATA", 2, "Apertura - Semi;Apertura - Final;Apertura - Quarter"),
            F(3, "Clausura", "PONTOS_CORRIDOS", 3, "Clausura"),
            F(4, "Clausura - Mata-Mata", "MATA_MATA", 4, "Clausura - Semi;Clausura - Final;Clausura - Quarter"),
        };

        [Fact]
        public void RodadaDeLiga_VaiParaAFaseDeLigaDoTorneio()
        {
            var r = FaseJogoClassifier.DistribuirPorFases(FasesArgentino(), new[]
            {
                J(1, "Apertura - 5"),
                J(2, "Clausura - 12"),
            });

            Assert.Equal(new[] { 1 }, r[1].Select(j => j.Id));
            Assert.Equal(new[] { 2 }, r[3].Select(j => j.Id));
            Assert.Empty(r[2]);
            Assert.Empty(r[4]);
        }

        [Fact]
        public void MataMata_NaoCaiNaFaseDeLiga_MesmoComPatternLargoCasando()
        {
            // "Apertura - Semi-finals" contém "Apertura" (pattern da fase de liga, de Ordem menor):
            // vence o pattern mais específico da fase eliminatória.
            var r = FaseJogoClassifier.DistribuirPorFases(FasesArgentino(), new[]
            {
                J(1, "Apertura - Semi-finals"),
                J(2, "Clausura - Final"),
            });

            Assert.Empty(r[1]);
            Assert.Empty(r[3]);
            Assert.Equal(new[] { 1 }, r[2].Select(j => j.Id));
            Assert.Equal(new[] { 2 }, r[4].Select(j => j.Id));
        }

        [Fact]
        public void MataMata_SemPatternNaFaseEliminatoria_AindaAssimNaoEntraNaTabela()
        {
            var fases = new List<CompeticaoFase>
            {
                F(1, "Clausura", "PONTOS_CORRIDOS", 1, "Clausura"),
                F(2, "Playoffs", "MATA_MATA", 2),
            };

            var r = FaseJogoClassifier.DistribuirPorFases(fases, new[] { J(1, "Clausura - Semi-finals") });

            Assert.Empty(r[1]);
            Assert.Equal(new[] { 1 }, r[2].Select(j => j.Id));
        }

        [Fact]
        public void PatternEmFaseEliminatoria_AindaRoteiaRoundLidoComoRodada()
        {
            // "Playoff - 1" termina em número (heurística = liga), mas o pattern explícito
            // continua mandando: o roteamento manual não foi restringido nessa direção.
            var fases = new List<CompeticaoFase>
            {
                F(1, "Fase Regular", "PONTOS_CORRIDOS", 1),
                F(2, "Playoffs", "MATA_MATA", 2, "Playoff"),
            };

            var r = FaseJogoClassifier.DistribuirPorFases(fases, new[] { J(1, "Playoff - 1") });

            Assert.Empty(r[1]);
            Assert.Equal(new[] { 1 }, r[2].Select(j => j.Id));
        }

        [Fact]
        public void RoundIndefinido_CaiNaPrimeiraFaseNaoEliminatoria()
        {
            var r = FaseJogoClassifier.DistribuirPorFases(FasesArgentino(), new[] { J(1, "Liga Profesional") });

            Assert.Equal(new[] { 1 }, r[1].Select(j => j.Id));
        }
    }
}
