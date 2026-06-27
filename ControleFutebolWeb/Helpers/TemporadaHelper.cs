using ControleFutebolWeb.Data;

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
    }
}
