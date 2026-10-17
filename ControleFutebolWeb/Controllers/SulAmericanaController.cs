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
        // Mesma tela da Libertadores (grupos, mata-mata e estatísticas), com nome,
        // logo, cores e regras de classificação da Sul-Americana. A tela antiga
        // tratava toda fase como grupo: oitavas, quartas e 16-avos viravam tabelas.
        public IActionResult Index(int? temporada = null)
        {
            var (vm, estatisticas) = CopaContinentalPainelBuilder.Montar(_context, COMPETICAO_ID, temporada);
            ViewBag.EstatisticasTimes = estatisticas;
            ViewData["CopaId"] = COMPETICAO_ID;
            ViewData["CopaNome"] = "Sul-Americana";
            ViewData["CopaTema"] = "sul";
            ViewData["CopaLogo"] = "~/images/background/sudamericana.png";
            ViewData["CopaTagline"] = "A Grande Conquista";
            // Fase de grupos: 1º vai direto às oitavas, 2º joga os playoffs
            // (16-avos) contra um 3º colocado da Libertadores
            ViewData["CopaVagasOitavas"] = 1;
            ViewData["CopaLegendaRepescagem"] = "Vai para os playoffs (16-avos)";
            return View("~/Views/Libertadores/Index.cshtml", vm);
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
    }
}
