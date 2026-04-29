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
        public async Task<IActionResult> Index(int? teamId, string? location, DateTime? startDate, DateTime? endDate)
        {
            var jogosQuery = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Gols).ThenInclude(g => g.Jogador)
                .Include(j => j.Escalacoes).ThenInclude(e => e.Jogador)
                .Include(j => j.Cartoes).ThenInclude(c => c.Jogador)
                .AsQueryable();

            // Filtro por time
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

            // Filtro por datas
            if (startDate.HasValue)
                jogosQuery = jogosQuery.Where(j => j.Data >= startDate.Value);
            if (endDate.HasValue)
                jogosQuery = jogosQuery.Where(j => j.Data <= endDate.Value);

            ViewBag.TimeList = new SelectList(_context.Times.OrderBy(t => t.Nome).ToList(), "Id", "Nome", teamId);
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

            if (jogo == null)
            {
                return NotFound();
            }

            return View(jogo);
        }


        // GET: Jogos/Create
        public IActionResult Create()
        {
            ViewBag.TimeCasaId = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.TimeVisitanteId = new SelectList(_context.Times, "Id", "Nome");

            // Dropdown de formações
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

                // Carrega as formações escolhidas
                var formacaoCasa = await _context.Formacoes
                    .Include(f => f.Posicoes)
                    .FirstOrDefaultAsync(f => f.Id == jogo.FormacaoCasaId);

                var formacaoVisitante = await _context.Formacoes
                    .Include(f => f.Posicoes)
                    .FirstOrDefaultAsync(f => f.Id == jogo.FormacaoVisitanteId);

                // Gera escalações automáticas
                if (formacaoCasa != null)
                {
                    foreach (var pos in formacaoCasa.Posicoes)
                    {
                        _context.Escalacoes.Add(new Escalacao
                        {
                            JogoId = jogo.Id,
                            Titular = true,
                            Posicao = pos.NomePosicao,
                            PosicaoX = pos.PosicaoX,
                            PosicaoY = pos.PosicaoY,
                            IsTimeCasa = true
                        });
                    }
                }

                if (formacaoVisitante != null)
                {
                    foreach (var pos in formacaoVisitante.Posicoes)
                    {
                        _context.Escalacoes.Add(new Escalacao
                        {
                            JogoId = jogo.Id,
                            Titular = true,
                            Posicao = pos.NomePosicao,
                            PosicaoX = pos.PosicaoX,
                            PosicaoY = pos.PosicaoY,
                            IsTimeCasa = false
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Se der erro de validação, recarrega dropdowns
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

            // Força o tipo de data para evitar erro de binding
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
                    if (!_context.Jogos.Any(e => e.Id == jogo.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            // Recarrega dropdowns se der erro
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

            // Atualiza escalações existentes
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

            // Adiciona novo jogador se informado
            if (novoJogadorId.HasValue)
            {
                _context.Escalacoes.Add(new Escalacao
                {
                    JogoId = id,
                    JogadorId = novoJogadorId.Value,
                    Posicao = novaPosicao,
                    Titular = novoTitular,
                    IsTimeCasa = novoIsTimeCasa
                });
            }

            _context.SaveChanges();
            return RedirectToAction("Details", new { id });
        }


        // Analisa tática do jogo
        public async Task<IActionResult> Analisar(int id, int? formacaoCasaId, int? formacaoVisitanteId)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            // Escalações já salvas para este jogo
            var escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id)
                .ToListAsync();

            var escalacoesCasa = escalacoes.Where(e => e.IsTimeCasa).ToList();
            var escalacoesVisitante = escalacoes.Where(e => !e.IsTimeCasa).ToList();

            // ── CASA ─────────────────────────────────────────────────────────────
            var idFormacaoCasa = formacaoCasaId ?? jogo.FormacaoCasaId ?? 0;
            bool formacaoCasaMudou = formacaoCasaId.HasValue && formacaoCasaId != jogo.FormacaoCasaId;

            if (escalacoesCasa.Count == 0 || formacaoCasaMudou)
            {
                if (escalacoesCasa.Count > 0)
                    _context.Escalacoes.RemoveRange(escalacoesCasa);

                List<Escalacao> novasCasa = new();

                // 1) Última escalação do time em outro jogo
                if (!formacaoCasaMudou)
                {
                    var ultimoJogoCasa = await _context.Jogos
                        .Where(j => j.Id != id && (j.TimeCasaId == jogo.TimeCasaId || j.TimeVisitanteId == jogo.TimeCasaId))
                        .OrderByDescending(j => j.Data)
                        .Select(j => j.Id)
                        .FirstOrDefaultAsync();

                    if (ultimoJogoCasa > 0)
                    {
                        var ultimasEscalacoes = await _context.Escalacoes
                            .Where(e => e.JogoId == ultimoJogoCasa &&
                                        ((e.IsTimeCasa && _context.Jogos.Any(j => j.Id == ultimoJogoCasa && j.TimeCasaId == jogo.TimeCasaId))
                                      || (!e.IsTimeCasa && _context.Jogos.Any(j => j.Id == ultimoJogoCasa && j.TimeVisitanteId == jogo.TimeCasaId))))
                            .ToListAsync();

                        novasCasa = ultimasEscalacoes.Select(e => new Escalacao
                        {
                            JogoId = id,
                            JogadorId = e.JogadorId,
                            Posicao = e.Posicao,
                            PosicaoX = e.PosicaoX,
                            PosicaoY = e.PosicaoY,
                            IsTimeCasa = true,
                            Titular = e.Titular
                        }).ToList();

                        // Pega a formação que estava salva naquele jogo
                        if (idFormacaoCasa == 0)
                        {
                            var jogoAnterior = await _context.Jogos.FindAsync(ultimoJogoCasa);
                            bool eraTimeCasa = jogoAnterior?.TimeCasaId == jogo.TimeCasaId;
                            idFormacaoCasa = (eraTimeCasa ? jogoAnterior?.FormacaoCasaId : jogoAnterior?.FormacaoVisitanteId) ?? 0;
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
                            Titular = p.Titular
                        }).ToList();

                        if (idFormacaoCasa == 0)
                            idFormacaoCasa = padrao.First().FormacaoId;
                    }
                }

                // 3) Formação em branco (cria slots vazios)
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
                        Titular = true
                    }).ToList();
                }

                _context.Escalacoes.AddRange(novasCasa);
                jogo.FormacaoCasaId = idFormacaoCasa > 0 ? idFormacaoCasa : jogo.FormacaoCasaId;
            }

            // ── VISITANTE ─────────────────────────────────────────────────────────
            var idFormacaoVisitante = formacaoVisitanteId ?? jogo.FormacaoVisitanteId ?? 0;
            bool formacaoVisitanteMudou = formacaoVisitanteId.HasValue && formacaoVisitanteId != jogo.FormacaoVisitanteId;

            if (escalacoesVisitante.Count == 0 || formacaoVisitanteMudou)
            {
                if (escalacoesVisitante.Count > 0)
                    _context.Escalacoes.RemoveRange(escalacoesVisitante);

                List<Escalacao> novasVisitante = new();

                // 1) Última escalação do time em outro jogo
                if (!formacaoVisitanteMudou)
                {
                    var ultimoJogoVis = await _context.Jogos
                        .Where(j => j.Id != id && (j.TimeCasaId == jogo.TimeVisitanteId || j.TimeVisitanteId == jogo.TimeVisitanteId))
                        .OrderByDescending(j => j.Data)
                        .Select(j => j.Id)
                        .FirstOrDefaultAsync();

                    if (ultimoJogoVis > 0)
                    {
                        var ultimasEscalacoes = await _context.Escalacoes
                            .Where(e => e.JogoId == ultimoJogoVis &&
                                        ((e.IsTimeCasa && _context.Jogos.Any(j => j.Id == ultimoJogoVis && j.TimeCasaId == jogo.TimeVisitanteId))
                                      || (!e.IsTimeCasa && _context.Jogos.Any(j => j.Id == ultimoJogoVis && j.TimeVisitanteId == jogo.TimeVisitanteId))))
                            .ToListAsync();

                        novasVisitante = ultimasEscalacoes.Select(e => new Escalacao
                        {
                            JogoId = id,
                            JogadorId = e.JogadorId,
                            Posicao = e.Posicao,
                            PosicaoX = e.PosicaoX,
                            PosicaoY = e.PosicaoY,
                            IsTimeCasa = false,
                            Titular = e.Titular
                        }).ToList();

                        if (idFormacaoVisitante == 0)
                        {
                            var jogoAnterior = await _context.Jogos.FindAsync(ultimoJogoVis);
                            bool eraTimeCasa = jogoAnterior?.TimeCasaId == jogo.TimeVisitanteId;
                            idFormacaoVisitante = (eraTimeCasa ? jogoAnterior?.FormacaoCasaId : jogoAnterior?.FormacaoVisitanteId) ?? 0;
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
                            Titular = p.Titular
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
                        Titular = true
                    }).ToList();
                }

                _context.Escalacoes.AddRange(novasVisitante);
                jogo.FormacaoVisitanteId = idFormacaoVisitante > 0 ? idFormacaoVisitante : jogo.FormacaoVisitanteId;
            }

            await _context.SaveChangesAsync();

            // Recarrega escalações finais
            escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id)
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

            ViewBag.ReservasCasa = escalacoes
                .Where(e => e.IsTimeCasa && !e.Titular)
                .ToList();

            ViewBag.ReservasVisitante = escalacoes
                .Where(e => !e.IsTimeCasa && !e.Titular)
                .ToList();

            ViewBag.FormacoesCasa = new SelectList(_context.Formacoes, "Id", "Nome", idFormacaoCasa);
            ViewBag.FormacoesVisitante = new SelectList(_context.Formacoes, "Id", "Nome", idFormacaoVisitante);
            ViewBag.JogadoresCasa = _context.Jogadores.Where(j => j.TimeId == jogo.TimeCasaId).ToList();
            ViewBag.JogadoresVisitante = _context.Jogadores.Where(j => j.TimeId == jogo.TimeVisitanteId).ToList();
            ViewBag.FormacaoCasaSelecionada = idFormacaoCasa;
            ViewBag.FormacaoVisitanteSelecionada = idFormacaoVisitante;

            return View(jogo);
        }


        [HttpPost]
        public async Task<IActionResult> SalvarEscalacao(
        int id,
        int formacaoCasaId,
        int formacaoVisitanteId,
        List<EscalacaoInput> escalacaoCasa,
        List<EscalacaoInput> escalacaoVisitante,
        List<EscalacaoInput> reservasCasa,       // ← novo
        List<EscalacaoInput> reservasVisitante)  // ← novo
        {
            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id)
                .ToListAsync();

            // Atualiza titulares (lógica existente)
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
                {
                    _context.Escalacoes.Add(new Escalacao
                    {
                        JogoId = id,
                        JogadorId = e.JogadorId,
                        Posicao = "RES",
                        PosicaoX = 0,
                        PosicaoY = 0,
                        IsTimeCasa = isTimeCasa,
                        Titular = false
                    });
                }
            }

            AdicionarReservas(reservasCasa, true);
            AdicionarReservas(reservasVisitante, false);

            await _context.SaveChangesAsync();
            return RedirectToAction("Analisar", new { id });
        }

    }
}