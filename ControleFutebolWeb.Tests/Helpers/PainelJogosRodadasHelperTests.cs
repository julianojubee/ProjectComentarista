using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Rodada em que o painel "Jogos" das competições abre
    public class PainelJogosRodadasHelperTests
    {
        private static readonly DateTime Agora = new(2026, 10, 21, 18, 0, 0, DateTimeKind.Utc);

        private static Jogo J(int diasDeHoje, int? casa = null, int? vis = null) =>
            new() { Data = Agora.AddDays(diasDeHoje), PlacarCasa = casa, PlacarVisitante = vis };

        private static RodadaJogosViewModel R(string rotulo, params Jogo[] jogos) =>
            new() { Rotulo = rotulo, Jogos = jogos.ToList() };

        [Fact]
        public void JogoAntigoSemPlacar_NaoPrendeOPainelNaRodadaVelha()
        {
            var rodadas = new List<RodadaJogosViewModel>
            {
                R("Grupos 4", J(-150, 1, 0), J(-150)),     // ficou sem placar no banco
                R("Oitavas", J(-60, 2, 1), J(-53, 0, 0)),
                R("Quartas", J(2), J(9)),
            };
            Assert.Equal(2, PainelJogosRodadasHelper.RodadaEmAndamento(rodadas, Agora));
        }

        [Fact]
        public void RodadaComJogoDeHoje_AbreNelaMesmoComResultadosJa()
        {
            var rodadas = new List<RodadaJogosViewModel>
            {
                R("Rodada 2", J(-7, 1, 1)),
                R("Rodada 3", J(-1, 2, 0), J(0)),
                R("Rodada 4", J(13)),
            };
            Assert.Equal(1, PainelJogosRodadasHelper.RodadaEmAndamento(rodadas, Agora));
        }

        [Fact]
        public void TemporadaEncerrada_AbreNaRodadaDoUltimoResultado()
        {
            var rodadas = new List<RodadaJogosViewModel>
            {
                R("Semifinal", J(-30, 1, 0)),
                R("Final", J(-20, 2, 1)),
            };
            Assert.Equal(1, PainelJogosRodadasHelper.RodadaEmAndamento(rodadas, Agora));
        }
    }
}
