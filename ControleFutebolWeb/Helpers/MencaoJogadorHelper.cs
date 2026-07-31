using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Reconhece menções "@Nome" escritas em textos livres (observações de jogo,
    /// anotações de time), comparando com uma lista de jogadores candidatos —
    /// o mesmo universo oferecido no dropdown de "@" do front (wwwroot/js/mencao-jogador.js).
    /// </summary>
    public static class MencaoJogadorHelper
    {
        /// <summary>
        /// Ids dos jogadores citados no texto. Compara os nomes mais longos primeiro
        /// para não confundir "@João" com "@João Silva".
        /// </summary>
        public static List<int> Extrair(string? texto, IEnumerable<Jogador> disponiveis)
        {
            var encontrados = new List<int>();
            if (string.IsNullOrWhiteSpace(texto)) return encontrados;

            foreach (var jogador in disponiveis
                         .Where(j => !string.IsNullOrWhiteSpace(j.NomeExibicao))
                         .OrderByDescending(j => j.NomeExibicao.Length))
            {
                var alvo = "@" + jogador.NomeExibicao;
                if (texto.Contains(alvo, StringComparison.OrdinalIgnoreCase) && !encontrados.Contains(jogador.Id))
                    encontrados.Add(jogador.Id);
            }

            return encontrados;
        }
    }
}
