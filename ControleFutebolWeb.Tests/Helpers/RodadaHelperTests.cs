using ControleFutebolWeb.Helpers;
using Xunit;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class RodadaHelperTests
    {
        [Theory]
        [InlineData("Regular Season - 34", 0, "Rodada 34")]
        [InlineData("Group Stage - 3", 0, "Fase de grupos, rodada 3")]
        [InlineData("League Stage - 8", 0, "Fase de liga, rodada 8")]
        [InlineData("Quarter-finals", 0, "Quartas de final")]
        [InlineData("Semi-finals", 0, "Semifinal")]
        [InlineData("Round of 16", 0, "Oitavas de final")]
        [InlineData("3rd Place Final", 0, "Disputa de 3º lugar")]
        [InlineData("Final", 0, "Final")]
        [InlineData("Relegation Round", 0, "Playoff de rebaixamento")]
        [InlineData("2nd Qualifying Round", 0, "2ª fase eliminatória")]
        [InlineData("Group B", 0, "Grupo B")]
        [InlineData("Round of 128", 0, "64-avos de final")]
        [InlineData("1/128-finals", 0, "128-avos de final")]
        [InlineData("Qualification Round 2", 0, "2ª fase preliminar")]
        [InlineData("1st Round", 0, "1ª fase")]
        public void TraduzORoundDaApi(string grupo, int rodada, string esperado)
        {
            Assert.Equal(esperado, RodadaHelper.Rotulo(grupo, rodada));
        }

        // Nome próprio (fase cadastrada à mão) passa como veio, em vez de sumir.
        [Fact]
        public void NomeDesconhecido_PassaComoVeio()
        {
            Assert.Equal("Fase final do Paulistão", RodadaHelper.Rotulo("Fase final do Paulistão", 3));
        }

        [Theory]
        [InlineData(null, 12, "Rodada 12")]
        [InlineData("", 0, "–")]
        public void SemRound_UsaONumeroDaRodada(string? grupo, int rodada, string esperado)
        {
            Assert.Equal(esperado, RodadaHelper.Rotulo(grupo, rodada));
        }

        [Theory]
        [InlineData("Group A", "Grupo A")]
        [InlineData("A", "Grupo A")]
        [InlineData("Grupo B", "Grupo B")]
        [InlineData("Quarter-finals", "Quartas de final")]
        public void RotuloGrupo(string nome, string esperado)
        {
            Assert.Equal(esperado, RodadaHelper.RotuloGrupo(nome));
        }
    }
}
