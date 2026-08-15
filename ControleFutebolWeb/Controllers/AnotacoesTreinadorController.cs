using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Anotações sobre um treinador — espelha /AnotacoesJogador, só que o dono é o técnico.
    // Diferente da tela do jogador, aqui não há seções "puxadas" de outros lugares: o
    // sistema não tem menção "@Nome" nem tag de observação de jogo para treinador.
    [Authorize]
    public class AnotacoesTreinadorController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnotacoesTreinadorController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private Task<Treinador?> TreinadorAsync(int treinadorId) =>
            _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .FirstOrDefaultAsync(t => t.Id == treinadorId);

        // GET: /AnotacoesTreinador?treinadorId=1&q=contrato
        public async Task<IActionResult> Index(int treinadorId, string? q)
        {
            var treinador = await TreinadorAsync(treinadorId);
            if (treinador == null) return NotFound();

            var uid = _userManager.GetUserId(User);
            var query = _context.AnotacoesTreinador
                .Where(a => a.TreinadorId == treinadorId && a.UsuarioId == uid)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a =>
                    a.Titulo.ToLower().Contains(q.ToLower()) ||
                    a.Conteudo.ToLower().Contains(q.ToLower()) ||
                    (a.Categoria != null && a.Categoria.ToLower().Contains(q.ToLower())));

            var anotacoes = await query.OrderByDescending(a => a.DtInc).ToListAsync();

            // Contagem por categoria (do total do treinador, não afetada pela busca "q")
            // — alimenta os chips e o card "Resumo".
            var contagemCategorias = await _context.AnotacoesTreinador
                .Where(a => a.TreinadorId == treinadorId && a.UsuarioId == uid)
                .GroupBy(a => a.Categoria ?? "")
                .Select(g => new { Categoria = g.Key, Qtd = g.Count() })
                .ToDictionaryAsync(x => x.Categoria, x => x.Qtd);

            ViewBag.Treinador = treinador;
            ViewBag.Q = q;
            ViewBag.ContagemCategorias = contagemCategorias;
            return View(anotacoes);
        }

        // GET: /AnotacoesTreinador/Nova?treinadorId=1
        [HttpGet]
        public async Task<IActionResult> Nova(int treinadorId)
        {
            var treinador = await TreinadorAsync(treinadorId);
            if (treinador == null) return NotFound();

            ViewBag.Treinador = treinador;
            return View(new AnotacaoTreinador { TreinadorId = treinadorId });
        }

        // POST: /AnotacoesTreinador/Nova
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nova(AnotacaoTreinador model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Treinador = await TreinadorAsync(model.TreinadorId);
                return View(model);
            }

            model.DtInc = DateTime.UtcNow;
            model.UsuarioId = _userManager.GetUserId(User);
            _context.AnotacoesTreinador.Add(model);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "✅ Anotação salva.";
            return RedirectToAction(nameof(Index), new { treinadorId = model.TreinadorId });
        }

        // GET: /AnotacoesTreinador/Editar/5
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesTreinador
                .Include(a => a.Treinador).ThenInclude(t => t.Time)
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            ViewBag.Treinador = anotacao.Treinador;
            return View(anotacao);
        }

        // POST: /AnotacoesTreinador/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, AnotacaoTreinador model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Treinador = await TreinadorAsync(model.TreinadorId);
                return View(model);
            }

            var uid = _userManager.GetUserId(User);
            var existing = await _context.AnotacoesTreinador.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (existing == null) return NotFound();

            existing.Titulo = model.Titulo;
            existing.Conteudo = model.Conteudo;
            existing.Categoria = model.Categoria;
            existing.DtAlt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "✅ Anotação atualizada.";
            return RedirectToAction(nameof(Index), new { treinadorId = existing.TreinadorId });
        }

        // POST: /AnotacoesTreinador/Excluir/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesTreinador.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            var treinadorId = anotacao.TreinadorId;
            _context.AnotacoesTreinador.Remove(anotacao);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "🗑️ Anotação excluída.";
            return RedirectToAction(nameof(Index), new { treinadorId });
        }
    }
}
