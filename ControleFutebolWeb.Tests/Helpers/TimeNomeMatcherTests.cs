using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Este casamento é o que decide se o jogo do nosso banco recebe a transmissão de TV
    // vinda do futnatv.net. Quando ele erra, o jogo aparece sem canal (falso negativo) ou
    // com o canal de outro jogo (falso positivo) — os dois casos estão cobertos aqui.
    public class TimeNomeMatcherTests
    {
        [Theory]
        [InlineData("Serie A", "Campeonato Italiano")]
        [InlineData("Serie B", "Brasileirão - Série B")]
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
        [InlineData("Serie A", "Brasileirão - Série A")]      // "Serie A" seca é a italiana
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
        [InlineData("Wolves", "Wolverhampton")]
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
        // Apóstrofo só de um lado: a ESPN escreve "Newell's Old Boys" e o cadastro veio da
        // api-football sem o apóstrofo. Sem tratamento, o apóstrofo virava separador e
        // "newell s old boys" não casava com "newells old boys" — o jogo não era achado.
        [InlineData("Newells Old Boys", "Newell's Old Boys")]
        [InlineData("O'Higgins", "O'Higgins")]
        [InlineData("OHiggins", "O'Higgins")]
        [InlineData("Inter Club d'Escaldes", "Inter Club d'Escaldes")]
        [InlineData("Sport Recife", "Sport")]
        [InlineData("FK Crvena Zvezda", "Red Star Belgrade")] // ESPN usa o nome em inglês
        [InlineData("Hapoel Beer Sheva", "Hapoel Be'er")]     // ESPN corta o nome no meio
        [InlineData("Levski Sofia", "Levski")]
        [InlineData("Red Bull Salzburg", "RB Salzburg")]
        [InlineData("OFI", "OFI Crete")]
        [InlineData("Plzen", "Viktoria Plzen")]
        [InlineData("NEC Nijmegen", "NEC")]
        [InlineData("Ferencvarosi TC", "Ferencváros")]
        [InlineData("Lillestrom", "Lilleström")]
        [InlineData("Norwich City", "Norwich")]
        [InlineData("Olimpia", "Club Olimpia")]
        [InlineData("Cienciano", "Cienciano del Cusco")]
        [InlineData("Central Cordoba de Santiago", "Central Córdoba (Santiago del Estero)")]
        [InlineData("Gimnasia M.", "Gimnasia (Mendoza)")]
        [InlineData("LDU de Quito", "Liga de Quito")]              // ESPN escreve "Club" por extenso
        [InlineData("Vasco DA Gama", "Vasco da Gama")]
        [InlineData("Celje", "NK Celje")]                     // sigla de agremiação no começo
        [InlineData("FC Porto", "Porto")]
        [InlineData("AS Roma", "Roma")]                       // futnatv corta o "AS"
        [InlineData("Sporting CP", "Sporting")]
        [InlineData("Inter", "Internazionale")]               // Inter de Milão na Série A
        [InlineData("Ipswich", "Ipswich Town")]               // "Town" é sigla de agremiação
        [InlineData("Coventry", "Coventry City")]             // futnatv mantém o "City"
        [InlineData("Deportivo La Coruña", "Deportivo de La Coruña")] // preposição solta some
        [InlineData("Vasco da Gama", "Vasco")]
        [InlineData("Athletic Club", "Athletic Bilbao")]
        [InlineData("Athletic Club", "Athletic")]             // ESPN corta o "Club" na Série B
        [InlineData("Operario-PR", "Operário PR")]
        [InlineData("Celta Vigo", "Celta de Vigo")]
        [InlineData("Real Betis", "Betis")]                   // futnatv corta o "Real"
        [InlineData("Estac Troyes", "Troyes")]
        [InlineData("Stade Brestois 29", "Brest")]
        [InlineData("Marseille", "Olympique de Marseille")]   // futnatv usa o nome completo
        [InlineData("Al Taawoun", "Al-Taawon")]
        [InlineData("Al-Faisaly FC", "Al-Faysaly")]           // transliteração diferente do árabe
        [InlineData("Al-Qadisiyah FC", "Al-Qadsiah")]
        [InlineData("Al-Hilal Saudi FC", "Al-Hilal")]        // só o nosso lado traz o "Saudi"
        [InlineData("Bayern München", "Bayern de Munique")]   // futnatv aportuguesa Munique
        [InlineData("Bayern München", "Bayern Munich")]
        [InlineData("VfB Stuttgart", "Stuttgart")]            // futnatv corta o "VfB"
        [InlineData("VfL Wolfsburg", "Wolfsburg")]
        [InlineData("Paris Saint Germain", "PSG")]            // aqui é o futnatv que abrevia
        [InlineData("Sarmiento Junin", "Sarmiento")]
        [InlineData("Talleres Cordoba", "Talleres")]
        [InlineData("Central Cordoba de Santiago", "Central Córdoba")]
        [InlineData("Borussia Mönchengladbach", "B. M'gladbach")] // futnatv abrevia as duas palavras
        [InlineData("Hamburger SV", "Hamburgo")]
        [InlineData("1. FC Köln", "Köln")]                    // ano de fundação na frente do nome
        [InlineData("1899 Hoffenheim", "Hoffenheim")]
        [InlineData("FSV Mainz 05", "Mainz 05")]              // número no fim faz parte do nome
        [InlineData("SV Elversberg", "Elversberg")]
        [InlineData("SC Paderborn 07", "Paderborn 07")]
        public void TimesEquivalentes_Correspondem(string nomeBanco, string nomeFutnatv)
        {
            Assert.True(TimeNomeMatcher.SaoMesmoTime(nomeBanco, nomeFutnatv));
        }

        [Theory]
        [InlineData("Internacional", "Inter de Limeira")]
        [InlineData("Atletico-MG", "Atlético-GO")]
        [InlineData("Botafogo", "Botafogo-PB")]
        [InlineData("Atlético-MG", "Atlético Madrid")]
        [InlineData("Athletic Club", "Athletico-PR")]        // Athletic e Athletico são outros        // abreviação não pode virar vale-tudo
        [InlineData("Sporting CP", "Sporting Cristal")]
        [InlineData("Independiente Medellín", "Ind. Rivadavia")]
        [InlineData("Botafogo SP", "Botafogo")]               // apelido do RJ não pode puxar o SP
        [InlineData("Atlético-GO", "Atlético")]               // sigla de estado não é de agremiação
        [InlineData("Sport Recife", "Sport Huancayo")]
        [InlineData("Vitória BA", "Vitória SC")]              // o Vitória de Guimarães fica de fora
        [InlineData("Gimnasia L.P.", "Gimnasia Mendoza")]     // outros Gimnasia não entram no grupo
        [InlineData("Botafogo SP W", "Botafogo RJ F")]        // dois lados com sigla: clubes diferentes
        [InlineData("Internacional RS", "Internacional SP")]  // fora do feminino a sigla não cai
        [InlineData("AC Milan", "Inter Milan")]
        [InlineData("FSV Mainz 05", "Mainz")]                // o número do fim não pode cair
        [InlineData("Schalke 04", "Schalke 07")]
        [InlineData("Paris FC", "PSG")]                      // o outro Paris fica de fora do grupo
        [InlineData("Manchester City", "Manchester United")] // "City" não é sufixo descartável               // tirar o prefixo não iguala o resto
        public void TimesDiferentes_NaoCorrespondem(string nomeBanco, string nomeFutnatv)
        {
            Assert.False(TimeNomeMatcher.SaoMesmoTime(nomeBanco, nomeFutnatv));
        }
    
        // Campeonato do Catar: os 12 clubes da temporada, cadastro x FotMob. Foram
        // conferidos um a um contra /api/data/leagues?id=535 — sem este de-para a
        // importação não localiza a partida e o jogo fica sem estatística nenhuma,
        // que é exatamente o buraco que a fonte veio tapar.
        [Theory]
        [InlineData("Al Sadd", "Al-Sadd")]
        [InlineData("Al Ahli Doha", "Al-Ahli")]
        [InlineData("Al Duhail", "Al-Duhail SC")]
        [InlineData("Al-Sailiya", "Al-Sailiya")]
        [InlineData("Al-Arabi SC", "Al-Arabi")]
        [InlineData("Al Shamal", "Al-Shamal")]
        [InlineData("Al-Gharafa", "Al-Gharafa")]
        [InlineData("Al Shahaniya", "Al-Shahaniya")]
        [InlineData("Al-Rayyan SC", "Al-Rayyan")]
        [InlineData("Lusail City", "Lusail SC")]
        [InlineData("Al Wakrah", "Al-Wakrah")]
        [InlineData("Qatar SC", "Qatar SC")]
        public void Qatar_CadastroCasaComOFotMob(string nosso, string doFotMob)
        {
            Assert.True(TimeNomeMatcher.SaoMesmoTime(nosso, doFotMob),
                $"\"{nosso}\" deveria casar com \"{doFotMob}\".");
        }

        // O de-para do Catar não pode ter afrouxado o casamento a ponto de juntar
        // clubes diferentes que compartilham o prefixo "Al".
        [Theory]
        [InlineData("Al Sadd", "Al-Shamal")]
        [InlineData("Al-Arabi SC", "Al-Ahli")]
        [InlineData("Al Duhail", "Al-Rayyan")]
        public void Qatar_ClubesDiferentesContinuamSeparados(string a, string b)
        {
            Assert.False(TimeNomeMatcher.SaoMesmoTime(a, b));
        }
    }
}
