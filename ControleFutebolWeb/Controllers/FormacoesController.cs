using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class FormacoesController : Controller
    {
        private readonly FutebolContext _context;

        public FormacoesController(FutebolContext context)
        {
            _context = context;
        }

        // GET: Formacoes
        public async Task<IActionResult> Index()
        {
            var formacoes = await _context.Formacoes
                .Include(f => f.Posicoes)
                .OrderBy(f => f.Nome)
                .ToListAsync();
            return View(formacoes);
        }

        // GET: Formacoes/Posicoes/5
        public async Task<IActionResult> Posicoes(int? id)
        {
            var formacoes = await _context.Formacoes.OrderBy(f => f.Nome).ToListAsync();

            if (!formacoes.Any())
            {
                TempData["Mensagem"] = "Nenhuma formação cadastrada. Crie uma formação primeiro.";
                return RedirectToAction(nameof(Index));
            }

            var formacaoId = id ?? formacoes.First().Id;
            var formacao = formacoes.FirstOrDefault(f => f.Id == formacaoId) ?? formacoes.First();

            var posicoes = await _context.PosicoesFormacao
                .Where(p => p.FormacaoId == formacao.Id)
                .OrderBy(p => p.Ordem)
                .ToListAsync();

            ViewBag.Formacoes = formacoes;
            ViewBag.FormacaoId = formacao.Id;
            ViewBag.FormacaoNome = formacao.Nome;

            return View(posicoes);
        }

        // POST: Formacoes/SalvarPosicoes  (JSON endpoint)
        [HttpPost]
        public async Task<IActionResult> SalvarPosicoes([FromBody] SalvarPosicoesRequest request)
        {
            if (request == null || request.FormacaoId == 0)
                return BadRequest(new { success = false, mensagem = "FormacaoId inválido." });

            var formacao = await _context.Formacoes.FindAsync(request.FormacaoId);
            if (formacao == null)
                return NotFound(new { success = false, mensagem = "Formação não encontrada." });

            // Remove posições existentes e recria
            var existentes = _context.PosicoesFormacao
                .Where(p => p.FormacaoId == request.FormacaoId);
            _context.PosicoesFormacao.RemoveRange(existentes);

            for (int i = 0; i < request.Posicoes.Count; i++)
            {
                var p = request.Posicoes[i];
                _context.PosicoesFormacao.Add(new PosicaoFormacao
                {
                    FormacaoId = request.FormacaoId,
                    NomePosicao = string.IsNullOrWhiteSpace(p.NomePosicao) ? $"P{i + 1}" : p.NomePosicao,
                    PosicaoX = Math.Round(p.PosicaoX, 2),
                    PosicaoY = Math.Round(p.PosicaoY, 2),
                    Ordem = p.Ordem > 0 ? p.Ordem : i + 1,
                    PosicaoId = p.PosicaoId
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, total = request.Posicoes.Count });
        }

        // POST: Formacoes/Criar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(string nome)
        {
            if (!string.IsNullOrWhiteSpace(nome))
            {
                _context.Formacoes.Add(new Formacao { Nome = nome.Trim() });
                await _context.SaveChangesAsync();
                TempData["Mensagem"] = $"Formação '{nome.Trim()}' criada com sucesso!";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Formacoes/Excluir/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var formacao = await _context.Formacoes.FindAsync(id);
            if (formacao != null)
            {
                _context.Formacoes.Remove(formacao);
                await _context.SaveChangesAsync();
                TempData["Mensagem"] = $"Formação '{formacao.Nome}' excluída.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ── DTOs ───────────────────────────────────────────────────────────────
        public class SalvarPosicoesRequest
        {
            public int FormacaoId { get; set; }
            public List<PosicaoFormacaoInput> Posicoes { get; set; } = new();
        }

        public class PosicaoFormacaoInput
        {
            public string NomePosicao { get; set; } = "";
            public double PosicaoX { get; set; }
            public double PosicaoY { get; set; }
            public int Ordem { get; set; }
            public int PosicaoId { get; set; }
        }
    }
}
