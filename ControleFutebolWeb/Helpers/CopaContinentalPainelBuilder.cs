using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Monta a tela das copas continentais da CONMEBOL (Libertadores e
    /// Sul-Americana): classificação da fase de grupos, chaveamento das oitavas
    /// à final, as fases eliminatórias antes das oitavas (preliminares e, na
    /// Sul-Americana, os playoffs de 16-avos) e as estatísticas.
    ///
    /// O campo Grupo do jogo guarda a fase da API ("Group A", "Round of 16",
    /// "1st Round"...): só "Group X" entra na classificação. A Sul-Americana
    /// antes tratava toda fase como grupo, e o mata-mata virava tabelas.
    /// </summary>
    public static class CopaContinentalPainelBuilder
    {
        public static (LibertadoresIndexViewModel Vm, List<EstatisticaTimeViewModel> Estatisticas) Montar(
            FutebolContext context, int competicaoId, int? temporada)
        {
            var (temporadasDisponiveis, temporadaSel) =
                TemporadaHelper.Resolver(context, competicaoId, temporada);
            var vm = new LibertadoresIndexViewModel
            {
                Temporada = temporadaSel,
                TemporadasDisponiveis = temporadasDisponiveis
            };

            // Todos os jogos com fase definida na temporada
            var jogos = context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == competicaoId && !string.IsNullOrEmpty(j.Grupo)
                         && (temporadaSel == null || j.Temporada == temporadaSel))
                .OrderBy(j => j.Data)
                .ToList();

            // ── Fase de grupos ──────────────────────────────────────────────
            var jogosGrupo = jogos.Where(j => EhFaseDeGrupos(j.Grupo)).ToList();
            var jogosRealizados = jogosGrupo
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            vm.Grupos = jogosGrupo
                .Select(j => j.Grupo!)
                .Distinct()
                .OrderBy(g => g)
                .Select(nome => new GrupoViewModel
                {
                    Nome = nome,
                    Times = ClassificacaoCalculator.Calcular(jogosRealizados.Where(j => j.Grupo == nome).ToList())
                })
                .ToList();

            // Próximos jogos da fase de grupos; sem futuros, os mais recentes
            var proximos = jogosGrupo
                .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();
            if (!proximos.Any())
                proximos = jogosGrupo.OrderByDescending(j => j.Data).Take(10).ToList();
            vm.ProximosJogos = proximos;
            vm.RodadaAtual = proximos.Any()
                ? proximos.Min(j => j.Rodada)
                : (jogos.Any() ? jogos.Max(j => j.Rodada) : 0);

            // ── Chaveamento: oitavas → final ───────────────────────────────
            var jogosMataMata = jogos
                .Where(j => ChaveamentoCopaBuilder.NormalizarFase(j.Grupo) != null)
                .ToList();
            vm.Chaveamento = ChaveamentoMataMataBuilder.Construir(jogosMataMata);

            // ── Fases eliminatórias antes das oitavas ──────────────────────
            // Preliminares e 16-avos (os playoffs da Sul-Americana: 2º do grupo
            // contra 3º da Libertadores). Não cabem na árvore de oitavas, então
            // aparecem como listas de confrontos, na ordem do torneio.
            var faseDaArvore = new HashSet<string> { "R16", "QF", "SF", "F", "TP" };
            vm.FasesAnteriores = jogos
                .Where(j => !EhGrupo(j.Grupo) && !faseDaArvore.Contains(ChaveamentoCopaBuilder.NormalizarFase(j.Grupo) ?? ""))
                .GroupBy(j => j.Grupo!)
                .OrderBy(g => CompeticaoPainelBuilder.OrdemFase(g.Key) ?? double.MaxValue)
                .ThenBy(g => g.Min(j => j.Data ?? DateTime.MaxValue))
                .Select(g =>
                {
                    var nome = RodadaHelper.RotuloGrupo(g.Key);
                    return new FaseConfrontosViewModel
                    {
                        Nome = nome,
                        Confrontos = ChaveamentoMataMataBuilder.Confrontos(g.ToList(), nome)
                    };
                })
                .ToList();

            // ── Aba "Estatísticas": todos os jogos realizados (grupos + mata-mata)
            var todosJogosRealizados = context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == competicaoId && (temporadaSel == null || j.Temporada == temporadaSel)
                         && j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            var jogoIdsEstat = todosJogosRealizados.Select(j => j.Id).ToHashSet();
            var golsEstat = context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => jogoIdsEstat.Contains(g.JogoId))
                .ToList();
            var cartoesEstat = context.Cartoes.AsNoTracking()
                .Include(c => c.Jogador)
                .Where(c => jogoIdsEstat.Contains(c.JogoId))
                .ToList();
            // O dono do gol/cartão é o time pelo qual o jogador entrou em campo
            // naquele jogo, não o clube atual do cadastro.
            var escalacoesEstat = LadoJogadorHelper.EscalacoesDosEventos(
                context, todosJogosRealizados.Select(j => j.Id),
                golsEstat.Select(g => g.JogadorId).Concat(cartoesEstat.Select(c => c.JogadorId))).ToList();

            var estatisticas = EstatisticaTimeCalculator.Calcular(todosJogosRealizados, golsEstat, cartoesEstat, escalacoesEstat,
                DesarmesPorTimeHelper.Carregar(context, todosJogosRealizados));

            return (vm, estatisticas);
        }

        // "Group A".."Group H" (não "Group Stage - 1", que é rodada e não grupo)
        private static bool EhFaseDeGrupos(string? grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo)) return false;
            return grupo.StartsWith("Group ", StringComparison.OrdinalIgnoreCase) &&
                   !grupo.Contains("Stage", StringComparison.OrdinalIgnoreCase);
        }

        // Qualquer coisa da fase de grupos, inclusive "Group Stage - N"
        private static bool EhGrupo(string? grupo) =>
            !string.IsNullOrWhiteSpace(grupo) && grupo.StartsWith("Group", StringComparison.OrdinalIgnoreCase);
    }
}
