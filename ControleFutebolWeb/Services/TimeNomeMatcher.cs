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
            new[] { "athletico-pr", "athletico paranaense", "atletico-pr", "atletico paranaense", "athletico" },
            new[] { "atletico-mg", "atletico mineiro", "atletico-mg" },
            new[] { "atletico-go", "atletico goianiense" },
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
        };

        private static readonly Dictionary<string, string> ApelidoParaGrupo = BuildApelidoIndex();

        private static Dictionary<string, string> BuildApelidoIndex()
        {
            var indice = new Dictionary<string, string>();
            foreach (var grupo in GruposDeTimes)
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

        private static string NormalizarTime(string? nomeTime)
        {
            var normalizado = Normalizar(nomeTime);
            // Sufixos de categoria de base que não existem no nosso cadastro de clube principal.
            foreach (var sufixo in new[] { " sub 17", " sub 20", " sub 15", " u17", " u20", " u15", " f" })
            {
                if (normalizado.EndsWith(sufixo.Trim(), StringComparison.Ordinal) &&
                    normalizado.Length > sufixo.Trim().Length)
                {
                    normalizado = normalizado[..^sufixo.Trim().Length].TrimEnd();
                }
            }
            return normalizado;
        }

        // Compara dois nomes de time considerando o de-para conhecido, com fallback
        // para igualdade normalizada. Não usa "contains" para evitar falso positivo
        // (ex.: "Inter" dentro de "Internacional de Limeira").
        public static bool SaoMesmoTime(string? nomeBanco, string? nomeFonteExterna)
        {
            var a = NormalizarTime(nomeBanco);
            var b = NormalizarTime(nomeFonteExterna);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a == b) return true;

            var grupoA = ApelidoParaGrupo.GetValueOrDefault(a, a);
            var grupoB = ApelidoParaGrupo.GetValueOrDefault(b, b);
            return grupoA == grupoB;
        }

        // Competição: exige batida por igualdade normalizada ou uma contendo a outra
        // (ex.: nosso "Brasileirão - Série A" vs "Brasileirão Série A" do site).
        public static bool SaoMesmaCompeticao(string? nomeBanco, string? nomeFonteExterna)
        {
            var a = Normalizar(nomeBanco);
            var b = Normalizar(nomeFonteExterna);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a == b) return true;

            return a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
        }
    }
}
