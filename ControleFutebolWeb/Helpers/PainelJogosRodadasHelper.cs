using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Monta o painel de jogos por rodada (_PainelJogosRodadas): agrupa os jogos pelo
    /// rótulo da rodada, na ordem em que cada uma começa, e escolhe a que o painel
    /// abre — a do próximo jogo sem placar ou, com a temporada encerrada, a última.
    /// </summary>
    public static class PainelJogosRodadasHelper
    {
        public static PainelJogosRodadasViewModel Montar(IEnumerable<Jogo> jogos, Func<Jogo, string>? rotulo = null)
        {
            rotulo ??= RotuloPadrao;

            var rodadas = jogos
                .GroupBy(rotulo)
                .Select(g => new RodadaJogosViewModel { Rotulo = g.Key, Jogos = g.OrderBy(j => j.Data).ToList() })
                .OrderBy(r => r.Jogos.Min(j => j.Data ?? DateTime.MaxValue))
                .ToList();

            return new PainelJogosRodadasViewModel { Rodadas = rodadas, Atual = RodadaEmAndamento(rodadas, DateTime.UtcNow) };
        }

        private static readonly System.Text.RegularExpressions.Regex Grupo =
            new(@"^(?:Group|Grupo)\s+[A-Z0-9]{1,2}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Rótulo da rodada no painel. Jogo de grupo ("Group A") entra na rodada da
        /// fase de grupos, junto com os dos outros grupos ("Fase de grupos, rodada 2"):
        /// um painel por grupo teria oito ou doze "rodadas" de dois jogos cada. O
        /// resto usa o rótulo de sempre ("Rodada 7", "Oitavas de final").
        /// </summary>
        public static string RotuloPadrao(Jogo j)
        {
            if (!string.IsNullOrWhiteSpace(j.Grupo) && Grupo.IsMatch(j.Grupo.Trim()))
                return j.Rodada > 0 ? $"Fase de grupos, rodada {j.Rodada}" : "Fase de grupos";
            return RodadaHelper.Rotulo(j.Grupo, j.Rodada);
        }

        // Abre na rodada do próximo jogo sem placar a partir de hoje (as datas são
        // UTC no banco; a margem de 12h mantém na tela um jogo que está rolando).
        // "Primeira rodada com jogo sem placar" não serve: um jogo antigo que ficou
        // sem resultado no banco prendia a Libertadores na fase de grupos com a
        // competição já nas quartas. Sem jogo pela frente, abre na do último resultado.
        public static int RodadaEmAndamento(List<RodadaJogosViewModel> rodadas, DateTime agoraUtc)
        {
            if (rodadas.Count == 0) return 0;
            static bool SemPlacar(Jogo j) => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue;

            var desde = agoraUtc.AddHours(-12);
            var proximo = rodadas
                .SelectMany((r, i) => r.Jogos.Where(j => SemPlacar(j) && j.Data >= desde).Select(j => (i, j.Data)))
                .OrderBy(x => x.Data)
                .FirstOrDefault();
            if (proximo.Data != null) return proximo.i;

            var ultimo = rodadas
                .SelectMany((r, i) => r.Jogos.Where(j => !SemPlacar(j)).Select(j => (i, j.Data)))
                .OrderByDescending(x => x.Data)
                .FirstOrDefault();
            return ultimo.Data != null ? ultimo.i : rodadas.Count - 1;
        }
    }
}
