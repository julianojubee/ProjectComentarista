using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Janela de Transferências: histórico das trocas de clube detectadas
    // automaticamente na importação (jogador cadastrado num time aparece na
    // escalação de outro clube). Filtros por time, competição e jogador.
    [Authorize]
    public class TransferenciasController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TransferenciasController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Transferencias?timeId=1&competicaoId=2&jogador=borre
        public async Task<IActionResult> Index(int? timeId, int? competicaoId, string? jogador)
        {
            var query = _context.Transferencias
                .AsNoTracking()
                .Include(t => t.Jogador)
                .Include(t => t.TimeOrigem)
                .Include(t => t.TimeDestino)
                .Include(t => t.Jogo).ThenInclude(j => j!.Competicao)
                .AsQueryable();

            // Time participa como origem OU destino
            if (timeId.HasValue)
                query = query.Where(t => t.TimeOrigemId == timeId || t.TimeDestinoId == timeId);

            if (competicaoId.HasValue)
                query = query.Where(t => t.Jogo != null && t.Jogo.CompeticaoId == competicaoId);

            if (!string.IsNullOrWhiteSpace(jogador))
                query = query.Where(t => t.Jogador.Nome.ToLower().Contains(jogador.ToLower()));

            var transferencias = await query
                .OrderByDescending(t => t.Data)
                .ThenByDescending(t => t.Id)
                .Take(300)
                .ToListAsync();

            // Combos dos filtros: só times/competições que aparecem no histórico
            var timeIdsUsados = await _context.Transferencias
                .Select(t => t.TimeDestinoId)
                .Union(_context.Transferencias
                    .Where(t => t.TimeOrigemId != null)
                    .Select(t => t.TimeOrigemId!.Value))
                .Distinct()
                .ToListAsync();

            var times = await _context.Times
                .Where(t => timeIdsUsados.Contains(t.Id))
                .OrderBy(t => t.Nome)
                .ToListAsync();

            var competicaoIdsUsadas = await _context.Transferencias
                .Where(t => t.JogoId != null)
                .Select(t => t.Jogo!.CompeticaoId)
                .Distinct()
                .ToListAsync();

            var competicoes = await _context.Competicoes
                .Where(c => competicaoIdsUsadas.Contains(c.Id))
                .OrderBy(c => c.Nome)
                .ToListAsync();

            ViewBag.Times = new SelectList(times, "Id", "Nome", timeId);
            ViewBag.Competicoes = new SelectList(competicoes, "Id", "Nome", competicaoId);
            ViewBag.FiltroTimeId = timeId;
            ViewBag.FiltroCompeticaoId = competicaoId;
            ViewBag.FiltroJogador = jogador;

            // Dados do formulário de transferência manual: todos os jogadores (com o
            // clube atual no rótulo, para conferência) e os clubes de destino possíveis.
            ViewBag.JogadoresManual = await _context.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .OrderBy(j => j.Nome)
                .Select(j => new { j.Id, Rotulo = j.Nome + " — " + (j.Time != null ? j.Time.Nome : "sem clube") })
                .ToListAsync();

            ViewBag.TimesDestino = new SelectList(
                await _context.Times.Where(t => !t.EhSelecao).OrderBy(t => t.Nome).ToListAsync(),
                "Id", "Nome");

            ViewBag.UsuarioAtualId = _userManager.GetUserId(User);

            return View(transferencias);
        }

        // POST: /Transferencias/TransferirManual
        // Transferência manual: o usuário escolhe o jogador e o clube de destino,
        // sem esperar o jogador aparecer escalado num jogo importado. Troca o clube
        // e registra no histórico, igual à detecção automática (JogoId fica null).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferirManual(int jogadorId, int timeDestinoId)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == jogadorId);
            var destino = await _context.Times.FirstOrDefaultAsync(t => t.Id == timeDestinoId);

            if (jogador == null || destino == null)
            {
                TempData["Erro"] = "Jogador ou clube de destino não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            if (destino.EhSelecao)
            {
                TempData["Erro"] = "O destino precisa ser um clube — seleções não contam como transferência.";
                return RedirectToAction(nameof(Index));
            }

            if (jogador.TimeId == destino.Id)
            {
                TempData["Erro"] = $"{jogador.NomeExibicao} já pertence a {destino.Nome}.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioId = _userManager.GetUserId(User);

            _context.Transferencias.Add(new Transferencia
            {
                JogadorId = jogador.Id,
                TimeOrigemId = jogador.TimeId,
                TimeDestinoId = destino.Id,
                JogoId = null,
                Data = DateTime.UtcNow,
                UsuarioId = usuarioId
            });

            var origemNome = jogador.Time?.Nome ?? "sem clube";
            jogador.TimeId = destino.Id;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{jogador.NomeExibicao} transferido: {origemNome} → {destino.Nome}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Transferencias/Excluir
        // Só quem criou a transferência manual pode excluí-la — um engano de um
        // usuário não deve afetar o histórico/dados dos outros. Se o jogador ainda
        // estiver no clube de destino (ninguém o transferiu de novo depois), o clube
        // volta pro de origem; se já houve outra transferência por cima, só remove
        // o registro do histórico (reverter o time seria incorreto).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            var transferencia = await _context.Transferencias
                .Include(t => t.Jogador)
                .Include(t => t.TimeOrigem)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transferencia == null)
            {
                TempData["Erro"] = "Transferência não encontrada.";
                return RedirectToAction(nameof(Index));
            }

            if (transferencia.JogoId != null || transferencia.UsuarioId != usuarioId)
            {
                TempData["Erro"] = "Só é possível excluir transferências manuais criadas por você.";
                return RedirectToAction(nameof(Index));
            }

            if (transferencia.Jogador.TimeId == transferencia.TimeDestinoId && transferencia.TimeOrigemId != null)
                transferencia.Jogador.TimeId = transferencia.TimeOrigemId.Value;

            _context.Transferencias.Remove(transferencia);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Transferência de {transferencia.Jogador.NomeExibicao} excluída.";
            return RedirectToAction(nameof(Index));
        }
    }
}
