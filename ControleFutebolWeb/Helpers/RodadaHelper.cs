using System.Text.RegularExpressions;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Rótulo em português da rodada/fase de um jogo. O campo Jogo.Grupo guarda o round
    /// como a api-football manda ("Regular Season - 34", "Quarter-finals"...), e as telas
    /// mostravam isso cru. O que não for reconhecido passa como veio — nome de grupo
    /// próprio ("Grupo A") ou de fase cadastrada à mão já está em português.
    /// </summary>
    public static class RodadaHelper
    {
        private static readonly (Regex Padrao, string Rotulo)[] _rounds =
        {
            (new Regex(@"^Regular Season\s*-\s*(\d+)$", RegexOptions.IgnoreCase), "Rodada $1"),
            (new Regex(@"^Group Stage\s*-\s*(\d+)$", RegexOptions.IgnoreCase), "Fase de grupos, rodada $1"),
            (new Regex(@"^League Stage\s*-\s*(\d+)$", RegexOptions.IgnoreCase), "Fase de liga, rodada $1"),
            (new Regex(@"^(?:Apertura|Clausura)\s*-\s*(\d+)$", RegexOptions.IgnoreCase), "Rodada $1"),
            (new Regex(@"^Group\s+([A-Z0-9]+)$", RegexOptions.IgnoreCase), "Grupo $1"),
            (new Regex(@"^Round of 128$", RegexOptions.IgnoreCase), "64-avos de final"),
            (new Regex(@"^Round of 64$", RegexOptions.IgnoreCase), "32-avos de final"),
            // "1/128-finals": a api-football numera pela quantidade de jogos da fase
            (new Regex(@"^1/(\d+)-finals?$", RegexOptions.IgnoreCase), "$1-avos de final"),
            (new Regex(@"^Round of 32$", RegexOptions.IgnoreCase), "16-avos de final"),
            (new Regex(@"^Round of 16$|^8th Finals$", RegexOptions.IgnoreCase), "Oitavas de final"),
            (new Regex(@"^Quarter-?finals?$", RegexOptions.IgnoreCase), "Quartas de final"),
            (new Regex(@"^Semi-?finals?$", RegexOptions.IgnoreCase), "Semifinal"),
            (new Regex(@"^3rd Place Final$|^Third Place$", RegexOptions.IgnoreCase), "Disputa de 3º lugar"),
            (new Regex(@"^Final$", RegexOptions.IgnoreCase), "Final"),
            (new Regex(@"^Relegation (?:Round|Play-?offs?|Decider)$", RegexOptions.IgnoreCase), "Playoff de rebaixamento"),
            (new Regex(@"^Promotion (?:Round|Play-?offs?)$", RegexOptions.IgnoreCase), "Playoff de acesso"),
            (new Regex(@"^Knockout Round Play-?offs$|^Play-?offs$", RegexOptions.IgnoreCase), "Playoff"),
            (new Regex(@"^Preliminary Round$", RegexOptions.IgnoreCase), "Fase preliminar"),
            (new Regex(@"^(\d)(?:st|nd|rd|th) Qualifying Round$", RegexOptions.IgnoreCase), "$1ª fase eliminatória"),
            (new Regex(@"^Qualification Round\s*(\d)$", RegexOptions.IgnoreCase), "$1ª fase preliminar"),
            (new Regex(@"^(\d)(?:st|nd|rd|th) Round$", RegexOptions.IgnoreCase), "$1ª fase"),
        };

        /// <summary>
        /// "Regular Season - 34" → "Rodada 34"; "Quarter-finals" → "Quartas de final".
        /// Sem round no Grupo, usa o número da rodada; sem nenhum dos dois, "–".
        /// </summary>
        public static string Rotulo(string? grupo, int rodada)
        {
            if (!string.IsNullOrWhiteSpace(grupo))
            {
                var g = grupo.Trim();
                foreach (var (padrao, rotulo) in _rounds)
                    if (padrao.IsMatch(g)) return padrao.Replace(g, rotulo);
                return g;
            }
            return rodada > 0 ? $"Rodada {rodada}" : "–";
        }

        /// <summary>
        /// Título de um grupo: "Group A" e "A" viram "Grupo A". Um round de mata-mata
        /// que caia aqui ("Quarter-finals") vira o nome da fase, sem "Grupo" na frente.
        /// </summary>
        public static string RotuloGrupo(string? nome)
        {
            var r = Rotulo(nome, 0);
            return r.Length <= 2 && r != "–" ? $"Grupo {r}" : r;
        }
    }
}
