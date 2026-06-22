using System.Collections.Generic;

namespace ControleFutebolWeb.Helpers
{
    public static class CompeticaoHelper
    {
        // Retorna o logo da competição (usa URL do banco se disponível)
        public static string GetLogoCompeticao(int competicaoId, string? logoUrlBanco = null)
        {
            if (!string.IsNullOrWhiteSpace(logoUrlBanco))
                return logoUrlBanco;

            return competicaoId switch
            {
                1 => "/images/background/campeonatobrasileiro.png",
                2 => "/images/background/libertadores.png",
                3 => "/images/background/copadobrasil.png",
                4 => "/images/background/sudamericana.png",
                5 => "/images/background/premierleague.png",
                _ => "/images/background/default.png"
            };
        }

        // Mapa competicaoId → nome (para tooltip, legendas, etc.)
        private static readonly Dictionary<int, string> compMap = new()
        {
            { 1, "Campeonato Brasileiro" },
            { 2, "Libertadores" },
            { 3, "Copa do Brasil" },
            { 4, "Sul-Americana" },
            { 5, "Premier League" }
        };

        // Retorna o nome da competição
        public static string GetNomeCompeticao(int competicaoId)
        {
            return compMap.TryGetValue(competicaoId, out var nome)
                ? nome
                : "Competição desconhecida";
        }
    }
}
