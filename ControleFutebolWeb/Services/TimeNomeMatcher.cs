using System.Globalization;
using System.Text;

namespace ControleFutebolWeb.Services
{
    // Casa nomes de time/competição do nosso banco com os nomes usados pela fonte externa
    // (futnatv.net), que raramente batem 100% (acentos, abreviações, apelidos regionais).
    public static class TimeNomeMatcher
    {
        // De-para de apelidos/variações conhecidas. Chave e valores já em forma normalizada
        // (sem acento, minúsculo) — ver NormalizarTime. Cada grupo representa "o mesmo time".
        private static readonly List<string[]> GruposDeTimes = new()
        {
            new[] { "athletico pr", "athletico paranaense", "atletico pr", "atletico paranaense", "athletico" },
            new[] { "atletico mg", "atletico mineiro" },
            new[] { "atletico go", "atletico goianiense" },
            new[] { "america mg", "america mineiro" },
            new[] { "rb bragantino", "bragantino", "red bull bragantino" },
            // O "Inter" do nosso cadastro é o de Milão na Série A e o Internacional no
            // Brasileirão — os dois no mesmo grupo, porque a competição é que separa e eles
            // nunca aparecem na mesma.
            new[] { "internacional", "inter", "internazionale", "inter milan" },
            new[] { "gremio", "gremio fbpa" },
            new[] { "sao paulo", "sao paulo fc", "spfc" },
            new[] { "vasco", "vasco da gama" },
            new[] { "botafogo", "botafogo rj" },
            new[] { "flamengo", "cr flamengo" },
            new[] { "fluminense", "fluminense fc" },
            new[] { "corinthians", "sc corinthians paulista" },
            new[] { "palmeiras", "se palmeiras" },
            new[] { "santos", "santos fc" },
            new[] { "cruzeiro", "cruzeiro ec" },
            new[] { "bahia", "ec bahia" },
            // "Vitória BA" (nosso cadastro) x "Vitória" (futnatv). A sigla de estado não sai em
            // NormalizarTime de propósito, então o de-para é que resolve. O Vitória português
            // ("Vitória SC") fica de fora do grupo e continua não casando.
            new[] { "vitoria", "ec vitoria", "vitoria ba" },
            new[] { "coritiba", "coritiba fc", "coxa" },
            new[] { "chapecoense", "chapecoense af", "chapecoense sc" },
            new[] { "remo", "clube do remo" },
            new[] { "mirassol", "mirassol fc" },
            // Série B: o futnatv encurta o nome tirando a cidade. "Sport" sozinho é seguro
            // porque a competição também é comparada. (O "FC" de "São Bernardo FC" já sai
            // sozinho em NormalizarTime, então não precisa de de-para.)
            new[] { "nautico recife", "nautico" },
            new[] { "sport recife", "sport" },
            // Sul-Americana / Libertadores: o futnatv encurta o nome dos clubes sul-americanos.
            new[] { "deportivo recoleta", "recoleta" },
            new[] { "independiente rivadavia", "ind rivadavia" },
            new[] { "universidad catolica", "u catolica" },
            new[] { "boca juniors", "boca jrs" },
            // O futnatv larga a cidade do Talleres de Córdoba. O de Remedios de Escalada não
            // está cadastrado nem joga a primeira divisão argentina.
            new[] { "talleres cordoba", "talleres", "talleres de cordoba" },
            // O futnatv larga a cidade do Sarmiento de Junín. O de Resistência não está
            // cadastrado nem joga a primeira divisão argentina, então "sarmiento" seco é seguro.
            new[] { "sarmiento junin", "sarmiento" },
            // A ESPN põe a província entre parênteses e por extenso: "Central Córdoba
            // (Santiago del Estero)". "central cordoba" seco entra porque o de Rosário não
            // está cadastrado nem joga a primeira divisão argentina.
            new[] { "central cordoba de santiago", "central cordoba santiago del estero",
                    "central cordoba", "central cordoba sde" },
            // A ESPN escreve "Liga de Quito" onde o nosso cadastro diz "LDU de Quito".
            new[] { "ldu de quito", "liga de quito", "ldu", "ldu quito" },
            // A ESPN põe a cidade que o nosso cadastro não tem: "Cienciano del Cusco".
            new[] { "cienciano", "cienciano del cusco" },
            new[] { "river plate", "river" },
            new[] { "atletico torque", "montevideo city", "montevideo city torque" },
            // O futnatv chama o Estudiantes de La Plata só de "Estudiantes". A competição e o
            // adversário é que separam do Estudiantes de Mérida no casamento.
            new[] { "estudiantes lp", "estudiantes", "estudiantes de la plata" },
            // Idem para o Gimnasia de La Plata. "gimnasia" sozinho fica de fora: existem
            // Gimnasia de Mendoza e de Jujuy, e casá-los aqui gravaria a transmissão errada.
            new[] { "gimnasia lp", "gimnasia la plata" },
            new[] { "union st gilloise", "union saint gilloise", "union sg" },
            // Eredivisie: o futnatv acrescenta a cidade que o nosso cadastro não tem.
            new[] { "az", "az alkmaar" },
            // Campeonato Saudita: os clubes árabes têm transliteração diferente de cada lado
            // ("Al-Faisaly" x "Al-Faysaly", "Al-Qadisiyah" x "Al-Qadsiah"), e a diferença cai
            // no meio da palavra, então nem a igualdade nem a abreviação resolvem.
            // Inglaterra: o futnatv mantém o "City"/"Town" do nome oficial que o nosso cadastro
            // corta. É de-para clube a clube de propósito — tirar "city" do nome no geral faria
            // "Manchester City" casar com o United.
            // Portugal: o futnatv chama o Sporting CP só de "Sporting". O Sporting Cristal e o
            // Sp. Braga não entram no grupo e continuam sem casar, porque a comparação exige
            // o nome inteiro igual.
            new[] { "sporting cp", "sporting" },
            new[] { "coventry", "coventry city" },
            new[] { "leicester", "leicester city" },
            new[] { "norwich city", "norwich" },
            // "Wolves" é o apelido que o nosso cadastro usa; o futnatv escreve "Wolverhampton".
            new[] { "wolves", "wolverhampton", "wolverhampton wanderers" },
            // La Liga: o futnatv corta o "Real" do nome. Só entra clube sem homônimo — "Real
            // Madrid" e "Real Sociedad" ficam de fora, que lá o "Real" faz parte do nome usado.
            new[] { "real betis", "betis" },
            // Os dois "Athletic Club" do nosso cadastro (o de Bilbao e o mineiro da Série B)
            // têm nome idêntico, então dividem o grupo à força — a competição é que separa.
            // O futnatv escreve "Athletic Bilbao" na La Liga e a ESPN escreve "Athletic" seco
            // na Série B. "Athletico-PR" e "Charlton Athletic" não colidem: são outras strings.
            new[] { "athletic club", "athletic bilbao", "athletic" },
            new[] { "celta vigo", "celta de vigo", "celta" },
            // Bundesliga: o futnatv aportuguesa o nome de Munique e a ESPN escreve em inglês.
            new[] { "bayern munchen", "bayern munique", "bayern munich" },
            // "B. M'gladbach" (futnatv): a abreviação bate em DUAS palavras de uma vez
            // ("Borussia" -> "B." e "Mönchengladbach" -> "M'gladbach"), então nem o de-para
            // por palavra abreviada resolve. O apóstrofo some na normalização, e é por isso
            // que a forma que chega aqui é "mgladbach", tudo junto.
            new[] { "borussia monchengladbach", "b mgladbach", "borussia mgladbach",
                    "monchengladbach", "mgladbach", "gladbach" },
            // O futnatv aportuguesa Hamburgo; o "SV" do nosso cadastro fica no fim do nome,
            // onde a lista de sufixos não mexe (lá "SC"/"SV" seriam sigla de estado).
            new[] { "hamburger sv", "hamburgo", "hamburg", "hamburger" },
            // Ligue 1: o futnatv usa o nome completo do clube e o nosso cadastro só a cidade.
            new[] { "marseille", "olympique de marseille", "olympique marseille" },
            new[] { "lyon", "olympique de lyon", "olympique lyonnais" },
            new[] { "estac troyes", "troyes", "troyes ac" },
            new[] { "stade brestois 29", "brest", "stade brestois", "stade brestois 29 fc" },
            // O caminho inverso: aqui é o futnatv que usa a sigla e o nosso cadastro o nome
            // por extenso. O "Paris FC" fica de fora do grupo e continua sem casar com o PSG.
            new[] { "paris saint germain", "psg", "paris sg" },
            // Campeonato do Catar: o FotMob (ver FotMobService) escreve o nome curto e
            // com hífen, e o nosso cadastro traz a cidade ou o "SC" que ele omite. O
            // hífen já sai na normalização; o que sobra é este de-para.
            //
            // "SC" NÃO é removido no geral de propósito: é a sigla de Santa Catarina e
            // tirá-la faria "Chapecoense SC" e "Vitória SC" colidirem com outros clubes.
            new[] { "al ahli doha", "al ahli" },
            new[] { "al duhail", "al duhail sc" },
            new[] { "al arabi sc", "al arabi" },
            new[] { "al rayyan sc", "al rayyan" },
            // O FotMob chama de "Lusail SC" o que o nosso cadastro tem como
            // "Lusail City". Clube novo, sem homônimo em outra liga cadastrada.
            new[] { "lusail city", "lusail sc", "lusail" },
            new[] { "al faisaly", "al faysaly" },
            new[] { "al taawoun", "al taawon", "al tawoun" },
            new[] { "al qadisiyah", "al qadsiah", "al qadisiya" },
            // "Al-Hilal Saudi FC" (nosso cadastro) x "Al-Hilal" (futnatv): o "FC" já sai
            // sozinho, o que sobra é o "Saudi" que só o nosso lado traz.
            new[] { "al hilal saudi", "al hilal" },
            // A ESPN (fonte de estatísticas de reserva, ver EspnEstatisticasService)
            // usa o nome em inglês ou corta o nome no meio. Sem o de-para o jogo não
            // é localizado no scoreboard e a importação falha.
            new[] { "crvena zvezda", "red star belgrade" },
            // "Hapoel Be'er" (futnatv). O apóstrofo some na normalização, então a forma
            // que chega aqui é "hapoel beer"; "hapoel be er" fica para a fonte que
            // escrever com espaço de verdade.
            new[] { "hapoel beer sheva", "hapoel beer", "hapoel be er" },
            // Europa League: o futnatv corta a cidade ("Levski", "NEC"), acrescenta a que o
            // nosso cadastro não tem ("OFI Crete", "Viktoria Plzen") ou usa a forma curta
            // ("RB Salzburg", "Ferencváros" sem o "TC" do nome oficial).
            new[] { "levski sofia", "levski" },
            new[] { "red bull salzburg", "rb salzburg", "salzburg" },
            new[] { "ofi", "ofi crete", "ofi creta" },
            new[] { "plzen", "viktoria plzen" },
            new[] { "nec nijmegen", "nec" },
            new[] { "ferencvarosi tc", "ferencvaros", "ferencvarosi" },
        };

