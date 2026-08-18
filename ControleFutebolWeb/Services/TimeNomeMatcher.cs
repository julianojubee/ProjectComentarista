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
            new[] { "internacional", "inter" },
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
            // A ESPN (fonte de estatísticas de reserva, ver EspnEstatisticasService)
            // usa o nome em inglês ou corta o nome no meio. Sem o de-para o jogo não
            // é localizado no scoreboard e a importação falha.
            new[] { "crvena zvezda", "red star belgrade" },
            new[] { "hapoel beer sheva", "hapoel be er" },
        };

        // Mesma ideia para competições: o nosso cadastro usa o nome "oficial"/da API de dados,
        // enquanto o futnatv usa o nome popular em português. Primeiro item = nome do nosso banco.
        private static readonly List<string[]> GruposDeCompeticoes = new()
        {
            new[] { "campeonato brasileiro", "brasileirao serie a", "brasileirao", "campeonato brasileiro serie a" },
            new[] { "brasileirao feminino", "campeonato brasileiro feminino" },
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
            new[] { "serie a tim", "campeonato italiano" },
            new[] { "bundesliga", "campeonato alemao" },
            new[] { "ligue 1", "campeonato frances" },
            // Nosso cadastro diz "Holandês"; o futnatv diz "Neerlandês".
            new[] { "campeonato holandes", "eredivisie", "campeonato neerlandes" },
            new[] { "copa do mundo", "copa do mundo fifa" },
        };

        private static readonly Dictionary<string, string> ApelidoParaGrupo = BuildIndice(GruposDeTimes);
        private static readonly Dictionary<string, string> CompeticaoParaGrupo = BuildIndice(GruposDeCompeticoes);

        private static Dictionary<string, string> BuildIndice(List<string[]> grupos)
        {
            var indice = new Dictionary<string, string>();
            foreach (var grupo in grupos)
            {
                var chaveGrupo = grupo[0];
                foreach (var apelido in grupo)
                    indice[apelido] = chaveGrupo;
            }
            return indice;
        }

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

                sb.Append(char.IsLetterOrDigit(c) || c == ' ' ? char.ToLowerInvariant(c) : ' ');
            }

            var partes = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', partes);
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
        private static readonly string[] SufixosAgremiacao =
            { "fc", "fk", "cf", "ec", "sk", "afc" };

        // A mesma sigla, mas no começo do nome: "NK Celje" (ESPN) x "Celje" (nosso
        // cadastro), "FC Porto" x "Porto". Tirar o prefixo só reaproveita o resto do
        // nome — a comparação continua exigindo que TODO o resto seja igual, então
        // "AC Milan" não passa a casar com "Inter Milan".
        private static readonly string[] PrefixosAgremiacao =
            { "fc", "fk", "nk", "sk", "ac", "sc", "cf", "ec", "cd", "afc" };

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
