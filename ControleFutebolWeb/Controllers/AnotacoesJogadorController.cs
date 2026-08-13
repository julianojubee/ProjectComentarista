using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Anotações sobre um jogador — espelha /AnotacoesTime, só que o dono é o atleta.
    // Além das anotações escritas aqui, a tela puxa o que já foi escrito sobre ele
    // em outros lugares: observações de jogos (/Jogos/Analisar) e anotações do
    // clube (/AnotacoesTime) em que ele foi citado com "@Nome".
    [Authorize]
    public class AnotacoesJogadorController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnotacoesJogadorController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private Task<Jogador?> JogadorAsync(int jogadorId) =>
            _context.Jogadores
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == jogadorId);

        // GET: /AnotacoesJogador?jogadorId=1&q=lesão
        public async Task<IActionResult> Index(int jogadorId, string? q)
        {
            var jogador = await JogadorAsync(jogadorId);
            if (jogador == null) return NotFound();

            var uid = _userManager.GetUserId(User);
            var query = _context.AnotacoesJogador
                .Where(a => a.JogadorId == jogadorId && a.UsuarioId == uid)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a =>
                    a.Titulo.ToLower().Contains(q.ToLower()) ||
                    a.Conteudo.ToLower().Contains(q.ToLower()) ||
                    (a.Categoria != null && a.Categoria.ToLower().Contains(q.ToLower())));

            var anotacoes = await query.OrderByDescending(a => a.DtInc).ToListAsync();

            // Contagem por categoria (sempre do total do jogador, não afetada pela
            // busca "q") — usada no card "Resumo" da barra lateral.
            var contagemCategorias = await _context.AnotacoesJogador
                .Where(a => a.JogadorId == jogadorId && a.UsuarioId == uid)
                .GroupBy(a => a.Categoria ?? "")
                .Select(g => new { Categoria = g.Key, Qtd = g.Count() })
                .ToDictionaryAsync(x => x.Categoria, x => x.Qtd);

            ViewBag.Jogador = jogador;
            ViewBag.Q = q;
            ViewBag.ObservacoesJogos = await ObservacoesJogosAsync(jogadorId, uid, q);
            ViewBag.AnotacoesClube = await AnotacoesClubeAsync(jogadorId, uid, q);
            ViewBag.ContagemCategorias = contagemCategorias;
            return View(anotacoes);
        }

        // Observações de jogo que se referem ao jogador: as marcadas com a tag
        // Jogador para ele e as em que ele foi citado com "@Nome", agrupadas por jogo.
        private async Task<List<ObservacaoJogoJogadorViewModel>> ObservacoesJogosAsync(int jogadorId, string? uid, string? q)
        {
            var obsDiretas = await _context.ObservacoesJogoTag
                .AsNoTracking()
                .Where(o => o.Tipo == "JOGADOR" && o.JogadorId == jogadorId && o.UsuarioId == uid)
                .Select(o => o.Id)
                .ToListAsync();

            var obsMencionadas = await _context.ObservacoesJogoTagMencoes
                .AsNoTracking()
                .Where(m => m.JogadorId == jogadorId && m.ObservacaoJogoTag.UsuarioId == uid)
                .Select(m => m.ObservacaoJogoTagId)
                .ToListAsync();

            var ids = obsDiretas.Concat(obsMencionadas).Distinct().ToList();
            if (ids.Count == 0) return new List<ObservacaoJogoJogadorViewModel>();

            var observacoes = await _context.ObservacoesJogoTag
                .AsNoTracking()
                .Where(o => ids.Contains(o.Id))
                .Include(o => o.Jogo).ThenInclude(g => g.TimeCasa)
                .Include(o => o.Jogo).ThenInclude(g => g.TimeVisitante)
                .Include(o => o.Jogo).ThenInclude(g => g.Competicao)
                .OrderBy(o => o.Ordem)
                .ToListAsync();

            return observacoes
                .Where(o => string.IsNullOrWhiteSpace(q) || o.Texto.Contains(q, StringComparison.OrdinalIgnoreCase))
                .GroupBy(o => o.JogoId)
                .Select(g => new ObservacaoJogoJogadorViewModel
                {
                    Jogo = g.First().Jogo,
                    Observacoes = g.Select(o => o.Texto).ToList()
                })
                .OrderByDescending(o => o.Jogo.Data ?? DateTime.MinValue)
                .ToList();
        }

        // Anotações do clube (/AnotacoesTime) em que o jogador foi citado com "@Nome".
        private async Task<List<AnotacaoTime>> AnotacoesClubeAsync(int jogadorId, string? uid, string? q)
        {
            var ids = await _context.AnotacoesTimeMencoes
                .AsNoTracking()
                .Where(m => m.JogadorId == jogadorId && m.AnotacaoTime.UsuarioId == uid)
                .Select(m => m.AnotacaoTimeId)
                .Distinct()
                .ToListAsync();

            if (ids.Count == 0) return new List<AnotacaoTime>();

            var anotacoes = await _context.AnotacoesTime
                .AsNoTracking()
                .Where(a => ids.Contains(a.Id))
                .Include(a => a.Time)
                .ToListAsync();

            return anotacoes
                .Where(a => string.IsNullOrWhiteSpace(q)
                         || a.Titulo.Contains(q, StringComparison.OrdinalIgnoreCase)
                         || a.Conteudo.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.DtInc)
                .ToList();
        }

        // GET: /AnotacoesJogador/Nova?jogadorId=1
        [HttpGet]
        public async Task<IActionResult> Nova(int jogadorId)
        {
            var jogador = await JogadorAsync(jogadorId);
            if (jogador == null) return NotFound();

            ViewBag.Jogador = jogador;
            return View(new AnotacaoJogador { JogadorId = jogadorId });
        }

        // POST: /AnotacoesJogador/Nova
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nova(AnotacaoJogador model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Jogador = await JogadorAsync(model.JogadorId);
                return View(model);
            }

            model.DtInc = DateTime.UtcNow;
            model.UsuarioId = _userManager.GetUserId(User);
            _context.AnotacoesJogador.Add(model);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "✅ Anotação salva.";
            return RedirectToAction(nameof(Index), new { jogadorId = model.JogadorId });
        }

        // GET: /AnotacoesJogador/Editar/5
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesJogador
                .Include(a => a.Jogador).ThenInclude(j => j.Time)
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            ViewBag.Jogador = anotacao.Jogador;
            return View(anotacao);
        }

        // POST: /AnotacoesJogador/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, AnotacaoJogador model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Jogador = await JogadorAsync(model.JogadorId);
                return View(model);
            }

            var uid = _userManager.GetUserId(User);
            var existing = await _context.AnotacoesJogador.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (existing == null) return NotFound();

            existing.Titulo = model.Titulo;
            existing.Conteudo = model.Conteudo;
            existing.Categoria = model.Categoria;
            existing.DtAlt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "✅ Anotação atualizada.";
            return RedirectToAction(nameof(Index), new { jogadorId = existing.JogadorId });
        }

        // POST: /AnotacoesJogador/Excluir/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesJogador.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            var jogadorId = anotacao.JogadorId;
            _context.AnotacoesJogador.Remove(anotacao);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "🗑️ Anotação excluída.";
            return RedirectToAction(nameof(Index), new { jogadorId });
        }
    }
}
