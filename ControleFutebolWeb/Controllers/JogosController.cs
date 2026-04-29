using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ControleFutebolWeb.Controllers
{
    public class JogosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<JogosController> _logger;

        public JogosController(FutebolContext context, ILogger<JogosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Jogos
        public async Task<IActionResult> Index(
            int? teamId,
            string? location,
            DateTime? startDate,
            DateTime? endDate,
            int? competicaoId,
            string? status)
        {
            var jogosQuery = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Gols).ThenInclude(g => g.Jogador)
                .Include(j => j.Escalacoes).ThenInclude(e => e.Jogador)
                .Include(j => j.Cartoes).ThenInclude(c => c.Jogador)
                .AsQueryable();

            if (teamId.HasValue)
            {
                var id = teamId.Value;
                switch ((location ?? "both").ToLowerInvariant())
                {
                    case "home":
                        jogosQuery = jogosQuery.Where(j => j.TimeCasaId == id);
                        break;
                    case "away":
                        jogosQuery = jogosQuery.Where(j => j.TimeVisitanteId == id);
                        break;
                    default:
                        jogosQuery = jogosQuery.Where(j => j.TimeCasaId == id || j.TimeVisitanteId == id);
                        break;
                }
            }

            if (competicaoId.HasValue)
                jogosQuery = jogosQuery.Where(j => j.CompeticaoId == competicaoId.Value);

            switch ((status ?? "all").ToLowerInvariant())
            {
                case "played":
                    jogosQuery = jogosQuery.Where(j => j.PlacarCasa >= 0 && j.PlacarVisitante >= 0);
                    break;
                case "scheduled":
                    jogosQuery = jogosQuery.Where(j => j.PlacarCasa < 0 || j.PlacarVisitante < 0 || j.PlacarCasa == null || j.PlacarVisitante == null);
                    break;
            }

            if (startDate.HasValue)
                jogosQuery = jogosQuery.Where(j => j.Data >= startDate.Value);
            if (endDate.HasValue)
                jogosQuery = jogosQuery.Where(j => j.Data <= endDate.Value);

            ViewBag.TimeList = new SelectList(_context.Times.OrderBy(t => t.Nome).ToList(), "Id", "Nome", teamId);
            ViewBag.CompeticaoList = new SelectList(_context.Competicoes.OrderBy(c => c.Nome).ToList(), "Id", "Nome", competicaoId);
            ViewBag.StatusList = new SelectList(
                new[] {
                    new { Value = "all", Text = "Todos" },
                    new { Value = "played", Text = "Realizados" },
                    new { Value = "scheduled", Text = "Não realizados" }
                },
                "Value", "Text", status ?? "all"
            );
            ViewBag.LocationList = new SelectList(
                new[] {
                    new { Value = "both", Text = "Casa ou Fora" },
                    new { Value = "home", Text = "Apenas Time da Casa" },
                    new { Value = "away", Text = "Apenas Time Visitante" }
                },
                "Value", "Text", location ?? "both"
            );

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            jogosQuery = jogosQuery.OrderByDescending(j => j.Data);

            ViewBag.CompeticoesMap = await _context.Competicoes
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Id, c => c.Nome);

            return View(await jogosQuery.ToListAsync());
        }

        // GET: Jogos/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Escalacoes).ThenInclude(e => e.Jogador)
                .Include(j => j.Gols).ThenInclude(g => g.Jogador)
                .Include(j => j.Cartoes).ThenInclude(c => c.Jogador)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jogo == null) return NotFound();

            return View(jogo);
        }

        // GET: Jogos/Create
        public IActionResult Create()
        {
            ViewBag.TimeCasaId = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.TimeVisitanteId = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.FormacaoCasaId = new SelectList(_context.Formacoes, "Id", "Nome");
            ViewBag.FormacaoVisitanteId = new SelectList(_context.Formacoes, "Id", "Nome");
            return View();
        }

        // POST: Jogos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Jogo jogo)
        {
            jogo.Data = DateTime.SpecifyKind(jogo.Data, DateTimeKind.Unspecified);

            if (ModelState.IsValid)
            {
                _context.Add(jogo);
                await _context.SaveChangesAsync();

                var formacaoCasa = await _context.Formacoes
                    .Include(f => f.Posicoes)
                    .FirstOrDefaultAsync(f => f.Id == jogo.FormacaoCasaId);

                var formacaoVisitante = await _context.Formacoes
                    .Include(f => f.Posicoes)
                    .FirstOrDefaultAsync(f => f.Id == jogo.FormacaoVisitanteId);

                if (formacaoCasa != null)
                    foreach (var pos in formacaoCasa.Posicoes)
                        _context.Escalacoes.Add(new Escalacao
                        {
                            JogoId = jogo.Id,
                            Titular = true,
                            Posicao = pos.NomePosicao,
                            PosicaoX = pos.PosicaoX,
                            PosicaoY = pos.PosicaoY,
                            IsTimeCasa = true
                        });

                if (formacaoVisitante != null)
                    foreach (var pos in formacaoVisitante.Posicoes)
                        _context.Escalacoes.Add(new Escalacao
                        {
                            JogoId = jogo.Id,
                            Titular = true,
                            Posicao = pos.NomePosicao,
                            PosicaoX = pos.PosicaoX,
                            PosicaoY = pos.PosicaoY,
                            IsTimeCasa = false
                        });

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.TimeCasaId = new SelectList(_context.Times, "Id", "Nome", jogo.TimeCasaId);
            ViewBag.TimeVisitanteId = new SelectList(_context.Times, "Id", "Nome", jogo.TimeVisitanteId);
            ViewBag.FormacaoCasaId = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoCasaId);
            ViewBag.FormacaoVisitanteId = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoVisitanteId);

            return View(jogo);
        }

        // GET: Jogos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo == null) return NotFound();

            ViewBag.TimeCasaId = new SelectList(_context.Times, "Id", "Nome", jogo.TimeCasaId);
            ViewBag.TimeVisitanteId = new SelectList(_context.Times, "Id", "Nome", jogo.TimeVisitanteId);
            ViewBag.FormacaoCasaId = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoCasaId);
            ViewBag.FormacaoVisitanteId = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoVisitanteId);
            return View(jogo);
        }

        // POST: Jogos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Jogo jogo)
        {
            if (id != jogo.Id) return NotFound();

            jogo.Data = DateTime.SpecifyKind(jogo.Data, DateTimeKind.Unspecified);

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(jogo);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Jogos.Any(e => e.Id == jogo.Id)) return NotFound();
                    throw;
                }
            }

            ViewBag.TimeCasaId = new SelectList(_context.Times, "Id", "Nome", jogo.TimeCasaId);
            ViewBag.TimeVisitanteId = new SelectList(_context.Times, "Id", "Nome", jogo.TimeVisitanteId);
            ViewBag.FormacaoCasaId = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoCasaId);
            ViewBag.FormacaoVisitanteId = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoVisitanteId);

            return View(jogo);
        }

        // GET: Jogos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jogo == null) return NotFound();

            return View(jogo);
        }

        // POST: Jogos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo != null)
            {
                _context.Jogos.Remove(jogo);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Escalacao/Edit
        public IActionResult EditEscalacao(int id)
        {
            var jogo = _context.Jogos
                .Include(j => j.Escalacoes).ThenInclude(e => e.Jogador)
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefault(j => j.Id == id);

            if (jogo == null) return NotFound();

            ViewBag.Jogadores = new SelectList(_context.Jogadores, "Id", "Nome");
            return View(jogo);
        }

        [HttpPost]
        public IActionResult EditEscalacao(
            int id,
            List<Escalacao> escalacoes,
            int? novoJogadorId,
            string novaPosicao,
            bool novoTitular,
            bool novoIsTimeCasa)
        {
            var jogo = _context.Jogos
                .Include(j => j.Escalacoes)
                .FirstOrDefault(j => j.Id == id);

            if (jogo == null) return NotFound();

            foreach (var esc in escalacoes)
            {
                var existente = jogo.Escalacoes.FirstOrDefault(e => e.Id == esc.Id);
                if (existente != null)
                {
                    existente.Posicao = esc.Posicao;
                    existente.PosicaoX = esc.PosicaoX;
                    existente.PosicaoY = esc.PosicaoY;
                    existente.Titular = esc.Titular;
                }
            }

            if (novoJogadorId.HasValue)
                _context.Escalacoes.Add(new Escalacao
                {
                    JogoId = id,
                    JogadorId = novoJogadorId.Value,
                    Posicao = novaPosicao,
                    Titular = novoTitular,
                    IsTimeCasa = novoIsTimeCasa
                });

            _context.SaveChanges();
            return RedirectToAction("Details", new { id });
        }

        // ── Analisar ────────────────────────────────────────────────────────────
        public async Task<IActionResult> Analisar(int id, int? formacaoCasaId, int? formacaoVisitanteId, string? faseEscalacao)
        {
            var faseAtual = string.Equals(faseEscalacao, "FINAL", StringComparison.OrdinalIgnoreCase) ? "FINAL" : "INICIAL";

            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var escalacoesFinaisExistem = await _context.Escalacoes
                .AnyAsync(e => e.JogoId == id && e.FaseEscalacao == "FINAL");

            if (faseAtual == "FINAL" && !escalacoesFinaisExistem)
            {
                var escalacoesIniciais = await _context.Escalacoes
                    .Where(e => e.JogoId == id && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                    .ToListAsync();

                if (escalacoesIniciais.Any())
                {
                    var clonesFinais = escalacoesIniciais.Select(e => new Escalacao
                    {
                        JogoId = e.JogoId,
                        JogadorId = e.JogadorId,
                        Titular = e.Titular,
                        Posicao = e.Posicao,
                        IsTimeCasa = e.IsTimeCasa,
                        PosicaoX = e.PosicaoX,
                        PosicaoY = e.PosicaoY,
                        FaseEscalacao = "FINAL"
                    }).ToList();

                    _context.Escalacoes.AddRange(clonesFinais);
                    await _context.SaveChangesAsync();
                }
            }

            // Escalações já salvas por fase
            var escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null)))
                .ToListAsync();

            var escalacoesCasa = escalacoes.Where(e => e.IsTimeCasa).ToList();
            var escalacoesVisitante = escalacoes.Where(e => !e.IsTimeCasa).ToList();

            // ── CASA ─────────────────────────────────────────────────────────
            var idFormacaoCasa = formacaoCasaId ?? jogo.FormacaoCasaId ?? 0;
            bool formacaoCasaMudou = formacaoCasaId.HasValue && formacaoCasaId.Value != (jogo.FormacaoCasaId ?? 0);

            if (escalacoesCasa.Count == 0 || formacaoCasaMudou)
            {
                if (escalacoesCasa.Count > 0)
                    _context.Escalacoes.RemoveRange(escalacoesCasa);

                List<Escalacao> novasCasa = new();

                // 1) Última escalação do time em outro jogo (sem subquery correlated)
                if (!formacaoCasaMudou)
                {
                    // Busca o jogo anterior mais recente que envolve o time da casa
                    var jogoAnterior = await _context.Jogos
                        .Where(j => j.Id != id
                                 && (j.TimeCasaId == jogo.TimeCasaId || j.TimeVisitanteId == jogo.TimeCasaId))
                        .OrderByDescending(j => j.Data)
                        .FirstOrDefaultAsync();

                    if (jogoAnterior != null)
                    {
                        bool eraTimeCasaAnterior = jogoAnterior.TimeCasaId == jogo.TimeCasaId;

                        var ultimasEscalacoes = await _context.Escalacoes
                            .Where(e => e.JogoId == jogoAnterior.Id
                                     && e.IsTimeCasa == eraTimeCasaAnterior
                                     && (e.FaseEscalacao == "FINAL" || e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                            .ToListAsync();

                        if (ultimasEscalacoes.Any(e => e.FaseEscalacao == "FINAL"))
                            ultimasEscalacoes = ultimasEscalacoes.Where(e => e.FaseEscalacao == "FINAL").ToList();
                        else
                            ultimasEscalacoes = ultimasEscalacoes.Where(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null).ToList();

                        // Só aproveita se houver ao menos um jogador atribuído (slot não vazio)
                        bool temJogadores = ultimasEscalacoes.Any(e => e.JogadorId != null);

                        if (temJogadores)
                        {
                            novasCasa = ultimasEscalacoes.Select(e => new Escalacao
                            {
                                JogoId = id,
                                JogadorId = e.JogadorId,
                                Posicao = e.Posicao,
                                PosicaoX = e.PosicaoX,
                                PosicaoY = e.PosicaoY,
                                IsTimeCasa = true,
                                Titular = e.Titular,
                                FaseEscalacao = faseAtual
                            }).ToList();

                            if (idFormacaoCasa == 0)
                                idFormacaoCasa = (eraTimeCasaAnterior
                                    ? jogoAnterior.FormacaoCasaId
                                    : jogoAnterior.FormacaoVisitanteId) ?? 0;
                        }
                    }
                }

                // 2) Escalação padrão do time
                if (novasCasa.Count == 0)
                {
                    var padrao = await _context.TimeEscalacaoPadrao
                        .Where(t => t.TimeId == jogo.TimeCasaId)
                        .ToListAsync();

                    if (padrao.Any())
                    {
                        novasCasa = padrao.Select(p => new Escalacao
                        {
                            JogoId = id,
                            JogadorId = p.JogadorId,
                            Posicao = p.Posicao,
                            PosicaoX = p.PosicaoX,
                            PosicaoY = p.PosicaoY,
                            IsTimeCasa = true,
                            Titular = true,  // escalação padrão = sempre titular
                            FaseEscalacao = faseAtual
                        }).ToList();

                        if (idFormacaoCasa == 0)
                            idFormacaoCasa = padrao.First().FormacaoId;
                    }
                }

                // 3) Formação em branco
                if (novasCasa.Count == 0 && idFormacaoCasa > 0)
                {
                    var posicoes = await _context.PosicoesFormacao
                        .Where(p => p.FormacaoId == idFormacaoCasa)
                        .ToListAsync();

                    novasCasa = posicoes.Select(pos => new Escalacao
                    {
                        JogoId = id,
                        Posicao = pos.NomePosicao,
                        PosicaoX = pos.PosicaoX,
                        PosicaoY = pos.PosicaoY,
                        IsTimeCasa = true,
                        Titular = true,
                        FaseEscalacao = faseAtual
                    }).ToList();
                }

                _context.Escalacoes.AddRange(novasCasa);
                if (idFormacaoCasa > 0) jogo.FormacaoCasaId = idFormacaoCasa;
            }

            // ── VISITANTE ─────────────────────────────────────────────────────
            var idFormacaoVisitante = formacaoVisitanteId ?? jogo.FormacaoVisitanteId ?? 0;
            bool formacaoVisitanteMudou = formacaoVisitanteId.HasValue && formacaoVisitanteId.Value != (jogo.FormacaoVisitanteId ?? 0);

            if (escalacoesVisitante.Count == 0 || formacaoVisitanteMudou)
            {
                if (escalacoesVisitante.Count > 0)
                    _context.Escalacoes.RemoveRange(escalacoesVisitante);

                List<Escalacao> novasVisitante = new();

                // 1) Última escalação do time visitante em outro jogo
                if (!formacaoVisitanteMudou)
                {
                    var jogoAnterior = await _context.Jogos
                        .Where(j => j.Id != id
                                 && (j.TimeCasaId == jogo.TimeVisitanteId || j.TimeVisitanteId == jogo.TimeVisitanteId))
                        .OrderByDescending(j => j.Data)
                        .FirstOrDefaultAsync();

                    if (jogoAnterior != null)
                    {
                        bool eraTimeCasaAnterior = jogoAnterior.TimeCasaId == jogo.TimeVisitanteId;

                        var ultimasEscalacoes = await _context.Escalacoes
                            .Where(e => e.JogoId == jogoAnterior.Id
                                     && e.IsTimeCasa == eraTimeCasaAnterior
                                     && (e.FaseEscalacao == "FINAL" || e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                            .ToListAsync();

                        if (ultimasEscalacoes.Any(e => e.FaseEscalacao == "FINAL"))
                            ultimasEscalacoes = ultimasEscalacoes.Where(e => e.FaseEscalacao == "FINAL").ToList();
                        else
                            ultimasEscalacoes = ultimasEscalacoes.Where(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null).ToList();

                        // Só aproveita se houver ao menos um jogador atribuído (slot não vazio)
                        bool temJogadores = ultimasEscalacoes.Any(e => e.JogadorId != null);

                        if (temJogadores)
                        {
                            novasVisitante = ultimasEscalacoes.Select(e => new Escalacao
                            {
                                JogoId = id,
                                JogadorId = e.JogadorId,
                                Posicao = e.Posicao,
                                PosicaoX = e.PosicaoX,
                                PosicaoY = e.PosicaoY,
                                IsTimeCasa = false,
                                Titular = e.Titular,
                                FaseEscalacao = faseAtual
                            }).ToList();

                            if (idFormacaoVisitante == 0)
                                idFormacaoVisitante = (eraTimeCasaAnterior
                                    ? jogoAnterior.FormacaoCasaId
                                    : jogoAnterior.FormacaoVisitanteId) ?? 0;
                        }
                    }
                }

                // 2) Escalação padrão do time
                if (novasVisitante.Count == 0)
                {
                    var padrao = await _context.TimeEscalacaoPadrao
                        .Where(t => t.TimeId == jogo.TimeVisitanteId)
                        .ToListAsync();

                    if (padrao.Any())
                    {
                        novasVisitante = padrao.Select(p => new Escalacao
                        {
                            JogoId = id,
                            JogadorId = p.JogadorId,
                            Posicao = p.Posicao,
                            PosicaoX = p.PosicaoX,
                            PosicaoY = p.PosicaoY,
                            IsTimeCasa = false,
                            Titular = true,  // escalação padrão = sempre titular
                            FaseEscalacao = faseAtual
                        }).ToList();

                        if (idFormacaoVisitante == 0)
                            idFormacaoVisitante = padrao.First().FormacaoId;
                    }
                }

                // 3) Formação em branco
                if (novasVisitante.Count == 0 && idFormacaoVisitante > 0)
                {
                    var posicoes = await _context.PosicoesFormacao
                        .Where(p => p.FormacaoId == idFormacaoVisitante)
                        .ToListAsync();

                    novasVisitante = posicoes.Select(pos => new Escalacao
                    {
                        JogoId = id,
                        Posicao = pos.NomePosicao,
                        PosicaoX = pos.PosicaoX,
                        PosicaoY = pos.PosicaoY,
                        IsTimeCasa = false,
                        Titular = true,
                        FaseEscalacao = faseAtual
                    }).ToList();
                }

                _context.Escalacoes.AddRange(novasVisitante);
                if (idFormacaoVisitante > 0) jogo.FormacaoVisitanteId = idFormacaoVisitante;
            }

            await _context.SaveChangesAsync();

            // Recarrega escalações finais
            escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null)))
                .ToListAsync();

            var ordemPosicoes = new Dictionary<string, int>
            {
                { "GL", 1 }, { "LD", 2 }, { "LE", 3 },
                { "ZG", 4 }, { "MC", 5 }, { "AT", 6 }
            };

            ViewBag.EscalacoesCasa = escalacoes
                .Where(e => e.IsTimeCasa && e.Titular)
                .OrderBy(e => ordemPosicoes.ContainsKey(e.Posicao) ? ordemPosicoes[e.Posicao] : 99)
                .ToList();

            ViewBag.EscalacoesVisitante = escalacoes
                .Where(e => !e.IsTimeCasa && e.Titular)
                .OrderBy(e => ordemPosicoes.ContainsKey(e.Posicao) ? ordemPosicoes[e.Posicao] : 99)
                .ToList();

            ViewBag.ReservasCasa = escalacoes.Where(e => e.IsTimeCasa && !e.Titular).ToList();
            ViewBag.ReservasVisitante = escalacoes.Where(e => !e.IsTimeCasa && !e.Titular).ToList();

            ViewBag.FormacoesCasa = new SelectList(_context.Formacoes, "Id", "Nome", idFormacaoCasa);
            ViewBag.FormacoesVisitante = new SelectList(_context.Formacoes, "Id", "Nome", idFormacaoVisitante);

            if (faseAtual == "FINAL")
            {
                var escalacoesIniciais = await _context.Escalacoes
                    .Include(e => e.Jogador)
                    .Where(e => e.JogoId == id && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                    .ToListAsync();

                var titularesIniciaisCasa = escalacoesIniciais
                    .Where(e => e.IsTimeCasa && e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value)
                    .ToHashSet();
                var titularesIniciaisVisitante = escalacoesIniciais
                    .Where(e => !e.IsTimeCasa && e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value)
                    .ToHashSet();

                var titularesFinaisCasa = escalacoes
                    .Where(e => e.IsTimeCasa && e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value)
                    .ToHashSet();
                var titularesFinaisVisitante = escalacoes
                    .Where(e => !e.IsTimeCasa && e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value)
                    .ToHashSet();

                var entrouCasa = titularesFinaisCasa.Except(titularesIniciaisCasa).ToHashSet();
                var entrouVisitante = titularesFinaisVisitante.Except(titularesIniciaisVisitante).ToHashSet();
                var saiuCasa = titularesIniciaisCasa.Except(titularesFinaisCasa).ToHashSet();
                var saiuVisitante = titularesIniciaisVisitante.Except(titularesFinaisVisitante).ToHashSet();

                ViewBag.JogadoresEntraramCasa = entrouCasa.ToList();
                ViewBag.JogadoresEntraramVisitante = entrouVisitante.ToList();
                ViewBag.JogadoresSairamCasa = saiuCasa.ToList();
                ViewBag.JogadoresSairamVisitante = saiuVisitante.ToList();

                var reservasIniciaisCasa = escalacoesIniciais
                    .Where(e => e.IsTimeCasa && !e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value)
                    .ToHashSet();
                var reservasIniciaisVisitante = escalacoesIniciais
                    .Where(e => !e.IsTimeCasa && !e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value)
                    .ToHashSet();

                var listaFinalCasaIds = reservasIniciaisCasa.Union(saiuCasa).ToHashSet();
                var listaFinalVisitanteIds = reservasIniciaisVisitante.Union(saiuVisitante).ToHashSet();

                var jogadoresCasaFinal = await _context.Jogadores
                    .Where(j => listaFinalCasaIds.Contains(j.Id))
                    .ToListAsync();

                var jogadoresVisitanteFinal = await _context.Jogadores
                    .Where(j => listaFinalVisitanteIds.Contains(j.Id))
                    .ToListAsync();

                ViewBag.JogadoresCasa = jogadoresCasaFinal
                    .OrderBy(j => ObterOrdemPosicao(j.Posicao))
                    .ThenBy(j => j.Nome)
                    .ToList();

                ViewBag.JogadoresVisitante = jogadoresVisitanteFinal
                    .OrderBy(j => ObterOrdemPosicao(j.Posicao))
                    .ThenBy(j => j.Nome)
                    .ToList();
            }
            else
            {
                ViewBag.JogadoresCasa = _context.Jogadores.Where(j => j.TimeId == jogo.TimeCasaId).ToList();
                ViewBag.JogadoresVisitante = _context.Jogadores.Where(j => j.TimeId == jogo.TimeVisitanteId).ToList();
            }

            ViewBag.FormacaoCasaSelecionada = idFormacaoCasa;
            ViewBag.FormacaoVisitanteSelecionada = idFormacaoVisitante;
            ViewBag.FaseEscalacaoAtual = faseAtual;
            ViewBag.EscalacaoFinalDisponivel = await _context.Escalacoes.AnyAsync(e => e.JogoId == id && e.FaseEscalacao == "FINAL");
            ViewBag.MostrarBancoReservas = faseAtual == "INICIAL";

            return View(jogo);
        }

        [HttpPost]
        public async Task<IActionResult> SalvarEscalacao(
            int id,
            int formacaoCasaId,
            int formacaoVisitanteId,
            string? faseEscalacao,
            string? observacoesComTags,
            List<EscalacaoInput> escalacaoCasa,
            List<EscalacaoInput> escalacaoVisitante,
            List<EscalacaoInput> reservasCasa,
            List<EscalacaoInput> reservasVisitante)
        {
            var faseAtual = string.Equals(faseEscalacao, "FINAL", StringComparison.OrdinalIgnoreCase) ? "FINAL" : "INICIAL";

            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null)))
                .ToListAsync();

            void AtualizarSlots(List<EscalacaoInput> inputs, bool isTimeCasa)
            {
                if (inputs == null) return;
                foreach (var e in inputs)
                {
                    var esc = escalacoes.FirstOrDefault(x => x.Id == e.Id);
                    if (esc == null) continue;
                    esc.PosicaoX = e.PosicaoX;
                    esc.PosicaoY = e.PosicaoY;
                    esc.JogadorId = e.JogadorId > 0 ? e.JogadorId : null;
                    esc.Titular = true;
                }
            }

            AtualizarSlots(escalacaoCasa, true);
            AtualizarSlots(escalacaoVisitante, false);

            // Remove reservas antigas e recria
            var reservasAntigas = escalacoes.Where(e => !e.Titular).ToList();
            _context.Escalacoes.RemoveRange(reservasAntigas);

            void AdicionarReservas(List<EscalacaoInput> inputs, bool isTimeCasa)
            {
                if (inputs == null) return;
                foreach (var e in inputs.Where(e => e.JogadorId > 0))
                    _context.Escalacoes.Add(new Escalacao
                    {
                        JogoId = id,
                        JogadorId = e.JogadorId,
                        Posicao = "RES",
                        PosicaoX = 0,
                        PosicaoY = 0,
                        IsTimeCasa = isTimeCasa,
                        Titular = false,
                        FaseEscalacao = faseAtual
                    });
            }

            AdicionarReservas(reservasCasa, true);
            AdicionarReservas(reservasVisitante, false);

            // Persiste as formações selecionadas no jogo
            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo != null)
            {
                if (formacaoCasaId > 0) jogo.FormacaoCasaId = formacaoCasaId;
                if (formacaoVisitanteId > 0) jogo.FormacaoVisitanteId = formacaoVisitanteId;
                if (faseAtual == "FINAL")
                    jogo.Observacoes = string.IsNullOrWhiteSpace(observacoesComTags) ? null : observacoesComTags.Trim();
            }

            if (faseAtual == "INICIAL")
            {
                var finalExiste = await _context.Escalacoes.AnyAsync(e => e.JogoId == id && e.FaseEscalacao == "FINAL");
                if (!finalExiste)
                {
                    var baseInicial = await _context.Escalacoes
                        .Where(e => e.JogoId == id && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                        .ToListAsync();

                    var cloneFinal = baseInicial.Select(e => new Escalacao
                    {
                        JogoId = e.JogoId,
                        JogadorId = e.JogadorId,
                        Titular = e.Titular,
                        Posicao = e.Posicao,
                        IsTimeCasa = e.IsTimeCasa,
                        PosicaoX = e.PosicaoX,
                        PosicaoY = e.PosicaoY,
                        FaseEscalacao = "FINAL"
                    });

                    _context.Escalacoes.AddRange(cloneFinal);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Analisar", new { id, faseEscalacao = faseAtual });
        }

        private static int ObterOrdemPosicao(string? posicao)
        {
            var p = (posicao ?? string.Empty).Trim().ToLowerInvariant();

            if (p.Contains("gol")) return 1;
            if (p.Contains("zag") || p.Contains("def") || p.Contains("lat")) return 2;
            if (p.Contains("mei") || p.Contains("vol")) return 3;
            if (p.Contains("ata") || p.Contains("ponta") || p.Contains("centro")) return 4;
            return 5;
        }

        // ── LimparEscalacoes ────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimparEscalacoes(int id, string? faseEscalacao)
        {
            var faseAtual = string.Equals(faseEscalacao, "FINAL", StringComparison.OrdinalIgnoreCase) ? "FINAL" : "INICIAL";

            // Remove todas as escalações do jogo
            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null)))
                .ToListAsync();
            _context.Escalacoes.RemoveRange(escalacoes);

            // Zera as formações salvas no jogo para forçar nova escolha
            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo != null)
            {
                jogo.FormacaoCasaId = null;
                jogo.FormacaoVisitanteId = null;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Analisar", new { id, faseEscalacao = faseAtual });
        }
    }
}
