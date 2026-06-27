using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class ChampionsLeagueController : Controller
    {
        private readonly FutebolContext _context;

        private const int COMPETICAO_ID = 6;

        public ChampionsLeagueController(FutebolContext context)
        {
            _context = context;
        }

        public IActionResult Index(int? temporada = null)
        {
            var (temporadasDisponiveis, temporadaSel) =
                TemporadaHelper.Resolver(_context, COMPETICAO_ID, temporada);
            var vm = new ChampionsLeagueIndexViewModel
            {
                Temporada = temporadaSel,
                TemporadasDisponiveis = temporadasDisponiveis
            };

            var todosJogos = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID
                         && (temporadaSel == null || j.Temporada == temporadaSel))
                .OrderBy(j => j.Data)
                .ToList();

            // Fase de liga: grupo vazio, ou nomes que indicam fase de liga
            var jogosLiga = todosJogos
                .Where(j => EhFaseDeLiga(j.Grupo))
                .ToList();

            // Mata-mata: grupo com nome de rodada eliminatória
            var jogosMata = todosJogos
                .Where(j => !EhFaseDeLiga(j.Grupo))
                .ToList();

            // Ranking único da fase de liga (baseado nos jogos realizados)
            var jogosLigaRealizados = jogosLiga
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            var ranking = ClassificacaoCalculator.Calcular(jogosLigaRealizados);

            // Sidebar: próximos jogos sem placar; se nenhum, usa os mais recentes
            var proximosJogos = todosJogos
                .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();

            if (!proximosJogos.Any())
            {
                proximosJogos = todosJogos
                    .OrderByDescending(j => j.Data)
                    .Take(10)
                    .ToList();
            }

            // Fases de mata-mata com resultados
            var fasesMata = jogosMata
                .GroupBy(j => j.Grupo ?? "")
                .OrderBy(g => OrdemFase(g.Key))
                .Select(g => new GrupoViewModel
                {
                    Nome = g.Key,
                    Times = new List<Classificacao>() // mata-mata não usa classificação
                })
                .ToList();

            vm.Ranking = ranking;
            vm.ProximosJogos = proximosJogos;
            vm.JogosMata = jogosMata;
            vm.FasesMata = fasesMata.Select(f => f.Nome).Distinct().ToList();
            vm.TotalJogos = todosJogos.Count;
            vm.JogosRealizados = todosJogos.Count(j => j.PlacarCasa.HasValue);

            return View(vm);
        }

        private static bool EhFaseDeLiga(string? grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo)) return true;
            var g = grupo.ToLower();
            // "UEFA Champions League" ou qualquer variante de fase de liga
            if (g.Contains("league stage") || g.Contains("uefa champions league")) return true;
            // Fases eliminatórias têm nomes específicos
            if (g.Contains("round") || g.Contains("quarter") || g.Contains("semi") ||
                g.Contains("final") || g.Contains("playoff") || g.Contains("oitavas") ||
                g.Contains("quartas") || g.Contains("qualifying")) return false;
            // Por padrão trata como fase de liga
            return true;
        }

        private static int OrdemFase(string fase) => fase.ToLower() switch
        {
            var f when f.Contains("round of 16") || f.Contains("oitavas") => 1,
            var f when f.Contains("quarter") || f.Contains("quartas") => 2,
            var f when f.Contains("semi") => 3,
            var f when f.Contains("final") => 4,
            _ => 0
        };

    }
}
