using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ControleFutebolWeb.Controllers
{
    public class TimesController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<TimesController> _logger;

        public TimesController(FutebolContext context, ILogger<TimesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Times
        // GET: Times
        public async Task<IActionResult> Index(List<int>? competicaoIds, List<int>? timeIds)
        {
            competicaoIds ??= new List<int>();
            timeIds ??= new List<int>();

            var query = _context.Times.AsQueryable();

            if (competicaoIds.Any())
            {
                // pega todos os times que jogaram nas competições selecionadas (união)
                var jogosCompeticao = _context.Jogos
                    .Where(j => competicaoIds.Contains(j.CompeticaoId));

                var timesCompeticao = await jogosCompeticao
                    .Select(j => j.TimeCasaId)
                    .Union(jogosCompeticao.Select(j => j.TimeVisitanteId))
                    .Distinct()
                    .ToListAsync();

                query = query.Where(t => timesCompeticao.Contains(t.Id));
            }

            if (timeIds.Any())
            {
                query = query.Where(t => timeIds.Contains(t.Id));
            }

            var times = await query.OrderBy(t => t.Nome).ToListAsync();

            // Listas completas para os tag selectors
            ViewBag.Competicoes = await _context.Competicoes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.Times = await _context.Times.OrderBy(t => t.Nome).ToListAsync();
            ViewBag.CompeticaoIdsFiltro = competicaoIds;
            ViewBag.TimeIdsFiltro = timeIds;

            return View(times);
        }


        // GET: Times/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var time = await _context.Times
                .Include(t => t.TimeEscalacaoPadrao)
                    .ThenInclude(te => te.Jogador)
                .Include(t => t.FormacaoPadrao)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (time == null) return NotFound();

            var escalacao = time.TimeEscalacaoPadrao.Any()
                ? time.TimeEscalacaoPadrao.Select(te => new TimeEscalacaoPadrao
                {
                    Id = te.Id,
                    TimeId = te.TimeId,
                    PosicaoId = te.PosicaoId,
                    Posicao = te.Posicao,
                    PosicaoX = te.PosicaoX,
                    PosicaoY = te.PosicaoY,
                    Titular = te.Titular,
                    JogadorId = te.JogadorId,
                    Jogador = te.Jogador
                }).ToList()
                : await _context.PosicoesFormacao
                    .Where(p => p.FormacaoId == time.FormacaoPadraoId)
                    .Select(p => new TimeEscalacaoPadrao
                    {
                        PosicaoId = p.PosicaoId,
                        Posicao = p.NomePosicao,
                        FormacaoId = p.FormacaoId,
                        PosicaoX = (int)p.PosicaoX,
                        PosicaoY = (int)p.PosicaoY,
                        Titular = true,          // ← fallback já nasce true
                        TimeId = id,
                        JogadorId = null
                    }).ToListAsync();

            var elenco = await _context.Jogadores
                .Include(j => j.Nacionalidade)
                .Include(j => j.Time)
                .Where(j => j.TimeId == id || j.SelecaoId == id)
                .ToListAsync();

            var jogos = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.TimeCasaId == id || j.TimeVisitanteId == id)
                .ToListAsync();

            var jogosPassados = jogos
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue && j.Data < DateTime.Now)
                .OrderByDescending(j => j.Data)
                .Take(5)
                .ToList();

            var jogosFuturos = jogos
                .Where(j => j.Data >= DateTime.Now)
                .OrderBy(j => j.Data)
                .Take(5)
                .ToList();

            var formacoes = await _context.Formacoes.ToListAsync();

            var treinador = await _context.Treinadores
                .Include(t => t.Nacionalidade)
                .Where(t => t.TimeId == id)
                .OrderByDescending(t => t.DtInc)
                .FirstOrDefaultAsync();

            // Competições com link apifoot: (usadas no painel de estatísticas da API)
            var competicaoIdsDoTime = jogos.Select(j => j.CompeticaoId).Distinct().ToList();
            var todasCompeticoesDoTime = await _context.Competicoes
                .Where(c => competicaoIdsDoTime.Contains(c.Id) &&
                            c.LinkTransfermarket != null &&
                            c.LinkTransfermarket.StartsWith("apifoot:"))
                .OrderBy(c => c.Nome)
                .ToListAsync();

            var competicoesApi = todasCompeticoesDoTime
                .Select(c =>
                {
                    var parts = c.LinkTransfermarket!.Split(':');
                    if (parts.Length >= 3 &&
                        int.TryParse(parts[1], out var lid) &&
                        int.TryParse(parts[2], out var sea))
                        return new CompeticaoApiItem { Nome = c.Nome, LeagueId = lid, Season = sea };
                    return null;
                })
                .Where(x => x != null)
                .Cast<CompeticaoApiItem>()
                .ToList();

            var viewModel = new TimeDetalhesViewModel
            {
                Time = time,
                Elenco = elenco,
                Jogos = jogos,
                JogosPassados = jogosPassados,
                JogosFuturos = jogosFuturos,
                TimeEscalacaoPadrao = escalacao,
                Formacoes = formacoes,
                Treinador = treinador,
                CompeticoesApi = competicoesApi
            };

            return View(viewModel);
        }

        // GET: Times/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Times/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Time time, IFormFile escudoFile)
        {
            if (ModelState.IsValid)
            {
                if (escudoFile != null && escudoFile.Length > 0)
                {
                    var fileName = Path.GetFileName(escudoFile.FileName);
                    var filePath = Path.Combine("wwwroot/images/escudos", fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await escudoFile.CopyToAsync(stream);
                    time.EscudoUrl = "/images/escudos/" + fileName;
                }

                _context.Add(time);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            _logger.LogWarning("ModelState inválido ao criar Time.");
            return View(time);
        }

        // GET: Times/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var time = await _context.Times.FindAsync(id);
            if (time == null) return NotFound();
            return View(time);
        }

        // POST: Times/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Time time, IFormFile escudoFile)
        {
            if (id != time.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (escudoFile != null && escudoFile.Length > 0)
                    {
                        var fileName = Path.GetFileName(escudoFile.FileName);
                        var filePath = Path.Combine("wwwroot/images/escudos", fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                            await escudoFile.CopyToAsync(stream);
                        time.EscudoUrl = "/images/escudos/" + fileName;
                    }

                    _context.Update(time);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Times.Any(e => e.Id == time.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(time);
        }

        // GET: Times/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var time = await _context.Times.FirstOrDefaultAsync(m => m.Id == id);
            if (time == null) return NotFound();
            return View(time);
        }

        // POST: Times/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var time = await _context.Times.FindAsync(id);
            if (time != null)
            {
                _context.Times.Remove(time);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarLinkTransfermarkt(int id, string linktransfermarket)
        {
            var time = await _context.Times.FindAsync(id);
            if (time == null) return NotFound();

            time.LinkTransfermarket = linktransfermarket;

            _context.Update(time);
            await _context.SaveChangesAsync();

            TempData["Mensagem"] = "Link Transfermarkt do time atualizado com sucesso!";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> ImportarUniforme(int id, IFormFile arquivo, string tipo = "casa")
        {
            var time = await _context.Times.FindAsync(id);
            if (time == null) return NotFound();

            if (arquivo != null && arquivo.Length > 0)
            {
                var sufixo = tipo == "visitante" ? "camisa_visitante" : "camisa";
                var fileName = $"{time.Nome}_{sufixo}.png";
                var path = Path.Combine("wwwroot/Images/kits", fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                    await arquivo.CopyToAsync(stream);

                if (tipo == "visitante")
                    time.CamisaVisitanteUrl = $"/Images/kits/{fileName}";
                else
                    time.CamisaUrl = $"/Images/kits/{fileName}";

                _context.Update(time);
                await _context.SaveChangesAsync();
                TempData["Mensagem"] = $"Camisa {tipo} importada com sucesso!";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult UploadBackground(int id, IFormFile backgroundFile)
        {
            var time = _context.Times.Find(id);
            if (time == null) return NotFound();

            if (backgroundFile != null && backgroundFile.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}_{backgroundFile.FileName}";
                var path = Path.Combine("wwwroot/images/backgrounds", fileName);
                using (var stream = new FileStream(path, FileMode.Create))
                    backgroundFile.CopyTo(stream);

                time.BackgroundUrl = $"/images/backgrounds/{fileName}";
                _context.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public IActionResult DefinirFormacao(int id, int formacaoPadraoId)
        {
            var time = _context.Times
                .Include(t => t.TimeEscalacaoPadrao)
                .FirstOrDefault(t => t.Id == id);

            if (time == null) return NotFound();

            // Atualiza a formação escolhida
            time.FormacaoPadraoId = formacaoPadraoId;

            // Remove posições antigas ao trocar de formação
            if (time.TimeEscalacaoPadrao.Any())
                _context.TimeEscalacaoPadrao.RemoveRange(time.TimeEscalacaoPadrao);

            // Busca todas as posições da formação escolhida
            var posicoesFormacao = _context.PosicoesFormacao
                .Where(pf => pf.FormacaoId == formacaoPadraoId)
                .ToList();

            // Cria os registros de escalação padrão para o time
            var posicoes = posicoesFormacao.Select(pf => new TimeEscalacaoPadrao
            {
                TimeId = time.Id,
                PosicaoId = pf.Id,
                Posicao = pf.NomePosicao,
                PosicaoX = (int)pf.PosicaoX,
                PosicaoY = (int)pf.PosicaoY,
                FormacaoId = formacaoPadraoId,
                Titular = true,              // ← CORRIGIDO: era omitido (default false)
                JogadorId = null
            }).ToList();

            _context.TimeEscalacaoPadrao.AddRange(posicoes);
            _context.SaveChanges();

            return RedirectToAction("Details", new { id = time.Id });
        }

        [HttpPost]
        public IActionResult SalvarEscalacaoPadrao(int id, int formacaoPadraoId, List<EscalacaoInput> escalacao)
        {
            var time = _context.Times
                .Include(t => t.TimeEscalacaoPadrao)
                    .ThenInclude(te => te.Jogador)
                .FirstOrDefault(t => t.Id == id);

            if (time == null) return NotFound();

            if (escalacao != null && escalacao.Any())
            {
                foreach (var e in escalacao)
                {
                    var posicao = time.TimeEscalacaoPadrao.FirstOrDefault(te => te.Id == e.Id);
                    if (posicao == null) continue;

                    posicao.FormacaoId = time.FormacaoPadraoId;
                    posicao.PosicaoX = e.PosicaoX;
                    posicao.PosicaoY = e.PosicaoY;
                    posicao.Titular = true;  // ← CORRIGIDO: nunca era setado (ficava false)

                    if (e.JogadorId > 0)
                    {
                        // Bloqueia duplicação verificando apenas outros slots (não o atual)
                        bool jogadorJaEscalado = time.TimeEscalacaoPadrao
                            .Any(te => te.JogadorId == e.JogadorId && te.Id != posicao.Id);

                        if (jogadorJaEscalado) continue;

                        posicao.JogadorId = e.JogadorId;
                    }
                    else
                    {
                        posicao.JogadorId = null;
                    }
                }
            }

            if (_context.Formacoes.Any(f => f.Id == formacaoPadraoId))
                time.FormacaoPadraoId = formacaoPadraoId;

            _context.SaveChanges();
            return RedirectToAction("Details", new { id = time.Id });
        }

        // GET: Times/EstatisticasApi?timeId=1&leagueId=71&season=2026
        [HttpGet]
        public async Task<IActionResult> EstatisticasApi(int timeId, int leagueId, int season)
        {
            var time = await _context.Times.FindAsync(timeId);
            if (time == null || time.IdApi == 0)
                return Json(new { erro = "Time não encontrado ou sem IdApi configurado." });

            var service = HttpContext.RequestServices.GetRequiredService<ControleFutebolWeb.Services.ApiFootballService>();
            var stats = await service.BuscarEstatisticasTimeAsync(time.IdApi, leagueId, season);
            if (stats == null)
                return Json(new { erro = "Nenhuma estatística encontrada para este time/liga/temporada." });

            // Retorna com chaves explícitas para evitar ambiguidade camelCase vs snake_case no JS
            return Json(new
            {
                form     = stats.Form,
                fixtures = stats.Fixtures,
                goals    = stats.Goals,
                biggest  = stats.Biggest,
                cleanSheet    = new { home = stats.CleanSheet.Home,    away = stats.CleanSheet.Away,    total = stats.CleanSheet.Total },
                failedToScore = new { home = stats.FailedToScore.Home, away = stats.FailedToScore.Away, total = stats.FailedToScore.Total },
                penalty  = stats.Penalty,
                lineups  = stats.Lineups,
                cards    = stats.Cards,
            });
        }
    }
}
