using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class CompeticaoPainelBuilderMataMataTests
    {
        private static int _id;

        private static Jogo J(string fase, int dia, int casa, int fora, int pc = 1, int pv = 0) => new()
        {
            Id = ++_id,
            Grupo = fase,
            Data = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc).AddDays(dia),
            TimeCasaId = casa, TimeVisitanteId = fora,
            TimeCasa = new Time { Id = casa, Nome = $"T{casa}" },
            TimeVisitante = new Time { Id = fora, Nome = $"T{fora}" },
            PlacarCasa = pc, PlacarVisitante = pv,
        };

        [Fact]
        public void FasesDaApi_SaemEmPortuguesENaOrdemDoChaveamento()
        {
            // Formato da Copa do Brasil 2026 na api-football: a lista fixa antiga
            // não conhecia "1/256-finals", "1/128-finals", "Round of 64"...
            var jogos = new List<Jogo>
            {
                J("Round of 32", 50, 1, 2),
                J("Quarter-finals", 90, 1, 3),
                J("1/256-finals", 1, 10, 11),
                J("Round of 16", 70, 1, 4),
                J("1/128-finals", 10, 12, 13),
                J("Round of 64", 30, 14, 15),
                J("Round of 128", 20, 16, 17),
                J("Semi-finals", 100, 1, 5),
                J("Final", 110, 1, 6),
            };

            var fases = CompeticaoPainelBuilder.MontarMataMata(jogos);

            Assert.Equal(new[]
            {
                "256-avos de final", "128-avos de final", "64-avos de final", "32-avos de final",
                "16-avos de final", "Oitavas de final", "Quartas de final", "Semifinal", "Final",
            }, fases.OrderBy(f => f.Ordem).Select(f => f.Nome));
        }

        [Fact]
        public void Final_DecideOTitulo_MesmoForaDeCompeticaoDeJogoUnico()
        {
            var fases = CompeticaoPainelBuilder.MontarMataMata(new List<Jogo>
            {
                J("Semi-finals", 1, 1, 2), J("Final", 10, 1, 3),
            });

            Assert.False(fases.Single(f => f.Nome == "Semifinal").DecideTitulo);
            Assert.True(fases.Single(f => f.Nome == "Final").DecideTitulo);
        }

        [Fact]
        public void FaseDesconhecida_VaiParaOFim_NaOrdemEmQueFoiJogada()
        {
            var fases = CompeticaoPainelBuilder.MontarMataMata(new List<Jogo>
            {
                J("Fase extra B", 30, 1, 2), J("Final", 20, 3, 4), J("Fase extra A", 25, 5, 6),
            });

            Assert.Equal(new[] { "Final", "Fase extra A", "Fase extra B" },
                fases.OrderBy(f => f.Ordem).Select(f => f.Nome));
        }

        [Theory]
        [InlineData("Qualification Round 1", "1st Round")]
        [InlineData("2nd Round", "32avos")]
        [InlineData("32avos", "16avos")]
        [InlineData("Oitavas", "Quartas")]
        [InlineData("Semifinal", "Final")]
        [InlineData("Final", "3rd Place Final")]
        public void OrdemFase_PrimeiraVemAntes(string antes, string depois)
        {
            Assert.True(CompeticaoPainelBuilder.OrdemFase(antes) < CompeticaoPainelBuilder.OrdemFase(depois));
        }
    }
}
