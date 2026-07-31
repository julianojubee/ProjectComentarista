using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Calcula a classificação round-robin (pontos corridos) a partir de uma lista
    /// de jogos realizados. Usado pelas fases de grupo/liga de competições simples
    /// (Libertadores, Sul-Americana, Champions League).
    ///
    /// NÃO cobre os critérios de desempate FIFA da Copa do Mundo (confronto direto,
    /// fair play) — essa lógica permanece em <c>CopaDoMundoController</c>.
    /// </summary>
    public static class ClassificacaoCalculator
    {
        /// <summary>
        /// Acumula pontos/gols e ordena por Pontos → Saldo → Gols pró → Vitórias.
        /// </summary>
        /// <param name="timesParticipantes">
        /// Opcional: times que devem aparecer zerados mesmo sem jogo realizado. Usado pelo
        /// simulador, onde a tabela precisa listar o campeonato inteiro desde a rodada 1.
        /// </param>
        public static List<Classificacao> Calcular(List<Jogo> jogos, IEnumerable<Time>? timesParticipantes = null)
        {
            var tabela = new Dictionary<int, Classificacao>();

            if (timesParticipantes != null)
            {
                foreach (var time in timesParticipantes)
                {
                    if (time != null && !tabela.ContainsKey(time.Id))
                        tabela[time.Id] = new Classificacao { TimeId = time.Id, Time = time };
                }
            }

            foreach (var jogo in jogos)
            {
                if (jogo.TimeCasa == null || jogo.TimeVisitante == null) continue;
                if (!jogo.PlacarCasa.HasValue || !jogo.PlacarVisitante.HasValue) continue;

                if (!tabela.ContainsKey(jogo.TimeCasaId))
                    tabela[jogo.TimeCasaId] = new Classificacao { TimeId = jogo.TimeCasaId, Time = jogo.TimeCasa };
                if (!tabela.ContainsKey(jogo.TimeVisitanteId))
                    tabela[jogo.TimeVisitanteId] = new Classificacao { TimeId = jogo.TimeVisitanteId, Time = jogo.TimeVisitante };

                var casa = tabela[jogo.TimeCasaId];
                var vis  = tabela[jogo.TimeVisitanteId];

                casa.Jogos++; vis.Jogos++;
                casa.GolsPro    += jogo.PlacarCasa.Value;
                casa.GolsContra += jogo.PlacarVisitante.Value;
                vis.GolsPro     += jogo.PlacarVisitante.Value;
                vis.GolsContra  += jogo.PlacarCasa.Value;

                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                { casa.Vitorias++; casa.Pontos += 3; vis.Derrotas++; }
                else if (jogo.PlacarCasa < jogo.PlacarVisitante)
                { vis.Vitorias++; vis.Pontos += 3; casa.Derrotas++; }
                else
                { casa.Empates++; vis.Empates++; casa.Pontos++; vis.Pontos++; }
            }

            var lista = tabela.Values
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.GolsPro - t.GolsContra)
                .ThenByDescending(t => t.GolsPro)
                .ThenByDescending(t => t.Vitorias)
                .ToList();

            for (int i = 0; i < lista.Count; i++)
                lista[i].Posicao = i + 1;

            return lista;
        }
    }
}
