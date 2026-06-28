using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
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
        private readonly ApiFootballService _transfermarkt;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosController(FutebolContext context, ILogger<JogosController> logger, ApiFootballService transfermarkt, IServiceScopeFactory scopeFactory, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _transfermarkt = transfermarkt;
            _scopeFactory = scopeFactory;
            _userManager = userManager;
        }

        // GET: Jogos/Hoje
        public async Task<IActionResult> Hoje(DateTime? data = null)
        {
            var uid = _userManager.GetUserId(User);

            // Jogos ficam em UTC no banco. Converte o dia escolhido (no fuso do Brasil, UTC-3) para UTC
            // para não perder jogos das primeiras horas da manhã (ex: 00:00 BRT = 03:00 UTC)
            var fusoHorarioBrasil = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var agoraBrasil = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fusoHorarioBrasil);

            // Dia exibido: o informado (?data=yyyy-MM-dd) ou hoje no Brasil
            var diaBrasil = (data?.Date) ?? agoraBrasil.Date;
            var fimDiaBrasil = diaBrasil.AddDays(1);
            var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(diaBrasil, DateTimeKind.Unspecified), fusoHorarioBrasil);
            var fimUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fimDiaBrasil, DateTimeKind.Unspecified), fusoHorarioBrasil);

            var jogos = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .Where(j => j.Data >= inicioUtc && j.Data < fimUtc)
                .OrderBy(j => j.Data)
                .ToListAsync();

            var jogosAnalisadosIds = uid != null
                ? await _context.JogosAnalisadosUsuario
                    .Where(j => j.UsuarioId == uid)
                    .Select(j => j.JogoId)
                    .ToHashSetAsync()
                : new HashSet<int>();

            ViewBag.JogosAnalisadosIds = jogosAnalisadosIds;
            ViewBag.DiaAtual = diaBrasil;
            ViewBag.DiaAnterior = diaBrasil.AddDays(-1);
            ViewBag.DiaSeguinte = diaBrasil.AddDays(1);
            ViewBag.EhHoje = diaBrasil == agoraBrasil.Date;
            return View(jogos);
        }

        // GET: Jogos
        public async Task<IActionResult> Index(
            int? teamId,
            string? location,
            DateTime? startDate,
            DateTime? endDate,
            int? competicaoId,
            string? status,
            int page = 1)
        {
            // A listagem só exibe data, competição, times e placar. Gols/Escalações/
            // Cartões não são usados aqui — incluí-los carregava milhares de linhas
            // por jogo e deixava a página lenta. AsNoTracking pois é somente leitura.
            var jogosQuery = _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
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

            var timeList = new SelectList(_context.Times.OrderBy(t => t.Nome).ToList(), "Id", "Nome", teamId);
            var uidJogos = _userManager.GetUserId(User)!;
            var topTierIdsJogos = _context.CompeticoesTopTierUsuario
                .Where(t => t.UsuarioId == uidJogos).Select(t => t.CompeticaoId).ToHashSet();
            var competicoesOrdenadas = _context.Competicoes
                .OrderBy(c => c.Nome).ToList()
                .OrderByDescending(c => topTierIdsJogos.Contains(c.Id))
                .ThenBy(c => c.Nome)
                .ToList();
            var competicaoList = new SelectList(competicoesOrdenadas, "Id", "Nome", competicaoId);
            var statusList = new SelectList(
                new[] {
                    new { Value = "all", Text = "Todos" },
                    new { Value = "played", Text = "Realizados" },
                    new { Value = "scheduled", Text = "Não realizados" }
                },
                "Value", "Text", status ?? "all"
            );
            var locationList = new SelectList(
                new[] {
                    new { Value = "both", Text = "Casa ou Fora" },
                    new { Value = "home", Text = "Apenas Time da Casa" },
                    new { Value = "away", Text = "Apenas Time Visitante" }
                },
                "Value", "Text", location ?? "both"
            );

            jogosQuery = jogosQuery.OrderByDescending(j => j.Data);

            const int pageSize = 50;
            var totalJogos = await jogosQuery.CountAsync();
            var totalFinalizados = await jogosQuery
                .CountAsync(j => j.PlacarCasa >= 0 && j.PlacarVisitante >= 0);
            var totalPaginas = (int)Math.Ceiling(totalJogos / (double)pageSize);
            if (page < 1) page = 1;
            if (totalPaginas > 0 && page > totalPaginas) page = totalPaginas;

            var jogosPagina = await jogosQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var competicoesMap = await _context.Competicoes
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Id, c => c.Nome);

            var vm = new JogosIndexViewModel
            {
                Jogos = jogosPagina,
                TimeList = timeList,
                CompeticaoList = competicaoList,
                StatusList = statusList,
                LocationList = locationList,
                StartDate = startDate?.ToString("yyyy-MM-dd"),
                EndDate = endDate?.ToString("yyyy-MM-dd"),
                CompeticoesMap = competicoesMap,
                PaginaAtual = page,
                TotalPaginas = totalPaginas,
                TotalJogos = totalJogos,
                TotalFinalizados = totalFinalizados,
                PageSize = pageSize,
                TeamIdFiltro = teamId,
                LocationFiltro = location,
                CompeticaoIdFiltro = competicaoId,
                StatusFiltro = status
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> TimesPorCompeticao(int competicaoId)
        {
            var jogos = await _context.Jogos
                .Where(j => j.CompeticaoId == competicaoId)
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .AsNoTracking()
                .ToListAsync();

            var times = jogos
                .SelectMany(j => new[] {
                    new { id = j.TimeCasaId, nome = j.TimeCasa?.Nome },
                    new { id = j.TimeVisitanteId, nome = j.TimeVisitante?.Nome }
                })
                .Where(t => t.nome != null)
                .GroupBy(t => t.id)
                .Select(g => new { id = g.Key, nome = g.First().nome })
                .OrderBy(t => t.nome)
                .ToList();

            return Json(times);
        }

        // Times que participam de uma ou mais competições (união). Usado no filtro multi de Relatórios.
        public async Task<IActionResult> TimesPorCompeticoes([FromQuery] int[] competicaoIds)
        {
            var ids = (competicaoIds ?? Array.Empty<int>()).Where(i => i > 0).Distinct().ToList();

            var jogosQuery = _context.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .AsQueryable();

            if (ids.Any())
                jogosQuery = jogosQuery.Where(j => ids.Contains(j.CompeticaoId));

            var jogos = await jogosQuery.ToListAsync();

            var times = jogos
                .SelectMany(j => new[] {
                    new { id = j.TimeCasaId, nome = j.TimeCasa?.Nome },
                    new { id = j.TimeVisitanteId, nome = j.TimeVisitante?.Nome }
                })
                .Where(t => t.nome != null)
                .GroupBy(t => t.id)
                .Select(g => new { id = g.Key, nome = g.First().nome })
                .OrderBy(t => t.nome)
                .ToList();

            return Json(times);
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
            jogo.Data = jogo.Data.HasValue
            ? DateTime.SpecifyKind(jogo.Data.Value, DateTimeKind.Utc)
            : (DateTime?)null;


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

            jogo.Data = jogo.Data.HasValue ? DateTime.SpecifyKind(jogo.Data.Value, DateTimeKind.Utc) : (DateTime?)null;

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
            var usuarioId = _userManager.GetUserId(User)!;

            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            // Fases táticas (timeline de escalações intermediárias) para montar as abas.
            var fasesTaticas = await _context.FasesTaticas
                .Where(f => f.JogoId == id && f.UsuarioId == usuarioId)
                .OrderBy(f => f.Ordem)
                .ToListAsync();
            var vm = new AnalisarViewModel { Jogo = jogo, FasesTaticas = fasesTaticas };

            // ── Fase intermediária: renderização própria (somente visual/tática) ──
            if (!string.IsNullOrWhiteSpace(faseEscalacao) &&
                !string.Equals(faseEscalacao, "FINAL", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(faseEscalacao, "INICIAL", StringComparison.OrdinalIgnoreCase))
            {
                var faseInter = fasesTaticas.FirstOrDefault(f => f.Chave == faseEscalacao);
                if (faseInter != null)
                {
                    var escFase = await _context.Escalacoes
                        .Include(e => e.Jogador).ThenInclude(j => j!.Nacionalidade)
                        .Where(e => e.JogoId == id && e.UsuarioId == usuarioId && e.FaseEscalacao == faseInter.Chave)
                        .ToListAsync();

                    var ordemPos = new Dictionary<string, int>
                        { { "GL", 1 }, { "LD", 2 }, { "LE", 3 }, { "ZG", 4 }, { "MC", 5 }, { "AT", 6 } };
                    int Ord(string? p) => p != null && ordemPos.ContainsKey(p) ? ordemPos[p] : 99;

                    vm.EscalacoesCasa = escFase.Where(e => e.IsTimeCasa && e.Titular).OrderBy(e => Ord(e.Posicao)).ToList();
                    vm.EscalacoesVisitante = escFase.Where(e => !e.IsTimeCasa && e.Titular).OrderBy(e => Ord(e.Posicao)).ToList();
                    vm.ReservasCasa = new List<Escalacao>();
                    vm.ReservasVisitante = new List<Escalacao>();

                    vm.JogadoresCasa = await _context.Jogadores
                        .Where(j => j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId).ToListAsync();
                    vm.JogadoresVisitante = await _context.Jogadores
                        .Where(j => j.TimeId == jogo.TimeVisitanteId || j.SelecaoId == jogo.TimeVisitanteId).ToListAsync();

                    vm.FormacoesCasa = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoCasaId);
                    vm.FormacoesVisitante = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoVisitanteId);
                    vm.FormacaoCasaSelecionada = jogo.FormacaoCasaId;
                    vm.FormacaoVisitanteSelecionada = jogo.FormacaoVisitanteId;

                    vm.FaseEscalacaoAtual = faseInter.Chave;
                    vm.MostrarBancoReservas = false;
                    vm.EscalacaoFinalDisponivel = await _context.Escalacoes
                        .AnyAsync(e => e.JogoId == id && e.FaseEscalacao == "FINAL" && e.UsuarioId == usuarioId);

                    vm.TreinadorCasa = await _context.Treinadores.Include(t => t.Nacionalidade)
                        .Where(t => t.TimeId == jogo.TimeCasaId).OrderByDescending(t => t.DtInc).FirstOrDefaultAsync();
                    vm.TreinadorVisitante = await _context.Treinadores.Include(t => t.Nacionalidade)
                        .Where(t => t.TimeId == jogo.TimeVisitanteId).OrderByDescending(t => t.DtInc).FirstOrDefaultAsync();

                    return View(vm);
                }
            }

            var escalacoesFinaisExistem = await _context.Escalacoes
                .AnyAsync(e => e.JogoId == id && e.FaseEscalacao == "FINAL" && e.UsuarioId == usuarioId);

            if (faseAtual == "FINAL" && !escalacoesFinaisExistem)
            {
                var escalacoesIniciais = await _context.Escalacoes
                    .Where(e => e.JogoId == id && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null) && e.UsuarioId == usuarioId)
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
                        FaseEscalacao = "FINAL",
                        UsuarioId = usuarioId
                    }).ToList();

                    // Aplica as substituições já importadas: o jogador que entrou assume o
                    // lugar (posição em campo) de quem saiu, e quem saiu vai para o banco.
                    var substituicoes = await _context.Substituicoes
                        .Where(s => s.JogoId == id)
                        .OrderBy(s => s.Minuto)
                        .ToListAsync();

                    foreach (var sub in substituicoes)
                    {
                        if (sub.JogadorSaiuId == null) continue;

                        var slotSaiu = clonesFinais.FirstOrDefault(c =>
                            c.JogadorId == sub.JogadorSaiuId && c.IsTimeCasa == sub.IsTimeCasa && c.Titular);
                        var slotEntrou = clonesFinais.FirstOrDefault(c =>
                            c.JogadorId == sub.JogadorEntrouId && c.IsTimeCasa == sub.IsTimeCasa);

                        if (slotSaiu == null || slotEntrou == null) continue;

                        var jogadorIdTemp = slotSaiu.JogadorId;
                        slotSaiu.JogadorId = slotEntrou.JogadorId;
                        slotEntrou.JogadorId = jogadorIdTemp;
                    }

                    _context.Escalacoes.AddRange(clonesFinais);
                    await _context.SaveChangesAsync();
                }
            }

            // Escalações do usuário para esta fase
            var escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador).ThenInclude(j => j!.Nacionalidade)
                .Where(e => e.JogoId == id
                         && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null))
                         && e.UsuarioId == usuarioId)
                .ToListAsync();

            // Se não tiver escalações do usuário, copia das compartilhadas (importadas da API, UsuarioId == null)
            if (!escalacoes.Any())
            {
                var compartilhadas = await _context.Escalacoes
                    .Where(e => e.JogoId == id
                             && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null))
                             && e.UsuarioId == null)
                    .ToListAsync();

                if (compartilhadas.Any())
                {
                    var copias = compartilhadas.Select(e => new Escalacao
                    {
                        JogoId = e.JogoId,
                        JogadorId = e.JogadorId,
                        Titular = e.Titular,
                        Posicao = e.Posicao,
                        IsTimeCasa = e.IsTimeCasa,
                        PosicaoX = e.PosicaoX,
                        PosicaoY = e.PosicaoY,
                        FaseEscalacao = e.FaseEscalacao ?? "INICIAL",
                        UsuarioId = usuarioId
                    }).ToList();
                    _context.Escalacoes.AddRange(copias);
                    await _context.SaveChangesAsync();
                    escalacoes = await _context.Escalacoes
                        .Include(e => e.Jogador).ThenInclude(j => j!.Nacionalidade)
                        .Where(e => e.JogoId == id
                                 && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null))
                                 && e.UsuarioId == usuarioId)
                        .ToListAsync();
                }
            }

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
                                FaseEscalacao = faseAtual,
                                UsuarioId = usuarioId
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
                            Titular = true,
                            FaseEscalacao = faseAtual,
                            UsuarioId = usuarioId
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
                        FaseEscalacao = faseAtual,
                        UsuarioId = usuarioId
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
                                FaseEscalacao = faseAtual,
                                UsuarioId = usuarioId
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
                            Titular = true,
                            FaseEscalacao = faseAtual,
                            UsuarioId = usuarioId
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
                        FaseEscalacao = faseAtual,
                        UsuarioId = usuarioId
                    }).ToList();
                }

                _context.Escalacoes.AddRange(novasVisitante);
                if (idFormacaoVisitante > 0) jogo.FormacaoVisitanteId = idFormacaoVisitante;
            }

            await _context.SaveChangesAsync();

            // Recarrega escalações finais do usuário
            escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador).ThenInclude(j => j!.Nacionalidade)
                .Where(e => e.JogoId == id
                         && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null))
                         && e.UsuarioId == usuarioId)
                .ToListAsync();

            var ordemPosicoes = new Dictionary<string, int>
            {
                { "GL", 1 }, { "LD", 2 }, { "LE", 3 },
                { "ZG", 4 }, { "MC", 5 }, { "AT", 6 }
            };

            vm.EscalacoesCasa = escalacoes
                .Where(e => e.IsTimeCasa && e.Titular)
                .OrderBy(e => e.Posicao != null && ordemPosicoes.ContainsKey(e.Posicao) ? ordemPosicoes[e.Posicao] : 99)
                .ToList();

            vm.EscalacoesVisitante = escalacoes
                .Where(e => !e.IsTimeCasa && e.Titular)
                .OrderBy(e => e.Posicao != null && ordemPosicoes.ContainsKey(e.Posicao) ? ordemPosicoes[e.Posicao] : 99)
                .ToList();

            vm.ReservasCasa = escalacoes.Where(e => e.IsTimeCasa && !e.Titular).ToList();
            vm.ReservasVisitante = escalacoes.Where(e => !e.IsTimeCasa && !e.Titular).ToList();

            vm.FormacoesCasa = new SelectList(_context.Formacoes, "Id", "Nome", idFormacaoCasa);
            vm.FormacoesVisitante = new SelectList(_context.Formacoes, "Id", "Nome", idFormacaoVisitante);

            if (faseAtual == "FINAL")
            {
                var escalacoesIniciais = await _context.Escalacoes
                    .Include(e => e.Jogador).ThenInclude(j => j!.Nacionalidade)
                    .Where(e => e.JogoId == id
                             && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null)
                             && e.UsuarioId == usuarioId)
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

                // Usa Substituicoes como fonte primária e complementa com diff INICIAL/FINAL
                var subsJogo = await _context.Substituicoes
                    .Where(s => s.JogoId == id)
                    .ToListAsync();

                var entrouCasaSubs     = subsJogo.Where(s => s.IsTimeCasa  && s.JogadorEntrouId != 0).Select(s => s.JogadorEntrouId).ToHashSet();
                var entrouVisitanteSubs= subsJogo.Where(s => !s.IsTimeCasa && s.JogadorEntrouId != 0).Select(s => s.JogadorEntrouId).ToHashSet();
                var saiuCasaSubs       = subsJogo.Where(s => s.IsTimeCasa  && s.JogadorSaiuId.HasValue).Select(s => s.JogadorSaiuId!.Value).ToHashSet();
                var saiuVisitanteSubs  = subsJogo.Where(s => !s.IsTimeCasa && s.JogadorSaiuId.HasValue).Select(s => s.JogadorSaiuId!.Value).ToHashSet();

                // Complementa com diff escalações (cobre casos sem registro de sub)
                var entrouCasa     = entrouCasaSubs.Union(titularesFinaisCasa.Except(titularesIniciaisCasa)).ToHashSet();
                var entrouVisitante= entrouVisitanteSubs.Union(titularesFinaisVisitante.Except(titularesIniciaisVisitante)).ToHashSet();
                var saiuCasa       = saiuCasaSubs.Union(titularesIniciaisCasa.Except(titularesFinaisCasa)).ToHashSet();
                var saiuVisitante  = saiuVisitanteSubs.Union(titularesIniciaisVisitante.Except(titularesFinaisVisitante)).ToHashSet();

                vm.JogadoresEntraramCasa = entrouCasa.ToList();
                vm.JogadoresEntraramVisitante = entrouVisitante.ToList();
                vm.JogadoresSairamCasa = saiuCasa.ToList();
                vm.JogadoresSairamVisitante = saiuVisitante.ToList();

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

                vm.JogadoresCasa = jogadoresCasaFinal
                    .OrderBy(j => ObterOrdemPosicao(j.Posicao))
                    .ThenBy(j => j.Nome)
                    .ToList();

                vm.JogadoresVisitante = jogadoresVisitanteFinal
                    .OrderBy(j => ObterOrdemPosicao(j.Posicao))
                    .ThenBy(j => j.Nome)
                    .ToList();
            }
            else
            {
                var jogadoresCasa = await _context.Jogadores
                    .Where(j => j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId).ToListAsync();
                var jogadoresVisitante = await _context.Jogadores
                    .Where(j => j.TimeId == jogo.TimeVisitanteId || j.SelecaoId == jogo.TimeVisitanteId).ToListAsync();

                // Jogo ainda não aconteceu (sem lineup importada) — nenhum jogador foi
                // descoberto ainda para este time. Busca o elenco completo na api-football
                // para que apareçam disponíveis na lista lateral.
                if (jogadoresCasa.Count == 0)
                {
                    var criados = await _transfermarkt.ImportarElencoAsync(_context, jogo.TimeCasa);
                    if (criados > 0)
                        jogadoresCasa = await _context.Jogadores
                            .Where(j => j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId).ToListAsync();
                }
                if (jogadoresVisitante.Count == 0)
                {
                    var criados = await _transfermarkt.ImportarElencoAsync(_context, jogo.TimeVisitante);
                    if (criados > 0)
                        jogadoresVisitante = await _context.Jogadores
                            .Where(j => j.TimeId == jogo.TimeVisitanteId || j.SelecaoId == jogo.TimeVisitanteId).ToListAsync();
                }

                vm.JogadoresCasa = jogadoresCasa;
                vm.JogadoresVisitante = jogadoresVisitante;
            }

            vm.FormacaoCasaSelecionada = idFormacaoCasa;
            vm.FormacaoVisitanteSelecionada = idFormacaoVisitante;
            vm.FaseEscalacaoAtual = faseAtual;
            vm.ObservacoesUsuario = (await _context.JogosAnalisadosUsuario
                .FirstOrDefaultAsync(j => j.JogoId == id && j.UsuarioId == usuarioId))?.Observacoes;
            vm.EscalacaoFinalDisponivel = await _context.Escalacoes.AnyAsync(e => e.JogoId == id && e.FaseEscalacao == "FINAL" && e.UsuarioId == usuarioId);
            vm.MostrarBancoReservas = faseAtual == "INICIAL";

            vm.TreinadorCasa = await _context.Treinadores
                .Include(t => t.Nacionalidade)
                .Where(t => t.TimeId == jogo.TimeCasaId)
                .OrderByDescending(t => t.DtInc)
                .FirstOrDefaultAsync();

            vm.TreinadorVisitante = await _context.Treinadores
                .Include(t => t.Nacionalidade)
                .Where(t => t.TimeId == jogo.TimeVisitanteId)
                .OrderByDescending(t => t.DtInc)
                .FirstOrDefaultAsync();

            // Gols e assistências por jogador na mesma competição
            var golsPorJogador = await _context.Gols
                .Where(g => g.Jogo.CompeticaoId == jogo.CompeticaoId && !g.Contra)
                .GroupBy(g => g.JogadorId)
                .Select(g => new { JogadorId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

            var assistsPorJogador = await _context.Assistencias
                .Where(a => a.Jogo.CompeticaoId == jogo.CompeticaoId)
                .GroupBy(a => a.JogadorId)
                .Select(a => new { JogadorId = a.Key, Total = a.Count() })
                .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

            vm.GolsPorJogador = golsPorJogador;
            vm.AssistsPorJogador = assistsPorJogador;

            return View(vm);
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
            var usuarioId = _userManager.GetUserId(User)!;

            // Fase intermediária (timeline): atualiza só as posições daquela fase e retorna,
            // sem tocar em INICIAL/FINAL. Edição "in place" de uma fase já salva.
            if (!string.IsNullOrWhiteSpace(faseEscalacao) &&
                !string.Equals(faseEscalacao, "FINAL", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(faseEscalacao, "INICIAL", StringComparison.OrdinalIgnoreCase))
            {
                var faseExiste = await _context.FasesTaticas
                    .AnyAsync(f => f.JogoId == id && f.UsuarioId == usuarioId && f.Chave == faseEscalacao);
                if (faseExiste)
                {
                    var escFase = await _context.Escalacoes
                        .Where(e => e.JogoId == id && e.UsuarioId == usuarioId && e.FaseEscalacao == faseEscalacao)
                        .ToListAsync();

                    void AtualizarFase(List<EscalacaoInput> inputs)
                    {
                        if (inputs == null) return;
                        foreach (var e in inputs)
                        {
                            var esc = escFase.FirstOrDefault(x => x.Id == e.Id);
                            if (esc == null) continue;
                            esc.PosicaoX = e.PosicaoX;
                            esc.PosicaoY = e.PosicaoY;
                            esc.JogadorId = e.JogadorId > 0 ? e.JogadorId : null;
                            esc.Titular = true;
                        }
                    }
                    AtualizarFase(escalacaoCasa);
                    AtualizarFase(escalacaoVisitante);

                    await _context.SaveChangesAsync();
                    return RedirectToAction("Analisar", new { id, faseEscalacao });
                }
            }

            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id
                         && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null))
                         && e.UsuarioId == usuarioId)
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
                        FaseEscalacao = faseAtual,
                        UsuarioId = usuarioId
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
                {
                    var observacoes = string.IsNullOrWhiteSpace(observacoesComTags) ? null : observacoesComTags.Trim();
                    // Registrar análise por usuário (observações são por usuário)
                    var registroUsuario = await _context.JogosAnalisadosUsuario
                        .FirstOrDefaultAsync(j => j.JogoId == id && j.UsuarioId == usuarioId);
                    if (registroUsuario == null)
                        _context.JogosAnalisadosUsuario.Add(new JogoAnalisadoUsuario { JogoId = id, UsuarioId = usuarioId, Observacoes = observacoes });
                    else
                        registroUsuario.Observacoes = observacoes;
                }
            }

            if (faseAtual == "INICIAL")
            {
                var finalExiste = await _context.Escalacoes
                    .AnyAsync(e => e.JogoId == id && e.FaseEscalacao == "FINAL" && e.UsuarioId == usuarioId);
                if (!finalExiste)
                {
                    var baseInicial = await _context.Escalacoes
                        .Where(e => e.JogoId == id && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null) && e.UsuarioId == usuarioId)
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
                        FaseEscalacao = "FINAL",
                        UsuarioId = usuarioId
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

            // Fase intermediária (timeline): "limpar" remove a fase inteira e volta para a Inicial.
            if (!string.IsNullOrWhiteSpace(faseEscalacao) &&
                !string.Equals(faseEscalacao, "FINAL", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(faseEscalacao, "INICIAL", StringComparison.OrdinalIgnoreCase))
            {
                var usuarioIdFase = _userManager.GetUserId(User)!;
                var fase = await _context.FasesTaticas
                    .FirstOrDefaultAsync(f => f.JogoId == id && f.UsuarioId == usuarioIdFase && f.Chave == faseEscalacao);
                if (fase != null)
                {
                    var escsFase = await _context.Escalacoes
                        .Where(e => e.JogoId == id && e.UsuarioId == usuarioIdFase && e.FaseEscalacao == faseEscalacao)
                        .ToListAsync();
                    _context.Escalacoes.RemoveRange(escsFase);
                    _context.FasesTaticas.Remove(fase);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction("Analisar", new { id, faseEscalacao = "INICIAL" });
            }

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

        /// <summary>
        /// Retorna gols e cartões de um jogo para popular a timeline.
        /// GET /Jogos/BuscarEventos?jogoId=X
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarEventos(int jogoId)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == jogoId);

            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            var gols = await _context.Gols
                .Include(g => g.Jogador)
                .Where(g => g.JogoId == jogoId)
                .OrderBy(g => g.Minuto)
                .ToListAsync();

            var assistencias = await _context.Assistencias
                .Include(a => a.Jogador)
                .Where(a => a.JogoId == jogoId)
                .ToListAsync();

            var cartoes = await _context.Cartoes
                .Include(c => c.Jogador)
                .Where(c => c.JogoId == jogoId)
                .OrderBy(c => c.Minuto)
                .ToListAsync();

            var substituicoes = await _context.Substituicoes
                .Include(s => s.JogadorEntrou)
                .Include(s => s.JogadorSaiu)
                .Where(s => s.JogoId == jogoId)
                .OrderBy(s => s.Minuto)
                .ToListAsync();

            var resultado = new
            {
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante,

                gols = gols.Select(g => new
                {
                    id = g.Id,
                    minuto = g.Minuto,
                    nomeJogador = g.Jogador?.Nome,
                    nomeAssistencia = assistencias
                        .Where(a => a.Minuto == g.Minuto && !g.Contra &&
                                    a.Jogador != null &&
                                    (a.Jogador.TimeId == g.Jogador!.TimeId ||
                                     a.Jogador.SelecaoId == g.Jogador!.TimeId ||
                                     a.Jogador.TimeId == g.Jogador!.SelecaoId ||
                                     (a.Jogador.SelecaoId != null && a.Jogador.SelecaoId == g.Jogador!.SelecaoId)))
                        .Select(a => a.Jogador!.Nome)
                        .FirstOrDefault(),
                    contra = g.Contra,
                    timeCasaId = g.Contra
                        ? ((g.Jogador?.TimeId == jogo.TimeCasaId || g.Jogador?.SelecaoId == jogo.TimeCasaId)
                            ? null : (int?)jogo.TimeCasaId)
                        : ((g.Jogador?.TimeId == jogo.TimeCasaId || g.Jogador?.SelecaoId == jogo.TimeCasaId)
                            ? (int?)jogo.TimeCasaId : null)
                }),

                cartoes = cartoes.Select(c => new
                {
                    id = c.Id,
                    minuto = c.Minuto,
                    tipo = c.Tipo,
                    nomeJogador = c.Jogador?.Nome,
                    timeCasaId = c.Jogador?.TimeId == jogo.TimeCasaId || c.Jogador?.SelecaoId == jogo.TimeCasaId
                        ? (int?)jogo.TimeCasaId : null
                }),

                substituicoes = substituicoes.Select(s => new
                {
                    id = s.Id,
                    minuto = s.Minuto,
                    nomeEntrou = s.JogadorEntrou?.Nome,
                    nomeSaiu = s.JogadorSaiu?.Nome,
                    timeCasaId = s.IsTimeCasa ? (int?)jogo.TimeCasaId : null
                })
            };

            return Ok(resultado);
        }

        public class RegistrarGolRequest
        {
            public int JogoId { get; set; }
            public int JogadorId { get; set; }
            public int? AssistenciaJogadorId { get; set; }
            public int Minuto { get; set; }
            public int Acrescimo { get; set; }
            public bool Contra { get; set; }
            public bool IsTimeCasa { get; set; }
        }

        /// <summary>
        /// Registra um gol manualmente.
        /// POST /Jogos/RegistrarGol
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RegistrarGol([FromBody] RegistrarGolRequest req)
        {
            if (req.JogadorId <= 0) return BadRequest(new { erro = "Jogador inválido." });

            var jogo = await _context.Jogos.FindAsync(req.JogoId);
            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            var gol = new Gol
            {
                JogoId = req.JogoId,
                JogadorId = req.JogadorId,
                Minuto = req.Minuto,
                Contra = req.Contra
            };
            _context.Gols.Add(gol);

            if (req.AssistenciaJogadorId.HasValue && req.AssistenciaJogadorId > 0 && !req.Contra)
            {
                _context.Assistencias.Add(new Assistencia
                {
                    JogoId = req.JogoId,
                    JogadorId = req.AssistenciaJogadorId.Value,
                    Minuto = req.Minuto
                });
            }

            // Recalcula placar contando os gols no banco + o novo
            if (!req.Contra)
            {
                if (req.IsTimeCasa)
                    jogo.PlacarCasa = (jogo.PlacarCasa ?? 0) + 1;
                else
                    jogo.PlacarVisitante = (jogo.PlacarVisitante ?? 0) + 1;
            }
            else // gol contra: ponto vai para o adversário
            {
                if (req.IsTimeCasa)
                    jogo.PlacarVisitante = (jogo.PlacarVisitante ?? 0) + 1;
                else
                    jogo.PlacarCasa = (jogo.PlacarCasa ?? 0) + 1;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = gol.Id,
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante
            });
        }

        /// <summary>
        /// Remove um gol e recalcula o placar.
        /// DELETE /Jogos/RemoverGol?id=X
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> RemoverGol(int id)
        {
            var gol = await _context.Gols
                .Include(g => g.Jogador)
                .Include(g => g.Jogo)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gol == null) return NotFound(new { erro = "Gol não encontrado." });

            var jogo = gol.Jogo;

            if (!gol.Contra)
            {
                bool isCasa = gol.Jogador?.TimeId == jogo.TimeCasaId;
                if (isCasa)
                    jogo.PlacarCasa = Math.Max(0, (jogo.PlacarCasa ?? 1) - 1);
                else
                    jogo.PlacarVisitante = Math.Max(0, (jogo.PlacarVisitante ?? 1) - 1);
            }
            else
            {
                bool isCasa = gol.Jogador?.TimeId == jogo.TimeCasaId;
                if (isCasa)
                    jogo.PlacarVisitante = Math.Max(0, (jogo.PlacarVisitante ?? 1) - 1);
                else
                    jogo.PlacarCasa = Math.Max(0, (jogo.PlacarCasa ?? 1) - 1);
            }

            _context.Gols.Remove(gol);

            // Remove assistência vinculada ao mesmo minuto (se existir)
            if (!gol.Contra && gol.Jogador != null)
            {
                var assist = await _context.Assistencias
                    .Include(a => a.Jogador)
                    .FirstOrDefaultAsync(a => a.JogoId == gol.JogoId && a.Minuto == gol.Minuto
                                           && a.Jogador != null && a.Jogador.TimeId == gol.Jogador.TimeId);
                if (assist != null)
                    _context.Assistencias.Remove(assist);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante
            });
        }

        public class RegistrarCartaoRequest
        {
            public int JogoId { get; set; }
            public int JogadorId { get; set; }
            public int Minuto { get; set; }
            public int Acrescimo { get; set; }
            public string Tipo { get; set; } = "Amarelo"; // "Amarelo" | "Vermelho"
            public bool IsTimeCasa { get; set; }
        }
        /// <summary>
        /// Registra um cartão manualmente.
        /// POST /Jogos/RegistrarCartao
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RegistrarCartao([FromBody] RegistrarCartaoRequest req)
        {
            if (req.JogadorId <= 0) return BadRequest(new { erro = "Jogador inválido." });

            var cartao = new Cartao
            {
                JogoId = req.JogoId,
                JogadorId = req.JogadorId,
                Minuto = req.Minuto,
                Tipo = req.Tipo
            };
            _context.Cartoes.Add(cartao);
            await _context.SaveChangesAsync();

            return Ok(new { id = cartao.Id });
        }

        /// <summary>
        /// Remove um cartão.
        /// DELETE /Jogos/RemoverCartao?id=X
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> RemoverCartao(int id)
        {
            var cartao = await _context.Cartoes.FindAsync(id);
            if (cartao == null) return NotFound(new { erro = "Cartão não encontrado." });

            _context.Cartoes.Remove(cartao);
            await _context.SaveChangesAsync();

            return Ok(new { removido = true });
        }

        public class AtualizarPlacarRequest
        {
            public int JogoId { get; set; }
            public int PlacarCasa { get; set; }
            public int PlacarVis { get; set; }
        }
        /// <summary>
        /// Atualiza o placar manualmente (clique nos números do placar).
        /// POST /Jogos/AtualizarPlacar
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AtualizarPlacar([FromBody] AtualizarPlacarRequest req)
        {
            var jogo = await _context.Jogos.FindAsync(req.JogoId);
            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            jogo.PlacarCasa = req.PlacarCasa;
            jogo.PlacarVisitante = req.PlacarVis;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante
            });
        }

        public class MarcarAnalisadoRequest
        {
            public int JogoId { get; set; }
            public int Analisado { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> MarcarAnalisado([FromBody] MarcarAnalisadoRequest req)
        {
            var jogo = await _context.Jogos.FindAsync(req.JogoId);
            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            var usuarioId = _userManager.GetUserId(User)!;
            var registro = await _context.JogosAnalisadosUsuario
                .FirstOrDefaultAsync(j => j.JogoId == req.JogoId && j.UsuarioId == usuarioId);

            if (req.Analisado == 1 && registro == null)
                _context.JogosAnalisadosUsuario.Add(new JogoAnalisadoUsuario { JogoId = req.JogoId, UsuarioId = usuarioId });
            else if (req.Analisado == 0 && registro != null)
                _context.JogosAnalisadosUsuario.Remove(registro);

            await _context.SaveChangesAsync();
            return Ok(new { analisado = req.Analisado });
        }

        // POST: Jogos/ReimportarEscalacao/12964
        // Re-busca a escalação do Transfermarkt, apaga os dados anteriores e reimporta
        // com o algoritmo de normalização dinâmica (corrige posições erradas de imports antigos).
        [HttpPost]
        public IActionResult ReimportarEscalacao(int id)
        {
            // Executa em background para não travar o request (operação pode levar 1-2 min)
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<FutebolContext>();
                var svc = scope.ServiceProvider.GetRequiredService<ApiFootballService>();
                try
                {
                    var (ok, msg) = await svc.ForcarReimportarEscalacaoAsync(ctx, id);
                    _logger.LogInformation("[ReimportarEscalacao] Jogo {Id}: {Ok} — {Msg}", id, ok, msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ReimportarEscalacao] Erro no jogo {Id}", id);
                }
            });

            TempData["Mensagem"] = "⏳ Re-importação iniciada em background. Aguarde ~1 minuto e recarregue a página.";
            return RedirectToAction("Analisar", new { id });
        }

        // POST: Jogos/BuscarGrupo/12964
        // Acessa o link do Transfermarkt do jogo e extrai o grupo da fase de grupos.
        [HttpPost]
        public async Task<IActionResult> BuscarGrupo(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null)
            {
                TempData["MensagemErro"] = "Jogo não encontrado.";
                return RedirectToAction("Analisar", new { id });
            }

            if (string.IsNullOrWhiteSpace(jogo.LinkDetalhes))
            {
                TempData["MensagemErro"] = "Este jogo não tem link do Transfermarkt — não é possível buscar o grupo automaticamente.";
                return RedirectToAction("Analisar", new { id });
            }

            try
            {
                var grupo = await _transfermarkt.BuscarGrupoDoJogoAsync(
                    jogo.LinkDetalhes, HttpContext.RequestAborted);

                if (!string.IsNullOrWhiteSpace(grupo))
                {
                    jogo.Grupo = grupo;
                    await _context.SaveChangesAsync();

                    TempData["Mensagem"] = $"✅ Grupo atualizado: \"{grupo}\"";
                    _logger.LogInformation("[BuscarGrupo] Jogo {Id} → grupo \"{Grupo}\"", id, grupo);
                }
                else
                {
                    TempData["MensagemErro"] = "Não foi possível identificar o grupo neste jogo. " +
                        "Verifique se é um jogo da fase de grupos e se o link está correto.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BuscarGrupo] Erro no jogo {Id}", id);
                TempData["MensagemErro"] = "Erro ao buscar grupo: " + ex.Message;
            }

            return RedirectToAction("Analisar", new { id });
        }

        // GET: Jogos/UltimosConfrontos/5 — retorna JSON com últimos H2H
        [HttpGet]
        public async Task<IActionResult> UltimosConfrontos(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            if (jogo.TimeCasa?.IdApi == 0 || jogo.TimeVisitante?.IdApi == 0)
                return BadRequest("Um dos times não tem ID da API configurado.");

            try
            {
                var confrontos = await _transfermarkt.BuscarH2HAsync(
                    jogo.TimeCasa!.IdApi, jogo.TimeVisitante!.IdApi, 5,
                    HttpContext.RequestAborted);

                var resultado = confrontos.Select(f => new
                {
                    data = f.Fixture.Date?.ToString("dd/MM/yyyy") ?? "-",
                    competicao = f.League.Name,
                    temporada = f.League.Season,
                    mandante = f.Teams.Home.Name,
                    visitante = f.Teams.Away.Name,
                    placarMandante = f.Goals.Home,
                    placarVisitante = f.Goals.Away,
                    logoMandante = f.Teams.Home.Logo,
                    logoVisitante = f.Teams.Away.Logo,
                    status = f.Fixture.Status.Short,
                    vencedor = f.Teams.Home.Winner == true ? "home"
                             : f.Teams.Away.Winner == true ? "away"
                             : "draw"
                }).ToList();

                return Json(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[H2H] Erro ao buscar confrontos para jogo {Id}", id);
                return StatusCode(500, "Erro ao buscar confrontos na API.");
            }
        }

        // POST: Jogos/BuscarGrupoEmLote
        // Atualiza o grupo de todos os jogos de uma competição que ainda não têm grupo.
        // Chamado a partir da listagem de jogos.
        [HttpPost]
        public async Task<IActionResult> BuscarGrupoEmLote(int competicaoId)
        {
            var jogos = await _context.Jogos
                .Where(j => j.CompeticaoId == competicaoId
                         && (string.IsNullOrEmpty(j.Grupo) || j.Grupo == "Group Stage")
                         && !string.IsNullOrEmpty(j.LinkDetalhes))
                .ToListAsync();

            if (!jogos.Any())
            {
                TempData["Mensagem"] = "Nenhum jogo sem grupo encontrado nesta competição.";
                return RedirectToAction("Index", new { competicaoId });
            }

            int atualizados = 0;
            int falhas = 0;

            foreach (var jogo in jogos)
            {
                try
                {
                    var grupo = await _transfermarkt.BuscarGrupoDoJogoAsync(
                        jogo.LinkDetalhes!, HttpContext.RequestAborted);

                    if (!string.IsNullOrWhiteSpace(grupo))
                    {
                        jogo.Grupo = grupo;
                        atualizados++;
                        _logger.LogInformation("[BuscarGrupoLote] Jogo {Id} → \"{G}\"", jogo.Id, grupo);
                    }
                    else
                    {
                        falhas++;
                    }

                    // Pausa entre requisições para não ser bloqueado
                    await Task.Delay(1500, HttpContext.RequestAborted);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BuscarGrupoLote] Erro jogo {Id}", jogo.Id);
                    falhas++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Mensagem"] = $"✅ Grupos atualizados: {atualizados} | Não encontrados: {falhas}";
            return RedirectToAction("Index", new { competicaoId });
        }

        // ════════════════════ CRONÔMETRO DA PARTIDA ════════════════════

        private static int CronometroSegundos(CronometroPartida c)
        {
            var s = c.SegundosAcumulados;
            if (c.Estado == "RODANDO" && c.InicioUtc.HasValue)
                s += (int)(DateTime.UtcNow - c.InicioUtc.Value).TotalSeconds;
            return Math.Max(0, s);
        }

        [HttpGet]
        public async Task<IActionResult> CronometroEstado(int jogoId)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var c = await _context.CronometrosPartida
                .FirstOrDefaultAsync(x => x.JogoId == jogoId && x.UsuarioId == usuarioId);
            if (c == null) return Ok(new { estado = "PARADO", segundos = 0 });
            return Ok(new { estado = c.Estado, segundos = CronometroSegundos(c) });
        }

        public class CronometroAcaoRequest { public int JogoId { get; set; } public string Acao { get; set; } = ""; }

        [HttpPost]
        public async Task<IActionResult> CronometroAcao([FromBody] CronometroAcaoRequest req)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var c = await _context.CronometrosPartida
                .FirstOrDefaultAsync(x => x.JogoId == req.JogoId && x.UsuarioId == usuarioId);
            if (c == null)
            {
                c = new CronometroPartida { JogoId = req.JogoId, UsuarioId = usuarioId, Estado = "PARADO" };
                _context.CronometrosPartida.Add(c);
            }

            switch ((req.Acao ?? "").ToLowerInvariant())
            {
                case "iniciar":
                    if (c.Estado != "RODANDO") { c.InicioUtc = DateTime.UtcNow; c.Estado = "RODANDO"; }
                    break;
                case "parar":
                    if (c.Estado == "RODANDO" && c.InicioUtc.HasValue)
                        c.SegundosAcumulados += (int)(DateTime.UtcNow - c.InicioUtc.Value).TotalSeconds;
                    c.InicioUtc = null;
                    c.Estado = "PARADO";
                    break;
                case "finalizar":
                    if (c.Estado == "RODANDO" && c.InicioUtc.HasValue)
                        c.SegundosAcumulados += (int)(DateTime.UtcNow - c.InicioUtc.Value).TotalSeconds;
                    c.InicioUtc = null;
                    c.Estado = "FINALIZADO";
                    break;
                case "zerar":
                    c.SegundosAcumulados = 0; c.InicioUtc = null; c.Estado = "PARADO";
                    break;
            }

            await _context.SaveChangesAsync();
            return Ok(new { estado = c.Estado, segundos = CronometroSegundos(c) });
        }

        // ════════════════════ FASES TÁTICAS (timeline) ════════════════════

        public class FaseTaticaSlot
        {
            public int? JogadorId { get; set; }
            public string? Posicao { get; set; }
            public double PosicaoX { get; set; }
            public double PosicaoY { get; set; }
            public bool IsTimeCasa { get; set; }
        }

        public class SalvarFaseTaticaRequest
        {
            public int JogoId { get; set; }
            public int Minuto { get; set; }
            public List<FaseTaticaSlot> Titulares { get; set; } = new();
        }

        [HttpPost]
        public async Task<IActionResult> SalvarFaseTatica([FromBody] SalvarFaseTaticaRequest req)
        {
            if (req == null || req.JogoId <= 0) return BadRequest("Dados inválidos.");
            var usuarioId = _userManager.GetUserId(User)!;

            var ordemMax = await _context.FasesTaticas
                .Where(f => f.JogoId == req.JogoId && f.UsuarioId == usuarioId)
                .Select(f => (int?)f.Ordem).MaxAsync() ?? 0;

            var chave = "FASE_" + Guid.NewGuid().ToString("N").Substring(0, 12);

            _context.FasesTaticas.Add(new FaseTatica
            {
                JogoId = req.JogoId,
                UsuarioId = usuarioId,
                Chave = chave,
                Ordem = ordemMax + 1,
                MinutoInicio = Math.Max(0, req.Minuto)
            });

            foreach (var s in req.Titulares.Where(s => s.JogadorId > 0))
            {
                _context.Escalacoes.Add(new Escalacao
                {
                    JogoId = req.JogoId,
                    JogadorId = s.JogadorId,
                    Posicao = s.Posicao,
                    PosicaoX = s.PosicaoX,
                    PosicaoY = s.PosicaoY,
                    IsTimeCasa = s.IsTimeCasa,
                    Titular = true,
                    FaseEscalacao = chave,
                    UsuarioId = usuarioId
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { chave });
        }

        public class ExcluirFaseTaticaRequest { public int JogoId { get; set; } public string Chave { get; set; } = ""; }

        [HttpPost]
        public async Task<IActionResult> ExcluirFaseTatica([FromBody] ExcluirFaseTaticaRequest req)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var fase = await _context.FasesTaticas
                .FirstOrDefaultAsync(f => f.JogoId == req.JogoId && f.UsuarioId == usuarioId && f.Chave == req.Chave);
            if (fase == null) return NotFound();

            var escs = await _context.Escalacoes
                .Where(e => e.JogoId == req.JogoId && e.UsuarioId == usuarioId && e.FaseEscalacao == req.Chave)
                .ToListAsync();
            _context.Escalacoes.RemoveRange(escs);
            _context.FasesTaticas.Remove(fase);
            await _context.SaveChangesAsync();
            return Ok(new { sucesso = true });
        }
    }

}
