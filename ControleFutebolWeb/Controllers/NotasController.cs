using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Controllers
{
    public class NotasController : Controller
    {
        private readonly FutebolContext _context;

        public NotasController(FutebolContext context)
        {
            _context = context;
        }

        // GET: Notas
        public async Task<IActionResult> Index()
        {
            var notas = await _context.Notas
                .Include(n => n.Jogador)
                .Include(n => n.Jogo)
                .ToListAsync();
            return View(notas);
        }

        // GET: Notas/Create
        public IActionResult Create()
        {
            ViewBag.Jogadores = _context.Jogadores.ToList();
            ViewBag.Jogos = _context.Jogos.ToList();
            return View();
        }

        // POST: Notas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Nota nota)
        {
            if (ModelState.IsValid)
            {
                _context.Add(nota);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(nota);
        }
    }
}