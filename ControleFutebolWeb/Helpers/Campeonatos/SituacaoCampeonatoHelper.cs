using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Helpers.Campeonatos
{
    /// <summary>Se o campeonato já está decidido e, quando dá para dizer, quem ganhou.</summary>
    public readonly record struct SituacaoCampeonato(bool Decidido, int? CampeaoParticipanteId);

    /// <summary>
    /// Decide o encerramento pela ÚLTIMA fase:
    ///   - mata-mata: a final tem vencedor (placar, agregado ou pênaltis) — e o jogo
    ///     de 3º lugar também, quando existe;
    ///   - pontos corridos: todas as partidas têm placar, e o campeão é o líder da
    ///     tabela pelos critérios de desempate do campeonato;
    ///   - grupos como última fase: todas com placar, mas sem campeão único.
    /// O campeão não é gravado: sai sempre das partidas, então corrigir um placar
    /// corrige o campeão (ou reabre o campeonato) sem nada para sincronizar.
    /// </summary>
    public static class SituacaoCampeonatoHelper
    {
        public static SituacaoCampeonato Avaliar(Campeonato campeonato)
        {
            var ultima = campeonato.Fases.OrderBy(f => f.Ordem).LastOrDefault();
            var tipo = ultima?.Tipo ?? campeonato.Tipo;
            var partidas = campeonato.Partidas
                .Where(p => ultima == null || p.FaseId == ultima.Id)
                .ToList();

            if (partidas.Count == 0) return new(false, null);

            if (tipo == "MATA_MATA")
            {
                // O 3º lugar, quando existe, precisa estar decidido também.
                var terceiro = partidas.Where(p => p.Grupo == GeradorPartidasCampeonato.NomeTerceiroLugar).ToList();
                if (terceiro.Count > 0 && GeradorPartidasCampeonato.Vencedor(terceiro) == null) return new(false, null);

                // A final é a etapa da última rodada, com um confronto só.
                var final = partidas
                    .Where(p => p.Grupo != GeradorPartidasCampeonato.NomeTerceiroLugar)
                    .GroupBy(p => p.Grupo ?? "")
                    .OrderBy(g => g.Min(p => p.Rodada))
                    .Last()
                    .ToList();
                if (final.Select(p => p.ChaveOrdem).Distinct().Count() != 1) return new(false, null);

                var campeao = GeradorPartidasCampeonato.Vencedor(final);
                return new(campeao != null, campeao);
            }

            if (partidas.Any(p => p.PlacarCasa == null || p.PlacarVisitante == null))
                return new(false, null);

            if (tipo == "PONTOS_CORRIDOS" && ultima != null)
            {
                var fase = CampeonatoPainelAdapter.MontarFase(CampeonatoPainelAdapter.Converter(campeonato), ultima);
                return new(true, fase.Classificacao.FirstOrDefault()?.TimeId);
            }

            if (tipo == "PONTOS_CORRIDOS")
            {
                var painel = CampeonatoPainelAdapter.MontarPainel(campeonato);
                return new(true, painel.Classificacao.FirstOrDefault()?.TimeId);
            }

            return new(true, null);
        }

        /// <summary>
        /// Acerta Status/EncerradoEm conforme a situação. Devolve true se mudou algo.
        /// Rascunho (partidas ainda não geradas) nunca é tocado.
        /// </summary>
        public static bool AplicarStatus(Campeonato campeonato, DateTime agoraUtc)
        {
            if (campeonato.Status == StatusCampeonato.Rascunho) return false;

            var decidido = Avaliar(campeonato).Decidido;
            if (decidido && campeonato.Status != StatusCampeonato.Encerrado)
            {
                campeonato.Status = StatusCampeonato.Encerrado;
                campeonato.EncerradoEm = agoraUtc;
                return true;
            }
            if (!decidido && campeonato.Status == StatusCampeonato.Encerrado)
            {
                campeonato.Status = StatusCampeonato.EmAndamento;
                campeonato.EncerradoEm = null;
                return true;
            }
            return false;
        }
    }
}
