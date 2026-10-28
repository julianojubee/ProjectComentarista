using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Services;
using static ControleFutebolWeb.Helpers.GruposInferidosHelper;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class GruposInferidosHelperTests
    {
        private static readonly DateTimeOffset D0 = new(2026, 10, 15, 20, 0, 0, TimeSpan.Zero);

        // Turno único de 4 times (6 jogos, 3 rodadas), como na Libertadores Feminina.
        private static List<Partida> Grupo(int t1, int t2, int t3, int t4, long idBase, int diaInicio)
        {
            var d = D0.AddDays(diaInicio);
            return new()
            {
                new(idBase + 1, t1, t2, d),            new(idBase + 2, t3, t4, d),
                new(idBase + 3, t1, t3, d.AddDays(3)), new(idBase + 4, t4, t2, d.AddDays(3)),
                new(idBase + 5, t4, t1, d.AddDays(6)), new(idBase + 6, t2, t3, d.AddDays(6)),
            };
        }

        [Fact]
        public void Inferir_DoisGruposEmTurnoCompleto_SeparaTimesELetrasPelaOrdemDoPrimeiroJogo()
        {
            var partidas = Grupo(10, 11, 12, 13, 200, diaInicio: 1)
                .Concat(Grupo(20, 21, 22, 23, 100, diaInicio: 0))
                .ToList();

            var r = Inferir(partidas);

            Assert.Equal(12, r.Count);
            Assert.All(Enumerable.Range(101, 6), id => Assert.Equal("Group A", r[id].Grupo));
            Assert.All(Enumerable.Range(201, 6), id => Assert.Equal("Group B", r[id].Grupo));
            Assert.Equal(new[] { 1, 1, 2, 2, 3, 3 }, Enumerable.Range(201, 6).Select(id => r[id].Rodada));
        }

        [Fact]
        public void Inferir_UmUnicoComponente_NaoEhFaseDeGrupos()
        {
            // Liga ou fase de liga: todos ligados entre si — nada é deduzido.
            Assert.Empty(Inferir(Grupo(1, 2, 3, 4, 0, 0)));
        }

        [Fact]
        public void Inferir_ComponenteSemTurnoCompleto_NaoDeduzNada()
        {
            // Mata-mata/confrontos soltos: pares isolados não são grupos.
            var partidas = new List<Partida>
            {
                new(1, 1, 2, D0), new(2, 3, 4, D0), new(3, 5, 6, D0),
            };
            Assert.Empty(Inferir(partidas));
        }

        [Theory]
        [InlineData("World Cup Women U17 - 1", 1)]
        [InlineData("Regular Season - 15", 15)]
        [InlineData("Group Stage - 3", 3)]
        [InlineData("1/128-finals", 1)]
        [InlineData("2nd Qualifying Round - 3rd Place Finals", 2)]
        [InlineData("Round of 16", 16)]
        [InlineData("Group Stage", 0)]
        public void ParseRodada_PrefereNumeroDoFimDoRound(string round, int esperado)
        {
            Assert.Equal(esperado, ApiFootballService.ParseRodada(round));
        }
    }
}
