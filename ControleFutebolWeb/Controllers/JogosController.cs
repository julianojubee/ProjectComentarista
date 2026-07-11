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
using System.Text.Json;

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
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .Where(j => j.Data >= inicioUtc && j.Data < fimUtc)
                .OrderBy(j => j.Data)
                .ToListAsync();

            var jogosAnalisadosIds = uid != null
                ? await _context.JogosAnalisadosUsuario
                    .Where(j => j.UsuarioId == uid && j.Analisado)
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

            // AsNoTracking: só exibição. jogo.Data é convertida pra Brasília só pra preencher
            // o campo do formulário — não pode ser uma entidade rastreada, senão essa
            // conversão "vazaria" pro banco se algo desse SaveChanges depois sem querer.
            var jogo = await _context.Jogos.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
            if (jogo == null) return NotFound();

            jogo.Data = DateHelper.ParaBrasilia(jogo.Data);

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

            // O formulário mostra/recebe o horário em Brasília (ver GET acima) — converte
            // de volta pra UTC antes de salvar, senão o jogo fica com a hora errada (3h a menos).
            jogo.Data = DateHelper.DeBrasiliaParaUtc(jogo.Data);

            if (ModelState.IsValid)
            {
                try
                {
                    // O form só edita Data/TimeCasaId/TimeVisitanteId/FormacaoCasaId/
                    // FormacaoVisitanteId — não tem campo pra CompeticaoId, placar, rodada,
                    // temporada etc. _context.Update(jogo) sobrescreveria TODAS as colunas
                    // com os valores default do model binding (ex.: CompeticaoId=0), quebrando
                    // a FK com competicoes. Por isso carrega a entidade existente e só altera
                    // os campos que o formulário realmente edita.
                    var existente = await _context.Jogos.FindAsync(id);
                    if (existente == null) return NotFound();

                    existente.Data = jogo.Data;
                    existente.TimeCasaId = jogo.TimeCasaId;
                    existente.TimeVisitanteId = jogo.TimeVisitanteId;
                    existente.FormacaoCasaId = jogo.FormacaoCasaId;
                    existente.FormacaoVisitanteId = jogo.FormacaoVisitanteId;

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

            // Jogadores expulsos (cartão vermelho) neste jogo — vale para qualquer fase,
            // já que quem foi expulso não pode mais ser movimentado em campo depois disso.
            vm.JogadoresComCartaoVermelho = (await _context.Cartoes
                .Where(c => c.JogoId == id && c.Tipo == "Vermelho")
                .Select(c => c.JogadorId)
                .ToListAsync())
                .ToHashSet();

            // Jogadores advertidos com cartão amarelo neste jogo — mostra um ícone no
            // botão do jogador em campo/banco (independe de fase, o cartão vale pro jogo todo).
            vm.JogadoresComCartaoAmarelo = (await _context.Cartoes
                .Where(c => c.JogoId == id && c.Tipo == "Amarelo")
                .Select(c => c.JogadorId)
                .ToListAsync())
                .ToHashSet();

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
                        .Include(e => e.Jogador).ThenInclude(j => j!.Time)
                        .Include(e => e.Setas)
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

                    vm.MediasPorJogador = await CalcularMediasPorJogadorAsync(escFase);
                    vm.TitularPorJogador = await CalcularTitularesPorJogadorAsync(escFase, usuarioId);

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
                        if (sub.JogadorSaiuId == null || sub.JogadorEntrouId == null
                            || sub.JogadorEntrouId == sub.JogadorSaiuId) // linhas antigas corrompidas (entrou=saiu)
                            continue;

                        // Slot em campo de quem saiu (titular). Sem ele, não há o que substituir.
                        var slotSaiu = clonesFinais.FirstOrDefault(c =>
                            c.JogadorId == sub.JogadorSaiuId && c.IsTimeCasa == sub.IsTimeCasa && c.Titular);
                        if (slotSaiu == null) continue;

                        // Se quem entrou já está em campo, nada a fazer (evita reprocessar).
                        bool jaEmCampo = clonesFinais.Any(c =>
                            c.JogadorId == sub.JogadorEntrouId && c.IsTimeCasa == sub.IsTimeCasa && c.Titular);
                        if (jaEmCampo) continue;

                        // Slot de quem entrou (no banco). Pode não existir se a importação trouxe
                        // o banco incompleto ou o jogador não estava entre os reservas.
                        var slotEntrou = clonesFinais.FirstOrDefault(c =>
                            c.JogadorId == sub.JogadorEntrouId && c.IsTimeCasa == sub.IsTimeCasa && !c.Titular);

                        // Quem saiu vai para o banco (se houver slot de reserva); senão, apenas deixa o campo.
                        if (slotEntrou != null)
                            slotEntrou.JogadorId = slotSaiu.JogadorId;

                        // Quem entrou assume a posição em campo de quem saiu — mesmo que não
                        // estivesse no banco importado (garante a seta verde no jogador certo).
                        slotSaiu.JogadorId = sub.JogadorEntrouId;
                    }

                    _context.Escalacoes.AddRange(clonesFinais);
                    await _context.SaveChangesAsync();
                }
            }

            // Escalações do usuário para esta fase
            var escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador).ThenInclude(j => j!.Nacionalidade)
                .Include(e => e.Jogador).ThenInclude(j => j!.Time)
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
                        .Include(e => e.Jogador).ThenInclude(j => j!.Time)
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
                .Include(e => e.Jogador).ThenInclude(j => j!.Time)
                .Include(e => e.Setas)
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
                    .Include(s => s.JogadorSaiu)
                    .Where(s => s.JogoId == id)
                    .ToListAsync();

                // Tooltip da seta verde: "Entrou no lugar de {nome} ({minuto}')"
                vm.EntrouNoLugarDe = subsJogo
                    .Where(s => s.JogadorEntrouId.HasValue
                             && s.JogadorEntrouId != s.JogadorSaiuId
                             && s.JogadorSaiu != null)
                    .GroupBy(s => s.JogadorEntrouId!.Value)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var s = g.First();
                            return s.Minuto > 0 ? $"{s.JogadorSaiu!.Nome} ({s.Minuto}')" : s.JogadorSaiu!.Nome;
                        });

                // Ignora "entrou == saiu": linhas gravadas pelo import antigo quando o
                // jogador que entrou não era resolvido (fallback errado, já removido).
                var entrouCasaSubs     = subsJogo.Where(s => s.IsTimeCasa  && s.JogadorEntrouId.HasValue && s.JogadorEntrouId != s.JogadorSaiuId).Select(s => s.JogadorEntrouId!.Value).ToHashSet();
                var entrouVisitanteSubs= subsJogo.Where(s => !s.IsTimeCasa && s.JogadorEntrouId.HasValue && s.JogadorEntrouId != s.JogadorSaiuId).Select(s => s.JogadorEntrouId!.Value).ToHashSet();
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
            // "Analisado" é por usuário (flag Analisado em JogosAnalisadosUsuario),
            // não o campo global Jogo.Analisado — assim o estado bate com a lista /Jogos
            // e não "desmarca" ao recarregar a tela após salvar a escalação.
            // A linha pode existir com Analisado=false (usuário desmarcou, mas as
            // Observacoes continuam preservadas nela) — por isso checa o flag, não
            // só a existência da linha.
            var analiseUsuario = await _context.JogosAnalisadosUsuario
                .FirstOrDefaultAsync(j => j.JogoId == id && j.UsuarioId == usuarioId);
            vm.Analisado = analiseUsuario?.Analisado == true;

            vm.ObservacoesTag = await _context.ObservacoesJogoTag
                .Include(o => o.Jogador)
                .Where(o => o.JogoId == id && o.UsuarioId == usuarioId)
                .OrderBy(o => o.Ordem)
                .ToListAsync();

            vm.JogadoresEscalados = await _context.Escalacoes
                .Where(e => e.JogoId == id && e.JogadorId != null)
                .Select(e => e.Jogador!)
                .Distinct()
                .OrderBy(j => j.Nome)
                .ToListAsync();
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

            vm.MediasPorJogador = await CalcularMediasPorJogadorAsync(escalacoes);
            vm.TitularPorJogador = await CalcularTitularesPorJogadorAsync(escalacoes, usuarioId);

            return View(vm);
        }

        // Médias por jogo das estatísticas importadas, em lote, para todos os
        // jogadores escalados — mesmas fórmulas de /Jogadores/Estatisticas (inclusive
        // o filtro Minutos > 0, que exclui reservas não utilizados). Alimenta o
        // tooltip de info do jogador em /Jogos/Analisar.
        private async Task<Dictionary<int, MediasPorJogo>> CalcularMediasPorJogadorAsync(
            IEnumerable<Escalacao> escalacoes)
        {
            var ids = escalacoes
                .Where(e => e.JogadorId != null)
                .Select(e => e.JogadorId!.Value)
                .Distinct()
                .ToList();
            if (ids.Count == 0) return new();

            var agregados = await _context.EstatisticasJogador
                .Where(e => ids.Contains(e.JogadorId) && e.Minutos != null && e.Minutos > 0)
                .GroupBy(e => e.JogadorId)
                .Select(g => new
                {
                    JogadorId = g.Key,
                    Jogos = g.Count(),
                    Passes = g.Average(e => (double)e.PassesTotal),
                    PassesChave = g.Average(e => (double)e.PassesChave),
                    Finalizacoes = g.Average(e => (double)e.FinalizacoesTotal),
                    FinalizacoesNoGolSum = g.Sum(e => e.FinalizacoesNoGol),
                    FinalizacoesSum = g.Sum(e => e.FinalizacoesTotal),
                    Dribles = g.Average(e => (double)e.DriblesTentados),
                    DriblesCertosSum = g.Sum(e => e.DriblesCertos),
                    DriblesSum = g.Sum(e => e.DriblesTentados),
                    Duelos = g.Average(e => (double)e.DuelosTotal),
                    DuelosVencidosSum = g.Sum(e => e.DuelosVencidos),
                    DuelosSum = g.Sum(e => e.DuelosTotal),
                    Desarmes = g.Average(e => (double)e.Desarmes),
                    Interceptacoes = g.Average(e => (double)e.Interceptacoes),
                    Bloqueios = g.Average(e => (double)e.Bloqueios),
                    Defesas = g.Average(e => (double)e.Defesas),
                    FaltasSofridas = g.Average(e => (double)e.FaltasSofridas),
                    FaltasCometidas = g.Average(e => (double)e.FaltasCometidas),
                })
                .ToListAsync();

            static int Pct(int certos, int total) =>
                total > 0 ? (int)Math.Round(100.0 * certos / total) : 0;

            return agregados.ToDictionary(a => a.JogadorId, a => new MediasPorJogo
            {
                Jogos = a.Jogos,
                Passes = Math.Round(a.Passes, 1),
                PassesChave = Math.Round(a.PassesChave, 1),
                Finalizacoes = Math.Round(a.Finalizacoes, 1),
                FinalizacoesPct = Pct(a.FinalizacoesNoGolSum, a.FinalizacoesSum),
                Dribles = Math.Round(a.Dribles, 1),
                DriblesPct = Pct(a.DriblesCertosSum, a.DriblesSum),
                Duelos = Math.Round(a.Duelos, 1),
                DuelosPct = Pct(a.DuelosVencidosSum, a.DuelosSum),
                Desarmes = Math.Round(a.Desarmes, 1),
                Interceptacoes = Math.Round(a.Interceptacoes, 1),
                Bloqueios = Math.Round(a.Bloqueios, 1),
                Defesas = Math.Round(a.Defesas, 1),
                FaltasSofridas = Math.Round(a.FaltasSofridas, 1),
                FaltasCometidas = Math.Round(a.FaltasCometidas, 1),
            });
        }

        // Total de jogos como titular (carreira), por jogador — mesmo critério de
        // dedupe usado em /Jogadores/Estatisticas: por jogo, prefere a escalação do
        // próprio usuário sobre a compartilhada (importada, UsuarioId null), e conta
        // só a fase INICIAL (a FINAL é a mesma partida, não um jogo a mais).
        private async Task<Dictionary<int, int>> CalcularTitularesPorJogadorAsync(
            IEnumerable<Escalacao> escalacoes, string usuarioId)
        {
            var ids = escalacoes
                .Where(e => e.JogadorId != null)
                .Select(e => e.JogadorId!.Value)
                .Distinct()
                .ToList();
            if (ids.Count == 0) return new();

            var candidatas = await _context.Escalacoes
                .Where(e => e.JogadorId != null && ids.Contains(e.JogadorId!.Value)
                         && e.Titular && e.Posicao != null && e.Posicao != "RES"
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null)
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null))
                .Select(e => new { e.JogadorId, e.JogoId, e.UsuarioId })
                .ToListAsync();

            return candidatas
                .GroupBy(e => e.JogadorId!.Value)
                .ToDictionary(g => g.Key, g => g
                    .GroupBy(e => e.JogoId)
                    .Select(gj => gj.OrderBy(e => e.UsuarioId == usuarioId ? 0 : 1).First())
                    .Count());
        }

        // ── Mapa de Calor ───────────────────────────────────────────────────
        // Pontos (PosicaoX/Y, em % do campo) de todas as escalações salvas pelo
        // usuário atual nesse jogo — INICIAL, FINAL e cada fase tática intermediária
        // contam como uma "foto" a mais, dando o histórico de posicionamento usado
        // pelo mapa de calor. Filtra por jogador OU por lado (casa/visitante).
        // Some-se o destino de cada seta de movimentação (EscalacaoSeta.X/Y) —
        // a origem da seta já é a própria Escalacao (não duplica), só o destino
        // é um ponto novo, dando mais densidade sem depender só das fases salvas.
        // Peso menor (mais fraco/amarelado no mapa): a seta indica um lugar por
        // onde o jogador passou/se movimentou, não a posição principal dele.
        public async Task<IActionResult> MapaCalor(int id, int? jogadorId, int? timeId)
        {
            if (jogadorId == null && timeId == null) return BadRequest();

            var usuarioId = _userManager.GetUserId(User);

            // Titular == true: reservas que ficaram no banco têm PosicaoX/Y fixo em
            // 0/0 (canto do campo, não é uma posição de jogo real) — incluí-las
            // gerava um ponto de calor falso no canto superior esquerdo.
            var query = _context.Escalacoes.AsNoTracking()
                .Where(e => e.JogoId == id && e.UsuarioId == usuarioId && e.JogadorId != null && e.Titular);

            if (jogadorId.HasValue)
            {
                query = query.Where(e => e.JogadorId == jogadorId.Value);
            }
            else
            {
                var jogo = await _context.Jogos.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
                if (jogo == null) return NotFound();
                var isCasa = timeId!.Value == jogo.TimeCasaId;
                query = query.Where(e => e.IsTimeCasa == isCasa);
            }

            var pontos = await query
                .Select(e => new { x = e.PosicaoX, y = e.PosicaoY, peso = 1.0 })
                .ToListAsync();

            var destinosSetas = await query
                .SelectMany(e => e.Setas)
                .Select(s => new { x = s.X, y = s.Y, peso = 0.45 })
                .ToListAsync();

            pontos.AddRange(destinosSetas);

            return Json(pontos);
        }

        // Jogadores elegíveis pro seletor do mapa de calor: só quem realmente entrou
        // em campo (titulares da escalação INICIAL do usuário + quem entrou como
        // substituição, segundo os eventos importados) — reservas que ficaram no
        // banco o jogo inteiro não aparecem.
        public async Task<IActionResult> MapaCalorJogadores(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            var titulares = await _context.Escalacoes.AsNoTracking()
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id && e.UsuarioId == usuarioId
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null)
                         && e.Titular && e.JogadorId != null)
                .ToListAsync();

            var subsEntraram = await _context.Substituicoes.AsNoTracking()
                .Include(s => s.JogadorEntrou)
                .Where(s => s.JogoId == id && s.JogadorEntrouId != null && s.JogadorEntrouId != s.JogadorSaiuId)
                .ToListAsync();

            List<object> MontarLado(bool isTimeCasa) =>
                titulares.Where(e => e.IsTimeCasa == isTimeCasa)
                    .Select(e => new { jogadorId = e.Jogador!.Id, nome = e.Jogador!.Nome })
                    .Concat(subsEntraram.Where(s => s.IsTimeCasa == isTimeCasa && s.JogadorEntrou != null)
                        .Select(s => new { jogadorId = s.JogadorEntrou!.Id, nome = s.JogadorEntrou!.Nome }))
                    .GroupBy(x => x.jogadorId)
                    .Select(g => g.First())
                    .OrderBy(x => x.nome)
                    .Cast<object>()
                    .ToList();

            return Json(new { casa = MontarLado(true), visitante = MontarLado(false) });
        }

        [HttpPost]
        public async Task<IActionResult> SalvarEscalacao(
            int id,
            int formacaoCasaId,
            int formacaoVisitanteId,
            string? faseEscalacao,
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
                    // Registrar análise por usuário. Analisado fica true pelo default do
                    // model — salvar a escalação final já marca o jogo como analisado
                    // (comportamento pré-existente).
                    var registroUsuario = await _context.JogosAnalisadosUsuario
                        .FirstOrDefaultAsync(j => j.JogoId == id && j.UsuarioId == usuarioId);
                    if (registroUsuario == null)
                        _context.JogosAnalisadosUsuario.Add(new JogoAnalisadoUsuario { JogoId = id, UsuarioId = usuarioId });
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

            // Atualiza automaticamente a posição (tática) dos jogadores envolvidos
            // neste jogo — mesmo cálculo do botão "Recalcular posições" em Serviços,
            // mas restrito aos jogadores do jogo. Falha aqui não pode impedir o save.
            try
            {
                var jogadoresDoJogo = await _context.Escalacoes
                    .Where(e => e.JogoId == id && e.JogadorId != null && e.Titular)
                    .Select(e => e.JogadorId!.Value)
                    .Distinct()
                    .ToListAsync();

                if (jogadoresDoJogo.Count > 0)
                    await PosicaoJogadorHelper.RecalcularAsync(_context, jogadoresDoJogo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SalvarEscalacao] Falha ao recalcular posições dos jogadores do jogo {JogoId}.", id);
            }

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

            var penaltisPerdidos = await _context.PenaltisPerdidos
                .Include(p => p.Jogador)
                .Where(p => p.JogoId == jogoId)
                .OrderBy(p => p.Minuto)
                .ToListAsync();

            var penaltisDisputa = await _context.PenaltisDisputa
                .Include(p => p.Jogador)
                .Where(p => p.JogoId == jogoId)
                .OrderBy(p => p.Ordem)
                .ToListAsync();

            var resultado = new
            {
                placarCasa = jogo.PlacarCasa,
                placarVis = jogo.PlacarVisitante,
                penaltisCasa = jogo.PenaltisCasa,
                penaltisVis = jogo.PenaltisVisitante,

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
                }),

                penaltisPerdidos = penaltisPerdidos.Select(p => new
                {
                    id = p.Id,
                    minuto = p.Minuto,
                    nomeJogador = p.Jogador?.Nome,
                    timeCasaId = p.IsTimeCasa ? (int?)jogo.TimeCasaId : null
                }),

                penaltisDisputa = penaltisDisputa.Select(p => new
                {
                    id = p.Id,
                    ordem = p.Ordem,
                    nomeJogador = p.Jogador?.Nome,
                    convertido = p.Convertido,
                    isCasa = p.IsTimeCasa
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

            // NUNCA remove a linha: ela também guarda as Observacoes da escalação
            // final (ver SalvarEscalacao). Remover apagava as observações do usuário
            // ao desmarcar "analisado" — aqui só alternamos o flag Analisado.
            if (registro == null)
                _context.JogosAnalisadosUsuario.Add(new JogoAnalisadoUsuario
                {
                    JogoId = req.JogoId,
                    UsuarioId = usuarioId,
                    Analisado = req.Analisado == 1
                });
            else
                registro.Analisado = req.Analisado == 1;

            await _context.SaveChangesAsync();
            return Ok(new { analisado = req.Analisado });
        }

        // POST: Jogos/ReimportarEscalacao/12964
        // Re-busca a escalação do Transfermarkt, apaga os dados anteriores e reimporta
        // com o algoritmo de normalização dinâmica (corrige posições erradas de imports antigos).
        [HttpPost]
        public IActionResult ReimportarEscalacao(int id)
        {
            // Captura o usuário ANTES do background — User não existe na thread do Task.Run.
            var uid = _userManager.GetUserId(User);

            // Executa em background para não travar o request (operação pode levar 1-2 min)
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<FutebolContext>();
                var svc = scope.ServiceProvider.GetRequiredService<ApiFootballService>();
                try
                {
                    var (ok, msg) = await svc.ForcarReimportarEscalacaoAsync(ctx, id);

                    // A reimportação atualiza só as escalações compartilhadas (UsuarioId == null).
                    // Se o usuário já tinha uma cópia pessoal deste jogo, ela "sombreia" a nova e
                    // ele continuaria vendo a escalação antiga. Remove a cópia pessoal dele para que
                    // a tela Analisar recopie da importação fresca no próximo acesso.
                    //
                    // IMPORTANTE: apaga SOMENTE as fases INICIAL/FINAL (as que a cópia-do-compartilhado
                    // regenera). As fases táticas intermediárias (cronômetro) têm FaseEscalacao == Chave
                    // e NÃO são recriáveis pela importação — preservá-las evita perder a escalação que o
                    // usuário salvou durante o jogo.
                    if (ok && !string.IsNullOrEmpty(uid))
                    {
                        var pessoais = await ctx.Escalacoes
                            .Where(e => e.JogoId == id && e.UsuarioId == uid
                                     && (e.FaseEscalacao == "INICIAL"
                                         || e.FaseEscalacao == "FINAL"
                                         || e.FaseEscalacao == null))
                            .ToListAsync();
                        if (pessoais.Count > 0)
                        {
                            ctx.Escalacoes.RemoveRange(pessoais);
                            await ctx.SaveChangesAsync();
                        }

                        // Recria as cópias pessoais JÁ AQUI, em vez de esperar a tela
                        // recopiar sob demanda: a recópia da FINAL clona a INICIAL
                        // pessoal, então abrir a aba Final (ou uma fase tática) antes
                        // da Inicial deixava a Final sem fonte e ela caía nos fallbacks
                        // (escalação de outro jogo / slots vazios).
                        await RecriarEscalacoesPessoaisAsync(ctx, id, uid);
                    }

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

        // Recria as cópias pessoais INICIAL e FINAL a partir da importação fresca:
        // INICIAL = cópia da escalação compartilhada; FINAL = INICIAL com as
        // substituições importadas aplicadas (mesma lógica da tela Analisar).
        private static async Task RecriarEscalacoesPessoaisAsync(FutebolContext ctx, int jogoId, string usuarioId)
        {
            var compartilhadas = await ctx.Escalacoes
                .Where(e => e.JogoId == jogoId && e.UsuarioId == null
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                .ToListAsync();
            if (compartilhadas.Count == 0) return;

            Escalacao Clonar(Escalacao e, string fase) => new()
            {
                JogoId = jogoId,
                JogadorId = e.JogadorId,
                Titular = e.Titular,
                Posicao = e.Posicao,
                IsTimeCasa = e.IsTimeCasa,
                PosicaoX = e.PosicaoX,
                PosicaoY = e.PosicaoY,
                FaseEscalacao = fase,
                UsuarioId = usuarioId
            };

            var iniciais = compartilhadas.Select(e => Clonar(e, "INICIAL")).ToList();
            var finais   = compartilhadas.Select(e => Clonar(e, "FINAL")).ToList();

            // Aplica as substituições na FINAL: quem entrou assume a posição em
            // campo de quem saiu; quem saiu vai para o banco (se houver slot).
            var substituicoes = await ctx.Substituicoes
                .Where(s => s.JogoId == jogoId)
                .OrderBy(s => s.Minuto)
                .ToListAsync();

            foreach (var sub in substituicoes)
            {
                if (sub.JogadorSaiuId == null || sub.JogadorEntrouId == null
                    || sub.JogadorEntrouId == sub.JogadorSaiuId)
                    continue;

                var slotSaiu = finais.FirstOrDefault(c =>
                    c.JogadorId == sub.JogadorSaiuId && c.IsTimeCasa == sub.IsTimeCasa && c.Titular);
                if (slotSaiu == null) continue;

                bool jaEmCampo = finais.Any(c =>
                    c.JogadorId == sub.JogadorEntrouId && c.IsTimeCasa == sub.IsTimeCasa && c.Titular);
                if (jaEmCampo) continue;

                var slotEntrou = finais.FirstOrDefault(c =>
                    c.JogadorId == sub.JogadorEntrouId && c.IsTimeCasa == sub.IsTimeCasa && !c.Titular);

                if (slotEntrou != null)
                    slotEntrou.JogadorId = slotSaiu.JogadorId;
                slotSaiu.JogadorId = sub.JogadorEntrouId;
            }

            ctx.Escalacoes.AddRange(iniciais);
            ctx.Escalacoes.AddRange(finais);
            await ctx.SaveChangesAsync();
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

        // GET: Jogos/PreJogo/5 — resumo pré-jogo (V/E/D, forma, destaques, observações)
        // dos dois times, calculado a partir do banco local (sem depender da API externa).
        [HttpGet]
        public async Task<IActionResult> PreJogo(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var casa = await MontarResumoPreJogoAsync(jogo, jogo.TimeCasaId, jogo.TimeCasa);
            var visitante = await MontarResumoPreJogoAsync(jogo, jogo.TimeVisitanteId, jogo.TimeVisitante);

            return Json(new { casa, visitante });
        }

        // GET: Jogos/PosJogo/5 — resumo pós-jogo (placar, notas dos jogadores,
        // observações digitadas na partida e estatísticas), no estilo do modal Pré-jogo.
        [HttpGet]
        public async Task<IActionResult> PosJogo(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var usuarioId = _userManager.GetUserId(User);

            // ── Notas dos jogadores (do usuário atual) ─────────────────────────
            // Nota "oficial" do jogador é sempre a calculada nos rankings (base fixa +
            // ações), nunca a manual. Quando o usuário nunca abriu a avaliação desse
            // jogador nessa partida (não existe linha em Notas), o ranking em
            // JogadoresController ainda mostra uma "nota automática" calculada em cima
            // das estatísticas importadas (EstatisticaJogador) — reproduz o mesmo
            // fallback aqui, senão jogadores só com estatística importada (ex.: Harry
            // Kane num jogo nunca avaliado manualmente) ficam sem nota no pós-jogo.
            double NotaFinal(double notaValor) =>
                Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima, Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + notaValor)), 1);

            var notas = await _context.Notas
                .Where(n => n.JogoId == id && n.UsuarioId == usuarioId)
                .ToListAsync();
            var notasPorJogador = notas.ToDictionary(n => n.JogadorId, n => NotaFinal(n.Valor));

            // A api-football importa uma linha de EstatisticaJogador pra todo o elenco
            // relacionado, inclusive quem ficou no banco o jogo inteiro (com Minutos
            // zerado/nulo) — só entra no fallback quem de fato jogou, senão reservas
            // que não entraram apareceriam com nota (4,0, só a base) igual quem jogou.
            var jogadorIdsComNotaManual = notasPorJogador.Keys.ToHashSet();
            var estatisticasJogo = await _context.EstatisticasJogador
                .Where(e => e.JogoId == id && !jogadorIdsComNotaManual.Contains(e.JogadorId) && e.Minutos > 0)
                .ToListAsync();
            if (estatisticasJogo.Count > 0)
            {
                var criteriosCompartilhados = await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync();
                var criteriosUsuario = await _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToListAsync();
                var criteriosBanco = CriteriosNotaHelper.MergeCriterios(criteriosCompartilhados, criteriosUsuario);

                foreach (var e in estatisticasJogo)
                    notasPorJogador[e.JogadorId] = NotaFinal(CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco));
            }

            double? NotaDe(int? jogadorId) =>
                jogadorId.HasValue && notasPorJogador.TryGetValue(jogadorId.Value, out var v) ? v : (double?)null;

            // ── Cartões, substituições, gols e assistências (ícones de evento) ──
            var cartoes = await _context.Cartoes.Where(c => c.JogoId == id).ToListAsync();
            var substituicoes = await _context.Substituicoes.Where(s => s.JogoId == id).ToListAsync();
            var golsJogo = await _context.Gols.Where(g => g.JogoId == id && !g.Contra).ToListAsync();
            var assistsJogo = await _context.Assistencias.Where(a => a.JogoId == id).ToListAsync();

            // ── Escalação inicial — usada tanto na lista (com os eventos ocorridos
            // durante a partida) quanto no campinho, que mostra a formação de quem
            // começou o jogo, não a formação final pós-substituições. ──
            var escInicial = await _context.Escalacoes
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id && e.UsuarioId == usuarioId && e.FaseEscalacao == "INICIAL")
                .ToListAsync();

            // Posição granular (ex.: "Lateral Direito") a partir da coordenada do slot
            // na formação usada nesse jogo — mesma lógica do histórico de jogador em
            // /Jogadores/Estatisticas. Cai pra categoria ampla (Escalacao.Posicao,
            // "Goleiro"/"Defensor"/...) quando não dá pra casar com uma formação.
            var slotsPorFormacao = (await _context.PosicoesFormacao.ToListAsync())
                .GroupBy(p => p.FormacaoId)
                .ToDictionary(g => g.Key, g => g.ToList());
            string? PosicaoGranularDe(Escalacao e)
            {
                var formacaoId = e.IsTimeCasa ? jogo.FormacaoCasaId : jogo.FormacaoVisitanteId;
                if (formacaoId == null || !slotsPorFormacao.TryGetValue(formacaoId.Value, out var slots))
                    return null;
                return PosicaoJogadorHelper.PosicaoGranular(slots, e.PosicaoX, e.PosicaoY);
            }

            object MontarJogadorLista(Escalacao e)
            {
                var j = e.Jogador!;
                return new
                {
                    jogadorId = j.Id,
                    nome = j.Nome,
                    numero = j.NumeroCamisa,
                    titular = e.Titular,
                    posicao = PosicaoGranularDe(e) ?? e.Posicao,
                    nota = NotaDe(j.Id),
                    gols = golsJogo.Count(g => g.JogadorId == j.Id),
                    assistencias = assistsJogo.Count(a => a.JogadorId == j.Id),
                    cartoesAmarelos = cartoes.Where(c => c.JogadorId == j.Id && c.Tipo == "Amarelo").Select(c => c.Minuto).OrderBy(m => m).ToList(),
                    cartaoVermelho = cartoes.Where(c => c.JogadorId == j.Id && c.Tipo == "Vermelho").Select(c => (int?)c.Minuto).FirstOrDefault(),
                    saiuMinuto = substituicoes.Where(s => s.JogadorSaiuId == j.Id).Select(s => (int?)s.Minuto).FirstOrDefault(),
                    entrouMinuto = substituicoes.Where(s => s.JogadorEntrouId == j.Id).Select(s => (int?)s.Minuto).FirstOrDefault()
                };
            }

            object MontarJogadorCampo(Escalacao e)
            {
                var j = e.Jogador!;
                return new
                {
                    jogadorId = j.Id,
                    nome = j.Nome,
                    numero = j.NumeroCamisa,
                    posicaoX = e.PosicaoX,
                    posicaoY = e.PosicaoY,
                    nota = NotaDe(j.Id),
                    gols = golsJogo.Count(g => g.JogadorId == j.Id),
                    assistencias = assistsJogo.Count(a => a.JogadorId == j.Id)
                };
            }

            // Lista ordenada por posição em campo (goleiro → defensor → meia →
            // atacante), como num escrete real — Escalacao.Posicao guarda essas
            // categorias amplas ("Goleiro"/"Defensor"/"Meia"/"Atacante").
            int OrdemPosicao(Escalacao e) => (e.Posicao ?? "").Trim().ToUpperInvariant() switch
            {
                "GOLEIRO" => 0,
                "DEFENSOR" => 1,
                "MEIA" => 2,
                "ATACANTE" => 3,
                _ => 4,
            };
            int Numero(Escalacao e) => e.Jogador?.NumeroCamisa ?? 999;

            var lineup = new
            {
                casaTitulares = escInicial.Where(e => e.IsTimeCasa && e.Titular && e.Jogador != null).OrderBy(OrdemPosicao).ThenBy(Numero).Select(MontarJogadorLista).ToList(),
                casaReservas = escInicial.Where(e => e.IsTimeCasa && !e.Titular && e.Jogador != null).OrderBy(OrdemPosicao).ThenBy(Numero).Select(MontarJogadorLista).ToList(),
                visTitulares = escInicial.Where(e => !e.IsTimeCasa && e.Titular && e.Jogador != null).OrderBy(OrdemPosicao).ThenBy(Numero).Select(MontarJogadorLista).ToList(),
                visReservas = escInicial.Where(e => !e.IsTimeCasa && !e.Titular && e.Jogador != null).OrderBy(OrdemPosicao).ThenBy(Numero).Select(MontarJogadorLista).ToList(),
            };

            // ── Média de nota dos titulares (cabeçalho do modal) ────────────────
            double? Media(IEnumerable<Escalacao> titulares)
            {
                var valores = titulares.Select(e => NotaDe(e.Jogador?.Id)).Where(n => n.HasValue).Select(n => n!.Value).ToList();
                return valores.Count > 0 ? Math.Round(valores.Average(), 1) : (double?)null;
            }
            var mediaCasa = Media(escInicial.Where(e => e.IsTimeCasa && e.Titular && e.Jogador != null));
            var mediaVisitante = Media(escInicial.Where(e => !e.IsTimeCasa && e.Titular && e.Jogador != null));

            // ── Forma recente (últimos 5 jogos até esta partida, na mesma
            // competição/temporada) — mesmo padrão do modal Pré-jogo. ────────────
            async Task<List<string>> FormaRecenteAsync(int timeId)
            {
                var jogosTime = await _context.Jogos
                    .Where(j => j.CompeticaoId == jogo.CompeticaoId
                             && j.Temporada == jogo.Temporada
                             && j.Id != id
                             && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId)
                             && j.PlacarCasa != null && j.PlacarVisitante != null
                             && (jogo.Data == null || j.Data <= jogo.Data))
                    .OrderByDescending(j => j.Data)
                    .Take(5)
                    .Select(j => new { j.TimeCasaId, j.PlacarCasa, j.PlacarVisitante })
                    .ToListAsync();

                var resultado = jogosTime.Select(j =>
                {
                    bool ehCasa = j.TimeCasaId == timeId;
                    int golsTime = (ehCasa ? j.PlacarCasa : j.PlacarVisitante) ?? 0;
                    int golsOpp = (ehCasa ? j.PlacarVisitante : j.PlacarCasa) ?? 0;
                    return golsTime > golsOpp ? "V" : golsTime == golsOpp ? "E" : "D";
                }).ToList();
                resultado.Reverse();
                return resultado;
            }
            var formaCasa = await FormaRecenteAsync(jogo.TimeCasaId);
            var formaVisitante = await FormaRecenteAsync(jogo.TimeVisitanteId);

            var campo = new
            {
                casa = escInicial.Where(e => e.IsTimeCasa && e.Titular && e.Jogador != null).Select(MontarJogadorCampo).ToList(),
                visitante = escInicial.Where(e => !e.IsTimeCasa && e.Titular && e.Jogador != null).Select(MontarJogadorCampo).ToList(),
            };

            // ── Observações digitadas na partida (tags do usuário atual) ──────
            var observacoes = await _context.ObservacoesJogoTag
                .Include(o => o.Jogador)
                .Where(o => o.JogoId == id && o.UsuarioId == usuarioId)
                .OrderBy(o => o.Ordem)
                .Select(o => new { id = o.Id, tipo = o.Tipo, jogadorNome = o.Jogador != null ? o.Jogador.Nome : null, texto = o.Texto })
                .ToListAsync();

            // ── Estatísticas da partida (mesma fonte usada no painel de Estatísticas) ──
            var statsCasa = new Dictionary<string, string>();
            var statsVis = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(jogo.EstatisticasJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(jogo.EstatisticasJson);
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var timeId = item.GetProperty("TimeId").GetInt32();
                        var stats = new Dictionary<string, string>();
                        if (item.TryGetProperty("Stats", out var statsEl))
                        {
                            foreach (var prop in statsEl.EnumerateObject())
                                stats[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                                    ? "0" : (prop.Value.GetString() ?? "0");
                        }

                        if (timeId == jogo.TimeCasa?.IdApi) statsCasa = stats;
                        else if (timeId == jogo.TimeVisitante?.IdApi) statsVis = stats;
                    }
                }
                catch { /* JSON inválido/antigo — ignora */ }
            }

            string Pegar(Dictionary<string, string> s, string k) => s.TryGetValue(k, out var v) ? v : "0";

            var metricas = new (string label, string chave)[]
            {
                ("Posse de bola", "Ball Possession"),
                ("Finalizações totais", "Total Shots"),
                ("Finalizações no gol", "Shots on Goal"),
                ("Escanteios", "Corner Kicks"),
                ("Faltas", "Fouls"),
                ("Cartões amarelos", "Yellow Cards"),
                ("Cartões vermelhos", "Red Cards"),
                ("Defesas do goleiro", "Goalkeeper Saves"),
            };

            var estatisticas = metricas
                .Where(m => statsCasa.ContainsKey(m.chave) || statsVis.ContainsKey(m.chave))
                .Select(m => new { label = m.label, casa = Pegar(statsCasa, m.chave), vis = Pegar(statsVis, m.chave) })
                .ToList();

            // ── Estatísticas por jogador (destaque de cada time por métrica) ────
            // Mesma fonte da nota automática (EstatisticaJogador, importada da
            // api-football) — para cada métrica, pega quem mais se destacou em cada
            // time e compara os dois, igual ao painel "Estatísticas Jogador".
            var estatisticasJogadores = await _context.EstatisticasJogador
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == id)
                .ToListAsync();

            var isCasaPorJogador = escInicial
                .Where(e => e.JogadorId.HasValue)
                .ToDictionary(e => e.JogadorId!.Value, e => e.IsTimeCasa);
            bool EhCasaJogador(Jogador j) =>
                isCasaPorJogador.TryGetValue(j.Id, out var isCasa) ? isCasa : (j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId);

            var metricasJogador = new (string label, Func<EstatisticaJogador, int> valor)[]
            {
                ("Chutes", e => e.FinalizacoesTotal),
                ("Chutes a gol", e => e.FinalizacoesNoGol),
                ("Duelos disputados", e => e.DuelosTotal),
                ("Duelos ganhos", e => e.DuelosVencidos),
                ("Passes", e => e.PassesTotal),
                ("Passes-chave", e => e.PassesChave),
                ("Defesas", e => e.Defesas),
                ("Faltas cometidas", e => e.FaltasCometidas),
                ("Faltas sofridas", e => e.FaltasSofridas),
            };

            object? MelhorJogadorDaMetrica(IEnumerable<EstatisticaJogador> lista, Func<EstatisticaJogador, int> valor)
            {
                var melhor = lista.OrderByDescending(valor).FirstOrDefault();
                if (melhor == null || valor(melhor) <= 0) return null;
                return new
                {
                    nome = melhor.Jogador.Nome,
                    foto = string.IsNullOrEmpty(melhor.Jogador.FotoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = melhor.Jogador.FotoUrl }),
                    valor = valor(melhor)
                };
            }

            var estatJogadoresCasa = estatisticasJogadores.Where(e => EhCasaJogador(e.Jogador)).ToList();
            var estatJogadoresVis = estatisticasJogadores.Where(e => !EhCasaJogador(e.Jogador)).ToList();

            var estatisticasJogador = metricasJogador
                .Select(m => new
                {
                    label = m.label,
                    casa = MelhorJogadorDaMetrica(estatJogadoresCasa, m.valor),
                    vis = MelhorJogadorDaMetrica(estatJogadoresVis, m.valor),
                })
                .Where(x => x.casa != null || x.vis != null)
                .ToList();

            // ── Resumo textual automático ──────────────────────────────────────
            var totalGols = await _context.Gols.CountAsync(g => g.JogoId == id && !g.Contra);
            var cartoesAmarelos = await _context.Cartoes.CountAsync(c => c.JogoId == id && c.Tipo == "Amarelo");
            var cartoesVermelhos = await _context.Cartoes.CountAsync(c => c.JogoId == id && c.Tipo == "Vermelho");

            var resumo = new List<string>();
            if (jogo.PlacarCasa.HasValue && jogo.PlacarVisitante.HasValue)
            {
                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                    resumo.Add($"{jogo.TimeCasa?.Nome} venceu {jogo.TimeVisitante?.Nome} por {jogo.PlacarCasa} a {jogo.PlacarVisitante}.");
                else if (jogo.PlacarVisitante > jogo.PlacarCasa)
                    resumo.Add($"{jogo.TimeVisitante?.Nome} venceu {jogo.TimeCasa?.Nome} por {jogo.PlacarVisitante} a {jogo.PlacarCasa}.");
                else
                    resumo.Add($"Empate entre {jogo.TimeCasa?.Nome} e {jogo.TimeVisitante?.Nome} em {jogo.PlacarCasa} a {jogo.PlacarVisitante}.");
            }
            if (totalGols > 0 || cartoesAmarelos > 0 || cartoesVermelhos > 0)
            {
                var partes = new List<string> { $"{totalGols} gol{(totalGols != 1 ? "s" : "")}" };
                if (cartoesAmarelos > 0) partes.Add($"{cartoesAmarelos} cartão(ões) amarelo(s)");
                if (cartoesVermelhos > 0) partes.Add($"{cartoesVermelhos} cartão(ões) vermelho(s)");
                resumo.Add(string.Join(", ", partes) + " na partida.");
            }
            return Json(new
            {
                placarCasa = jogo.PlacarCasa,
                placarVisitante = jogo.PlacarVisitante,
                penaltisCasa = jogo.PenaltisCasa,
                penaltisVisitante = jogo.PenaltisVisitante,
                competicao = jogo.Competicao?.Nome,
                rodada = jogo.Rodada > 0 ? jogo.Rodada : (int?)null,
                data = DateHelper.FormatarData(jogo.Data, "dd/MM/yyyy · HH:mm"),
                casa = new { nome = jogo.TimeCasa?.Nome, escudo = string.IsNullOrEmpty(jogo.TimeCasa?.EscudoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = jogo.TimeCasa.EscudoUrl }) },
                visitante = new { nome = jogo.TimeVisitante?.Nome, escudo = string.IsNullOrEmpty(jogo.TimeVisitante?.EscudoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = jogo.TimeVisitante.EscudoUrl }) },
                mediaCasa,
                mediaVisitante,
                formaCasa,
                formaVisitante,
                resumo,
                lineup,
                campo,
                observacoes,
                estatisticas,
                estatisticasJogador
            });
        }

        // Resumo de um time para o modal Pré-jogo, restrito à mesma competição/temporada do jogo.
        private async Task<object> MontarResumoPreJogoAsync(Jogo jogo, int timeId, Time? time)
        {
            // Jogos finalizados do time na mesma competição/temporada (mais recentes primeiro)
            var jogosTime = await _context.Jogos
                .Where(j => j.CompeticaoId == jogo.CompeticaoId
                         && j.Temporada == jogo.Temporada
                         && j.Id != jogo.Id
                         && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId)
                         && j.PlacarCasa != null && j.PlacarVisitante != null)
                .OrderByDescending(j => j.Data)
                .Select(j => new { j.TimeCasaId, j.PlacarCasa, j.PlacarVisitante })
                .ToListAsync();

            int v = 0, e = 0, d = 0, golsPro = 0, golsContra = 0;
            var form = new List<string>();
            foreach (var j in jogosTime)
            {
                bool ehCasa = j.TimeCasaId == timeId;
                int golsTime = (ehCasa ? j.PlacarCasa : j.PlacarVisitante) ?? 0;
                int golsOpp = (ehCasa ? j.PlacarVisitante : j.PlacarCasa) ?? 0;
                golsPro += golsTime;
                golsContra += golsOpp;

                string r = golsTime > golsOpp ? "V" : golsTime == golsOpp ? "E" : "D";
                if (r == "V") v++; else if (r == "E") e++; else d++;
                if (form.Count < 5) form.Add(r);
            }
            int total = v + e + d;
            int aproveitamento = total > 0 ? (int)Math.Round((v * 3 + e) * 100.0 / (total * 3)) : 0;

            // Artilheiros do time na competição/temporada
            var artilheiros = await _context.Gols
                .Where(g => g.Jogo.CompeticaoId == jogo.CompeticaoId
                         && g.Jogo.Temporada == jogo.Temporada
                         && !g.Contra
                         && (g.Jogador.TimeId == timeId || g.Jogador.SelecaoId == timeId))
                .GroupBy(g => new { g.JogadorId, g.Jogador.Nome, g.Jogador.FotoUrl })
                .Select(grp => new { grp.Key.JogadorId, grp.Key.Nome, grp.Key.FotoUrl, Gols = grp.Count() })
                .OrderByDescending(x => x.Gols)
                .Take(5)
                .ToListAsync();

            // Assistências do time na competição/temporada (mapa jogador → total)
            var assistsLista = await _context.Assistencias
                .Where(a => a.Jogo.CompeticaoId == jogo.CompeticaoId
                         && a.Jogo.Temporada == jogo.Temporada
                         && (a.Jogador.TimeId == timeId || a.Jogador.SelecaoId == timeId))
                .GroupBy(a => new { a.JogadorId, a.Jogador.Nome, a.Jogador.FotoUrl })
                .Select(grp => new { grp.Key.JogadorId, grp.Key.Nome, grp.Key.FotoUrl, Assists = grp.Count() })
                .OrderByDescending(x => x.Assists)
                .ToListAsync();
            var assistsMap = assistsLista.ToDictionary(x => x.JogadorId, x => x.Assists);

            // Destaques: artilheiros + até 2 maiores assistentes que ainda não apareceram
            var idsArtilheiros = artilheiros.Select(a => a.JogadorId).ToHashSet();
            var destaques = artilheiros
                .Select(a => new
                {
                    nome = a.Nome,
                    foto = string.IsNullOrEmpty(a.FotoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = a.FotoUrl }),
                    gols = a.Gols,
                    assists = assistsMap.TryGetValue(a.JogadorId, out var asi) ? asi : 0
                })
                .Concat(assistsLista
                    .Where(a => !idsArtilheiros.Contains(a.JogadorId))
                    .Take(2)
                    .Select(a => new
                    {
                        nome = a.Nome,
                        foto = string.IsNullOrEmpty(a.FotoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = a.FotoUrl }),
                        gols = 0,
                        assists = a.Assists
                    }))
                .ToList();

            // Observações automáticas a partir dos números
            var observacoes = new List<string>();
            if (total == 0)
            {
                observacoes.Add("Sem jogos finalizados nesta competição/temporada.");
            }
            else
            {
                observacoes.Add($"{v}V · {e}E · {d}D em {total} jogo(s) — {aproveitamento}% de aproveitamento.");
                int saldo = golsPro - golsContra;
                observacoes.Add($"Gols: {golsPro} marcados, {golsContra} sofridos (saldo {(saldo >= 0 ? "+" : "")}{saldo}).");

                // Sequência atual (a partir do jogo mais recente)
                if (form.Count > 0)
                {
                    string atual = form[0];
                    int seq = 0;
                    foreach (var f in form) { if (f == atual) seq++; else break; }
                    if (seq > 1)
                    {
                        string plural = atual == "V" ? "vitórias" : atual == "E" ? "empates" : "derrotas";
                        observacoes.Add($"Sequência de {seq} {plural}.");
                    }
                }

                if (destaques.Count > 0 && destaques[0].gols > 0)
                {
                    var art = destaques[0];
                    observacoes.Add($"Destaque: {art.nome} ({art.gols} gol{(art.gols > 1 ? "s" : "")}).");
                }
            }

            return new
            {
                nome = time?.Nome,
                escudo = string.IsNullOrEmpty(time?.EscudoUrl) ? null : Url.Action("Imagem", "MediaProxy", new { url = time!.EscudoUrl }),
                vitorias = v,
                empates = e,
                derrotas = d,
                jogos = total,
                golsPro,
                golsContra,
                aproveitamento,
                form,
                destaques,
                observacoes
            };
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
            public List<SetaSlot> Setas { get; set; } = new();
        }

        public class SetaSlot
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class SalvarFaseTaticaRequest
        {
            public int JogoId { get; set; }
            public int Minuto { get; set; }
            public string? Nome { get; set; }
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
                MinutoInicio = Math.Max(0, req.Minuto),
                Nome = string.IsNullOrWhiteSpace(req.Nome) ? null : req.Nome.Trim()
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
                    UsuarioId = usuarioId,
                    // Congela as setas de movimentação atuais junto com a fase
                    Setas = s.Setas
                        .Select(t => new EscalacaoSeta { X = Math.Clamp(t.X, 0, 100), Y = Math.Clamp(t.Y, 0, 100) })
                        .ToList()
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

        // ── Setas de movimentação no campinho tático ────────────────────────

        public class AdicionarSetaRequest
        {
            public int EscalacaoId { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> AdicionarSeta([FromBody] AdicionarSetaRequest req)
        {
            if (req == null || req.EscalacaoId <= 0) return BadRequest("Dados inválidos.");
            var usuarioId = _userManager.GetUserId(User)!;

            var escalacao = await _context.Escalacoes
                .FirstOrDefaultAsync(e => e.Id == req.EscalacaoId
                                       && (e.UsuarioId == usuarioId || e.UsuarioId == null));
            if (escalacao == null) return NotFound();
            if (escalacao.JogadorId == null) return BadRequest("Slot sem jogador.");

            var seta = new EscalacaoSeta
            {
                EscalacaoId = escalacao.Id,
                X = Math.Clamp(req.X, 0, 100),
                Y = Math.Clamp(req.Y, 0, 100)
            };
            _context.SetasEscalacao.Add(seta);
            await _context.SaveChangesAsync();

            return Ok(new { seta.Id, seta.EscalacaoId, seta.X, seta.Y });
        }

        public class RemoverSetaRequest { public int Id { get; set; } }

        [HttpPost]
        public async Task<IActionResult> RemoverSeta([FromBody] RemoverSetaRequest req)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var seta = await _context.SetasEscalacao
                .FirstOrDefaultAsync(s => s.Id == req.Id
                                       && (s.Escalacao.UsuarioId == usuarioId || s.Escalacao.UsuarioId == null));
            if (seta == null) return NotFound();

            _context.SetasEscalacao.Remove(seta);
            await _context.SaveChangesAsync();
            return Ok(new { sucesso = true });
        }

        private static readonly string[] TiposObservacaoTagValidos = { "MANDANTE", "VISITANTE", "COMPETICAO", "JOGADOR", "MARCO" };

        public class ObservacaoTagRequest
        {
            public int JogoId { get; set; }
            public string Tipo { get; set; } = "";
            public int? JogadorId { get; set; }
            public string Texto { get; set; } = "";
        }

        [HttpPost]
        public async Task<IActionResult> AdicionarObservacaoTag([FromBody] ObservacaoTagRequest req)
        {
            if (req == null || req.JogoId <= 0 || !TiposObservacaoTagValidos.Contains(req.Tipo) || string.IsNullOrWhiteSpace(req.Texto))
                return BadRequest("Dados inválidos.");
            if (req.Tipo == "JOGADOR" && (req.JogadorId is null || req.JogadorId <= 0))
                return BadRequest("Selecione o jogador.");

            var usuarioId = _userManager.GetUserId(User)!;

            var ordemMax = await _context.ObservacoesJogoTag
                .Where(o => o.JogoId == req.JogoId && o.UsuarioId == usuarioId)
                .Select(o => (int?)o.Ordem).MaxAsync() ?? 0;

            var obs = new ObservacaoJogoTag
            {
                JogoId = req.JogoId,
                UsuarioId = usuarioId,
                Tipo = req.Tipo,
                JogadorId = req.Tipo == "JOGADOR" ? req.JogadorId : null,
                Texto = req.Texto.Trim(),
                Ordem = ordemMax + 1
            };
            _context.ObservacoesJogoTag.Add(obs);
            await _context.SaveChangesAsync();

            string? jogadorNome = null;
            if (obs.JogadorId.HasValue)
            {
                var jogadorObs = await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == obs.JogadorId);
                jogadorNome = jogadorObs?.NomeExibicao;
            }

            return Ok(new { obs.Id, obs.Tipo, obs.JogadorId, jogadorNome, obs.Texto });
        }

        public class RemoverObservacaoTagRequest { public int Id { get; set; } }

        [HttpPost]
        public async Task<IActionResult> RemoverObservacaoTag([FromBody] RemoverObservacaoTagRequest req)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            // Delete atômico: com carregar-e-remover, um clique duplo fazia a 2ª
            // requisição deletar uma linha já apagada e o EF estourava
            // DbUpdateConcurrencyException (500 em produção, 11/07/2026).
            var removidos = await _context.ObservacoesJogoTag
                .Where(o => o.Id == req.Id && o.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();
            if (removidos == 0) return NotFound();
            return Ok(new { sucesso = true });
        }
    }

}
