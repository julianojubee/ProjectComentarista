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

            // Fases eliminatórias com confrontos ida/volta (mesmo modelo da Copa do Brasil)
            var fasesMataMata = jogosMata
                .GroupBy(j => j.Grupo ?? "")
                .OrderBy(g => OrdemFase(g.Key))
                .Select(g => new FaseMataUclViewModel
                {
                    GrupoOriginal = g.Key,
                    Nome = NomeFase(g.Key),
                    JogoUnico = EhJogoUnico(g.Key),
                    Confrontos = MontarConfrontos(g.ToList(), NomeFase(g.Key))
                })
                .ToList();

            vm.Ranking = ranking;
            vm.ProximosJogos = proximosJogos;
            vm.FasesMataMata = fasesMataMata;
            vm.TotalJogos = todosJogos.Count;
            vm.JogosRealizados = todosJogos.Count(j => j.PlacarCasa.HasValue);

            return View(vm);
        }

        private static bool EhFaseDeLiga(string? grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo)) return true;
            // Normaliza hífens: a API grava "Play-offs", "Quarter-finals" etc.
            var g = grupo.ToLower().Replace("-", " ");
            // "UEFA Champions League" ou qualquer variante de fase de liga
            if (g.Contains("league stage") || g.Contains("uefa champions league")) return true;
            // Fases eliminatórias têm nomes específicos
            if (g.Contains("round") || g.Contains("quarter") || g.Contains("semi") ||
                g.Contains("final") || g.Contains("play off") || g.Contains("playoff") ||
                g.Contains("knockout") || g.Contains("oitavas") || g.Contains("quartas") ||
                g.Contains("qualifying")) return false;
            // Por padrão trata como fase de liga
            return true;
        }

        // Ordem cronológica das fases eliminatórias (pré-eliminatórias → final)
        private static int OrdemFase(string fase)
        {
            var f = fase.ToLower().Replace("-", " ");
            if (f.Contains("1st qualifying")) return 1;
            if (f.Contains("2nd qualifying")) return 2;
            if (f.Contains("3rd qualifying")) return 3;
            if (f.Contains("qualifying")) return 4;
            if (f.Contains("play off") || f.Contains("playoff") || f.Contains("knockout")) return 5;
            if (f.Contains("round of 32")) return 6;
            if (f.Contains("round of 16") || f.Contains("oitavas")) return 7;
            if (f.Contains("quarter") || f.Contains("quartas")) return 8;
            if (f.Contains("semi")) return 9;
            if (f.Contains("final")) return 10;
            return 0;
        }

        private static string NomeFase(string fase)
        {
            var f = fase.ToLower().Replace("-", " ");
            if (f.Contains("1st qualifying")) return "1ª Pré-Eliminatória";
            if (f.Contains("2nd qualifying")) return "2ª Pré-Eliminatória";
            if (f.Contains("3rd qualifying")) return "3ª Pré-Eliminatória";
            if (f.Contains("qualifying")) return "Pré-Eliminatória";
            if (f.Contains("play off") || f.Contains("playoff") || f.Contains("knockout")) return "Playoff do Mata-Mata";
            if (f.Contains("round of 32")) return "16 Avos de Final";
            if (f.Contains("round of 16") || f.Contains("oitavas")) return "Oitavas de Final";
            if (f.Contains("quarter") || f.Contains("quartas")) return "Quartas de Final";
            if (f.Contains("semi")) return "Semifinal";
            if (f.Contains("final")) return "Final";
            return fase;
        }

        // A Final da Champions é em jogo único; as demais fases são ida e volta.
        private static bool EhJogoUnico(string fase)
        {
            var f = fase.ToLower().Replace("-", " ");
            return f.Contains("final") && !f.Contains("semi") && !f.Contains("quarter");
        }

        // ── Monta pares ida/volta dentro de uma fase (mesma lógica da Copa do Brasil) ──
        private static List<ConfrontoMataMata> MontarConfrontos(List<Jogo> jogos, string faseName)
        {
            var confrontos = new List<ConfrontoMataMata>();
            var usados = new HashSet<int>();

            // Ordena por data para que a ida venha antes da volta
            var ordenados = jogos.OrderBy(j => j.Data).ToList();

            foreach (var jogo in ordenados)
            {
                if (usados.Contains(jogo.Id)) continue;

                // Procura o jogo inverso (volta): times trocados, ainda não usado
                var volta = ordenados.FirstOrDefault(j =>
                    !usados.Contains(j.Id) &&
                    j.Id != jogo.Id &&
                    j.TimeCasaId == jogo.TimeVisitanteId &&
                    j.TimeVisitanteId == jogo.TimeCasaId);

                confrontos.Add(new ConfrontoMataMata
                {
                    TimeA = jogo.TimeCasa!,
                    TimeB = jogo.TimeVisitante!,
                    JogoIda = jogo,
                    JogoVolta = volta,
                    FaseName = faseName,
                });

                usados.Add(jogo.Id);
                if (volta != null) usados.Add(volta.Id);
            }

            return confrontos;
        }
    }
}