        // Mesma ideia para competições: o nosso cadastro usa o nome "oficial"/da API de dados,
        // enquanto o futnatv usa o nome popular em português. Primeiro item = nome do nosso banco.
        private static readonly List<string[]> GruposDeCompeticoes = new()
        {
            new[] { "campeonato brasileiro", "brasileirao serie a", "brasileirao", "campeonato brasileiro serie a" },
            new[] { "brasileirao feminino", "campeonato brasileiro feminino" },
            // Série B: o futnatv diz "Brasileirão - Série B" e o nosso cadastro guarda o nome
            // cru da api-football, "Serie B". O "serie b" seco fica aqui porque hoje a Série B
            // italiana não está cadastrada; se ela entrar, precisa entrar com o país no nome
            // (como "Serie A TIM"), senão as duas viram a mesma competição aqui.
            new[] { "serie b", "brasileirao serie b", "campeonato brasileiro serie b" },
            new[] { "copa do brasil" },
            new[] { "libertadores", "copa libertadores", "conmebol libertadores", "libertadores da america" },
            new[] { "sulamericana", "sul americana", "copa sul americana", "conmebol sul americana" },
            new[] { "premier league", "campeonato ingles" },
            // "Campeonato Espanhol (2ª div.)" não entra no grupo e, como a comparação é por
            // igualdade (nunca "contains"), continua sem casar com a primeira divisão.
            new[] { "la liga", "laliga", "campeonato espanhol" },
            // O futnatv chama a fase preliminar/qualificatória de "Pré-Champions League",
            // mas no nosso cadastro esses jogos entram na própria Champions League.
            new[] { "champions league", "liga dos campeoes", "uefa champions league", "pre champions league" },
            // "Serie A" seco é como o nosso cadastro guarda o italiano. Não colide com o
            // brasileiro: lá o nome vem sempre com o país junto ("Brasileirão - Série A").
            new[] { "serie a tim", "campeonato italiano", "serie a" },
            new[] { "bundesliga", "campeonato alemao" },
            new[] { "ligue 1", "campeonato frances" },
            // Nosso cadastro diz "Holandês"; o futnatv diz "Neerlandês".
            new[] { "campeonato holandes", "eredivisie", "campeonato neerlandes" },
            new[] { "copa do mundo", "copa do mundo fifa" },
        };

