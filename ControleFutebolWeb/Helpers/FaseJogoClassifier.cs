using ControleFutebolWeb.Models;
using System.Text.RegularExpressions;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Categoria de um jogo dentro da competição, inferida a partir de Jogo.Grupo
    /// (round da api-football: "Group A", "Regular Season - 15", "Quarterfinals"...).
    /// </summary>
    public enum FaseCategoria
    {
        Grupos,
        Liga,
        MataMata,
        Indefinida,
    }

    /// <summary>
    /// Classifica jogos em categorias de fase e os distribui entre as fases
    /// declaradas de uma competição (<see cref="CompeticaoFase"/>). Os jogos não
    /// referenciam a fase no banco — a associação é sempre calculada em leitura,
    /// já que Jogo.Grupo é mantido estável pela importação da api-football.
    /// </summary>
    public static class FaseJogoClassifier
    {
        // Palavras que identificam rounds eliminatórios na api-football e nos
        // rótulos em português já usados por MontarMataMata no CompeticoesController.
        private static readonly string[] PalavrasMataMata =
        {
            "Final", "Semi", "Quarter", "Round of", "Knockout", "Play",
            "Qualification", "Preliminary", "Oitavas", "Quartas", "avos",
        };

        // Round que termina em número ("Regular Season - 15", "Apertura - 3")
        // indica rodada de liga/pontos corridos.
        private static readonly Regex TerminaEmNumero = new(@"\d+\s*$", RegexOptions.Compiled);

        public static FaseCategoria Classificar(string? grupo)
        {
            var nome = (grupo ?? "").Trim();

            if (nome.Length == 0)
                return FaseCategoria.Indefinida;

            if (nome.StartsWith("Group", StringComparison.OrdinalIgnoreCase) ||
                nome.StartsWith("Grupo", StringComparison.OrdinalIgnoreCase))
                return FaseCategoria.Grupos;

            if (PalavrasMataMata.Any(p => nome.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return FaseCategoria.MataMata;

            if (TerminaEmNumero.IsMatch(nome))
                return FaseCategoria.Liga;

            // Rounds desconhecidos sem número são quase sempre eliminatórios.
            return FaseCategoria.MataMata;
        }

        /// <summary>
        /// Distribui os jogos entre as fases declaradas:
        /// 1. RoundsPattern (padrões ";"-separados, Contains case-insensitive) sempre vence;
        /// 2. senão, a categoria heurística vai para a primeira fase (por Ordem) de Tipo
        ///    correspondente (Grupos→GRUPOS, Liga→PONTOS_CORRIDOS, MataMata→MATA_MATA);
        /// 3. Indefinida ou categoria sem fase correspondente cai na primeira fase
        ///    não-MATA_MATA (ou na primeira fase, se todas forem MATA_MATA) — assim
        ///    nenhum jogo desaparece da tela.
        /// </summary>
        public static Dictionary<int, List<Jogo>> DistribuirPorFases(
            IReadOnlyList<CompeticaoFase> fases, IEnumerable<Jogo> jogos)
        {
            var ordenadas = fases.OrderBy(f => f.Ordem).ThenBy(f => f.Id).ToList();
            var resultado = ordenadas.ToDictionary(f => f.Id, _ => new List<Jogo>());
            if (ordenadas.Count == 0) return resultado;

            var fasePadrao = ordenadas.FirstOrDefault(f => f.Tipo != "MATA_MATA") ?? ordenadas[0];

            foreach (var jogo in jogos)
            {
                var fase = FasePorPattern(ordenadas, jogo.Grupo)
                    ?? FasePorCategoria(ordenadas, Classificar(jogo.Grupo))
                    ?? fasePadrao;
                resultado[fase.Id].Add(jogo);
            }

            return resultado;
        }

        private static CompeticaoFase? FasePorPattern(List<CompeticaoFase> fases, string? grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo)) return null;

            return fases.FirstOrDefault(f =>
                !string.IsNullOrWhiteSpace(f.RoundsPattern) &&
                f.RoundsPattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(p => grupo.Contains(p, StringComparison.OrdinalIgnoreCase)));
        }

        private static CompeticaoFase? FasePorCategoria(List<CompeticaoFase> fases, FaseCategoria categoria)
        {
            var tipo = categoria switch
            {
                FaseCategoria.Grupos => "GRUPOS",
                FaseCategoria.Liga => "PONTOS_CORRIDOS",
                FaseCategoria.MataMata => "MATA_MATA",
                _ => null,
            };
            return tipo == null ? null : fases.FirstOrDefault(f => f.Tipo == tipo);
        }
    }
}
