using ControleFutebolWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    [Authorize(Policy = "Admin")]
    public class TransfermarktLogsController : Controller
    {
        private readonly FutebolContext _context;

        public TransfermarktLogsController(FutebolContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? tipo, string? acao)
        {
            var query = _context.TransfermarktSincronizacaoLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(tipo))
                query = query.Where(l => l.Tipo == tipo);

            if (!string.IsNullOrWhiteSpace(acao))
                query = query.Where(l => l.Acao == acao);

            ViewBag.Tipo = tipo;
            ViewBag.Acao = acao;
            ViewBag.Tipos = await _context.TransfermarktSincronizacaoLogs
                .AsNoTracking()
                .Select(l => l.Tipo)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
            ViewBag.Acoes = await _context.TransfermarktSincronizacaoLogs
                .AsNoTracking()
                .Select(l => l.Acao)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            var logs = await query
                .OrderByDescending(l => l.Data)
                .Take(300)
                .ToListAsync();

            return View(logs);
        }
    }
}
