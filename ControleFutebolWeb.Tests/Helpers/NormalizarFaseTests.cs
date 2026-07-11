using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class NormalizarFaseTests
    {
        [Theory]
        // Rounds como vêm da api-football
        [InlineData("Round of 32", "R32")]
        [InlineData("Round of 16", "R16")]
        [InlineData("Quarter-finals", "QF")]
        [InlineData("Semi-finals", "SF")]
        [InlineData("Final", "F")]
        [InlineData("3rd Place Final", "TP")] // 3º lugar NÃO pode cair no "final" genérico
        // Variações em português usadas manualmente
        [InlineData("Oitavas de Final", "R16")]
        [InlineData("Quartas de final", "QF")]
        [InlineData("Disputa de terceiro lugar", "TP")]
        // Entradas sem fase reconhecível
        [InlineData("Group A", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void NormalizaRoundsDaApiEVariacoesEmPortugues(string? entrada, string? esperado)
        {
            Assert.Equal(esperado, ChaveamentoCopaBuilder.NormalizarFase(entrada));
        }
    }
}
