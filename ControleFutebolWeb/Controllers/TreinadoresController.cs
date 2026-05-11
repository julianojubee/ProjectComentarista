using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class TreinadoresController : Controller
    {
        private readonly FutebolContext _context;

        public TreinadoresController(FutebolContext context)
        {
            _context = context;
        }

        // GET: Treinadores
        public async Task<IActionResult> Index(string funcao, string nacionalidade, int? timeId)
        {
            // Filtros
            ViewBag.Nacionalidades = new SelectList(
            await _context.Nacionalidades.OrderBy(n => n.Nome).ToListAsync(),
            "Nome", "Nome"
);
            ViewBag.Times = new SelectList(await _context.Times.OrderBy(t => t.Nome).ToListAsync(), "Id", "Nome");

            var query = _context.Treinadores
            .Include(t => t.Time)
            .Include(t => t.Nacionalidade)
            .AsQueryable();

            if (!string.IsNullOrEmpty(nacionalidade))
                query = query.Where(t => t.Nacionalidade.Nome == nacionalidade);

            if (timeId.HasValue)
                query = query.Where(t => t.TimeId == timeId.Value);

            var treinadores = await query.ToListAsync();
            return View(treinadores);
        }

        // GET: Treinadores/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .Include(t => t.Historicos)
                .ThenInclude(h => h.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null)
                return NotFound();

            return View(treinador);
        }

        public IActionResult Create()
        {
            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Treinador treinador)
        {
            if (ModelState.IsValid)
            {
                treinador.DtInc = DateTime.UtcNow;
                _context.Add(treinador);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", treinador.TimeId);
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome", treinador.NacionalidadeId);
            return View(treinador);
        }


        // GET: Treinadores/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var treinador = await _context.Treinadores.FindAsync(id);
            if (treinador == null) return NotFound();

            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", treinador.TimeId);
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome", treinador.NacionalidadeId);
            return View(treinador);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Treinador treinador)
        {
            if (id != treinador.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    treinador.DtAlt = DateTime.UtcNow;
                    _context.Update(treinador);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Treinadores.Any(e => e.Id == treinador.Id))
                        return NotFound();
                    else throw;
                }
            }
            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", treinador.TimeId);
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome", treinador.NacionalidadeId);
            return View(treinador);
        }

        // GET: Treinadores/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null)
                return NotFound();

            return View(treinador);
        }

        // POST: Treinadores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var treinador = await _context.Treinadores.FindAsync(id);
            if (treinador != null)
            {
                _context.Treinadores.Remove(treinador);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
