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
            new[] { "vitoria", "ec vitoria" },
            new[] { "coritiba", "coritiba fc", "coxa" },
            new[] { "chapecoense", "chapecoense af", "chapecoense sc" },
            new[] { "remo", "clube do remo" },
            new[] { "mirassol", "mirassol fc" },
            // Sul-Americana / Libertadores: o futnatv encurta o nome dos clubes sul-americanos.
            new[] { "deportivo recoleta", "recoleta" },
            new[] { "independiente rivadavia", "ind rivadavia" },
            new[] { "universidad catolica", "u catolica" },
            new[] { "boca juniors", "boca jrs" },
            new[] { "river plate", "river" },
            new[] { "atletico torque", "montevideo city", "montevideo city torque" },
            // O futnatv chama o Estudiantes de La Plata só de "Estudiantes". A competição e o
            // adversário é que separam do Estudiantes de Mérida no casamento.
            new[] { "estudiantes l p", "estudiantes", "estudiantes de la plata" },
            new[] { "union st gilloise", "union saint gilloise", "union sg" },
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
            // O futnatv chama a fase preliminar/qualificatória de "Pré-Champions League",
            // mas no nosso cadastro esses jogos entram na própria Champions League.
            new[] { "champions league", "liga dos campeoes", "uefa champions league", "pre champions league" },
            new[] { "serie a tim", "campeonato italiano" },
            new[] { "bundesliga", "campeonato alemao" },
            new[] { "ligue 1", "campeonato frances" },
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

        // Remove acentos, pontuação e normaliza espaços/caixa. Ex.: "Grêmio F.B.Pa" -> "gremio fbpa"
        private static string Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";

            var semAcento = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in semAcento)
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
                if (categoria == UnicodeCategory.NonSpacingMark) continue;
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

        private static string NormalizarTime(string? nomeTime)
        {
            var normalizado = Normalizar(nomeTime);
            // Exige o espaço antes do sufixo para não mutilar nomes que só terminam na mesma letra.
            foreach (var sufixo in SufixosCategoria)
            {
                if (normalizado.EndsWith(" " + sufixo, StringComparison.Ordinal))
                {
                    normalizado = normalizado[..^(sufixo.Length + 1)].TrimEnd();
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
            var a = NormalizarTime(nomeBanco);
            var b = NormalizarTime(nomeFonteExterna);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a == b) return true;

            var grupoA = ApelidoParaGrupo.GetValueOrDefault(a, a);
            var grupoB = ApelidoParaGrupo.GetValueOrDefault(b, b);
            if (grupoA == grupoB) return true;

            return UmaPalavraEhAbreviacaoDaOutra(grupoA, grupoB);
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
