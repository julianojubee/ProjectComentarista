using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
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

            ViewBag.Time = time;
            ViewBag.Q    = q;
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
