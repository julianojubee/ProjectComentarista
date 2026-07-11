using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class ClassificacaoCalculatorTests
    {
        private static Time T(int id, string nome) => new() { Id = id, Nome = nome, Cidade = "" };

        private static Jogo J(int id, Time casa, Time visitante, int? placarCasa, int? placarVisitante) => new()
        {
            Id = id,
            TimeCasa = casa,
            TimeCasaId = casa.Id,
            TimeVisitante = visitante,
            TimeVisitanteId = visitante.Id,
            PlacarCasa = placarCasa,
            PlacarVisitante = placarVisitante
        };

        [Fact]
        public void Vitoria_Da3Pontos_EmpateDa1_ContabilizaGols()
        {
            var a = T(1, "A"); var b = T(2, "B"); var c = T(3, "C");
            var tabela = ClassificacaoCalculator.Calcular(new List<Jogo>
            {
                J(1, a, b, 2, 1), // A vence
                J(2, b, c, 1, 1), // empate
            });

            var linhaA = tabela.Single(t => t.TimeId == 1);
            var linhaB = tabela.Single(t => t.TimeId == 2);
            var linhaC = tabela.Single(t => t.TimeId == 3);

            Assert.Equal(3, linhaA.Pontos);
            Assert.Equal(1, linhaA.Vitorias);
            Assert.Equal(2, linhaA.GolsPro);
            Assert.Equal(1, linhaA.GolsContra);

            Assert.Equal(1, linhaB.Pontos);
            Assert.Equal(2, linhaB.Jogos);
            Assert.Equal(1, linhaB.Derrotas);
            Assert.Equal(1, linhaB.Empates);

            Assert.Equal(1, linhaC.Pontos);
            Assert.Equal(1, linhaC.Jogos);
        }

        [Fact]
        public void JogoSemPlacar_NaoEntraNaTabela()
        {
            var a = T(1, "A"); var b = T(2, "B");
            var tabela = ClassificacaoCalculator.Calcular(new List<Jogo>
            {
                J(1, a, b, null, null), // ainda não jogado
            });

            Assert.Empty(tabela);
        }

        [Fact]
        public void Ordenacao_Pontos_DepoisSaldo_DepoisGolsPro()
        {
            var a = T(1, "A"); var b = T(2, "B"); var c = T(3, "C"); var d = T(4, "D");
            // A e B terminam com 3 pontos cada; A com saldo maior deve ficar acima.
            // C e D terminam com 0 pontos; mesmo saldo (-1 e -3)... montamos assim:
            //   A 3x0 C  → A: +3 | C: -3
            //   B 1x0 D  → B: +1 | D: -1
            var tabela = ClassificacaoCalculator.Calcular(new List<Jogo>
            {
                J(1, a, c, 3, 0),
                J(2, b, d, 1, 0),
            });

            Assert.Equal(new[] { 1, 2, 4, 3 }, tabela.Select(t => t.TimeId).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4 }, tabela.Select(t => t.Posicao).ToArray());
        }

        [Fact]
        public void Desempate_PorGolsPro_QuandoPontosESaldoIguais()
        {
            var a = T(1, "A"); var b = T(2, "B"); var c = T(3, "C"); var d = T(4, "D");
            // A: 2x2 e 3x3 (2 pts, saldo 0, GP 5) | C: dois 0x0... precisa de adversários:
            //   A 3x3 B  |  C 0x0 D  → todos com 1 jogo... vamos com dois jogos por dupla:
            //   A 3x3 B (A GP3), C 0x0 D (C GP0) — pontos iguais (1), saldo igual (0),
            //   A na frente por gols pró.
            var tabela = ClassificacaoCalculator.Calcular(new List<Jogo>
            {
                J(1, a, b, 3, 3),
                J(2, c, d, 0, 0),
            });

            Assert.True(tabela.FindIndex(t => t.TimeId == 1) < tabela.FindIndex(t => t.TimeId == 3));
        }
    }
}
