using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Helpers.Campeonatos
{
    /// <summary>O que o card da lista de campeonatos mostra sobre o andamento.</summary>
    public sealed record ResumoCampeonato(
        int PartidasJogadas,
        int PartidasTotal,
        string? Etapa,
        CampeonatoParticipante? Lider,
        string? ProximoPasso);

    /// <summary>
    /// Resumo de um campeonato para a lista de "Meus campeonatos": quantas partidas já
    /// têm placar, em que etapa ele está, quem lidera (só em pontos corridos, onde
    /// existe um líder único) e, no rascunho, o que falta para começar.
    /// </summary>
    public static class ResumoCampeonatoHelper
    {
        /// <summary>Mínimo de participantes para gerar as partidas (mesma regra do serviço).</summary>
        public const int MinimoParticipantes = 2;

        public static ResumoCampeonato Montar(Campeonato campeonato)
        {
            var partidas = campeonato.Partidas.ToList();
            var jogadas = partidas.Count(Jogada);

            if (campeonato.Status == StatusCampeonato.Rascunho || partidas.Count == 0)
            {
                var qtd = campeonato.Participantes.Count;
                var passo = qtd < MinimoParticipantes
                    ? $"Adicione os participantes ({qtd} de no mínimo {MinimoParticipantes})"
                    : "Pronto para gerar as partidas";
                return new(jogadas, partidas.Count, null, null, passo);
            }

            // Fase atual: a primeira (pela ordem) que ainda tem partida sem placar;
            // com tudo jogado, a última que tem partidas.
            var fases = campeonato.Fases.OrderBy(f => f.Ordem).ToList();
            var fase = fases.FirstOrDefault(f => partidas.Any(p => p.FaseId == f.Id && !Jogada(p)))
                       ?? fases.LastOrDefault(f => partidas.Any(p => p.FaseId == f.Id));

            string? etapa = null;
            CampeonatoParticipante? lider = null;

            if (fase != null)
            {
                var daFase = partidas.Where(p => p.FaseId == fase.Id).ToList();
                var pendente = daFase.Where(p => !Jogada(p)).OrderBy(p => p.Rodada).FirstOrDefault();

                switch (fase.Tipo)
                {
                    case "MATA_MATA":
                        // Etapa = nome do grupo da partida pendente ("Semifinal", "Final"...)
                        etapa = (pendente ?? daFase.OrderBy(p => p.Rodada).Last()).Grupo;
                        break;
                    case "GRUPOS":
                        etapa = fases.Count > 1 ? fase.Nome : null;
                        if (pendente != null) etapa = Juntar(etapa, $"rodada {pendente.Rodada}");
                        break;
                    default:
                        if (pendente != null) etapa = $"Rodada {pendente.Rodada}";
                        if (daFase.Any(Jogada))
                        {
                            var conv = CampeonatoPainelAdapter.Converter(campeonato);
                            var tabela = CampeonatoPainelAdapter.MontarFase(conv, fase).Classificacao;
                            var liderId = tabela.FirstOrDefault()?.TimeId;
                            lider = campeonato.Participantes.FirstOrDefault(p => p.Id == liderId);
                        }
                        break;
                }
            }

            if (etapa != null) etapa = char.ToUpper(etapa[0]) + etapa[1..];
            return new(jogadas, partidas.Count, etapa, lider, null);
        }

        private static bool Jogada(PartidaCampeonato p) => p.PlacarCasa.HasValue && p.PlacarVisitante.HasValue;

        private static string Juntar(string? a, string b) => string.IsNullOrEmpty(a) ? b : $"{a}, {b}";
    }
}
