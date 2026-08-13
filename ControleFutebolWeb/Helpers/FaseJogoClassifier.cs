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
            "Qualification", "Preliminary", "Relegation", "Promotion",
            "Oitavas", "Quartas", "avos",
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

            // Round desconhecido e sem número NÃO é assumido como eliminatório: importações
            // antigas gravavam em Jogo.Grupo o nome do "grupo" do standings, que em liga de
            // tabela única é o próprio nome da competição ("Bundesliga", "Serie A"...). Tratar
            // isso como mata-mata jogava a liga inteira para a aba de playoffs e deixava a
            // classificação vazia. Indefinida entra na tabela de pontos corridos e, quando há
            // fases declaradas, cai na fase padrão (não-eliminatória).
            return FaseCategoria.Indefinida;
        }

        /// <summary>
        /// Distribui os jogos entre as fases declaradas:
        /// 1. RoundsPattern (padrões ";"-separados, Contains case-insensitive) sempre vence;
        /// 2. senão, a categoria heurística vai para a primeira fase (por Ordem) de Tipo
        ///    correspondente (Grupos→GRUPOS, Liga→PONTOS_CORRIDOS, MataMata→MATA_MATA/JOGO_UNICO);
        /// 3. Indefinida ou categoria sem fase correspondente cai na primeira fase
        ///    não-eliminatória (ou na primeira fase, se todas forem eliminatórias) — assim
        ///    nenhum jogo desaparece da tela.
        /// </summary>
        public static Dictionary<int, List<Jogo>> DistribuirPorFases(
            IReadOnlyList<CompeticaoFase> fases, IEnumerable<Jogo> jogos)
        {
            var ordenadas = fases.OrderBy(f => f.Ordem).ThenBy(f => f.Id).ToList();
            var resultado = ordenadas.ToDictionary(f => f.Id, _ => new List<Jogo>());
            if (ordenadas.Count == 0) return resultado;

            var fasePadrao = ordenadas.FirstOrDefault(f => !EhEliminatoria(f.Tipo)) ?? ordenadas[0];

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
            // MataMata também casa com fases de JOGO_UNICO (final única): as duas são
            // eliminatórias e recebem os mesmos rounds ("Final", "Semi-finals"...).
            return categoria switch
            {
                FaseCategoria.Grupos => fases.FirstOrDefault(f => f.Tipo == "GRUPOS"),
                FaseCategoria.Liga => fases.FirstOrDefault(f => f.Tipo == "PONTOS_CORRIDOS"),
                FaseCategoria.MataMata => fases.FirstOrDefault(f => EhEliminatoria(f.Tipo)),
                _ => null,
            };
        }

        /// <summary>
        /// Fase decidida em confronto direto — mata-mata ou partida única —, que nunca
        /// vira tabela de pontos corridos.
        /// </summary>
        public static bool EhEliminatoria(string? tipo)
            => tipo is "MATA_MATA" or "JOGO_UNICO";
    }
}
