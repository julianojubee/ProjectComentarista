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
        [InlineData("La Liga", "Campeonato Espanhol")]
        [InlineData("Campeonato Holandês", "Campeonato Neerlandês")]
        public void CompeticoesEquivalentes_Correspondem(string nomeBanco, string nomeFutnatv)
        {
            Assert.True(TimeNomeMatcher.SaoMesmaCompeticao(nomeBanco, nomeFutnatv));
        }

        [Theory]
        [InlineData("Campeonato Brasileiro", "Brasileirão - Série B")]
        [InlineData("Campeonato Brasileiro", "Brasileirão Feminino")]
        [InlineData("Bundesliga", "Campeonato Alemão (2ª div.)")]
        [InlineData("Premier League", "Copa da Liga Inglesa")]
        [InlineData("La Liga", "Campeonato Espanhol (2ª div.)")]
        [InlineData("Campeonato Holandês", "Campeonato Neerlandês (2ª div.)")]
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
        [InlineData("Gimnasia L.P.", "Gimnasia La Plata")]
        [InlineData("Vitória BA W", "Vitória")]           // futnatv larga a sigla de estado
        [InlineData("Internacional RS W", "Internacional")] // idem, e sem de-para: cai a sigla
        [InlineData("America Mineiro W", "América-MG")]
        [InlineData("Botafogo SP W", "Botafogo")]         // no feminino não há homônimo pra confundir
        [InlineData("AZ", "AZ Alkmaar")]
        [InlineData("Kasımpaşa", "Kasimpasa")]           // "ı" turco não decompõe em i + acento
        [InlineData("Al Ittihad", "Al-Ittihad")]         // hífen só de um lado
        [InlineData("Al Kholood", "Al-Kholood")]
        [InlineData("Gençlerbirliği S.K.", "Genclerbirligi")] // sigla com ponto no fim do nome
        [InlineData("São Bernardo", "São Bernardo FC")]   // sigla de agremiação só de um lado
        [InlineData("Çorum FK", "Corum")]
        [InlineData("Cruzeiro EC", "Cruzeiro")]
        [InlineData("Nautico Recife", "Náutico")]         // Série B: futnatv tira a cidade
        [InlineData("Sport Recife", "Sport")]
        [InlineData("FK Crvena Zvezda", "Red Star Belgrade")] // ESPN usa o nome em inglês
        [InlineData("Hapoel Beer Sheva", "Hapoel Be'er")]     // ESPN corta o nome no meio
        [InlineData("Celje", "NK Celje")]                     // sigla de agremiação no começo
        [InlineData("FC Porto", "Porto")]
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
        [InlineData("Botafogo SP", "Botafogo")]               // apelido do RJ não pode puxar o SP
        [InlineData("Atlético-GO", "Atlético")]               // sigla de estado não é de agremiação
        [InlineData("Sport Recife", "Sport Huancayo")]
        [InlineData("Vitória BA", "Vitória SC")]              // o Vitória de Guimarães fica de fora
        [InlineData("Gimnasia L.P.", "Gimnasia Mendoza")]     // outros Gimnasia não entram no grupo
        [InlineData("Botafogo SP W", "Botafogo RJ F")]        // dois lados com sigla: clubes diferentes
        [InlineData("Internacional RS", "Internacional SP")]  // fora do feminino a sigla não cai
        [InlineData("AC Milan", "Inter Milan")]               // tirar o prefixo não iguala o resto
        public void TimesDiferentes_NaoCorrespondem(string nomeBanco, string nomeFutnatv)
        {
            Assert.False(TimeNomeMatcher.SaoMesmoTime(nomeBanco, nomeFutnatv));
        }
    }
}