        // Letras que NÃO são "letra + acento" em Unicode e por isso sobrevivem ao FormD:
        // o "ı" sem ponto do turco ("Kasımpaşa"), o "ø" nórdico, o "đ" e o "ł". Sem isso
        // "Kasımpaşa" virava "kasımpaşa" e nunca casava com o "Kasimpasa" do futnatv.
        private static char? LetraSemDecomposicao(char c) => char.ToLowerInvariant(c) switch
        {
            'ı' => 'i',
            'ø' => 'o',
            'đ' => 'd',
            'ð' => 'd',
            'ł' => 'l',
            _ => null,
        };

        // Ponto de abreviação colado na letra ("S.K.", "L.P.", "F.B.Pa") some sem virar espaço,
        // senão a sigla se quebra em tokens de uma letra. O ponto que fecha a palavra
        // ("Ind. Rivadavia") continua virando espaço, porque ali ele separa mesmo.
        private static string RemoverPontosDeSigla(string texto)
        {
            var sb = new StringBuilder(texto.Length);
            for (var i = 0; i < texto.Length; i++)
            {
                var colaLetras = texto[i] == '.'
                    && i > 0 && char.IsLetterOrDigit(texto[i - 1])
                    && (i == texto.Length - 1 || char.IsLetterOrDigit(texto[i + 1]));
                if (!colaLetras) sb.Append(texto[i]);
            }
            return sb.ToString();
        }

