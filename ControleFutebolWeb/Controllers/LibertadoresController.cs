using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class LibertadoresController : Controller
    {
        private readonly FutebolContext _context;

        public LibertadoresController(FutebolContext context)
        {
            _context = context;
        }

        public IActionResult Index(int? temporada = null)
        {
            var (temporadasDisponiveis, temporadaSel) =
                TemporadaHelper.Resolver(_context, 2, temporada);
            var vm = new LibertadoresIndexViewModel
            {
                Temporada = temporadaSel,
                TemporadasDisponiveis = temporadasDisponiveis
            };

            // Busca jogos da Libertadores (CompeticaoId = 2) que tenham grupo definido
            var jogos = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == 2 && !string.IsNullOrEmpty(j.Grupo)
                         && (temporadaSel == null || j.Temporada == temporadaSel))
                .OrderBy(j => j.Data)
                .ToList();

            // Apenas jogos da fase de grupos (exclui Qualification, Playoff, etc.)
            var jogosGrupo = jogos
                .Where(j => EhFaseDeGrupos(j.Grupo))
                .ToList();

            // Jogos já realizados: tem placar
            var jogosRealizados = jogosGrupo
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            // Todos os grupos (realizados + agendados), apenas fase de grupos
            var todosGrupos = jogosGrupo
                .Select(j => j.Grupo!)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            var grupos = new List<GrupoViewModel>();

            foreach (var nomeGrupo in todosGrupos)
            {
                var jogosDoGrupo = jogosRealizados
                    .Where(j => j.Grupo == nomeGrupo)
                    .ToList();

                var classificacao = ClassificacaoCalculator.Calcular(jogosDoGrupo);

                grupos.Add(new GrupoViewModel
                {
                    Nome = nomeGrupo,
                    Times = classificacao
                });
            }

            // Próximos jogos da fase de grupos: sem placar, ordenados por data
            var proximosJogos = jogosGrupo
                .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();

            // Se não houver futuros, pega os mais recentes realizados
            if (!proximosJogos.Any())
            {
                proximosJogos = jogosGrupo
                    .OrderByDescending(j => j.Data)
                    .Take(10)
                    .ToList();
            }

            // Rodada atual
            var rodadaAtual = proximosJogos.Any()
                ? proximosJogos.Min(j => j.Rodada)
                : (jogos.Any() ? jogos.Max(j => j.Rodada) : 0);

            // ── Chaveamento (mata-mata) ─────────────────────────────────────────
            // Jogos das fases finais já importados (Round of 16, Quarter-finals, etc.).
            var jogosMataMata = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == 2 && !string.IsNullOrEmpty(j.Grupo)
                         && (temporadaSel == null || j.Temporada == temporadaSel))
                .ToList()
                .Where(j => ChaveamentoCopaBuilder.NormalizarFase(j.Grupo) != null)
                .ToList();

            var chaveamento = ChaveamentoMataMataBuilder.Construir(jogosMataMata);

            vm.Grupos = grupos;
            vm.ProximosJogos = proximosJogos;
            vm.RodadaAtual = rodadaAtual;
            vm.Chaveamento = chaveamento;

            return View(vm);
        }

        private static bool EhFaseDeGrupos(string? grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo)) return false;
            return grupo.StartsWith("Group ", StringComparison.OrdinalIgnoreCase) &&
                   !grupo.Contains("Stage", StringComparison.OrdinalIgnoreCase);
        }

    }
}
