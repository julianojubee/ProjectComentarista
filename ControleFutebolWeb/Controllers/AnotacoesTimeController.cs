using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    [Authorize]
    public class AnotacoesTimeController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnotacoesTimeController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /AnotacoesTime?timeId=1&q=sondagem
        public async Task<IActionResult> Index(int timeId, string? q)
        {
            var time = await _context.Times.FindAsync(timeId);
            if (time == null) return NotFound();

            var uid = _userManager.GetUserId(User);
            var query = _context.AnotacoesTime
                .Where(a => a.TimeId == timeId && a.UsuarioId == uid)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a =>
                    a.Titulo.ToLower().Contains(q.ToLower()) ||
                    a.Conteudo.ToLower().Contains(q.ToLower()) ||
                    (a.Categoria != null && a.Categoria.ToLower().Contains(q.ToLower())));

            var anotacoes = await query.OrderByDescending(a => a.DtInc).ToListAsync();

            // Contagem por categoria (sempre do total do time, não afetada pela busca "q") —
            // usada no card "Resumo" da barra lateral.
            var contagemCategorias = await _context.AnotacoesTime
                .Where(a => a.TimeId == timeId && a.UsuarioId == uid)
                .GroupBy(a => a.Categoria ?? "")
                .Select(g => new { Categoria = g.Key, Qtd = g.Count() })
                .ToDictionaryAsync(x => x.Categoria, x => x.Qtd);

            // Observações sobre este time feitas na tela de analisar (tags Mandante/Visitante,
            // do próprio usuário). Mantemos as do lado em que o time jogou em cada partida.
            var tagsDoTime = await _context.ObservacoesJogoTag
                .Where(o => o.UsuarioId == uid
                         && (o.Tipo == "MANDANTE" || o.Tipo == "VISITANTE")
                         && (o.Jogo.TimeCasaId == timeId || o.Jogo.TimeVisitanteId == timeId))
                .Include(o => o.Jogo).ThenInclude(g => g.TimeCasa)
                .Include(o => o.Jogo).ThenInclude(g => g.TimeVisitante)
                .Include(o => o.Jogo).ThenInclude(g => g.Competicao)
                .OrderBy(o => o.Ordem)
                .ToListAsync();

            var observacoesJogos = tagsDoTime
                .Where(o =>
                {
                    bool ehCasa = o.Jogo.TimeCasaId == timeId;
                    var tagEsperada = ehCasa ? "MANDANTE" : "VISITANTE";
                    if (o.Tipo != tagEsperada) return false;
                    return string.IsNullOrWhiteSpace(q) || o.Texto.Contains(q, StringComparison.OrdinalIgnoreCase);
                })
                .GroupBy(o => o.JogoId)
                .Select(g => new ObservacaoJogoTimeViewModel
                {
                    Jogo = g.First().Jogo,
                    TimeEhCasa = g.First().Jogo.TimeCasaId == timeId,
                    Observacoes = g.Select(o => o.Texto).ToList()
                })
                .OrderByDescending(o => o.Jogo.Data ?? DateTime.MinValue)
                .ToList();

            ViewBag.Time = time;
            ViewBag.Q    = q;
            ViewBag.ObservacoesJogos = observacoesJogos;
            ViewBag.ContagemCategorias = contagemCategorias;
            ViewBag.NovaAnotacao = new AnotacaoTime { TimeId = timeId };
            return View(anotacoes);
        }

        // GET: /AnotacoesTime/Nova?timeId=1
        [HttpGet]
        public async Task<IActionResult> Nova(int timeId)
        {
            var time = await _context.Times.FindAsync(timeId);
            if (time == null) return NotFound();

            ViewBag.Time = time;
            return View(new AnotacaoTime { TimeId = timeId });
        }

        // POST: /AnotacoesTime/Nova
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nova(AnotacaoTime model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Time = await _context.Times.FindAsync(model.TimeId);
                return View(model);
            }

            model.DtInc    = DateTime.UtcNow;
            model.UsuarioId = _userManager.GetUserId(User);
            _context.AnotacoesTime.Add(model);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "✅ Anotação salva.";
            return RedirectToAction(nameof(Index), new { timeId = model.TimeId });
        }

        // GET: /AnotacoesTime/Editar/5
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesTime
                .Include(a => a.Time)
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            ViewBag.Time = anotacao.Time;
            return View(anotacao);
        }

        // POST: /AnotacoesTime/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, AnotacaoTime model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Time = await _context.Times.FindAsync(model.TimeId);
                return View(model);
            }

            var uid = _userManager.GetUserId(User);
            var existing = await _context.AnotacoesTime.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (existing == null) return NotFound();

            existing.Titulo    = model.Titulo;
            existing.Conteudo  = model.Conteudo;
            existing.Categoria = model.Categoria;
            existing.DtAlt     = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "✅ Anotação atualizada.";
            return RedirectToAction(nameof(Index), new { timeId = existing.TimeId });
        }

        // POST: /AnotacoesTime/Excluir/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesTime.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            var timeId = anotacao.TimeId;
            _context.AnotacoesTime.Remove(anotacao);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "🗑️ Anotação excluída.";
            return RedirectToAction(nameof(Index), new { timeId });
        }
    }
}