        // Preposição/artigo solto no meio do nome: uma fonte põe, a outra não
        // ("Celta de Vigo" x "Celta Vigo", "Olympique de Marseille" x "Olympique Marseille").
        // Sai de todo nome comparado — e também das chaves do de-para, que passam pela mesma
        // normalização em BuildIndice, senão "vasco da gama" viraria uma chave inalcançável.
        // As variantes que as fontes usam para o mesmo sinal: aspa reta do teclado, a
        // curva que editores trocam sozinhos, o acento agudo solto e a modificadora
        // Unicode.
        private static readonly HashSet<char> Apostrofos = new() { '\'', '\u2019', '\u2018', '\u02BC', '\u00B4', '`' };

        private static readonly HashSet<string> PalavrasDeLigacao = new()
        {
            "de", "da", "do", "dos", "das", "del", "di", "du", "des", "of",
        };

        // Remove acentos, pontuação e normaliza espaços/caixa. Ex.: "Grêmio F.B.Pa" -> "gremio fbpa"
        private static string Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";

            var semAcento = RemoverPontosDeSigla(texto).Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in semAcento)
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
                if (categoria == UnicodeCategory.NonSpacingMark) continue;

                var equivalente = LetraSemDecomposicao(c);
                if (equivalente.HasValue) { sb.Append(equivalente.Value); continue; }

