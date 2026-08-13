using ControleFutebolWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Centraliza a lógica repetida de seleção de temporada usada pelas telas
    /// de competição (Libertadores, Sul-Americana, Champions, Copa do Mundo, Copa do Brasil…).
    /// </summary>
    public static class TemporadaHelper
    {
        /// <summary>
        /// Retorna as temporadas distintas disponíveis para a competição (mais recente primeiro)
        /// e a temporada selecionada — a informada por parâmetro ou, na ausência, a mais recente.
        /// </summary>
        public static (List<int> Disponiveis, int? Selecionada) Resolver(
            FutebolContext context, int competicaoId, int? temporada)
        {
            var disponiveis = context.Jogos
                .Where(j => j.CompeticaoId == competicaoId)
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t).ToList();

            int? selecionada = temporada
                ?? (disponiveis.Any() ? disponiveis.First() : (int?)null);

            return (disponiveis, selecionada);
        }

        /// <summary>
        /// Rótulo de exibição da temporada. As ligas europeias atravessam o ano civil
        /// — a temporada 2025 vai de agosto/2025 a maio/2026, e aparece como "2025/26";
        /// as brasileiras/sul-americanas coincidem com o ano e aparecem só como "2025".
        /// </summary>
        public static string Rotulo(int temporada, bool cruzaAno)
            => cruzaAno ? $"{temporada}/{(temporada + 1) % 100:00}" : temporada.ToString();

        /// <summary>
        /// Descobre quais competições usam o calendário europeu (temporada atravessando
        /// o ano civil) olhando as datas dos jogos — não há campo no cadastro dizendo
        /// se a liga é europeia. Um jogo isolado no ano seguinte (final adiada, por
        /// exemplo) não transforma uma competição brasileira em europeia: exige-se pelo
        /// menos 25% dos jogos de alguma temporada disputados já no ano seguinte,
        /// proporção que só o calendário europeu (metade da temporada em cada ano)
        /// alcança. Vale para a competição inteira, e não temporada a temporada: a
        /// temporada em andamento ainda não tem jogos do ano seguinte e, sozinha,
        /// pareceria de ano cheio.
        /// </summary>
        public static async Task<HashSet<int>> CompeticoesDeCalendarioEuropeuAsync(
            FutebolContext context, ICollection<int> competicaoIds)
        {
            if (competicaoIds.Count == 0) return new HashSet<int>();

            var jogosPorAno = await context.Jogos
                .Where(j => competicaoIds.Contains(j.CompeticaoId) && j.Data != null)
                .GroupBy(j => new { j.CompeticaoId, j.Temporada, Ano = j.Data!.Value.Year })
                .Select(g => new { g.Key.CompeticaoId, g.Key.Temporada, g.Key.Ano, Jogos = g.Count() })
                .ToListAsync();

            return jogosPorAno
                .GroupBy(x => new { x.CompeticaoId, x.Temporada })
                .Where(g =>
                {
                    var total = g.Sum(x => x.Jogos);
                    var noAnoSeguinte = g.Where(x => x.Ano == g.Key.Temporada + 1).Sum(x => x.Jogos);
                    return total > 0 && noAnoSeguinte >= total * 0.25;
                })
                .Select(g => g.Key.CompeticaoId)
                .ToHashSet();
        }
    }
}
