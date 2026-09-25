using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Helpers.Campeonatos
{
    /// <summary>Uma linha da artilharia/disciplina do campeonato.</summary>
    public sealed class EstatisticaAtleta
    {
        public string Nome { get; init; } = "";
        public int ParticipanteId { get; init; }
        public int? JogadorId { get; init; }
        public int? JogadorProprioId { get; init; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int Amarelos { get; set; }
        public int Vermelhos { get; set; }
    }

    /// <summary>
    /// Artilharia, garçons e disciplina a partir da súmula (EventoPartidaCampeonato).
    /// O atleta é identificado pelo cadastro (real ou próprio) e, quando o evento foi
    /// lançado só com nome digitado, pelo nome dentro do participante — o mesmo "Zé"
    /// em dois times são duas pessoas. Gol contra não conta para ninguém.
    /// </summary>
    public static class EstatisticasCampeonatoHelper
    {
        public static List<EstatisticaAtleta> PorAtleta(IEnumerable<EventoPartidaCampeonato> eventos)
        {
            var linhas = new Dictionary<string, EstatisticaAtleta>();

            foreach (var e in eventos)
            {
                if (e.Tipo == TipoEventoPartida.GolContra) continue;

                var chave = e.JogadorId != null ? $"r{e.JogadorId}"
                    : e.JogadorProprioId != null ? $"p{e.JogadorProprioId}"
                    : $"n{e.ParticipanteId}:{e.NomeSnapshot.Trim().ToLowerInvariant()}";

                if (!linhas.TryGetValue(chave, out var linha))
                {
                    linha = new EstatisticaAtleta
                    {
                        Nome = e.NomeSnapshot,
                        ParticipanteId = e.ParticipanteId,
                        JogadorId = e.JogadorId,
                        JogadorProprioId = e.JogadorProprioId
                    };
                    linhas[chave] = linha;
                }

                switch (e.Tipo)
                {
                    case TipoEventoPartida.Gol: linha.Gols++; break;
                    case TipoEventoPartida.Assistencia: linha.Assistencias++; break;
                    case TipoEventoPartida.CartaoAmarelo: linha.Amarelos++; break;
                    case TipoEventoPartida.CartaoVermelho: linha.Vermelhos++; break;
                }
            }

            return linhas.Values
                .OrderByDescending(l => l.Gols)
                .ThenByDescending(l => l.Assistencias)
                .ThenBy(l => l.Nome, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