                // Apóstrofo SOME em vez de virar espaço: ele fica no meio da palavra, não
                // entre palavras. A ESPN escreve "Newell's Old Boys" e o nosso cadastro
                // (que veio da api-football) escreve "Newells Old Boys" — virando espaço,
                // um lado normalizava para "newell s old boys" e o outro para "newells old
                // boys", e a partida não era localizada. Vale para "O'Higgins" e
                // "Inter Club d'Escaldes" pelo mesmo motivo.
                if (Apostrofos.Contains(c)) continue;

                sb.Append(char.IsLetterOrDigit(c) || c == ' ' ? char.ToLowerInvariant(c) : ' ');
            }

            var partes = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var semLigacao = partes.Where(p => !PalavrasDeLigacao.Contains(p)).ToArray();
            // Se o nome era SÓ ligação (nunca deveria acontecer), fica o original.
            return string.Join(' ', semLigacao.Length > 0 ? semLigacao : partes);
        }

        // Sufixos de categoria de base / futebol feminino, que não fazem parte do nome do clube.
        // "w" (nosso cadastro) e "f" (futnatv) marcam o time feminino — a competição é quem
        // desambigua masculino x feminino no casamento, então podem sair do nome.
        private static readonly string[] SufixosCategoria =
            { "sub 17", "sub 20", "sub 15", "u17", "u20", "u15", "w", "f" };

        // Sigla de tipo de clube no fim do nome ("Çorum FK", "São Bernardo FC", "Cruzeiro EC"):
        // um lado põe, o outro não, e isso sozinho já quebrava o casamento. Só siglas de
        // agremiação entram aqui — sigla de estado ("Botafogo SP", "Atlético GO") fica de fora
        // de propósito, porque lá ela é justamente o que distingue dois clubes homônimos.
        // "town" entra aqui porque nenhum clube inglês com "Town" tem homônimo sem ele
        // (Ipswich, Huddersfield, Luton). Já "City" fica no de-para clube a clube: cortá-lo
        // faria "Manchester City" virar "Manchester" e casar errado.
        private static readonly string[] SufixosAgremiacao =
            { "fc", "fk", "cf", "ec", "sk", "afc", "town" };

        // "club" por extenso entra junto: a ESPN escreve "Club Olimpia", "Club Brugge",
        // e o nosso cadastro só a segunda palavra.
        // A mesma sigla, mas no começo do nome: "NK Celje" (ESPN) x "Celje" (nosso
        // cadastro), "FC Porto" x "Porto". Tirar o prefixo só reaproveita o resto do
        // nome — a comparação continua exigindo que TODO o resto seja igual, então
        // "AC Milan" não passa a casar com "Inter Milan".
        // "VfB"/"VfL"/"SV"/"FSV"/"TSG"/"TSV" são o mesmo tipo de sigla no futebol alemão, e o
        // futnatv corta todas ("VfB Stuttgart" x "Stuttgart", "SV Elversberg" x "Elversberg",
        // "FSV Mainz 05" x "Mainz 05"). Nenhum clube alemão cadastrado fica ambíguo sem elas —
        // o que sobra é sempre o nome da cidade.
        // "AS" é o mesmo caso no italiano/francês: o futnatv escreve "Roma" onde o nosso
        // cadastro tem "AS Roma".
        private static readonly string[] PrefixosAgremiacao =
            { "fc", "fk", "nk", "sk", "ac", "as", "sc", "cf", "ec", "cd", "afc", "club",
              "vfb", "vfl", "sv", "fsv", "tsg", "tsv" };

        // Siglas de UF que aparecem coladas no nome do clube ("Internacional RS", "Vitória BA").
        private static readonly HashSet<string> SiglasEstado = new()
        {
            "ac", "al", "am", "ap", "ba", "ce", "df", "es", "go", "ma", "mg", "ms", "mt",
            "pa", "pb", "pe", "pi", "pr", "rj", "rn", "ro", "rr", "rs", "sc", "se", "sp", "to",
        };

        // Separa a sigla de estado do fim do nome, dizendo se ela existia. Quem chama decide
        // se pode tirá-la — no masculino ela é o que distingue Botafogo-RJ de Botafogo-SP.
        private static (string nome, bool tinhaSigla) SepararSiglaEstado(string normalizado)
        {
            var corte = normalizado.LastIndexOf(' ');
            if (corte <= 0) return (normalizado, false);

            var ultima = normalizado[(corte + 1)..];
            return SiglasEstado.Contains(ultima) ? (normalizado[..corte], true) : (normalizado, false);
        }

        private static string NormalizarTime(string? nomeTime) => NormalizarTime(nomeTime, out _);

        // feminino: o nome vinha marcado como time feminino ("Vitória BA W" no nosso cadastro,
        // "Botafogo F" no futnatv). Sai do nome, mas quem chama ainda precisa saber.
        private static string NormalizarTime(string? nomeTime, out bool feminino)
        {
            feminino = false;
            var normalizado = Normalizar(nomeTime);
            // Exige o espaço antes do sufixo para não mutilar nomes que só terminam na mesma letra.
            foreach (var sufixo in SufixosCategoria)
            {
                if (normalizado.EndsWith(" " + sufixo, StringComparison.Ordinal))
                {
                    feminino = sufixo is "w" or "f";
                    normalizado = normalizado[..^(sufixo.Length + 1)].TrimEnd();
                    break;
                }
            }
            foreach (var sufixo in SufixosAgremiacao)
            {
                if (normalizado.EndsWith(" " + sufixo, StringComparison.Ordinal))
                {
                    normalizado = normalizado[..^(sufixo.Length + 1)].TrimEnd();
                    break;
                }
            }
            // Número na frente do nome é convenção alemã de ano de fundação ("1. FC Köln",
            // "1899 Hoffenheim") e nenhuma fonte é consistente com ele. Sai antes do prefixo
            // para que "1 fc koln" ainda vire "koln". O número no FIM continua intocado: lá
            // ele faz parte do nome usado pelos dois lados ("Mainz 05", "Schalke 04").
            var primeiroEspaco = normalizado.IndexOf(' ');
            if (primeiroEspaco > 0 && normalizado[..primeiroEspaco].All(char.IsDigit))
                normalizado = normalizado[(primeiroEspaco + 1)..];

            // Só tira o prefixo se sobrar nome: "FC" sozinho continua "fc".
            foreach (var prefixo in PrefixosAgremiacao)
            {
                if (normalizado.StartsWith(prefixo + " ", StringComparison.Ordinal))
                {
                    normalizado = normalizado[(prefixo.Length + 1)..].TrimStart();
                    break;
                }
            }
            return normalizado;
        }

        // O futnatv abrevia uma palavra do nome ("Ind. Rivadavia", "U. Católica", "Dep. A Coruña")
        // e o nosso cadastro pode abreviar de outro jeito ("Independ. Rivadavia"). Aceita o par
        // quando os nomes têm o mesmo número de palavras, só UMA difere, e essa palavra é prefixo
        // da outra. Exigir todas as demais palavras iguais é o que segura o falso positivo:
        // "Atlético-MG" x "Atlético Madrid" não casa ("mg" não é prefixo de "madrid").
        private static bool UmaPalavraEhAbreviacaoDaOutra(string a, string b)
        {
            var pa = a.Split(' ');
            var pb = b.Split(' ');
            if (pa.Length != pb.Length || pa.Length < 2) return false;

            var diferentes = 0;
            var abreviacaoValida = false;
            for (var i = 0; i < pa.Length; i++)
            {
                if (pa[i] == pb[i]) continue;
                if (++diferentes > 1) return false;

                var (curta, longa) = pa[i].Length < pb[i].Length ? (pa[i], pb[i]) : (pb[i], pa[i]);
                abreviacaoValida = longa.StartsWith(curta, StringComparison.Ordinal);
            }

            return diferentes == 1 && abreviacaoValida;
        }

        // As chaves do de-para são escritas à mão e precisam passar pela MESMA normalização
        // dos nomes comparados — senão uma entrada como "vasco da gama" ou "sc corinthians
        // paulista" nunca seria encontrada no índice. Fica aqui embaixo de propósito:
        // inicializador de campo estático roda na ordem do arquivo, e NormalizarTime depende
        // dos arrays de sufixo/prefixo declarados acima.
        private static readonly Dictionary<string, string> ApelidoParaGrupo =
            BuildIndice(GruposDeTimes, n => NormalizarTime(n));
        private static readonly Dictionary<string, string> CompeticaoParaGrupo =
            BuildIndice(GruposDeCompeticoes, Normalizar);

        private static Dictionary<string, string> BuildIndice(
            List<string[]> grupos, Func<string, string> normalizar)
        {
            var indice = new Dictionary<string, string>();
            foreach (var grupo in grupos)
            {
                var chaveGrupo = normalizar(grupo[0]);
                foreach (var apelido in grupo)
                {
                    var chave = normalizar(apelido);
                    // Duas normalizações iguais em grupos diferentes casariam times distintos
                    // sem ninguém perceber; melhor estourar na subida do que gravar canal errado.
                    if (indice.TryGetValue(chave, out var existente) && existente != chaveGrupo)
                        throw new InvalidOperationException(
                            $"De-para ambíguo: '{chave}' aparece nos grupos '{existente}' e '{chaveGrupo}'.");
                    indice[chave] = chaveGrupo;
                }
            }
            return indice;
        }

        // Compara dois nomes de time considerando o de-para conhecido, com fallback
        // para igualdade normalizada e para abreviação de uma palavra. Não usa "contains"
        // para evitar falso positivo (ex.: "Inter" dentro de "Internacional de Limeira").
        public static bool SaoMesmoTime(string? nomeBanco, string? nomeFonteExterna)
        {
            var a = NormalizarTime(nomeBanco, out var femininoA);
            var b = NormalizarTime(nomeFonteExterna, out var femininoB);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a == b) return true;

            var grupoA = ApelidoParaGrupo.GetValueOrDefault(a, a);
            var grupoB = ApelidoParaGrupo.GetValueOrDefault(b, b);
            if (grupoA == grupoB) return true;

            if (UmaPalavraEhAbreviacaoDaOutra(grupoA, grupoB)) return true;

            // No feminino o nosso cadastro carrega a sigla de estado herdada do masculino
            // ("Internacional RS W", "Vitória BA W") e o futnatv usa o nome puro. O feminino
            // não tem os homônimos que fazem a sigla ser obrigatória no masculino, então aqui
            // ela pode cair — mas só quando apenas UM dos lados a traz: se os dois trazem
            // siglas diferentes, são clubes diferentes mesmo (Botafogo-SP x Botafogo-RJ).
            if (!femininoA && !femininoB) return false;

            // Olha o nome cru, não o do grupo: o de-para pode ter trocado "botafogo rj" por
            // "botafogo" e aí os dois lados pareceriam estar sem sigla.
            var (semSiglaA, tinhaSiglaA) = SepararSiglaEstado(a);
            var (semSiglaB, tinhaSiglaB) = SepararSiglaEstado(b);
            if (tinhaSiglaA == tinhaSiglaB) return false;

            return ApelidoParaGrupo.GetValueOrDefault(semSiglaA, semSiglaA)
                == ApelidoParaGrupo.GetValueOrDefault(semSiglaB, semSiglaB);
        }

        // Competição: igualdade normalizada ou de-para conhecido. Deliberadamente NÃO usa
        // "contains": "Campeonato Alemão" casaria com "Campeonato Alemão (2ª div.)" e
        // "Brasileirão" com "Brasileirão Feminino", gravando a transmissão do jogo errado.
        public static bool SaoMesmaCompeticao(string? nomeBanco, string? nomeFonteExterna)
        {
            var a = Normalizar(nomeBanco);
            var b = Normalizar(nomeFonteExterna);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a == b) return true;

            var grupoA = CompeticaoParaGrupo.GetValueOrDefault(a, a);
            var grupoB = CompeticaoParaGrupo.GetValueOrDefault(b, b);
            return grupoA == grupoB;
        }
    }
}
