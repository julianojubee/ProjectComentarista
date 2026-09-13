using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Anotações sobre uma competição — espelha /AnotacoesTreinador, só que o dono é o
    // torneio. Como no treinador, não há seções "puxadas" de outros lugares: não existe
    // menção "@Nome" nem tag de observação de jogo para competição.
    [Authorize]
    public class AnotacoesCompeticaoController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnotacoesCompeticaoController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private Task<Competicao?> CompeticaoAsync(int competicaoId) =>
            _context.Competicoes.FirstOrDefaultAsync(c => c.Id == competicaoId);

        // De onde o usuário veio: as competições com tela fixa (Brasileirão, Libertadores,
        // Copa do Brasil, Sul-Americana, Champions, Copa do Mundo) mandam a própria URL no
        // returnUrl pelo partial _LinksEstatisticasCompeticao, para o "voltar" cair na tela
        // de origem e não em /Competicoes/Detalhes. Só aceita caminho local (sem isso o
        // parâmetro viraria um open redirect).
        private string? VoltarPara(string? returnUrl) =>
            Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        // GET: /AnotacoesCompeticao?competicaoId=1&q=regulamento
        public async Task<IActionResult> Index(int competicaoId, string? q, string? returnUrl = null)
        {
            var competicao = await CompeticaoAsync(competicaoId);
            if (competicao == null) return NotFound();

            var uid = _userManager.GetUserId(User);
            var query = _context.AnotacoesCompeticao
                .Where(a => a.CompeticaoId == competicaoId && a.UsuarioId == uid)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a =>
                    a.Titulo.ToLower().Contains(q.ToLower()) ||
                    a.Conteudo.ToLower().Contains(q.ToLower()) ||
                    (a.Categoria != null && a.Categoria.ToLower().Contains(q.ToLower())));

            var anotacoes = await query.OrderByDescending(a => a.DtInc).ToListAsync();

            // Contagem por categoria (do total da competição, não afetada pela busca "q")
            // — alimenta os chips e o card "Resumo".
            var contagemCategorias = await _context.AnotacoesCompeticao
                .Where(a => a.CompeticaoId == competicaoId && a.UsuarioId == uid)
                .GroupBy(a => a.Categoria ?? "")
                .Select(g => new { Categoria = g.Key, Qtd = g.Count() })
                .ToDictionaryAsync(x => x.Categoria, x => x.Qtd);

            ViewBag.Competicao = competicao;
            ViewBag.Q = q;
            ViewBag.ReturnUrl = VoltarPara(returnUrl);
            ViewBag.ContagemCategorias = contagemCategorias;
            return View(anotacoes);
        }

        // GET: /AnotacoesCompeticao/Nova?competicaoId=1
        [HttpGet]
        public async Task<IActionResult> Nova(int competicaoId, string? returnUrl = null)
        {
            var competicao = await CompeticaoAsync(competicaoId);
            if (competicao == null) return NotFound();

            ViewBag.Competicao = competicao;
            ViewBag.ReturnUrl = VoltarPara(returnUrl);
            return View(new AnotacaoCompeticao { CompeticaoId = competicaoId });
        }

        // POST: /AnotacoesCompeticao/Nova
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nova(AnotacaoCompeticao model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Competicao = await CompeticaoAsync(model.CompeticaoId);
                ViewBag.ReturnUrl = VoltarPara(returnUrl);
                return View(model);
            }

            model.DtInc = DateTime.UtcNow;
            model.UsuarioId = _userManager.GetUserId(User);
            _context.AnotacoesCompeticao.Add(model);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "✅ Anotação salva.";
            return RedirectToAction(nameof(Index), new { competicaoId = model.CompeticaoId, returnUrl = VoltarPara(returnUrl) });
        }

        // GET: /AnotacoesCompeticao/Editar/5
        [HttpGet]
        public async Task<IActionResult> Editar(int id, string? returnUrl = null)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesCompeticao
                .Include(a => a.Competicao)
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            ViewBag.Competicao = anotacao.Competicao;
            ViewBag.ReturnUrl = VoltarPara(returnUrl);
            return View(anotacao);
        }

        // POST: /AnotacoesCompeticao/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, AnotacaoCompeticao model, string? returnUrl = null)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Competicao = await CompeticaoAsync(model.CompeticaoId);
                ViewBag.ReturnUrl = VoltarPara(returnUrl);
                return View(model);
            }

            var uid = _userManager.GetUserId(User);
            var existing = await _context.AnotacoesCompeticao.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (existing == null) return NotFound();

            existing.Titulo = model.Titulo;
            existing.Conteudo = model.Conteudo;
            existing.Categoria = model.Categoria;
            existing.DtAlt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "✅ Anotação atualizada.";
            return RedirectToAction(nameof(Index), new { competicaoId = existing.CompeticaoId, returnUrl = VoltarPara(returnUrl) });
        }

        // POST: /AnotacoesCompeticao/Excluir/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id, string? returnUrl = null)
        {
            var uid = _userManager.GetUserId(User);
            var anotacao = await _context.AnotacoesCompeticao.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == uid);
            if (anotacao == null) return NotFound();

            var competicaoId = anotacao.CompeticaoId;
            _context.AnotacoesCompeticao.Remove(anotacao);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "🗑️ Anotação excluída.";
            return RedirectToAction(nameof(Index), new { competicaoId, returnUrl = VoltarPara(returnUrl) });
        }
    }
}
