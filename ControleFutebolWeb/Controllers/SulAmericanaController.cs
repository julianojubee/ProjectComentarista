// ControleFutebolWeb/Controllers/SulAmericanaController.cs
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class SulAmericanaController : Controller
    {
        private readonly FutebolContext _context;

        // ID fixo da competição Sul-Americana no banco — ajuste se necessário
        private const int COMPETICAO_ID = 4;

        public SulAmericanaController(FutebolContext context)
        {
            _context = context;
        }
        // GET: /SulAmericana
        public IActionResult Index(int? temporada = null)
        {
            var (temporadasDisponiveis, temporadaSel) =
                TemporadaHelper.Resolver(_context, COMPETICAO_ID, temporada);
            var vm = new SulAmericanaIndexViewModel
            {
                Temporada = temporadaSel,
                TemporadasDisponiveis = temporadasDisponiveis
            };

            var grupos = MontarGrupos(temporadaSel);
            var proximosJogos = BuscarProximosJogos(temporadaSel);
            var rodadaAtual = proximosJogos.Any()
                ? proximosJogos.Min(j => j.Rodada)
                : (_context.Jogos.Any(j => j.CompeticaoId == COMPETICAO_ID)
                    ? _context.Jogos.Where(j => j.CompeticaoId == COMPETICAO_ID).Max(j => j.Rodada)
                    : 0);

            vm.Grupos = grupos;
            vm.ProximosJogos = proximosJogos;
            vm.RodadaAtual = rodadaAtual;

            return View(vm);
        }

        // ── GET: retorna o status de um jogo específico (AJAX) ───────────────
        // GET: /SulAmericana/StatusJogo/5
        [HttpGet]
        public async Task<IActionResult> StatusJogo(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Gols).ThenInclude(g => g.Jogador)
                .Include(j => j.Escalacoes).ThenInclude(e => e.Jogador)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            return Ok(new
            {
                id = jogo.Id,
                placarCasa = jogo.PlacarCasa,
                placarVisitante = jogo.PlacarVisitante,
                atualizado = jogo.Atualizado,
                gols = jogo.Gols?.Count ?? 0,
                escalacoes = jogo.Escalacoes?.Count ?? 0
            });
        }

        // ── POST: reinicia a flag Atualizado de um jogo ───────────────────────
        // POST: /SulAmericana/ResetarJogo/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetarJogo(int id)
        {
            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo == null) return NotFound();

            jogo.Atualizado = 0;

            // Remove escalações e gols para reimportar na próxima sincronização
            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id).ToListAsync();
            _context.Escalacoes.RemoveRange(escalacoes);

            var gols = await _context.Gols
                .Where(g => g.JogoId == id).ToListAsync();
            _context.Gols.RemoveRange(gols);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] =
                $"Jogo {id} resetado. Será reprocessado na próxima sincronização.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS PRIVADOS
        // ─────────────────────────────────────────────────────────────────────

        private List<GrupoViewModel> MontarGrupos(int? temporada = null)
        {
            var jogosRealizados = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID &&
                            j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue &&
                            !string.IsNullOrEmpty(j.Grupo) &&
                            (temporada == null || j.Temporada == temporada))
                .OrderBy(j => j.Data)
                .ToList();

            return jogosRealizados
                .Select(j => j.Grupo!)
                .Distinct()
                .OrderBy(g => g)
                .Select(nomeGrupo =>
                {
                    var jogosGrupo = jogosRealizados
                        .Where(j => j.Grupo == nomeGrupo).ToList();
                    return new GrupoViewModel
                    {
                        Nome = nomeGrupo,
                        Times = ClassificacaoCalculator.Calcular(jogosGrupo)
                    };
                })
                .ToList();
        }

        private List<Jogo> BuscarProximosJogos(int? temporada = null)
        {
            // Jogos sem placar (agendados) ou com placar mas recentes
            var sem = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID &&
                            (!j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue) &&
                            (temporada == null || j.Temporada == temporada))
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();

            if (sem.Any()) return sem;

            return _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID &&
                            (temporada == null || j.Temporada == temporada))
                .OrderByDescending(j => j.Data)
                .Take(10)
                .ToList();
        }

    }
}