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

            // Dropdowns de formações
            ViewBag.FormacoesCasa = new SelectList(_context.Formacoes, "Id", "Nome", formacaoCasaId ?? jogo.FormacaoCasaId);
            ViewBag.FormacoesVisitante = new SelectList(_context.Formacoes, "Id", "Nome", formacaoVisitanteId ?? jogo.FormacaoVisitanteId);

            // Jogadores disponíveis
            ViewBag.JogadoresCasa = _context.Jogadores.Where(j => j.TimeId == jogo.TimeCasaId).ToList();
            ViewBag.JogadoresVisitante = _context.Jogadores.Where(j => j.TimeId == jogo.TimeVisitanteId).ToList();

            // Escalações salvas
            var escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id)
                .ToListAsync();

            var escalacoesCasa = escalacoes.Where(e => e.IsTimeCasa).ToList();
            var escalacoesVisitante = escalacoes.Where(e => !e.IsTimeCasa).ToList();

            // ── Casa: recria se formação mudou ou não existe ──────────────────────
            var idFormacaoCasa = formacaoCasaId ?? jogo.FormacaoCasaId ?? 0;
            bool formacaoCasaMudou = escalacoesCasa.Count == 0 ||
                                     (formacaoCasaId.HasValue && formacaoCasaId != jogo.FormacaoCasaId);

            if (formacaoCasaMudou && idFormacaoCasa > 0)
            {
                _context.Escalacoes.RemoveRange(escalacoesCasa);

                var posicoesCasa = await _context.PosicoesFormacao
                    .Where(p => p.FormacaoId == idFormacaoCasa)
                    .ToListAsync();

                foreach (var pos in posicoesCasa)
                {
                    _context.Escalacoes.Add(new Escalacao
                    {
                        JogoId = id,
                        Posicao = pos.NomePosicao,
                        PosicaoX = (int)pos.PosicaoX,
                        PosicaoY = (int)pos.PosicaoY,
                        IsTimeCasa = true,
                        Titular = true
                    });
                }

                jogo.FormacaoCasaId = idFormacaoCasa;
            }

            // ── Visitante: recria se formação mudou ou não existe ─────────────────
            var idFormacaoVisitante = formacaoVisitanteId ?? jogo.FormacaoVisitanteId ?? 0;
            bool formacaoVisitanteMudou = escalacoesVisitante.Count == 0 ||
                                          (formacaoVisitanteId.HasValue && formacaoVisitanteId != jogo.FormacaoVisitanteId);

            if (formacaoVisitanteMudou && idFormacaoVisitante > 0)
            {
                _context.Escalacoes.RemoveRange(escalacoesVisitante);

                var posicoesVisitante = await _context.PosicoesFormacao
                    .Where(p => p.FormacaoId == idFormacaoVisitante)
                    .ToListAsync();

                foreach (var pos in posicoesVisitante)
                {
                    _context.Escalacoes.Add(new Escalacao
                    {
                        JogoId = id,
                        Posicao = pos.NomePosicao,
                        PosicaoX = (int)pos.PosicaoX,
                        PosicaoY = (int)pos.PosicaoY,
                        IsTimeCasa = false,
                        Titular = true
                    });
                }

                jogo.FormacaoVisitanteId = idFormacaoVisitante;
            }

            await _context.SaveChangesAsync();

            // Recarrega escalações atualizadas
            escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id)
                .ToListAsync();

            // Ordem customizada das posições
            var ordemPosicoes = new Dictionary<string, int>
    {
        { "GL", 1 }, { "LD", 2 }, { "LE", 3 },
        { "ZG", 4 }, { "MC", 5 }, { "AT", 6 }
    };

            ViewBag.EscalacoesCasa = escalacoes
                .Where(e => e.IsTimeCasa)
                .OrderBy(e => ordemPosicoes.ContainsKey(e.Posicao) ? ordemPosicoes[e.Posicao] : 99)
                .ToList();

            ViewBag.EscalacoesVisitante = escalacoes
                .Where(e => !e.IsTimeCasa)
                .OrderBy(e => ordemPosicoes.ContainsKey(e.Posicao) ? ordemPosicoes[e.Posicao] : 99)
                .ToList();

            ViewBag.FormacaoCasaSelecionada = formacaoCasaId ?? jogo.FormacaoCasaId;
            ViewBag.FormacaoVisitanteSelecionada = formacaoVisitanteId ?? jogo.FormacaoVisitanteId;

            return View(jogo);
        }



        [HttpPost]
        public async Task<IActionResult> SalvarEscalacao(
        int id,
        int formacaoCasaId,
        int formacaoVisitanteId,
        List<EscalacaoInput> escalacaoCasa,
        List<EscalacaoInput> escalacaoVisitante)
            {
            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id)
                .ToListAsync();

            void AtualizarSlots(List<EscalacaoInput> inputs)
            {
                if (inputs == null) return;
                foreach (var e in inputs)
                {
                    var esc = escalacoes.FirstOrDefault(x => x.Id == e.Id);
                    if (esc == null) continue;

                    esc.PosicaoX = e.PosicaoX;
                    esc.PosicaoY = e.PosicaoY;
                    esc.JogadorId = e.JogadorId > 0 ? e.JogadorId : null;
                }
            }

            AtualizarSlots(escalacaoCasa);
            AtualizarSlots(escalacaoVisitante);

            await _context.SaveChangesAsync();
            return RedirectToAction("Analisar", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> LimparEscalacoes(int id)
        {
            var escalacoes = _context.Escalacoes.Where(e => e.JogoId == id);
            _context.Escalacoes.RemoveRange(escalacoes);
            await _context.SaveChangesAsync();

            // Depois de limpar, redireciona de volta para a página de análise
            return RedirectToAction("Analisar", new { id });
        }

    }
}