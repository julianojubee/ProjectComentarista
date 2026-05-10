using HtmlAgilityPack;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    public class TransfermarktPlayerInfo
    {
        public DateTime? DataNascimento { get; set; }
        public string? Nacionalidade { get; set; }
        public string? NomeCompleto { get; set; }
        public string? Clube { get; set; }
    }

    public class TransfermarktService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TransfermarktService> _logger;
        private readonly FutebolContext _context;

        // Mapeamento de nacionalidades (inglês/alemão → português)
        private static readonly Dictionary<string, string> _mapaFlags = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Brazil", "Brasil" }, { "Brasil", "Brasil" },
            { "Argentina", "Argentina" },
            { "Uruguay", "Uruguai" },
            { "Chile", "Chile" },
            { "Paraguay", "Paraguai" },
            { "Bolivia", "Bolívia" }, { "Bolivien", "Bolívia" },
            { "Peru", "Peru" },
            { "Ecuador", "Equador" },
            { "Colombia", "Colômbia" }, { "Kolumbien", "Colômbia" },
            { "Venezuela", "Venezuela" },
            { "Portugal", "Portugal" },
            { "Spain", "Espanha" }, { "Spanien", "Espanha" },
            { "France", "França" }, { "Frankreich", "França" },
            { "Germany", "Alemanha" }, { "Deutschland", "Alemanha" },
            { "Italy", "Itália" }, { "Italien", "Itália" },
            { "England", "Inglaterra" },
            { "Netherlands", "Holanda" }, { "Niederlande", "Holanda" },
            { "Belgium", "Bélgica" }, { "Belgien", "Bélgica" },
            { "Switzerland", "Suíça" }, { "Schweiz", "Suíça" },
            { "Croatia", "Croácia" }, { "Kroatien", "Croácia" },
            { "Mexico", "México" }, { "Mexiko", "México" },
            { "United States", "Estados Unidos" }, { "USA", "Estados Unidos" },
            { "Canada", "Canadá" }, { "Kanada", "Canadá" },
            { "Morocco", "Marrocos" }, { "Marokko", "Marrocos" },
            { "Senegal", "Senegal" },
            { "Ghana", "Gana" },
            { "Ivory Coast", "Costa do Marfim" },
            { "Nigeria", "Nigéria" },
            { "Cameroon", "Camarões" }, { "Kamerun", "Camarões" },
            { "Democratic Republic of Congo", "República Democrática do Congo" },
            { "Angola", "Angola" },
            { "Ukraine", "Ucrânia" },
            { "Serbia", "Sérvia" }, { "Serbien", "Sérvia" },
            { "Denmark", "Dinamarca" }, { "Dänemark", "Dinamarca" },
            { "Greece", "Grécia" }, { "Griechenland", "Grécia" },
            { "Panama", "Panamá" },
            { "Guinea", "Guiné" },
        };

        // Dicionários para correções/aliases (preencha conforme necessário)
        // Ex.: {"Internacional SC", "Internacional"}, {"Atletico-MG", "Atlético Mineiro"}
        private static readonly Dictionary<string, string> _teamAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // colocar aqui correções conhecidas entre oGol e os nomes do seu banco
         
            {"Carabobo", "Carabobo FC"},
            {"Caracas", "Caracas"},
            {"Nacional Asuncion", "Nacional Asuncion"},
            {"Vasco", "CR Vasco da Gama"},
            {"São Paulo", "São Paulo FC"},
            {"Olimpia Asuncion", "Olimpia Asuncion"},
            {"Macará", "Macara"},
            {"Orense", "Orense"},
            {"Santos", "Santos FC"},
            {"Millonarios", "Millonarios"},
            {"America de Cali", "America De Cali"},
            {"Palestino", "Palestino"},
            {"Dep. Cuenca", "Dep. Cuenca"},
            {"FBC Melgar", "FBC Melgar"},
            {"Juventud", "Juventud de Las Piedras"},
            {"River Plate", "River Plate"},
            {"Atl. Nacional", "Atl. Nacional"},
            {"Botafogo", "Botafogo FR"},
            {"Barra", "Barra FC"},
            {"A. Italiano", "A. Italiano"},
            {"Puerto Cabello", "Puerto Cabello"},
            {"Dep. Riestra", "Dep. Riestra"},
            {"Cobresal", "Cobresal"},
            {"Metropolitanos", "Metropolitanos"},
            {"Recoleta", "Recoleta"},
            {"CA Mineiro", "CA Mineiro"},
            {"Cienciano", "Cienciano"},
            {"Sportivo Trinidense", "Sportivo Trinidense"},
            {"Deportivo Garcilaso", "Deportivo Garcilaso"},
            {"SA Bulo Bulo", "SA Bulo Bulo"},
            {"RB Bragantino", "RB Bragantino"},
            {"Alianza Atl.", "Alianza Atl."},
            {"San Lorenzo", "San Lorenzo"},
            {"Guabirá", "Guabira"},
            {"Montevideo City", "Montevideo City"},
            {"Blooming", "Blooming"},
            {"Racing Montevideo", "Racing Montevideo"},
            {"Bucaramanga", "Bucaramanga"},
            {"U. De Chile", "U. De Chile"},
            {"Independiente Rivadavia", "CS Independiente Rivadavia"},
            {"Boston River", "Boston River"},
            {"Grêmio", "Grêmio FBPA"},
            {"GAS", "GAS"},
            {"Defensor", "Defensor Sp."},
            {"O'Higgins", "O'Higgins FC"},
            {"Tigre", "Tigre"},
            {"Racing Club", "Racing Club"},




        };

        // Dicionário de variantes de nomes de jogadores (preencha conforme necessário)
        private static readonly Dictionary<string, string> _playerAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // {"J. Silva", "João Silva"}
            
            {"P. de Paula", "Pedro de Paula"},
            {"S. Pino", "Steffan Pino"},
            {"V. Mendonça", "Vinicius Mendonça"},
            {"J. Rattalino", "Juan Rattalino"},
            {"M. Zaracho", "Matías Zaracho"},
            {"L. González", "Lucas González"},
            {"R. Júnior", "Robson Júnior"},
            {"A. Ramos", "Alejandro Ramos"},
            {"E. Carrión", "Edgar Carrión"},
            {"L. Maestre", "Luis Maestre"},
            {"G. Reis", "Gustavo Reis"},
            {"G. Tapia", "Gonzalo Tapia"},
            {"P. Ramírez", "Pedro Ramírez"},
            {"M. Cissé", "Mamady Cissé"},
            {"G. Ayine", "George Ayine"},
            {"F. Machado", "Facundo Machado"},
            {"J. M. Jorge", "Juan Manuel Jorge"},
            {"T. Volpi", "Tiago Volpi"},
            {"A. da Silva", "Alexander da Silva"},
            {"N. Ribeiro", "Nathan Ribeiro"},
            {"E. Matus", "Esteban Matus"},
            {"T. Chamba", "Tommy Chamba"},
            {"Y. Gamero", "Yimy Gamero"},
            {"Tchê Tchê", "Tchê Tchê"},
            {"M. Guerrero", "Maximiliano Guerrero"},
            {"M. Fedor", "Miku Fedor"},
            {"S. Guerrero", "Sebastian Guerrero"},
            {"G. Delfim", "Gabriel Delfim"},
            {"J. Herrera", "Jonathan Herrera"},
            {"A. Wlk", "Allan Wlk"},
            {"A. Frías", "Adonis Frías"},
            {"M. Núñez", "Matías Núñez"},
            {"M. Dufour", "Matías Dufour"},
            {"Julio Herrera", "Julio Herrera"},
            {"A. Samudio", "Alejandro Samudio"},
            {"M. De Lima", "Marcelo De Lima"},
            {"Matheuzinho", "Matheuzinho"},
            {"K. Pascini", "Kauã Pascini"},
            {"B. Tamayo", "Bianneider Tamayo"},
            {"J. del Castillo", "Jorge del Castillo"},
            {"A. Ladstatter", "Agustin Ladstatter"},
            {"J. D'Arrigo", "Jhamir D'Arrigo"},
            {"M. Villarroel", "Moisés Villarroel"},
            {"B. Vereza", "Breno Vereza"},
            {"G. Rodríguez", "Gastón Rodríguez"},
            {"Ferreira", "Ferreira"},
            {"J. Tapia", "Joaquín Tapia"},
            {"P. Erustes", "Pablo Erustes"},
            {"K. Cataño", "Kevin Cataño"},
            {"B. Da Silva", "Beto Da Silva"},
            {"N. Sosa", "Nicolás Sosa"},
            {"E. Díaz", "Enzo Díaz"},
            {"Ythallo", "Ythallo"},
            {"C. Ramos", "Carlos Ramos"},
            {"L. Ferro", "Luca Ferro"},
            {"L. Freitas", "Lucas Freitas"},
            {"I. Rodríguez", "Ignacio Rodríguez"},
            {"I. Subiabre", "Ian Subiabre"},
            {"Adelan Santos Da Silva", "Adelan Santos Da Silva"},
            {"B. Ferreira", "Brahian Ferreira"},
            {"E. Perlaza", "Elvis Perlaza"},
            {"E. Fereira", "Eduardo Fereira"},
            {"C. Souza", "Cristian Souza"},
            {"M. Salas", "Maximiliano Salas"},
            {"F. González", "Francisco González"},
            {"R. Ramírez", "Roberto Ramírez"},
            {"Alison", "Alison"},
            {"A. Mercado", "Alan Mercado"},
            {"G. Viera", "Gustavo Viera"},
            {"L. Montenegro", "Lautaro Montenegro"},
            {"M. Rivero", "Mateo Rivero"},
            {"M. Riquelme", "Matías Riquelme"},
            {"Diego", "Diego"},
            {"M. Viña", "Matías Viña"},
            {"M. Diaz", "Melvin Diaz"},
            {"J. Movillo", "José Movillo"},
            {"T. Goncalves", "Tiago Goncalves"},
            {"Weverton", "Weverton"},
            {"L. Pons", "Luciano Pons"},
            {"J. Chura", "Jeyson Chura"},
            {"Y. Cabral", "Yonatan Cabral"},
            {"H. Castillo", "Harlen Castillo"},
            {"Álex Vázquez", "Álex Vázquez"},
            {"J. Ingratti", "José Ingratti"},
            {"F. Torgnascioli", "Franco Torgnascioli"},
            {"N. Cabanillas", "Nelson Cabanillas"},
            {"L. Moura", "Lucas Moura"},
            {"L. Eduardo", "Lucas Eduardo"},
            {"L. Pavez Munoz", "Luis Pavez Munoz"},
            {"Júnior Alonso", "Júnior Alonso"},
            {"I. Sosa", "Ignacio Sosa"},
            {"Adson", "Adson"},
            {"Dodi", "Dodi"},
            {"Y. Yustiz", "Yhonattan Yustiz"},
            {"M. Belém", "Matheus Belém"},
            {"G. Flores", "Gonzalo Flores"},
            {"H. Quintana", "Hugo Quintana"},
            {"T. Serrago", "Tiago Serrago"},
            {"A. Zárate", "Aldair Zárate"},
            {"C. Valle", "Caio Valle"},
            {"L. Agazzi", "Lucas Agazzi"},
            {"J. De Santis", "Jeriel De Santis"},
            {"Geanfranco Rodríguez", "Geanfranco Rodríguez"},
            {"B. Lopes", "Bruno Lopes"},
            {"Riquelme Henrique", "Riquelme Henrique"},
            {"J. Brea", "Julián Brea"},
            {"M. Doria", "Matheus Doria"},
            {"V. Lira", "Vinícius Lira"},
            {"V. Espinoza", "Vicente Espinoza"},
            {"S. Etchebarne", "Santiago Etchebarne"},
            {"W. Leonardo", "Wagner Leonardo"},
            {"G. Meléndez", "Geremías Meléndez"},
            {"J. Santacruz", "Juan Santacruz"},
            {"Jose Camacho", "Jose Camacho"},
            {"Lucas González 2", "Lucas González"},
            {"J. Quintana", "Juan Quintana"},
            {"F. López", "Franner López"},
            {"J. Nunes", "Jhuan Nunes"},
            {"R. Monteiro", "Rafael Monteiro"},
            {"D. Cuero", "Darwin Cuero"},
            {"S. Cavero", "Sebastian Cavero"},
            {"Vavá", "Vavá"},
            {"N. Ferreira", "Nelson Ferreira"},
            {"M. Rojo", "Marcos Rojo"},
            {"M. Uribe", "Matheus Uribe"},
            {"E. Bello", "Eduard Bello"},
            {"T. Conechny", "Tomás Conechny"},
            {"A. Machado", "Alexander Machado"},
            {"K. Roa", "Keiber Roa"},
            {"G. Rivero", "Germán Rivero"},
            {"Lyanco", "Lyanco"},
            {"L. Roldán", "Leonel Roldán"},
            {"F. Pardo", "Franco Pardo"},
            {"F. Jaramillo", "Felipe Jaramillo"},
            {"A. Santander", "Alejandro Santander"},
            {"B. Rodriguez", "Baltasar Rodriguez"},
            {"Marlon", "Marlon"},
            {"G. Scarpa", "Gustavo Scarpa"},
            {"Andre", "Andre"},
            {"D. Romero", "David Romero"},
            {"D. Villalba", "David Villalba"},
            {"O. Gimenez", "Oscar Gimenez"},
            {"Cauã Campos", "Cauã Campos"},
            {"B. Zortea", "Bernardo Zortea"},
            {"S. Jesús", "Samuel Jesús"},
            {"F. Risso", "Franco Risso"},
            {"T. Ribeiro", "Thiago Ribeiro"},
            {"Hugo", "Hugo"},
            {"A. Amado", "Agustín Amado"},
            {"J. Schmidt", "João Schmidt"},
            {"S. Ramírez", "Sharif Ramírez"},
            {"E. Valencia", "Esteban Valencia"},
            {"L. Monzon", "Lucas Monzon"},
            {"M. Ortíz", "Marcelo Ortíz"},
            {"A. Sarmiento", "Andrés Sarmiento"},
            {"Fábio", "Fábio"},
            {"G. Montes", "Gonzalo Montes"},
            {"R. Acosta", "Rafael Acosta"},
            {"F. Bonfiglio", "Francisco Bonfiglio"},
            {"N. Figueroa", "Nicolás Figueroa"},
            {"J. Arias", "Jorge Arias"},
            {"C. Cáceda", "Carlos Cáceda"},
            {"A. Astudillo", "Aaron Astudillo"},
            {"J. García", "José García"},
            {"Luís Gustavo", "Luís Gustavo"},
            {"Júnior Santos", "Júnior Santos"},
            {"R. Arboleda", "Robert Arboleda"},
            {"S. López", "Santiago López"},
            {"Lucca Marques", "Lucca Marques"},
            {"P. Lima", "Pablo Lima"},
            {"M. Vinagre", "Marcos Vinagre"},
            {"B. Kociubinski", "Bautista Kociubinski"},
            {"B. Medina", "Blas Medina"},
            {"M. Fernandes", "Matheus Fernandes"},
            {"A. López", "Andy López"},
            {"Riquelme Avellar", "Riquelme Avellar"},
            {"D. Quintero", "Darwin Quintero"},
            {"G. De Amores", "Guillermo De Amores"},
            {"F. Negrucci", "Felipe Negrucci"},
            {"A. González", "Agustín González"},
            {"K. Sandoval", "Kevin Sandoval"},
            {"L. Veríssimo", "Lucas Veríssimo"},
            {"H. Freitas", "Henrique Freitas"},
            {"G. Pinto", "Gabriel Pinto"},
            {"Kaua Branco", "Kaua Branco"},
            {"S. Mina", "Sixto Mina"},
            {"E. Cabrera", "Elías Cabrera"},
            {"R. Chagas", "Rodrigo Chagas"},
            {"G. Cristaldo", "Gustavo Cristaldo"},
            {"Young", "Young"},
            {"A. Cruz", "Alejo Cruz"},
            {"D. Monreal", "Diego Monreal"},
            {"J. Elias", "Jalil Elias"},
            {"A. Franco", "Alan Franco"},
            {"R. Bustos", "Ronald Bustos"},
            {"B. Uraezaña", "Braulio Uraezaña"},
            {"J. Soto", "Jorge Soto"},
            {"B. Perez", "Benjamin Perez"},
            {"C. Romero", "César Romero"},
            {"R. Contreras", "Rodrigo Contreras"},
            {"S. Torres", "Saúl Torres"},
            {"G. Almada", "Gustavo Almada"},
            {"G. Martirena", "Gastón Martirena"},
            {"G. Barbosa", "Gabriel Barbosa"},
            {"G. Soares", "Gabriel Soares"},
            {"S. Velásquez", "Samuel Velásquez"},
            {"M. Cardoso", "Murillo Cardoso"},
            {"J. Bizama", "José Bizama"},
            {"C. Bordacahar", "Cristian Bordacahar"},
            {"D. Navarro", "Diego Navarro"},
            {"Raphael", "Raphael"},
            {"G. Lazaroni", "Guilherme Lazaroni"},
            {"S. González", "Sebastián González"},
            {"J. Lira", "João Lira"},
            {"G. Reyna", "Gonzalo Reyna"},
            {"S. Mosquera", "Sebastián Mosquera"},
            {"R. Cristobal", "Ramiro Cristobal"},
            {"A. Alves", "Arthur Alves"},
            {"Rodrigo", "Rodrigo"},
            {"B. Zuculini", "Bruno Zuculini"},
            {"S. Vásquez", "Sergio Vásquez"},
            {"R. Figueroa", "Richard Figueroa"},
            {"I. Mosquera", "Ignacio Mosquera"},
            {"J. Colmán", "Josué Colmán"},
            {"E. Porto", "Eduardo Porto"},
            {"G. Soto", "Guillermo Soto"},
            {"J. Lucumí", "Jan Lucumí"},
            {"A. Cañete", "Axel Cañete"},
            {"Pedro", "Pedro"},
            {"Rafinha", "Rafinha"},
            {"K. Pantaleão", "Kaio Pantaleão"},
            {"Lider Yanarico", "Lider Yanarico"},
            {"A. Medina", "Alexis Medina"},
            {"R. Huendra", "Rodrigo Huendra"},
            {"M. Márquez", "Mauricio Márquez"},
            {"J. Berríos", "Joshuan Berríos"},
            {"J. Gimenez", "Javier Gimenez"},
            {"J. Alencar", "João Alencar"},
            {"A. Ruiz Diaz", "Armando Ruiz Diaz"},
            {"R. Cardozo", "Rudy Cardozo"},
            {"X. Moreno", "Xavi Moreno"},
            {"M. Portillo", "Milciades Portillo"},
            {"F. Colidio", "Facundo Colidio"},
            {"Gabriel", "Gabriel"},
            {"R. Sánchez", "Renzo Sánchez"},
            {"L. Mancinelli", "Lucas Mancinelli"},
            {"R. Gonzaga", "Rafael Gonzaga"},
            {"R. Sayavedra", "Rodrigo Sayavedra"},
            {"R. Gallo", "Rodrigo Gallo"},
            {"J. Pinos", "Jorge Pinos"},
            {"R. Báez", "Ronaldo Báez"},
            {"F. Perez", "Facundo Perez"},
            {"F. Echeguren", "Facundo Echeguren"},
            {"G. Olveira", "Gastón Olveira"},
            {"M. Smarra", "Mauro Smarra"},
            {"V. Melo Vitão", "Victor Melo Vitão"},
            {"W. Correa", "Wilfred Correa"},
            {"C. Benitez", "Cesar Benitez"},
            {"Ronal Dominguez", "Ronal Dominguez"},
            {"J. Bosca Fraquelli", "Juan Bosca Fraquelli"},
            {"J. Vegas", "Juan Vegas"},
            {"I. Pitta", "Isidro Pitta"},
            {"G. Rojas", "Gabriel Rojas"},
            {"T. Ángel", "Tomás Ángel"},
            {"Vitinho", "Vitinho"},
            {"W. Kannemann", "Walter Kannemann"},
            {"Jose Hernandez Chavez", "Jose Hernandez Chavez"},
            {"T. Vecino", "Thiago Vecino"},
            {"F. Dafonte", "Federico Dafonte"},
            {"Tetê", "Tetê"},
            {"J. Calleri", "Jonathan Calleri"},
            {"J. Feliu", "Juan Feliu"},
            {"N. Leguizamón", "Nicolás Leguizamón"},
            {"A. Vásquez", "Anthony Vásquez"},
            {"Y. Orozco", "Yohandry Orozco"},
            {"A. Hohberg", "Alejandro Hohberg"},
            {"A. Gordillo", "Anthony Gordillo"},
            {"F. Zanelatto", "Franco Zanelatto"},
            {"W. Tesillo", "William Tesillo"},
            {"Marcelinho", "Marcelinho"},
            {"Moises", "Moises"},
            {"L. Guzmán", "Lautaro Guzmán"},
            {"Kauan", "Kauan"},
            {"L. Silva", "Lucas Silva"},
            {"M. Diaz 2", "Marlon Diaz"},
            {"B. Rollheiser", "Benjamín Rollheiser"},
            {"Gustavinho", "Gustavinho"},
            {"Bernard", "Bernard"},
            {"J. Victor", "João Victor"},
            {"G. Bamba", "Giovani Bamba"},
            {"Dewar Victoria", "Dewar Victoria"},
            {"Miguelito", "Miguelito"},
            {"M. Covea", "Michael Covea"},
            {"F. Cairus", "Felipe Cairus"},
            {"Newton", "Newton"},
            {"A. Moreno", "Andrés Moreno"},
            {"A. Gutiérrez", "Aldair Gutiérrez"},
            {"C. Ortíz", "Christian Ortíz"},
            {"F. Cambeses", "Facundo Cambeses"},
            {"F. Montes", "Francisco Montes"},
            {"P. Lago", "Pablo Lago"},
            {"Nadson Maia", "Nadson Maia"},
            {"Willian", "Willian"},
            {"Cristhian Loor", "Cristhian Loor"},
            {"G. Aguirre", "Gonzalo Aguirre"},
            {"E. Castillo", "Edson Castillo"},
            {"F. Manenti", "Francisco Manenti"},
            {"J. Devecchi", "José Devecchi"},
            {"R. Lutkowski", "Rafael Lutkowski"},
            {"E. Farías", "Edder Farías"},
            {"Allan", "Allan"},
            {"E. Noriega", "Erick Noriega"},
            {"M. Ferrel", "Manuel Ferrel"},
            {"M. Roki", "Milan Roki"},
            {"F. Álvarez", "Federico Álvarez"},
            {"Joyce Ossa", "Joyce Ossa"},
            {"Pedrinho", "Pedrinho"},
            {"J. M. Rengifo", "Juan Manuel Rengifo"},
            {"N. Masskooni", "Nicolas Masskooni"},
            {"J. Tiznado", "José Tiznado"},
            {"I. Bailone", "Ignacio Bailone"},
            {"A. Frugone", "Axel Frugone"},
            {"R. Palma", "Ryan Palma"},
            {"S. Garcia", "Simon Garcia"},
            {"F. Hormazábal", "Fabián Hormazábal"},
            {"B. Cuesta", "Bernardo Cuesta"},
            {"J. Hurtado", "José Hurtado"},
            {"J. Pérez", "Jimmy Pérez"},
            {"Matheusinho", "Matheusinho"},
            {"A. Figueroa", "Angel Figueroa"},
            {"G. Abdias", "Gabriel Abdias"},
            {"M. Martins", "Matheus Martins"},
            {"F. Sorondo", "Francisco Sorondo"},
            {"R. Menacho Miranda", "Rafael Menacho Miranda"},
            {"J. Correa", "Joaquín Correa"},
            {"Cleiton", "Cleiton"},
            {"A. Álvarez", "Agustín Álvarez"},
            {"E. Batalla", "Emerson Batalla"},
            {"J. Marchán", "Jhon Marchán"},
            {"G. De Los Santos", "Guillermo De Los Santos"},
            {"J. Mosqueira", "Joaquin Mosqueira"},
            {"E. Mendoza", "Esdras Mendoza"},
            {"S. Mendes", "Samuel Mendes"},
            {"R. Brazionis", "Ramiro Brazionis"},
            {"Natanael", "Natanael"},
            {"P. Díaz", "Paulo Díaz"},
            {"L. Diaz", "Lautaro Diaz"},
            {"Hulk", "Hulk"},
            {"S. Gallegos", "Sebastián Gallegos"},
            {"I. Russo", "Ignacio Russo"},
            {"J. Riasco", "José Riasco"},
            {"M. Jiménez", "Martín Jiménez"},
            {"H. Linares", "Heiber Linares"},
            {"M. Maccari", "Mateo Maccari"},
            {"G. Gómez", "Gastón Gómez"},
            {"F. Pradella", "Fernando Pradella"},
            {"J. Jiménez", "John Jiménez"},
            {"Fernando", "Fernando"},
            {"L. Camilo", "Lucas Camilo"},
            {"K. Sotto", "Kevin Sotto"},
            {"M. Viera", "Mateo Viera"},
            {"E. Vega", "Edison Vega"},
            {"H. Caparó", "Henry Caparó"},
            {"Cédric", "Cédric"},
            {"F. Cardozo", "Fernando Cardozo"},
            {"L. Ayala", "Luis Ayala"},
            {"L. Espinoza", "Leonar Espinoza"},
            {"E. Valderrey", "Ely Valderrey"},
            {"M. Sarrafiore", "Martín Sarrafiore"},
            {"A. Jaimes", "Angel Jaimes"},
            {"C. Santos", "Carlos Santos"},
            {"N. Banegas", "Nahuel Banegas"},
            {"T. Chavez", "Thiago Chavez"},
            {"Y. Goitia", "Yonatan Goitia"},
            {"S. Torales", "Silvio Torales"},
            {"N. Ramírez", "Nicolás Ramírez"},
            {"L. Paul De los Santos", "Lucas Paul De los Santos"},
            {"J. Vera", "Juan Vera"},
            {"B. Antúnez", "Bruno Antúnez"},
            {"E. Tortolero", "Edson Tortolero"},
            {"Nuno Moreira", "Nuno Moreira"},
            {"E. Neira", "Ezequiel Neira"},
            {"S. Valencia", "Sebastián Valencia"},
            {"C. Larotonda", "Christian Larotonda"},
            {"T. Rodríguez Pagano", "Teo Rodríguez Pagano"},
            {"F. Bechtholdt", "Franco Bechtholdt"},
            {"N. Medina", "Nicolás Medina"},
            {"M. Bracamonte", "Mariano Bracamonte"},
            {"P. Rios", "Pedro Rios"},
            {"J. Mendieta", "Jesus Mendieta"},
            {"F. Meza", "Fernando Meza"},
            {"A. Terrazas", "Adalid Terrazas"},
            {"J. Fernandes", "Jean Fernandes"},
            {"A. Llinás", "Andrés Llinás"},
            {"M. Hornos", "Marcelo Hornos"},
            {"T. Mendes", "Thiago Mendes"},
            {"M. Villaroel", "Miguel Villaroel"},
            {"L. Duré", "Lucas Duré"},
            {"S. Vargas", "Sebastian Vargas"},
            {"Tetê 2", "Tetê"},
            {"J. Briceño", "José Briceño"},
            {"B. García", "Bryan García"},
            {"A. Fernández", "Adrián Fernández"},
            {"H. A. Santos Cardoso", "Hugo Alexandre Santos Cardoso"},
            {"A. Telles", "Alex Telles"},
            {"G. Kagelmacher", "Gary Kagelmacher"},
            {"B. Valenzuela", "Benjamín Valenzuela"},
            {"F. Loyola", "Favian Loyola"},
            {"J. Mercado", "Juan Mercado"},
            {"C. Tovar", "Cristian Tovar"},
            {"D. da Silva", "Dener da Silva"},
            {"D. Robles", "David Robles"},
            {"P. Núnez", "Patricio Núnez"},
            {"S. Mendoza", "Sergio Mendoza"},
            {"A. Cuello", "Alexis Cuello"},
            {"S. Ramírez 2", "Saimon Ramírez"},
            {"F. Zenobio", "Felipe Zenobio"},
            {"J. Yendis", "Jesus Yendis"},
            {"J. Quintero", "Jesús Quintero"},
            {"C. Oliva", "Christian Oliva"},
            {"S. Ferreira", "Sebastián Ferreira"},
            {"J. Capixaba", "Juninho Capixaba"},
            {"A. Bahachille", "Abraham Bahachille"},
            {"F. Pérez", "Franco Pérez"},
            {"Marcelinho Braz", "Marcelinho Braz"},
            {"J. Altamirano", "Javier Altamirano"},
            {"W. Vivas", "Weimar Vivas"},
            {"S. Rodríguez", "Salomón Rodríguez"},
            {"E. Canales", "Erick Canales"},
            {"H. A. Benítez", "Hugo Adrián Benítez"},
            {"Fabinho", "Fabinho"},
            {"M. Medranda", "Marlon Medranda"},
            {"A. Gomes", "Alexsander Gomes"},
            {"Renatinha", "Renatinha"},
            {"P. Loza", "Percy Loza"},
            {"Ó. Quiñónez", "Óscar Quiñónez"},
            {"P. Grass", "Pablo Grass"},
            {"J. I. González", "Juan Ignacio González"},
            {"F. Arancibia", "Francisco Arancibia"},
            {"B. Carrasco", "Bryan Carrasco"},
            {"Jefinho", "Jefinho"},
            {"J. Nsumoh", "Johnson Nsumoh"},
            {"I. Garguez", "Ian Garguez"},
            {"J. Romagnoli", "Juan Romagnoli"},
            {"H. Diaz", "Henrry Diaz"},
            {"P. Zubczuk", "Patrick Zubczuk"},
            {"E. Sasha", "Eduardo Sasha"},
            {"F. Henrique", "Fernando Henrique"},
            {"I. Arce", "Ignacio Arce"},
            {"G. Abrego", "Gonzalo Abrego"},
            {"F. González", "Facundo González"},
            {"A. Rojas", "Alvaro Rojas"},
            {"L. Mago", "Luis Mago"},
            {"A. Canete", "Alexis Canete"},
            {"S. Pérez", "Sebastián Pérez"},
            {"André Silva", "André Silva"},
            {"S. Lencina", "Santiago Lencina"},
            {"G. Rodríguez 2", "Guzmán Rodríguez"},
            {"R. Huerta", "Renato Huerta"},
            {"F. Volpi", "Fabian Volpi"},
            {"D. M. Silva", "David Macalister Silva"},
            {"M. Brizuela", "Miguel Brizuela"},
            {"G. Corujo", "Guzmán Corujo"},
            {"L. Matheus", "Luis Matheus"},
            {"G. Charrupi", "Gustavo Charrupi"},
            {"E. Hermoza", "Eder Hermoza"},
            {"J. Romaña", "Jhohan Romaña"},
            {"O. Nuñez", "Orlando Nuñez"},
            {"E. Más", "Emmanuel Más"},
            {"L. Martínez", "Loureins Martínez"},
            {"D. Meza", "Dimas Meza"},
            {"K. Ortiz", "Kavier Ortiz"},
            {"F. Suárez", "Franco Suárez"},
            {"Filipe", "Filipe"},
            {"S. Mendoza 2", "Sebastián Mendoza"},
            {"D. Vargas", "Diego Vargas"},
            {"W. Freitas", "Wallysson Freitas"},
            {"Vanderlan", "Vanderlan"},
            {"Mayke", "Mayke"},
            {"Diogenes", "Diogenes"},
            {"P. Velasco", "Pedro Velasco"},
            {"M. Cassierra", "Mateo Cassierra"},
            {"Brenner", "Brenner"},
            {"Á. Barreal", "Álvaro Barreal"},
            {"J. Delgado", "Juan Delgado"},
            {"C. Moreno", "Christian Moreno"},
            {"D. Machís", "Darwin Machís"},
            {"M. Perez", "Marcelo Perez"},
            {"M. Lazo", "Matías Lazo"},
            {"M. Ponte", "Mateo Ponte"},
            {"J. Portales", "Jefferson Portales"},
            {"R. Sandoval", "Rodrigo Sandoval"},
            {"F. Romero", "Franco Romero"},
            {"E. Guevara", "Eddie Guevara"},
            {"A. Izaque", "Arthur Izaque"},
            {"F. Posse", "Franco Posse"},
            {"C. Piña", "Cristóbal Piña"},
            {"N. Colunga", "Nadhir Colunga"},
            {"V. Zenteno", "Vicente Zenteno"},
            {"C. Vinícius", "Carlos Vinícius"},
            {"L. Galarza", "Lucas Galarza"},
            {"Arthur", "Arthur"},
            {"M. Piedra", "Mateo Piedra"},
            {"K. de Assis", "Kaio de Assis"},
            {"Riquelme", "Riquelme"},
            {"J. Moreira", "João Moreira"},
            {"B. Praxedes", "Bruno Praxedes"},
            {"W. Santos", "Weliton Santos"},
            {"F. Coronel", "Franco Coronel"},
            {"M. Miljevic", "Matko Miljevic"},
            {"D. Zúñiga", "Dilan Zúñiga"},
            {"P. Henrique", "Pedro Henrique"},
            {"M. Caldas", "Miguel Caldas"},
            {"J. Mohor", "Jordán Mohor"},
            {"A. González 2", "Alexander González"},
            {"É. Alemão", "Éverton Alemão"},
            {"P. Gabriel", "Pedro Gabriel"},
            {"R. Auzmendi", "Rodrigo Auzmendi"},
            {"M. Zegarra", "Matías Zegarra"},
            {"I. Gudiño", "Irving Gudiño"},
            {"N. Maná", "Nicolás Maná"},
            {"Cauê", "Cauê"},
            {"A. Morosini", "Angel Morosini"},
            {"N. Parra", "Neider Parra"},
            {"Zé Rafael", "Zé Rafael"},
            {"C. Medina", "Cristian Medina"},
            {"Igor", "Igor"},
            {"W. Guzmán", "Williams Guzmán"},
            {"L. Bruera", "Lucas Bruera"},
            {"Borges", "Borges"},
            {"L. Pereyra", "Lautaro Pereyra"},
            {"G. Barreto", "Gerson Barreto"},
            {"Maik", "Maik"},
            {"J. Enamorado", "José Enamorado"},
            {"T. Habib", "Tomas Habib"},
            {"L. Osorio", "Luis Osorio"},
            {"Guilmar Centella", "Guilmar Centella"},
            {"S. Cáceres", "Sebastián Cáceres"},
            {"Y. Sulbaran", "Yerwin Sulbaran"},
            {"V. Samudio", "Victor Samudio"},
            {"I. López", "Iván López"},
            {"D. Cheuquepal", "Diego Cheuquepal"},
            {"Anthony", "Anthony"},
            {"A. Castillo", "Arnaldo Castillo"},
            {"B. Bentaberry", "Brayan BentabERRY"},
            {"Bastos", "Bastos"},
            {"L. Navarro", "Lautaro Navarro"},
            {"Samuel", "Samuel"},
            {"S. Rodríguez 2", "Santiago Rodríguez"},
            {"A. Castro", "Alex Castro"},
            {"E. Pernía", "Edwuin Pernía"},
            {"F. Amuzu", "Francis Amuzu"},
            {"R. Peralta", "Richard Peralta"},
            {"N. Bandiera", "Neri Bandiera"},
            {"E. Ramírez", "Eric Ramírez"},
            {"L. Castro", "Leonardo Castro"},
            {"J. Pedro", "João Pedro"},
            {"I. Leguizamon", "Ivan Leguizamon"},
            {"G. Castellón", "Gabriel Castellón"},
            {"G. Tapia 2", "Gonzalo Tapia"},
            {"N. Wunsch", "Nicolás Wunsch"},
            {"Dudu", "Dudu"},
            {"N. Marotta", "Nicolás Marotta"},
            {"M. Tello", "Martin Tello"},
            {"D. Coelho", "Diego Coelho"},
            {"M. García", "Mateo García"},
            {"A. Minda", "Alan Minda"},
            {"L. Villalba", "Lucas Villalba"},
            {"R. Ferrufino Arauz", "Roler Ferrufino Arauz"},
            {"A. Robles", "Ademar Robles"},
            {"R. Lezama", "Rodhier Lezama"},
            {"A. Lorenzo", "Alan Lorenzo"},
            {"T. Cayuqueo", "Tomás Cayuqueo"},
            {"Danielzinho", "Danielzinho"},
            {"P. Maia", "Pablo Maia"},
            {"J. Neto", "João Neto"},
            {"F. Rodriguez", "Francisco Rodriguez"},
            {"F. Barrios", "Francisco Barrios"},
            {"G. Cotugno", "Guillermo Cotugno"},
            {"R. Rodrigues", "Ryan Rodrigues"},
            {"J. Sabino", "José Sabino"},
            {"B. Carvallo", "Bryan Carvallo"},
            {"L. Díaz", "Leandro Díaz"},
            {"I. Vinícius", "Igor Vinícius"},
            {"E. Elizalde", "Edgar Elizalde"},
            {"W. Baez", "Wilfrido Baez"},
            {"S. Solari", "Santiago Solari"},
            {"D. Campos", "Diego Campos"},
            {"R. Ortiz", "Richard Ortiz"},
            {"M. Di Cesare", "Marco Di Cesare"},
            {"H. Contreras", "Harrison Contreras"},
            {"J. F. Alfaro", "Juan Fernando Alfaro"},
            {"Y. Bravo", "Yinsop Bravo"},
            {"F. Romero 2", "Fernando Romero"},
            {"I. Gomes", "Igor Gomes"},
            {"J. Gutiérrez", "Johan Gutiérrez"},
            {"R. Flores", "Robinson Flores"},
            {"A. Morelos", "Alfredo Morelos"},
            {"Robert", "Robert"},
            {"M. Maitan", "Marcos Maitan"},
            {"R. Hernández", "Robert Hernández"},
            {"Everson", "Everson"},
            {"J. Vargas", "Jefre Vargas"},
            {"L. Suhr", "Leandro Suhr"},
            {"E. Herrera", "Eduardo Herrera"},
            {"G. Vargas", "Gustavo Vargas"},
            {"R. Dias", "Roger Dias"},
            {"F. Calderón", "Franco Calderón"},
            {"A. Barboza", "Alexander Barboza"},
            {"J. León", "Jason León"},
            {"Willie", "Willie"},
            {"T. Pérez", "Tomás Pérez"},
            {"R. Peralta 2", "Ramiro Peralta"},
            {"C. Sarabia", "Carlos Sarabia"},
            {"A. Mosquera", "Andrés Mosquera"},
            {"Adrián Fernández 2", "Adrián Fernández"},
            {"M. Agustín Graneros", "Miguel Agustín Graneros"},
            {"L. Giossa", "Luca Giossa"},
            {"I. Ramirez", "Isaac Ramirez"},
            {"E. Santos", "Eduardo Santos"},
            {"C. Paulista", "Caio Paulista"},
            {"L. Casiani", "Luis Casiani"},
            {"R. Lecchini", "Ramiro Lecchini"},
            {"C. Pavón", "Cristian Pavón"},
            {"B. Moreno", "Benjamin Moreno"},
            {"L. Linck", "Leo Linck"},
            {"Nicolás", "Nicolás"},
            {"M. Franca", "Matheus Franca"},
            {"A. Forneris", "Alan Forneris"},
            {"J. Ramos 2", "Jiovany Ramos"},
            {"S. Postel", "Santiago Postel"},
            {"Vinicius", "Vinicius"},
            {"G. Marques", "Gustavo Marques"},
            {"J. Murillo", "Jhon Murillo"},
            {"T. Pérez 2", "Tomás Pérez"},
            {"G. Chapeco", "Gabriel Chapeco"},
            {"J. Barros", "João Barros"},
            {"David", "David"},
            {"B. Caicedo", "Brandon Caicedo"},
            {"R. Bernabé", "Renan Bernabé"},
            {"L. Romero", "Lucas Romero"},
            {"A. Cano", "Alan Cano"},
            {"H. Mosquera", "Henry Mosquera"},
            {"D. Pizarro", "Damián Pizarro"},
            {"G. Piñeiro", "Gonzalo Piñeiro"},
            {"C. Vera", "Carlos Vera"},
            {"Isabela Matos", "Isabela Matos"},
            {"Neto", "Neto"},
            {"A. Sant'Anna", "Ariel Sant'Anna"},
            {"L. Peres", "Luan Peres"},
            {"N. Watson", "Nicolas Watson"},
            {"C. Romaña", "Carlos Romaña"},
            {"E. Flores", "Enrique Flores"},
            {"J. Boselli", "Juan Boselli"},
            {"M. Tagliamonte", "Matías Tagliamonte"},
            {"S. Sosa", "Sebastián Sosa"},
            {"C. Toselli", "Cristopher Toselli"},
            {"G. Sosa", "Gonzalo Sosa"},
            {"B. Viñán", "Bryan Viñán"},
            {"V. Fernandes", "Vitor Fernandes"},
            {"I. Manzur", "Ivan Manzur"},
            {"A. Alcaraz", "Adrián Alcaraz"},
            {"F. Salomoni", "Felipe Salomoni"},
            {"A. Gomez", "Agustin Gomez"},
            {"C. González", "Clementino González"},
            {"M. Braithwaite", "Martin Braithwaite"},
            {"J. Vera 2", "Jhunior Vera"},
            {"B. Valim", "Bernardo Valim"},
            {"J. O'Neil", "Jairo O'Neil"},
            {"J. Colorado", "Jean Colorado"},
            {"B. Caicedo 2", "Beder Caicedo"},
            {"J. Franco", "Juan Franco"},
            {"J. Cabezudo", "Jorge Cabezudo"},
            {"A. Oliveros", "Auli Oliveros"},
            {"Juniors Barbieri", "Juniors Barbieri"},
            {"Yuri Silva", "Yuri Silva"},
            {"E. Cannavo", "Ezequiel Cannavo"},
            {"F. Ogaz", "Felipe Ogaz"},
            {"A. Ponce", "Andrés Ponce"},
            {"C. Spinelli", "Claudio Spinelli"},
            {"L. De la Cruz", "Luis De la Cruz"},
            {"J. Salas", "Javier Salas"},
            {"P. Sérgio", "Paulo Sérgio"},
            {"S. Severiche", "Saul Severiche"},
            {"B. Montenegro", "Brian Montenegro"},
            {"S. da Silva", "Samuel da Silva"},
            {"J. Vidales", "Jhonny Vidales"},
            {"C. Áñez", "Carlos Áñez"},
            {"M. Andrés López", "Marcos Andrés López"},
            {"G. Blanc", "Gaston Blanc"},
            {"F. Galeano", "Fernando Galeano"},
            {"G. Henrique", "Gustavo Henrique"},
            {"J. Sinisterra", "José Sinisterra"},
            {"Joāo Paulo", "Joāo Paulo"},
            {"F. Varese", "Federico Varese"},
            {"A. Nadruz", "Agustin Nadruz"},
            {"D. Valencia", "Daniel Valencia"},
            {"G. Pereiro", "Gastón Pereiro"},
            {"F. Hinestroza", "Fredy Hinestroza"},
            {"J. Ramírez", "Jeizon Ramírez"},
            {"A. Antilef", "Alejo Antilef"},
            {"E. Ferrario", "Enzo Ferrario"},
            {"R. Rodriguez", "Ronald Rodriguez"},
            {"P.", "Pedro"},
            {"Mathías Villasanti", "Mathías Villasanti"},
            {"N. Fernández", "Nicolás Fernández"},
            {"R. Carrascal", "Rafael Carrascal"},
            {"S. Lentinelly", "Sebastián Lentinelly"},
            {"M. Rivas", "Marco Rivas"},
            {"M. Manso", "Mateus Manso"},
            {"K. Silva", "Kevin Silva"},
            {"H. Leanos", "Heber Leanos"},
            {"R. Suárez", "Román Suárez"},
            {"J. Rodríguez", "José Rodríguez"},
            {"L. Araujo", "Leonardo Araujo"},
            {"D. Piña", "Daniel Piña"},
            {"Bruno", "Bruno"},
            {"O. Gill", "Orlando Gill"},
            {"J. Goicochea", "Juan Pablo Goicochea"},
            {"G. Ortiz", "Gerardo Ortiz"},
            {"A. Gauto", "Ariel Gauto"},
            {"G. Rostagno", "Gonzalo Martin Rostagno"},
            {"Y. Guzmán", "Yeison Guzmán"},
            {"H. Lupú", "Hernan Lupú"},
            {"C. Valladolid", "Christian Valladolid"},
            {"E. Centurión", "Ezequiel Centurión"},
            {"R. Garcés", "Robert Garcés"},
            {"J. Acosta", "Juan Acosta"},
            {"T. Galván", "Tomás Galván"},
            {"P. Madero", "Patricio Madero"},
            {"M. Iseppe", "Mateus Iseppe"},
            {"G. Diaz", "German Diaz"},
            {"A. Ruberto", "Agustín Ruberto"},
            {"C. Paz", "Cristian Paz"},
            {"C. de las Salas", "Carlos de las Salas"},
            {"Jean Pierre", "Jean Pierre"},
            {"A. Cabral", "Arthur Cabral"},
            {"Ryan", "Ryan"},
            {"E. Lima", "Eduardo Lima"},
            {"J. Saralegui", "Jabes Saralegui"},
            {"M. Klimowicz", "Matas Klimowicz"},
            {"P. Henrique", "Paulo Henrique"},
            {"F. Barrandeguy Martino", "Federico Barrandeguy Martino"},
            {"F. Diaz", "Fernando Diaz"},
            {"F.", "Felipe"},
            {"N.", "Nicolas"},
            {"A. Salazar", "Aldair Salazar"},
            {"J. Ortiz", "Jordy Ortiz"},
            {"L. Cardozo", "Luis Cardozo"},
            {"M. Garay", "Martín Garay"},
            {"Wagner", "Wagner"},
            {"Maycon", "Maycon"},
            {"V. Rodríguez", "Valentín Rodríguez"},
            {"Luan", "Luan"},
            {"A. Silva", "Alejandro Silva"},
            {"G. Bontempo", "Gabriel Bontempo"},
            {"T. Rayer", "Tomas Rayer"},
            {"S. Vega", "Stiven Vega"},
            {"A. Alonso", "Antony Alonso"},
            {"S. Callegari", "Stéfano Callegari"},
            {"L. Vásquez", "Luis Vásquez"},
            {"J. Lucero", "Juan Martín Lucero"},
            {"Í. Espinoza", "Ítalo Espinoza"},
            {"M. Hinestroza", "Marino Hinestroza"},
            {"Ewerton", "Ewerton"},
            {"L. Mina", "Luis Mina"},
            {"A. Rodríguez", "Alan Rodríguez"},
            {"C. Martinez", "Chris Martinez"},
            {"J. Zambrano", "Jordano Zambrano"},
            {"J. Zapata", "Juan Zapata"},
            {"P. Siles", "Pablo Siles"},
            {"M. Insaurralde", "Manuel Insaurralde"},
            {"J. Alcantar", "Jesus Alcantar"},
            {"E. Agüero", "Eduardo Agüero"},
            {"M. Collao", "Marco Collao"},
            {"M. Mancebo", "Marco Mancebo"},
            {"D. Bolanos", "Denilson Bolanos"},
            {"J. Tarán", "José Tarán"},
            {"H. Valdez", "Hugo Valdez"},
            {"K. Gutiérrez", "Kevin Gutiérrez"},
            {"P. Garrido", "Pedro Garrido"},
            {"I. Alba", "Israel Alba"},
            {"D. Alves", "Davi Alves"},
            {"C. Roque", "Caio Roque"},
            {"J. Vitor", "João Vitor"},
            {"D. Gomes", "Davi Gomes"},
            {"B. Yáñez", "Bastián Yáñez"},
            {"P. Silva", "Patrick Silva"},
            {"M. Casco", "Milton Casco"},
            {"R. Gomes", "Rafael Gomes"},
            {"C. Menacho", "César Menacho"},
            {"V. Hugo", "Victor Hugo"},
            {"C. Olivares", "Christopher Olivares"},
            {"P. Paulo", "Pedro Paulo"},
            {"L. Montenegro 2", "Leonardo Montenegro"},
            {"O. Gaona Lugo", "Orlando Gaona Lugo"},
            {"S. Salas", "Sebastián Salas"},
            {"R. Prieto", "Richard Prieto"},
            {"J. Vila", "Julio Vila"},
            {"E. Roco", "Enzo Roco"},
            {"D. Guzmán", "Diogo Guzmán"},
            {"Y. Perez", "Yulwuis Perez"},
            {"C. Haydar", "César Haydar"},
            {"P. Pernicone", "Patricio Pernicone"},
            {"L. Pagano", "Luigi Pagano"},
            {"E. De Los Santos", "Erik De Los Santos"},
            {"G. Chiaverano", "Giovani Chiaverano"},
            {"K. Barría", "Kadir Barría"},
            {"B. Schamine", "Benjamin Schamine"},
            {"R. Fernández", "Ronnie Fernández"},
            {"M. Maturana", "Martín Maturana"},
            {"R. Cabral", "Rodrigo Cabral"},
            {"Isac", "Isac"},
            {"M. Amaro", "Mauricio Amaro"},
            {"M. Martinich", "Marcos Martinich"},
            {"J. Parada", "Juan Carlos Parada"},
            {"J. Castro", "João Castro"},
            {"J. Castillo", "Jean Castillo"},
            {"Thaciano", "Thaciano"},
            {"V. Burgoa", "Valentín Burgoa"},
            {"J. Villamil", "Jaime Villamil"},
            {"S. del Castillo", "Sebastián del Castillo"},
            {"R. Lezcano", "Rubén Lezcano"},
            {"Jair", "Jair"},
            {"G. Menegon", "Gabriel Menegon"},
            {"J. Leiva", "Juan Leiva"},
            {"C. Cuesta", "Carlos Cuesta"},
            {"F. Frías", "Franco Frías"},
            {"F. Mimbacas", "Fernando Mimbacas"},
            {"S. Quiroga", "Sergio Quiroga"},
            {"M. Guaramato", "Marcel Guaramato"},
            {"M. Rea", "Martín Rea"},
            {"R. Sangiovani", "Rafael Sangiovani"},
            {"Saymom", "Saymom"},
            {"V. Moreno", "Valentín Moreno"},
            {"G. Gonzalez", "Gustavo Gonzalez"},
            {"S. Fernández", "Stefano Fernández"},
            {"J. Escobar", "Josen Escobar"},
            {"D. Borrero", "Dylan Borrero"},
            {"R. da Silva", "Rafael da Silva"},
            {"D. Prieto", "Daniel Prieto"},
            {"M. de Ritis", "Mathias de Ritis"},
            {"R. Silva", "Rolando Silva"},
            {"T. Cuello", "Tomás Cuello"},
            {"L. Martínez Quarta", "Lucas Martínez Quarta"},
            {"Marçal", "Marçal"},
            {"S. Gil", "Sergio Gil"},
            {"L. Guedes", "Luis Guedes"},
            {"L. Ramon", "Lucas Ramon"},
            {"Perivan", "Perivan"},
            {"L. Duarte", "Lautaro Duarte"},
            {"M. Pereira", "Marcos Pereira"},
            {"G. Benitez", "Gaston Benitez"},
            {"J. Espinola", "Jose Espinola"},
            {"F. Muñoa", "Facundo Muñoa"},
            {"Cauly", "Cauly"},
            {"S. Guerra", "Saulo Guerra"},
            {"A. Torterolo", "Alan Torterolo"},
            {"J. Martínez", "Jayson Martínez"},
            {"J. Noguera", "Junior Noguera"},
            {"G. Mec", "Gabriel Mec"},
            {"A. Arce", "Agustín Arce"},
            {"R. Hinojosa", "Roberto Hinojosa"},
            {"A. Saldivia", "Alan Saldivia"},
            {"João Pedro 2", "João Pedro"},
            {"Luciano 2", "Luciano"},
            {"B. Ramírez", "Benjamín Ramírez"},
            {"D. González", "David González"},
            {"B. Leyes", "Bruno Leyes"},
            {"D. Herazo", "Diego Herazo"},
            {"Cuiabano", "Cuiabano"},
            {"M. Valdés", "Martín Valdés"},
            {"E. Cecchini", "Emanuel Cecchini"},
            {"F. Martinicorena", "Francisco Martinicorena"},
            {"R. Santa Cruz", "Roque Santa Cruz"},
            {"M. Antônio", "Marcos Antônio"},
            {"Zappelini", "Zappelini"},
            {"J. Randazzo", "Juan Randazzo"},
            {"R. Pérez", "Rodrigo Pérez"},
            {"F. Basante", "Fernando Basante"},
            {"B. André", "Bruno André"},
            {"F. Sambueza", "Fabián Sambueza"},
            {"Smiley", "Smiley"},
            {"I. Alegría", "Ian Alegría"},
            {"B. Simone", "Bruno Simone"},
            {"M. Moreno", "Marlos Moreno"},
            {"Djhordney", "Djhordney"},
            {"N. Quagliata", "Nicolás Quagliata"},
            {"Lucyo", "Lucyo"},
            {"J. M. Carrasco", "José María Carrasco"},
            {"S. Beltrán", "Santiago Beltrán"},
            {"G. Barrios", "Germán Barrios"},
            {"N. Camacho", "Néstor Camacho"},
            {"L. Zuccarello", "Lukas Zuccarello"},
            {"J. Ahumada", "Joel Ahumada"},
            {"D. Banguero", "Danovis Banguero"},
            {"J. Cristaldo", "Jonathan Cristaldo"},
            {"G. Larios", "Guillermo Larios"},
            {"A. Yovera", "Alonso Yovera"},
            {"J. Hernández", "Jhoan Hernández"},
            {"M. Amondarain", "Maximiliano Amondarain"},
            {"G. Peredo", "Gustavo Peredo"},
            {"N. Meza", "Nicolás Meza"},
            {"L. Barbosa", "Lucas Barbosa"},
            {"P. Charpentier", "Paul Charpentier"},
            {"Fabrício", "Fabrício"},
            {"Wanyson", "Wanyson"},
            {"Neymar", "Neymar"},
            {"R. Cáceres", "Raúl Cáceres"},
            {"S. Morocho", "Stalin Morocho"},
            {"J. Luján", "José Luján"},
            {"J. Bauza", "Juan Bauza"},
            {"H. Sandoval", "Hugo Sandoval"},
            {"P. 2", "Pablo"},
            {"M. Hernández", "Matías Hernández"},
            {"O. Carabalí", "Omar Carabalí"},
            {"E. Plúas", "Erick Plúas"},
            {"D. Bejarano", "Danny Bejarano"},
            {"J. Bilbao", "Jonathan Bilbao"},
            {"H. López", "Héctor López"},
            {"F. Parada", "Facundo Parada"},
            {"J. Herrera 2", "José Herrera"},
            {"L. Vargas Cavalcanti", "Lucas Vargas Cavalcanti"},
            {"F. Román", "Fernando Román"},
            {"M. Miranda", "Matías Miranda"},
            {"E. Ramires", "Eric Ramires"},
            {"Cauan Lucas", "Cauan Lucas"},
            {"A. Gomez 2", "Albert Gomez"},
            {"M. Vegas", "Miguel Vegas"},
            {"M. Acuña", "Marcos Acuña"},
            {"A. Franco 2", "Alex Franco"},
            {"M. Cova", "Maurice Cova"},
            {"I. Vásquez", "Ignacio Vásquez"},
            {"N. Ferraresi", "Nahuel Ferraresi"},
            {"K. Becerra", "Kevin Becerra"},
            {"C. Gómez", "Carlos Gómez"},
            {"E. Caicedo", "Eber Caicedo"},
            {"B. Tomatis", "Bautista Tomatis"},
            {"R. Tolói", "Rafael Tolói"},
            {"M. Vadulli", "Michael Vadulli"},
            {"G. Mendoza", "Gustavo Mendoza"},
            {"J. Cavadia", "José Cavadia"},
            {"E. Soares", "Eric Soares"},
            {"Robert 2", "Robert"},
            {"E. da Costa", "Eduardo da Costa"},
            {"R. Rabino", "Renzo Rabino"},
            {"A. Ramos 2", "Adrián Ramos"},
            {"D. Fuzato", "Daniel Fuzato"},
            {"T. Palacios", "Tilman Palacios"},
            {"R. Luca", "Rhyan Luca"},
            {"F. Januário", "Felipe Januário"},
            {"M. Aguiar", "Mathías Aguiar"},
            {"R. Medeiros", "Rogerio Medeiros"},
            {"J. Villalba", "Juan Villalba"},
            {"Y. Muñoz", "Yonatan Muñoz"},
            {"M. Bermúdez", "Michael Bermúdez"},
            {"R. Lodi", "Renan Lodi"},
            {"M. Araya", "Martín Araya"},
            {"A. Ceza", "Antonio Ceza"},
            {"G. Pacheco", "Guillermo Pacheco"},
            {"A. Montoro", "Alvaro Montoro"},
            {"V. Popó", "Vinicius Popó"},
            {"J. Villegas", "José Villegas"},
            {"J. Ordóñez", "Jorge Ordóñez"},
            {"Y. Erique", "Yeltzin Erique"},
            {"M. Monsalve", "Miguel Monsalve"},
            {"K. Londoño", "Kevin Londoño"},
            {"A. Quintana", "Aldair Quintana"},
            {"J. Phelipe", "José Phelipe"},
            {"C. Coronel", "Carlos Coronel"},
            {"M. Morales", "Marcelo Morales"},
            {"Viery", "Viery"},
            {"C. Arboleda", "Carlos Arboleda"},
            {"N. Da Silva", "Nelson Da Silva"},
            {"E. Da Silva", "Esteban Da Silva"},
            {"João Pedro 3", "João Pedro"},
            {"R. Arias", "Ramón Arias"},
            {"A. Franco 3", "Alan Franco"},
            {"J. Fuentes", "Juan Fuentes"},
            {"R. Dudok", "Rodrigo Dudok"},
            {"R. Jaramillo", "Renny Jaramillo"},
            {"G. Gomez", "Gonzalo Gomez"},
            {"C. Ramos 2", "Chris Ramos"},
            {"R. Corrales", "Rafael Corrales"},
            {"J. Laso", "Joaquín Laso"},
            {"G. Menino", "Gabriel Menino"},
            {"C. Sierra", "Carlos Sierra"},
            {"P. Henrique 2", "Pedro Henrique"},
            {"N. Rossi", "Nicolás Rossi"},
            {"G. Rodriguez 3", "Gregorio Rodriguez"},
            {"A. Atum", "Axel Atum"},
            {"T. Beltrame", "Thiago Beltrame"},
            {"A. Herrera", "Agustin Herrera"},
            {"F. Oliveros", "Franyer Oliveros"},
            {"O. Bertel", "Omar Bertel"},
            {"D. dos Santos de Oliveira", "Danilo dos Santos de Oliveira"},
            {"Y. González", "Yair González"},
            {"P. Gabriel 2", "Phillipe Gabriel"},
            {"R. Felipe", "Ryan Felipe"},
            {"V. Robaldo", "Valentín Robaldo"},
            {"B. Rabello", "Bryan Rabello"},
            {"G. Bortagaray", "Gerónimo Bortagaray"},
            {"Raul 2", "Raul"},
            {"I. Poblete", "Israel Poblete"},
            {"Ó. Toledo", "Oscar Toledo"},
            {"G. Brazão", "Gabriel Brazão"},
            {"A. Guedes", "Alexandre Guedes"},
            {"N. Barrios", "Nahuel Barrios"},
            {"J. Freitas", "Joaquin Freitas"},
            {"G. Martins", "Gustavo Martins"},
            {"I. Teixeira", "Igor Teixeira"},
            {"J. Peña", "Jorge Peña"},
            {"Á. Preciado", "Ángelo Preciado"},
            {"Cléo Silva", "Cléo Silva"},
            {"F. Jara", "Fabrizio Jara"},
            {"M. Sandoval", "Mario Sandoval"},
            {"F. Paz", "Federico Paz"},
            {"R. Ureña", "Rodrigo Ureña"},
            {"Rony", "Rony"},
            {"M. Abisab", "Matías Abisab"},
            {"N. Garrido", "Nicolás Garrido"},
            {"C. Yanis", "César Yanis"},
            {"F. Flores", "Franchesco Flores"},
            {"M. Torres", "Marlon Torres"},
            {"Enzo", "Enzo"},
            {"P. Monje", "Pablo Monje"},
            {"S. Natera", "Santiago Natera"},
            {"Ruan", "Ruan"},
            {"J. Obando", "Juan Obando"},
            {"C. Machado", "Cristhian Machado"},
            {"G. Valverde", "Gabriel Valverde"},
            {"R. Rebolledo", "Raimundo Rebolledo"},
            {"Gustavinho 2", "Gustavinho"},
            {"T. Sultani", "Tomás Sultani"},
            {"L. Louback", "Lucas Louback"},
            {"C. Vegas", "Charly Vegas"},
            {"J. Magallanes", "Juan Magallanes"},
            {"D. Fernandez", "Denilso Fernandez"},
            {"Zé Ivaldo", "Zé Ivaldo"},
            {"Y. Oyarzo", "Yury Oyarzo"},
            {"G. Montiel", "Gonzalo Montiel"},
            {"H. Moura", "Hugo Moura"},
            {"D. Asprilla", "Dairon Asprilla"},
            {"T. Rincón", "Tomás Rincón"},
            {"F. Gulli", "Facundo Gulli"},
            {"F. López", "Fabricio López"},
            {"J. Rojas", "Johan Rojas"},
            {"Hendel", "Hendel"},
            {"K. Páez", "Kendry Páez"},
            {"C. Aránguiz", "Charles Aránguiz"},
            {"J. Klinger", "Jose Klinger"},
            {"I. Fernandez", "Ignacio Fernandez"},
            {"A. Deneumostier", "Alec Deneumostier"},
            {"L. Piton", "Lucas Piton"},
            {"C. Morales", "Cristian Morales"},
            {"Aytor Herrera", "Aytor Herrera"},
            {"J. C. Estacio", "Jean Carlos Estacio"},
            {"R. Montero", "Ronny Montero"},
            {"Andrey", "Andrey"},
            {"G. Escobar", "Gonzalo Escobar"},
            {"G. Obredor", "Gabriel Obredor"},
            {"J. C. Pérez", "Juan Camilo Pérez"},
            {"F. Alvarez", "Felipe Alvarez"},
            {"Lucas 2", "Lucas"},
            {"R. Sandoval 2", "Ray Sandoval"},
            {"R. Rodríguez", "Rodrigo Rodríguez"},
            {"D. Salgado", "Dilan Salgado"},
            {"T. Vidal", "Thiago Vidal"},
            {"J. F. Roncal", "Jean Franco Roncal"},
            {"H. Benincasa", "Horacio Benincasa"},
            {"A. Cordoba", "Andres Cordoba"},
            {"F. Faúndez", "Felipe Faúndez"},
            {"J. Barro", "Joaquín Barro"},
            {"I. Odoni", "Igor Odoni"},
            {"J. Moreno", "Júnior Moreno"},
            {"J. A. Pérez", "Juan Antonio Pérez"},
            {"Léo Jardim", "Léo Jardim"},
            {"M. Díaz", "Marcelo Díaz"},
            {"M. Caucaia", "Mateus Caucaia"},
            {"D. Armas", "Diego Armas"},
            {"V. Hugo 2", "Vitor Hugo"},
            {"K. Dawson", "Kevin Dawson"},
            {"G. Galoppo", "Giuliano Galoppo"},
            {"E. Perleche", "Erick Perleche"},
            {"M. Izaguirre", "Mateo Izaguirre"},
            {"A. Moreno 2", "Aníbal Moreno"},
            {"E. Minda", "Ethan Minda"},
            {"F. Silvera", "Facundo Silvera"},
            {"L. Rivero", "Lautaro Rivero"},
            {"R. Rosales", "Roberto Rosales"},
            {"V. Hugo Gomes", "Victor Hugo Gomes"},
            {"J. Mosquera", "Juan Mosquera"},
            {"M. Rocha", "Marcos Rocha"},
            {"J. Mena", "Jefferson Mena"},
            {"E. Obregón", "Esteban Obregón"},
            {"A. da Silveira", "Agustín da Silveira"},
            {"J. Barrera", "Jordan Barrera"},
            {"C. Ramírez", "Cristian Ramírez"},
            {"C. Espinola", "Carlos Espinola"},
            {"A. Moreno 3", "Alex Moreno"},
            {"J. C. Meza", "Juan Cruz Meza"},
            {"J. Romero", "Joel Romero"},
            {"L. Flores", "Leonardo Flores"},
            {"C. Arango", "Cristian Arango"},
            {"Tucuruí", "Tucuruí"},
            {"A. González 3", "Alexander González"},
            {"T. Leyton", "Tomás Leyton"},
            {"Wallace 2", "Wallace"},
            {"M. Lozano", "Matias Lozano"},
            {"W. Arão", "Willian Arão"},
            {"J. Campuzano", "Jorman Campuzano"},
            {"D. Ospina", "David Ospina"},
            {"F. Salazar", "Fredy Salazar"},
            {"E. Álvarez", "Eduardo Álvarez"},
            {"F. González 2", "Franklin González"},
            {"B. Bustos", "Fabricio Bustos"},
            {"B. Lozano", "Brian Lozano"},
            {"M. Caballero", "Mateo Caballero"},
            {"P. Guajardo", "Paolo Guajardo"},
            {"F. Carballo", "Felipe Carballo"},
            {"Wendell", "Wendell"},
            {"M. Shupp", "Manuel Shupp"},
            {"B. Correa", "Brayan Correa"},
            {"Da Rocha", "Da Rocha"},
            {"I. Román", "Iván Román"},
            {"A. G. Basso", "Agustín García Basso"},
            {"R. Benítez", "Romeo Benítez"},
            {"J. Arauz", "Jorge Arauz"},
            {"R. Aguilar", "Rotceh Aguilar"},
            {"B. Rojas", "Benjamín Rojas"},
            {"V. Ramon", "Vitor Ramon"},
            {"J. Hurtado 2", "Jorge Hurtado"},
            {"C. Penilla", "Cristian Penilla"},
            {"João Pedro 4", "João Pedro"},
            {"D. Bobadilla", "Damian Bobadilla"},
            {"D. Rojas", "Daniel Rojas"},
            {"E. Zambrano", "Erick Zambrano"},
            {"M. Espinoza Pino", "Martín Espinoza Pino"},
            {"A. Thawan", "Athos Thawan"},
            {"Warley", "Warley"},
            {"R. Rivas González", "Rodrigo Rivas González"},
            {"E. Vargas", "Eduardo Vargas"},
            {"G. Lopes", "Gabriel Lopes"},
            {"Y. Caricote", "Yolfran Caricote"},
            {"G. Santos Silva", "Gabriel Santos Silva"},
            {"F. Ferrero", "Facundo Ferrero"},
            {"A. Román", "Andrés Román"},
            {"B. Garcés", "Bayron Garcés"},
            {"P. Assis", "Pedro Assis"},
            {"R. Ballivián", "Ramiro Ballivián"},
            {"D. Zalzman", "David Zalzman"},
            {"D. Mercado", "Diego Mercado"},
            {"J. Fuentes 2", "Jean Fuentes"},
            {"N. Colombo", "Nazareno Colombo"},
            {"J. Lara", "Jesus Lara"},
            {"S. Rojas", "Santiago Rojas"},
            {"S. Medina", "Sebastián Medina"},
            {"F. Martinez", "Facundo Martinez"},
            {"I. Perruzzi", "Ignacio Perruzzi"},
            {"B. Castro", "Beckham Castro"},
            {"João Vitor 2", "João Vitor"},
            {"G. García", "Gian García"},
            {"F. Charrupí", "Felix Charrupí"},
            {"J. Angulo", "Julian Angulo"},
            {"D. Melgarejo", "Diego Melgarejo"},
            {"J. Nardoni", "Juan Ignacio Nardoni"},
            {"A. Ricardo", "André Ricardo"},
            {"J. Marcelino", "Jorge Marcelino"},
            {"G. Pezzella", "Germán Pezzella"},
            {"T. Gutiérrez", "Thomas Gutiérrez"},
            {"L. Meza", "Leandro Meza"},
            {"P. Boolsen", "Patricio Boolsen"},
            {"D. Novoa", "Diego Novoa"},
            {"C. Garcés", "Carlos Garcés"},
            {"N. Tripichio", "Nicolas Tripichio"},
            {"T. Riveros", "Thomas Riveros"},
            {"M. Cañete", "Marcelo Cañete"},
            {"N. Hernández", "Nicolás Hernández"},
            {"W. Tandazo", "Walter Tandazo"},
            {"Á. Stringa", "Ángel Stringa"},
            {"G. Falcón", "Gonzalo Falcón"},
            {"D. Vergara", "Duván Vergara"},
            {"L. Perez", "Leonel Perez"},
            {"Jeferson", "Jeferson"},
            {"J. Cazares", "Jose Cazares"},
            {"J. L. Marrufo", "José Luis Marrufo"},
            {"C. Nunez", "Claudio Nunez"},
            {"R. Rique", "Ramon Rique"},
            {"X. Biscayzacu", "Xavier Biscayzacu"},
            {"G. Ramirez", "Gaston Ramirez"},
            {"D. Vargas 2", "Diego Vargas"},
            {"C. Torrejón", "Claudio Torrejón"},
            {"L. Morales", "Lucas Morales"},
            {"E. Herrera 2", "Ezequiel Herrera"},
            {"G. Pereira", "Gabriel Pereira"},
            {"M. Barbosa", "Matheus Barbosa"},
            {"M. Reali", "Matias Reali"},
            {"E. Moreira", "Esteban Moreira"},
            {"M. Gamarra", "Mateo Gamarra"},
            {"N. Rodríguez", "Nicolás Rodríguez"},
            {"F. Miño", "Facundo Miño"},
            {"F. Benítez", "Frankarlos Benítez"},
            {"L. Palma", "Luis Palma"},
            {"P. Vaca", "Pablo Vaca"},
            {"R. Sánchez", "Richard Sánchez"},
            {"J. Caraballo", "Jeferson Caraballo"},
            {"N. Molina", "Nixon Molina"},
            {"M. Succar", "Matías Succar"},
            {"D. Mosquera", "Dairon Mosquera"},
            {"A. Perez", "Angel Perez"},
            {"F. Balbuena", "Fabián Balbuena"},
            {"L. Machado", "Lucas Machado"},
            {"P. 3", "Pablo"},
            {"A. Barrionuevo", "Alan Barrionuevo"},
            {"G. Medina", "Gleyfer Medina"},
            {"M. Enoumba", "Marc Enoumba"},
            {"T. Ahumada", "Tomás Ahumada"},
            {"Wallace 3", "Wallace"},
            {"J. Mejia", "Jeremy Mejia"},
            {"J. Navarro", "Jailerth Navarro"},
            {"M. González", "Martín González"},
            {"A. Guimaraes", "Artur Guimaraes"},
            {"M. Maciel", "Milton Maciel"},
            {"C. Munder", "César Munder"},
            {"N. Perez", "Nicolas Perez"},
            {"Reinier", "Reinier"},
            {"J. Graterol", "Joel Graterol"},
        };

        public TransfermarktService(HttpClient httpClient, ILogger<TransfermarktService> logger, FutebolContext context)
        {
            _httpClient = httpClient;
            _logger = logger;
            _context = context;

            // Headers obrigatórios para não receber 403 do Transfermarkt
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.transfermarkt.com.br/");
        }
        public async Task<string?> BuscarFotoJogador(string nomeJogador, string? nomeClube = null)
        {
            try
            {
                var query = HttpUtility.UrlEncode(nomeJogador);
                var url = $"https://www.transfermarkt.com.br/schnellsuche/ergebnis/schnellsuche?query={query}";

                _logger.LogInformation("[Foto] Buscando: {Nome} | Clube: {Clube}", nomeJogador, nomeClube);

                var html = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // A tabela de jogadores é a primeira com class 'items'
                var tabela = doc.DocumentNode
                    .SelectNodes("//table[contains(@class,'items')]")
                    ?.FirstOrDefault();

                if (tabela == null)
                {
                    _logger.LogWarning("[Foto] Nenhuma tabela de resultados: {Nome}", nomeJogador);
                    return null;
                }

                var linhas = tabela.SelectNodes(".//tbody/tr[not(contains(@class,'thead'))]");
                if (linhas == null || !linhas.Any()) return null;

                // Log todos os candidatos para diagnóstico
                foreach (var l in linhas)
                {
                    var nomeCell = ExtrairNomeLinha(l);
                    var clubeCell = ExtrairClubeLinha(l);
                    _logger.LogInformation("[Foto] Candidato: Nome={Nome} | Clube={Clube}", nomeCell, clubeCell);
                }

                HtmlNode? linhaSelecionada = null;

                if (!string.IsNullOrWhiteSpace(nomeClube))
                {
                    // Tenta match exato normalizado primeiro
                    linhaSelecionada = linhas.FirstOrDefault(l =>
                        NomesClubeSimilares(ExtrairClubeLinha(l), nomeClube));

                    if (linhaSelecionada != null)
                        _logger.LogInformation("[Foto] Clube encontrado (match): {Clube}",
                            ExtrairClubeLinha(linhaSelecionada));
                }

                // Fallback: primeiro resultado
                linhaSelecionada ??= linhas.First();
                _logger.LogInformation("[Foto] Linha selecionada: {Nome} / {Clube}",
                    ExtrairNomeLinha(linhaSelecionada), ExtrairClubeLinha(linhaSelecionada));

                return await ExtrairFotoDaLinha(linhaSelecionada);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Foto] Erro ao buscar foto de {Nome}", nomeJogador);
                return null;
            }
        }

        private static string ExtrairNomeLinha(HtmlNode linha)
        {
            // Estrutura: <td class="hauptlink"><a>Nome</a></td>
            var link = linha.SelectSingleNode(".//td[contains(@class,'hauptlink')]//a");
            return HtmlEntity.DeEntitize(link?.InnerText?.Trim() ?? "");
        }

        private static string ExtrairClubeLinha(HtmlNode linha)
        {
            // Estratégia 1: link para /verein/ (o mais confiável)
            var linkVerein = linha.SelectNodes(".//a[contains(@href,'/verein/')]")
                ?.FirstOrDefault();
            if (linkVerein != null)
            {
                // Prefere o atributo title (nome completo do clube)
                var title = linkVerein.GetAttributeValue("title", "").Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    return HtmlEntity.DeEntitize(title);

                var texto = linkVerein.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(texto))
                    return HtmlEntity.DeEntitize(texto);
            }

            // Estratégia 2: segunda célula da inline-table (abaixo do nome)
            var subTds = linha.SelectNodes(".//td[@class='inline-table']//tr");
            if (subTds?.Count >= 2)
            {
                var clubeTexto = subTds[1].InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(clubeTexto))
                    return HtmlEntity.DeEntitize(clubeTexto);
            }

            return "";
        }

        private static bool NomesClubeSimilares(string nomesite, string nomeBanco)
        {
            if (string.IsNullOrWhiteSpace(nomesite) || string.IsNullOrWhiteSpace(nomeBanco))
                return false;

            var a = NormalizarClube(nomesite);
            var b = NormalizarClube(nomeBanco);

            // Match exato após normalização
            if (a == b) return true;

            // Um contém o outro (cobre "Internacional" ↔ "SC Internacional Porto Alegre")
            if (a.Contains(b) || b.Contains(a)) return true;

            // Match parcial: cada token de b aparece em a
            var tokensB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokensB.Length > 0 && tokensB.All(t => a.Contains(t))) return true;

            return false;
        }


        private static string NormalizarClube(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "";

            var s = nome.ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                .Replace("ç", "c").Replace("ñ", "n");

            // Remove prefixos/sufixos comuns
            var stopwords = new[] { "sc", "cr", "ec", "fc", "cd", "ca", "ac", "se",
                             "sport", "clube", "club", "futebol", "football",
                             "de", "do", "da", "dos", "las", "los",
                             "porto", "alegre" }; // cidade removida para não interferir

            var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(t => !stopwords.Contains(t) && t.Length > 1)
                          .ToArray();

            return string.Join(" ", tokens);
        }

        /// <summary>Acessa o perfil e extrai a URL da foto via og:image.</summary>
        private async Task<string?> ExtrairFotoDaLinha(HtmlNode linha)
        {
            try
            {
                var linkNode = linha.SelectSingleNode(".//td[contains(@class,'hauptlink')]//a")
                             ?? linha.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                if (linkNode == null) return null;

                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) return null;

                var profileUrl = href.StartsWith("http")
                    ? href
                    : "https://www.transfermarkt.com.br" + href;

                _logger.LogInformation("[Foto] Acessando perfil: {Url}", profileUrl);
                await Task.Delay(TimeSpan.FromSeconds(1.5));

                var profileHtml = await _httpClient.GetStringAsync(profileUrl);
                var profileDoc = new HtmlDocument();
                profileDoc.LoadHtml(profileHtml);

                // Extrai og:image (mais confiável)
                var ogImage = profileDoc.DocumentNode
                    .SelectSingleNode("//meta[@property='og:image']");
                var fotoUrl = ogImage?.GetAttributeValue("content", "")?.Trim();

                if (!string.IsNullOrWhiteSpace(fotoUrl) && fotoUrl.Contains("transfermarkt"))
                {
                    _logger.LogInformation("[Foto] og:image encontrado: {Url}", fotoUrl);
                    return fotoUrl;
                }

                // Fallback: img de perfil
                var imgPerfil = profileDoc.DocumentNode
                    .SelectSingleNode("//img[contains(@class,'data-header__profile-image')]");
                fotoUrl = imgPerfil?.GetAttributeValue("src", "")?.Trim();

                if (!string.IsNullOrWhiteSpace(fotoUrl))
                {
                    _logger.LogInformation("[Foto] img perfil encontrado: {Url}", fotoUrl);
                    return fotoUrl;
                }

                _logger.LogWarning("[Foto] Nenhuma foto encontrada na página de perfil.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Foto] Erro ao acessar perfil");
                return null;
            }
        }

        /// <summary>
        /// Busca dados do jogador no Transfermarkt pelo nome.
        /// Quando há múltiplos resultados, compara com o nome do clube para pegar o correto.
        /// </summary>
        public async Task<TransfermarktPlayerInfo?> BuscarJogador(string nomeJogador, string? nomeClube = null)
        {
            try
            {
                var query = HttpUtility.UrlEncode(nomeJogador);
                var url = $"https://www.transfermarkt.com.br/schnellsuche/ergebnis/schnellsuche?query={query}";

                _logger.LogInformation("[Transfermarkt] Buscando: {Nome} (clube: {Clube})", nomeJogador, nomeClube);

                var html = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var tabelas = doc.DocumentNode.SelectNodes("//table[contains(@class,'items')]");
                if (tabelas == null || !tabelas.Any())
                {
                    _logger.LogWarning("[Transfermarkt] Nenhum resultado para: {Nome}", nomeJogador);
                    return null;
                }

                var linhas = tabelas[0].SelectNodes(".//tbody/tr[not(contains(@class,'thead'))]");
                if (linhas == null || !linhas.Any())
                    return null;

                // 🔹 Log de todos os candidatos (sem idade aqui, porque não é confiável na lista)
                foreach (var linha in linhas)
                {
                    var nomeCell = linha.SelectSingleNode(".//td[@class='hauptlink']/a")?.InnerText?.Trim();
                    var clubeCell = linha.SelectSingleNode(".//td[@class='zentriert']/a")?.InnerText?.Trim();
                    var nacCell = linha.SelectSingleNode(".//td[@class='zentriert']/img")?.GetAttributeValue("title", "");

                    _logger.LogInformation("[Transfermarkt] Candidato: Nome={Nome}, Clube={Clube}, Nac={Nac}",
                        nomeCell, clubeCell, nacCell);
                }

                HtmlNode? linhaSelecionada = null;

                // 🔹 1. Tenta pelo clube
                if (!string.IsNullOrWhiteSpace(nomeClube) && linhas.Count > 1)
                {
                    foreach (var linha in linhas)
                    {
                        var clubeTexto = ExtrairClubeLinha(linha);

                        if (NomesClubeSimilares(clubeTexto, nomeClube))
                        {
                            linhaSelecionada = linha;
                            _logger.LogInformation("[Transfermarkt] Selecionado pelo clube: {Clube}", clubeTexto);
                            break;
                        }
                    }
                }

                // 🔹 2. Se não achou pelo clube, tenta pela nacionalidade
                if (linhaSelecionada == null && linhas.Count > 1)
                {
                    foreach (var linha in linhas)
                    {
                        var nacCell = linha.SelectSingleNode(".//td[@class='zentriert']/img");
                        var nacTexto = nacCell?.GetAttributeValue("title", "") ?? "";

                        if (!string.IsNullOrWhiteSpace(nacTexto) &&
                            nacTexto.Equals("Brasil", StringComparison.OrdinalIgnoreCase))
                        {
                            linhaSelecionada = linha;
                            _logger.LogInformation("[Transfermarkt] Selecionado pela nacionalidade: {Nac}", nacTexto);
                            break;
                        }
                    }
                }

                // 🔹 3. Fallback final
                linhaSelecionada ??= linhas[0];
                _logger.LogInformation("[Transfermarkt] Selecionado fallback: {Nome}",
                    linhaSelecionada.SelectSingleNode(".//td[@class='hauptlink']/a")?.InnerText?.Trim());

                // 🔹 Extrai dados completos do perfil (onde idade e nascimento são confiáveis)
                return await ExtrairDadosDaLinha(linhaSelecionada);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transfermarkt] Erro inesperado ao buscar {Nome}", nomeJogador);
                return null;
            }
        }

        private async Task<TransfermarktPlayerInfo?> ExtrairDadosDaLinha(HtmlNode linha)
        {
            try
            {
                // Link do perfil
                var linkNode = linha.SelectSingleNode(".//td[@class='hauptlink']/a")
                             ?? linha.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                if (linkNode == null) return null;

                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) return null;

                var profileUrl = href.StartsWith("http")
                    ? href
                    : "https://www.transfermarkt.com.br" + href;

                _logger.LogInformation("[Transfermarkt] Acessando perfil: {Url}", profileUrl);

                await Task.Delay(TimeSpan.FromSeconds(1.5)); // respeita rate limit

                var profileHtml = await _httpClient.GetStringAsync(profileUrl);
                var profileDoc = new HtmlDocument();
                profileDoc.LoadHtml(profileHtml);

                var info = new TransfermarktPlayerInfo();

                // Nome completo
                info.NomeCompleto = profileDoc.DocumentNode
                    .SelectSingleNode("//h1[contains(@class,'data-header__headline')]")?.InnerText?.Trim()
                    ?? profileDoc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();

                // 🔹 Primeiro tenta pegar pelo itemprop birthDate
                var birthNode = profileDoc.DocumentNode.SelectSingleNode("//span[@itemprop='birthDate']");
                if (birthNode != null)
                {
                    var texto = birthNode.InnerText.Trim();
                    var partes = texto.Split('(')[0].Trim(); // removes idade entre parênteses
                    var dt = ParseDataNascimento(partes);
                    if (dt.HasValue)
                    {
                        info.DataNascimento = dt;
                    }
                }

                // Extrai dados da tabela (fallback)
                var infoNodes = profileDoc.DocumentNode.SelectNodes("//span[@class='info-table__content info-table__content--bold']");
                var labelNodes = profileDoc.DocumentNode.SelectNodes("//span[@class='info-table__content info-table__content--regular']");

                if (infoNodes != null && labelNodes != null)
                {
                    for (int i = 0; i < Math.Min(infoNodes.Count, labelNodes.Count); i++)
                    {
                        var label = labelNodes[i].InnerText.Trim().ToLower();
                        var valor = HtmlEntity.DeEntitize(infoNodes[i].InnerText.Trim());

                        if ((label.Contains("nascimento") || label.Contains("geboren") || label.Contains("date of birth"))
                            && info.DataNascimento == null) // só se não achou antes
                        {
                            info.DataNascimento = ParseDataNascimento(valor);
                            if (info.DataNascimento.HasValue)
                            {
                                if (info.DataNascimento.Value.Year < 1900 ||
                                    info.DataNascimento.Value > DateTime.Today)
                                {
                                    _logger.LogWarning("[Transfermarkt] Data inválida detectada: {Data}", info.DataNascimento);
                                    info.DataNascimento = null;
                                }
                            }
                        }
                        else if (label.Contains("nacionalidade") || label.Contains("nationalität") || label.Contains("nation"))
                        {
                            var imgAlt = infoNodes[i].SelectSingleNode(".//img")
                                ?.GetAttributeValue("title", "")
                                ?? infoNodes[i].SelectSingleNode(".//img")
                                    ?.GetAttributeValue("alt", "");

                            var nomeNacRaw = !string.IsNullOrWhiteSpace(imgAlt) ? imgAlt : valor;
                            info.Nacionalidade = NormalizarNacionalidade(nomeNacRaw);
                        }
                        else if (label.Contains("clube") || label.Contains("verein") || label.Contains("club"))
                        {
                            info.Clube = valor;
                        }
                    }
                }

                // Regex fallback (última tentativa)
                if (info.DataNascimento == null)
                {
                    var matchData = Regex.Match(profileHtml, @"(\d{2}/\d{2}/\d{4})");
                    if (matchData.Success)
                        info.DataNascimento = ParseDataNascimento(matchData.Groups[1].Value);
                }

                // Validação final
                if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year < 1900)
                    info.DataNascimento = null;

                _logger.LogInformation(
                    "[Transfermarkt] Perfil escolhido: Nome={Nome}, Clube={Clube}, Nasc={Nasc}, Nac={Nac}",
                    info.NomeCompleto,
                    info.Clube ?? "não informado",
                    info.DataNascimento?.ToString("dd/MM/yyyy") ?? "null",
                    info.Nacionalidade ?? "não informada");

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transfermarkt] Erro ao extrair dados do perfil");
                return null;
            }
        }

        private DateTime? ParseDataNascimento(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;

            // 1. Formato dd/MM/yyyy
            var match = Regex.Match(valor, @"(\d{2}/\d{2}/\d{4})");
            if (match.Success &&
                DateTime.TryParseExact(match.Groups[1].Value, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            // 2. Formato "Nov 18, 1999"
            var matchEn = Regex.Match(valor, @"(\w+ \d{1,2}, \d{4})");
            if (matchEn.Success &&
                DateTime.TryParse(matchEn.Groups[1].Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dtEn))
            {
                return dtEn;
            }

            // 3. Fallback genérico
            if (DateTime.TryParse(valor, out var dtAny))
                return dtAny;

            return null;
        }

        private string NormalizarNacionalidade(string raw)
        {
            var limpo = raw.Trim();
            return _mapaFlags.TryGetValue(limpo, out var traduzido)
                ? traduzido
                : limpo; // mantém original se não tiver mapeamento
        }

        /// <summary>
        /// Verifica se dois nomes de clube são parecidos (ignora maiúsculas, acentos, "FC", "CR" etc.)
        /// </summary>
        private static bool NomesParecidos(string a, string b)
        {
            static string Normalizar(string s) =>
                Regex.Replace(
                    s.ToLowerInvariant()
                     .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                     .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                     .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                     .Replace("ç", "c").Replace("ñ", "n"),
                    @"\b(cr|fc|sc|ec|ac|se|esporte|clube|club|futebol|de|do|da|dos|las|los)\b|\s+",
                    "");

            var na = Normalizar(a);
            var nb = Normalizar(b);

            return na == nb
                || na.Contains(nb)
                || nb.Contains(na);
        }

        private static string MapearPosicaoTransfermarkt(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "MC";

            var s = raw.ToLowerInvariant();

            // Goleiro
            if (s.Contains("gk") || s.Contains("goal") || s.Contains("goleiro") || s.Contains("keeper") || s.Contains("torhüter"))
                return "GL";

            // Zagueiro / defensores / laterais
            if (s.Contains("cb") || s.Contains("center back") || s.Contains("central") || s.Contains("zagueiro") ||
                s.Contains("def") || s.Contains("df") || s.Contains("lb") || s.Contains("rb") ||
                s.Contains("lateral") || s.Contains("wing back") || s.Contains("back"))
                return "ZG";

            // Meia / volantes / meio-campo / ofensivo médio
            if (s.Contains("dm") || s.Contains("cdm") || s.Contains("volante") ||
                s.Contains("cm") || s.Contains("mid") || s.Contains("meia") || s.Contains("am") || s.Contains("cam") || s.Contains("att-mid"))
                return "MC";

            // Atacantes / pontas / centroavante / forwards / striker
            if (s.Contains("fw") || s.Contains("st") || s.Contains("striker") || s.Contains("atacante") ||
                s.Contains("forward") || s.Contains("cf") || s.Contains("lw") || s.Contains("rw") || s.Contains("wing"))
                return "AT";

            // Reservas ou não identificado
            if (s.Contains("sub") || s.Contains("res"))
                return "RES";

            // fallback
            return "MC";
        }

        // helper local dentro da classe TransfermarktService
        private static string ExpandirSiglaPosicaoParaNome(string sigla)
        {
            if (string.IsNullOrWhiteSpace(sigla)) return string.Empty;
            return sigla.ToUpperInvariant() switch
            {
                "GL" => "Goleiro",
                "ZG" => "Zagueiro",
                "MC" => "Meio",
                "AT" => "Atacante",
                "RES" => "Reserva",
                _ => sigla // deixa como veio (pode ser já um nome)
            };
        }

        // Adicione dentro da classe TransfermarktService (ex.: após ExpandirSiglaPosicaoParaNome)
        private static string CleanTeamName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // normaliza espaços e quebras
            var s = Regex.Replace(raw, @"\s+", " ").Replace("\r", "").Replace("\n", "").Trim();

            // remove tokens numéricos grandes que aparecem como ruído (ids, contadores)
            s = Regex.Replace(s, @"\d{2,}", "");

            // normaliza separadores e pontuação
            s = Regex.Replace(s, @"\s*[-–—/\\]\s*", " ");
            s = Regex.Replace(s, @"[,:;·•\u2022]+", " ");

            // remove textos entre parênteses que costumam trazer informação extra
            s = Regex.Replace(s, @"\s*\([^)]*\)\s*", " ");

            // remove múltiplos espaços remanescentes e trim final
            s = Regex.Replace(s, @"\s{2,}", " ").Trim();

            // retira pontuação inicial/final
            return s.Trim(new char[] { '-', '–', '—', '/', '\\', ',', '.', '(', ')' });
        }

        private static string RemoveNoiseTokens(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) return string.Empty;

            var noise = new[]
            {
            "Agenda", "FT", "UCL", "Copa Libertadores", "UEFA Champions League",
            "Copa Sul-Americana", "Copa do Nordeste", "Sudamericano", "CONCACAF Champions Cup",
            "TV", "SC", "SF", "horário", "horario", "min", "′", "’"
             };

            foreach (var n in noise)
                txt = Regex.Replace(txt, Regex.Escape(n), "", RegexOptions.IgnoreCase);

            // remove timestamps e padrões óbvios de ruído
            txt = Regex.Replace(txt, @"\d{1,2}[:h]\d{2}", ""); // 15:00 ou 15h30
            txt = Regex.Replace(txt, @"\b[A-Z]{2,6}\b", "", RegexOptions.IgnoreCase); // tokens tipo TV, UCL
            txt = Regex.Replace(txt, @"\s{2,}", " ").Trim();

            return txt;
        }
      
        // Necessário comparer para HashSet de HtmlNode baseado em XPath
        private class HtmlNodeXPathComparer : IEqualityComparer<HtmlAgilityPack.HtmlNode>
        {
            public static HtmlNodeXPathComparer Instance { get; } = new HtmlNodeXPathComparer();

            public bool Equals(HtmlAgilityPack.HtmlNode? x, HtmlAgilityPack.HtmlNode? y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return string.Equals(x.XPath, y.XPath, StringComparison.Ordinal);
            }

            public int GetHashCode(HtmlAgilityPack.HtmlNode obj)
            {
                return obj.XPath?.GetHashCode() ?? 0;
            }
        }
    }
}