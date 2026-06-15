using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class TreinadoresController : Controller
    {
        private readonly FutebolContext _context;
        private readonly TransfermarktTreinadorService _tmTreinadorService;

        public TreinadoresController(
            FutebolContext context,
            TransfermarktTreinadorService tmTreinadorService)
        {
            _context = context;
            _tmTreinadorService = tmTreinadorService;
        }

        // GET: Treinadores
        public async Task<IActionResult> Index(string funcao, string nacionalidade, int? timeId)
        {
            ViewBag.Nacionalidades = new SelectList(
                await _context.Nacionalidades.OrderBy(n => n.Nome).ToListAsync(),
                "Nome", "Nome");
            ViewBag.Times = new SelectList(
                await _context.Times.OrderBy(t => t.Nome).ToListAsync(), "Id", "Nome");

            var query = _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .AsQueryable();

            if (!string.IsNullOrEmpty(nacionalidade))
                query = query.Where(t => t.Nacionalidade.Nome == nacionalidade);

            if (timeId.HasValue)
                query = query.Where(t => t.TimeId == timeId.Value);

            return View(await query.ToListAsync());
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

            if (treinador == null) return NotFound();

            return View(treinador);
        }

        // GET: Treinadores/Create
        public IActionResult Create()
        {
            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ConsultarHistorico(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            // Busca histórico já salvo no banco
            var historico = await _context.TreinadoresHistorico
            .Include(h => h.Time)
            .Where(h => h.TreinadorId == id)
            .OrderByDescending(h => h.DtInicio) // último trabalho primeiro
            .ToListAsync();

            ViewBag.Treinador = treinador;
            return View("HistoricoConsulta", historico);
        }


        // POST: Treinadores/Create
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

        // POST: Treinadores/Edit/5
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
                    throw;
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

            if (treinador == null) return NotFound();
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

        // ── Buscar dados (foto + idade + nacionalidade) via ogol ────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarFoto(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            TempData["Erro"] = $"❌ Busca automática de foto de treinador não está disponível. " +
                               "Informe a foto manualmente pelo formulário de edição.";
            return RedirectToAction(nameof(Index));
        }

        // ── Importar histórico pelo nome (busca automática) ──────────────────

        /// <summary>
        /// GET: Abre a tela de pré-visualização do histórico antes de salvar.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PreVisualizarHistorico(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            var info = await _tmTreinadorService.BuscarTreinadorAsync(
                treinador.Nome, treinador.Time?.Nome);

            if (info == null || !info.Historico.Any())
            {
                TempData["Erro"] =
                    $"Nenhum histórico encontrado para '{treinador.Nome}' no Transfermarkt. " +
                    "Tente usar a URL direta do perfil.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Treinador = treinador;
            ViewBag.Historico = info.Historico;
            ViewBag.ProfileUrl = info.ProfileUrl;
            ViewBag.FotoUrl = info.FotoUrl;

            return View("HistoricoPreVisualizacao", info);
        }

        /// <summary>
        /// GET: Busca histórico via URL direta do Transfermarkt.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PreVisualizarHistoricoUrl(int id, string url)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            if (string.IsNullOrWhiteSpace(url))
            {
                TempData["Erro"] = "URL inválida.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Converte URL de "estadisticas" ou "leistungsdaten" para "profil"
            url = NormalizarUrlPerfil(url);

            var info = await _tmTreinadorService.BuscarPerfilAsync(url);

            if (info == null || !info.Historico.Any())
            {
                TempData["Erro"] =
                    "Nenhum histórico encontrado na URL informada. " +
                    "Verifique se é uma URL de perfil de treinador válida.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Treinador = treinador;
            ViewBag.Historico = info.Historico;
            ViewBag.ProfileUrl = info.ProfileUrl;
            ViewBag.FotoUrl = info.FotoUrl;
            ViewBag.TreinadorId = id;

            return View("HistoricoPreVisualizacao", info);
        }

        /// <summary>
        /// POST: Confirma e salva o histórico no banco.
        /// Recebe os dados como JSON serializado nos hidden inputs.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarHistorico(
            int treinadorId,
            string profileUrl,
            bool atualizarFoto = false)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == treinadorId);

            if (treinador == null) return NotFound();

            if (string.IsNullOrWhiteSpace(profileUrl))
            {
                TempData["Erro"] = "URL do perfil não informada.";
                return RedirectToAction(nameof(Details), new { id = treinadorId });
            }

            profileUrl = NormalizarUrlPerfil(profileUrl);

            var info = await _tmTreinadorService.BuscarPerfilAsync(profileUrl);

            if (info == null)
            {
                TempData["Erro"] = "Não foi possível acessar o perfil para salvar o histórico.";
                return RedirectToAction(nameof(Details), new { id = treinadorId });
            }

            // Salva histórico
            var resultado = await _tmTreinadorService.SalvarHistoricoAsync(
                _context, treinadorId, info.Historico);

            // Atualiza foto se solicitado
            if (atualizarFoto && !string.IsNullOrWhiteSpace(info.FotoUrl))
            {
                treinador.FotoUrl = info.FotoUrl;
                treinador.DtAlt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] =
                $"✅ Histórico salvo! {resultado.RegistrosSalvos} registro(s), " +
                $"{resultado.TimesCreados} time(s) criado(s).";

            if (resultado.Avisos.Any())
                TempData["Avisos"] = string.Join(" | ", resultado.Avisos.Take(5));

            return RedirectToAction(nameof(Details), new { id = treinadorId });
        }

        // ─── Helper ───────────────────────────────────────────────────────────

        private static string NormalizarUrlPerfil(string url)
        {
            // Garante que a URL aponta para /profil/trainer/
            if (!url.Contains("/profil/trainer/"))
            {
                // Tenta converter leistungsdaten → profil
                url = System.Text.RegularExpressions.Regex.Replace(
                    url,
                    @"/(leistungsdaten|statistik|transfers|steckbrief)/trainer/",
                    "/profil/trainer/");
            }
            return url;
        }
    }
}