using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Este casamento é o que decide se o jogo do nosso banco recebe a transmissão de TV
    // vinda do futnatv.net. Quando ele erra, o jogo aparece sem canal (falso negativo) ou
    // com o canal de outro jogo (falso positivo) — os dois casos estão cobertos aqui.
    public class TimeNomeMatcherTests
    {
        [Theory]
        [InlineData("Campeonato Brasileiro", "Brasileirão - Série A")] // nome oficial x nome popular
        [InlineData("Brasileirão Feminino", "Brasileirão Feminino")]
        [InlineData("Premier League", "Campeonato Inglês")]
        [InlineData("Serie A TIM", "Campeonato Italiano")]
        [InlineData("Bundesliga", "Campeonato Alemão")]
        [InlineData("SulAmericana", "Copa Sul-Americana")]
        [InlineData("Champions League", "Pré-Champions League")] // fase preliminar, mesma competição pra nós
        public void CompeticoesEquivalentes_Correspondem(string nomeBanco, string nomeFutnatv)
        {
            Assert.True(TimeNomeMatcher.SaoMesmaCompeticao(nomeBanco, nomeFutnatv));
        }

        [Theory]
        [InlineData("Campeonato Brasileiro", "Brasileirão - Série B")]
        [InlineData("Campeonato Brasileiro", "Brasileirão Feminino")]
        [InlineData("Bundesliga", "Campeonato Alemão (2ª div.)")]
        [InlineData("Premier League", "Copa da Liga Inglesa")]
        public void CompeticoesDiferentes_NaoCorrespondem(string nomeBanco, string nomeFutnatv)
        {
            Assert.False(TimeNomeMatcher.SaoMesmaCompeticao(nomeBanco, nomeFutnatv));
        }

        [Theory]
        [InlineData("Gremio", "Grêmio")]
        [InlineData("Sao Paulo", "São Paulo")]
        [InlineData("Vasco DA Gama", "Vasco")]
        [InlineData("Atletico-MG", "Atlético Mineiro")]
        [InlineData("Athletico-PR", "Atlético-PR")]
        [InlineData("Ferroviaria W", "Ferroviária F")]   // sufixo de time feminino de cada lado
        [InlineData("Atlético Mineiro W", "Atlético-MG")]
        [InlineData("Deportivo Recoleta", "Recoleta")]     // futnatv encurta clubes sul-americanos
        [InlineData("Independiente Rivadavia", "Ind. Rivadavia")]
        [InlineData("Union St. Gilloise", "Union Saint-Gilloise")]
        [InlineData("Cerro Porteno", "Cerro Porteño")]
        [InlineData("Atletico Torque", "Montevideo City")]
        [InlineData("Independ. Rivadavia", "Ind. Rivadavia")] // abreviação diferente dos dois lados
        [InlineData("Universidad Católica", "U. Católica")]
        [InlineData("Deportivo A Coruña", "Dep. A Coruña")]
        [InlineData("Estudiantes L.P.", "Estudiantes")]
        public void TimesEquivalentes_Correspondem(string nomeBanco, string nomeFutnatv)
        {
            Assert.True(TimeNomeMatcher.SaoMesmoTime(nomeBanco, nomeFutnatv));
        }

        [Theory]
        [InlineData("Internacional", "Inter de Limeira")]
        [InlineData("Atletico-MG", "Atlético-GO")]
        [InlineData("Botafogo", "Botafogo-PB")]
        [InlineData("Atlético-MG", "Atlético Madrid")]        // abreviação não pode virar vale-tudo
        [InlineData("Sporting CP", "Sporting Cristal")]
        [InlineData("Independiente Medellín", "Ind. Rivadavia")]
        public void TimesDiferentes_NaoCorrespondem(string nomeBanco, string nomeFutnatv)
        {
            Assert.False(TimeNomeMatcher.SaoMesmoTime(nomeBanco, nomeFutnatv));
        }
    }
}
