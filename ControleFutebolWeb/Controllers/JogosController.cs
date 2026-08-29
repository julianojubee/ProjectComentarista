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
        private readonly TransmissaoJogoService _transmissaoJogo;
        private readonly EspnEstatisticasService _espn;
        private readonly EspnEscalacaoService _espnEscalacao;
        private readonly EspnEventosService _espnEventos;
        private readonly CraqueDaPartidaService _craques;

        public JogosController(FutebolContext context, ILogger<JogosController> logger, ApiFootballService transfermarkt, IServiceScopeFactory scopeFactory, UserManager<ApplicationUser> userManager, TransmissaoJogoService transmissaoJogo, EspnEstatisticasService espn, EspnEscalacaoService espnEscalacao, EspnEventosService espnEventos, CraqueDaPartidaService craques)
        {
            _context = context;
            _logger = logger;
            _transfermarkt = transfermarkt;
            _scopeFactory = scopeFactory;
            _userManager = userManager;
            _transmissaoJogo = transmissaoJogo;
            _espn = espn;
            _espnEscalacao = espnEscalacao;
            _espnEventos = espnEventos;
            _craques = craques;
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

            // As datas do filtro chegam como dia puro no fuso de Brasília, mas j.Data está
            // em UTC. Sem converter, um jogo de 30/05 13:30 BRT (16:30 UTC) ficava de fora
            // do filtro 30/05–30/05. O fim do intervalo é exclusivo no dia seguinte pra
            // incluir o dia inteiro, não só a meia-noite.
            if (startDate.HasValue)
            {
                var inicioUtc = DateHelper.DeBrasiliaParaUtc(startDate.Value.Date)!.Value;
                jogosQuery = jogosQuery.Where(j => j.Data >= inicioUtc);
            }
            if (endDate.HasValue)
            {
                var fimUtc = DateHelper.DeBrasiliaParaUtc(endDate.Value.Date.AddDays(1))!.Value;
                jogosQuery = jogosQuery.Where(j => j.Data < fimUtc);
            }

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


        // GET: Jogos/Create
        public IActionResult Create()
        {
            ViewBag.TimeCasaId = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.TimeVisitanteId = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.FormacaoCasaId = new SelectList(_context.Formacoes, "Id", "Nome");
            ViewBag.FormacaoVisitanteId = new SelectList(_context.Formacoes, "Id", "Nome");
            ViewBag.CompeticaoId = new SelectList(_context.Competicoes.OrderBy(c => c.Nome), "Id", "Nome");
            return View();
        }

        // POST: Jogos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Jogo jogo)
        {
            // O formulário recebe a data no fuso de Brasília (mesma convenção do Edit) —
            // precisa converter pra UTC antes de salvar, senão o jogo fica ~3h atrasado no banco.
            jogo.Data = DateHelper.DeBrasiliaParaUtc(jogo.Data);


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

                // Jogo dentro da janela que o futnatv já publica: busca a transmissão na hora
                // em vez de esperar o próximo ciclo do AtualizarTransmissoesService (até 3h
                // depois). Falha aqui não pode derrubar o cadastro do jogo — só fica sem a
                // transmissão por ora.
                var diaBrasilDoJogo = TransmissaoJogoService.DiaBrasilDoJogo(jogo.Data);
                if (TransmissaoJogoService.EstaNaJanela(diaBrasilDoJogo))
                {
                    try
                    {
                        await _transmissaoJogo.AtualizarTransmissoesDoDiaAsync(diaBrasilDoJogo.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Jogos/Create] Falha ao buscar transmissão do jogo {Id} recém-criado.", jogo.Id);
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.TimeCasaId = new SelectList(_context.Times, "Id", "Nome", jogo.TimeCasaId);
            ViewBag.TimeVisitanteId = new SelectList(_context.Times, "Id", "Nome", jogo.TimeVisitanteId);
            ViewBag.FormacaoCasaId = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoCasaId);
            ViewBag.FormacaoVisitanteId = new SelectList(_context.Formacoes, "Id", "Nome", jogo.FormacaoVisitanteId);
            ViewBag.CompeticaoId = new SelectList(_context.Competicoes.OrderBy(c => c.Nome), "Id", "Nome", jogo.CompeticaoId);

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

            // Capitão de cada time neste jogo, vindo das estatísticas importadas da api-football.
            vm.JogadoresCapitao = (await _context.EstatisticasJogador
                .Where(e => e.JogoId == id && e.Capitao)
                .Select(e => e.JogadorId)
                .ToListAsync())
                .ToHashSet();

            // Craque da partida (a coroa). Calcula e grava na primeira vez que a análise
            // do jogo é aberta — é por aqui que o histórico antigo vai sendo preenchido.
            var craque = await _craques.ObterDoJogoAsync(id, usuarioId);
            vm.CraqueJogadorId = craque?.JogadorId;
            vm.CraqueNota = craque?.Nota ?? 0;

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
                        .Include(j => j.Nacionalidade).Include(j => j.Time)
                        .Where(j => j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId).ToListAsync();
                    vm.JogadoresVisitante = await _context.Jogadores
                        .Include(j => j.Nacionalidade).Include(j => j.Time)
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

                    await PreencherDadosTooltipAsync(vm, jogo, escFase, usuarioId);
                    await PreencherOrigemEscalacaoAsync(vm, id, faseAtual, escFase);

                    return View(vm);
                }
            }

            var escalacoesFinaisExistem = await _context.Escalacoes
                .AnyAsync(e => e.JogoId == id && e.FaseEscalacao == "FINAL" && e.UsuarioId == usuarioId);

            if (faseAtual == "FINAL" && !escalacoesFinaisExistem)
            {
                var clonesFinais = await ConstruirFinalDaInicialAsync(_context, id, usuarioId);
                if (clonesFinais.Any())
                {
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

            // Se não tiver escalações do usuário, copia das compartilhadas (importadas da API, UsuarioId == null).
            //
            // A cópia pessoal com TODOS os slots vazios conta como "não tem": ela nasce
            // quando o jogo é aberto antes de a API ter a escalação e depois sombreava a
            // importação que chegou — o campo continuava vazio para sempre, sem o usuário
            // entender o motivo. Nesse caso ela é descartada e recopiada da importação.
            bool pessoaisVazias = escalacoes.Any() && escalacoes.All(e => e.JogadorId == null);

            if (!escalacoes.Any() || pessoaisVazias)
            {
                var compartilhadas = await _context.Escalacoes
                    .Where(e => e.JogoId == id
                             && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null))
                             && e.UsuarioId == null)
                    .ToListAsync();

                // Trocar slots vazios por outros slots vazios não ajuda em nada.
                if (pessoaisVazias && !compartilhadas.Any(e => e.JogadorId != null))
                    compartilhadas.Clear();

                if (compartilhadas.Any())
                {
                    if (pessoaisVazias)
                        _context.Escalacoes.RemoveRange(escalacoes);

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
            // Sem escalação (importação não trouxe nada) → monta a base pelo
            // EscalacaoBaseHelper (último jogo → escalação padrão → formação vazia).
            // Troca de formação COM jogadores em campo → reposiciona os jogadores nos
            // slots da nova formação (só mudam de lugar, não são apagados). Troca de
            // formação sem jogadores → slots vazios da nova formação.
            var idFormacaoCasa = formacaoCasaId ?? jogo.FormacaoCasaId ?? 0;
            bool formacaoCasaMudou = formacaoCasaId.HasValue && formacaoCasaId.Value != (jogo.FormacaoCasaId ?? 0);

            if (escalacoesCasa.Count == 0 || formacaoCasaMudou)
            {
                EscalacaoBaseHelper.Resultado baseCasa;
                if (formacaoCasaMudou && escalacoesCasa.Any(e => e.JogadorId != null))
                    baseCasa = await EscalacaoBaseHelper.RemapearFormacaoAsync(_context, jogo, true, usuarioId, faseAtual, idFormacaoCasa, escalacoesCasa);
                else if (formacaoCasaMudou)
                    baseCasa = await EscalacaoBaseHelper.MontarDaFormacaoAsync(_context, jogo, true, usuarioId, faseAtual, idFormacaoCasa);
                else
                    baseCasa = await EscalacaoBaseHelper.MontarAsync(_context, jogo, true, usuarioId, faseAtual, idFormacaoCasa);

                if (escalacoesCasa.Count > 0)
                    _context.Escalacoes.RemoveRange(escalacoesCasa);

                _context.Escalacoes.AddRange(baseCasa.Escalacoes);
                if (baseCasa.FormacaoId > 0) idFormacaoCasa = baseCasa.FormacaoId;
                if (idFormacaoCasa > 0) jogo.FormacaoCasaId = idFormacaoCasa;
            }

            // ── VISITANTE ─────────────────────────────────────────────────────
            var idFormacaoVisitante = formacaoVisitanteId ?? jogo.FormacaoVisitanteId ?? 0;
            bool formacaoVisitanteMudou = formacaoVisitanteId.HasValue && formacaoVisitanteId.Value != (jogo.FormacaoVisitanteId ?? 0);

            if (escalacoesVisitante.Count == 0 || formacaoVisitanteMudou)
            {
                EscalacaoBaseHelper.Resultado baseVisitante;
                if (formacaoVisitanteMudou && escalacoesVisitante.Any(e => e.JogadorId != null))
                    baseVisitante = await EscalacaoBaseHelper.RemapearFormacaoAsync(_context, jogo, false, usuarioId, faseAtual, idFormacaoVisitante, escalacoesVisitante);
                else if (formacaoVisitanteMudou)
                    baseVisitante = await EscalacaoBaseHelper.MontarDaFormacaoAsync(_context, jogo, false, usuarioId, faseAtual, idFormacaoVisitante);
                else
                    baseVisitante = await EscalacaoBaseHelper.MontarAsync(_context, jogo, false, usuarioId, faseAtual, idFormacaoVisitante);

                if (escalacoesVisitante.Count > 0)
                    _context.Escalacoes.RemoveRange(escalacoesVisitante);

                _context.Escalacoes.AddRange(baseVisitante.Escalacoes);
                if (baseVisitante.FormacaoId > 0) idFormacaoVisitante = baseVisitante.FormacaoId;
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

                // Nacionalidade/Time: o tooltip ℹ da lista lateral mostra bandeira e
                // clube, como o dos jogadores em campo.
                var jogadoresCasaFinal = await _context.Jogadores
                    .Include(j => j.Nacionalidade).Include(j => j.Time)
                    .Where(j => listaFinalCasaIds.Contains(j.Id))
                    .ToListAsync();

                var jogadoresVisitanteFinal = await _context.Jogadores
                    .Include(j => j.Nacionalidade).Include(j => j.Time)
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
                    .Include(j => j.Nacionalidade).Include(j => j.Time)
                    .Where(j => j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId).ToListAsync();
                var jogadoresVisitante = await _context.Jogadores
                    .Include(j => j.Nacionalidade).Include(j => j.Time)
                    .Where(j => j.TimeId == jogo.TimeVisitanteId || j.SelecaoId == jogo.TimeVisitanteId).ToListAsync();

                // Jogo ainda não aconteceu (sem lineup importada) — nenhum jogador foi
                // descoberto ainda para este time. Busca o elenco completo na api-football
                // para que apareçam disponíveis na lista lateral.
                if (jogadoresCasa.Count == 0)
                {
                    var criados = await _transfermarkt.ImportarElencoAsync(_context, jogo.TimeCasa);
                    if (criados > 0)
                        jogadoresCasa = await _context.Jogadores
                            .Include(j => j.Nacionalidade).Include(j => j.Time)
                            .Where(j => j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId).ToListAsync();
                }
                if (jogadoresVisitante.Count == 0)
                {
                    var criados = await _transfermarkt.ImportarElencoAsync(_context, jogo.TimeVisitante);
                    if (criados > 0)
                        jogadoresVisitante = await _context.Jogadores
                            .Include(j => j.Nacionalidade).Include(j => j.Time)
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

            await PreencherDadosTooltipAsync(vm, jogo, escalacoes, usuarioId);
            await PreencherOrigemEscalacaoAsync(vm, id, faseAtual, escalacoes);

            return View(vm);
        }

        // Origem da escalação que está na tela, para o selo da barra de status.
        //
        // A importação grava as escalações como linhas compartilhadas (UsuarioId ==
        // null) e a tela copia essas linhas para o usuário; quando a API não trouxe
        // nada, a escalação é montada pela última de cada time (EscalacaoBaseHelper) e
        // fica igualzinha na tela — sem o selo não havia como diferenciar as duas, e o
        // erro só aparecia no meio do jogo.
        private async Task PreencherOrigemEscalacaoAsync(
            AnalisarViewModel vm, int jogoId, string faseAtual, IEnumerable<Escalacao> escalacoesTela)
        {
            var importadas = await _context.Escalacoes
                .Where(e => e.JogoId == jogoId && e.UsuarioId == null && e.JogadorId != null
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                .Select(e => new { e.IsTimeCasa, e.Titular, e.Fonte, JogadorId = e.JogadorId!.Value })
                .ToListAsync();

            // Fonte null em linha importada é import antigo, anterior à coluna: conta
            // como api-football, que era a única fonte que gravava compartilhada.
            string? FonteDoLado(bool casa) => importadas.Any(e => e.IsTimeCasa == casa)
                ? importadas.First(e => e.IsTimeCasa == casa).Fonte ?? FonteEscalacao.ApiFootball
                : null;

            vm.FonteEscalacaoCasa = FonteDoLado(true);
            vm.FonteEscalacaoVisitante = FonteDoLado(false);

            // Botão "Buscar ESPN". Duas situações o justificam, e as duas exigem a
            // competição mapeada em ligas-espn.json — sem slug a chamada só devolveria
            // erro:
            //
            //  1. falta escalação de algum lado (o que está em campo é chute);
            //  2. a escalação está completa mas falta estatística. É o jogo que veio da
            //     ESPN numa passada anterior: a escalação entrou e a estatística ficou
            //     para trás, e resolver isso exigia sair daqui e ir em Serviços › Jogos
            //     sem estatísticas.
            //
            // Jogo que ainda não começou não entra por (2): estatística faltando ali é
            // o normal, não um problema a corrigir.
            var faltaEscalacao = !vm.EscalacaoApiCasa || !vm.EscalacaoApiVisitante;

            var jogoJaComecou = vm.Jogo.Data != null && vm.Jogo.Data <= DateTime.UtcNow;

            var faltaEstatistica = jogoJaComecou
                && await EstatisticasFaltandoAsync(_context, jogoId);

            var faltaEvento = jogoJaComecou
                && await EventosFaltandoAsync(_context, jogoId);

            if (faltaEscalacao || faltaEstatistica || faltaEvento)
            {
                var idApiLiga = await _context.Jogos
                    .Where(j => j.Id == jogoId)
                    .Select(j => j.Competicao!.IdApi)
                    .FirstOrDefaultAsync();

                vm.EspnDisponivel = _espn.SlugDaLiga(idApiLiga) != null;
                vm.EspnSoComplementos = vm.EspnDisponivel && !faltaEscalacao;
            }

            // Na FINAL a escalação da tela difere da importada por construção (as
            // substituições), então a comparação só vale na INICIAL.
            if (faseAtual != "INICIAL" || importadas.Count == 0) return;

            bool MesmosTitulares(bool casa)
            {
                var daApi = importadas.Where(e => e.IsTimeCasa == casa && e.Titular)
                    .Select(e => e.JogadorId).ToHashSet();
                if (daApi.Count == 0) return true;   // lado sem importação não conta como edição
                var naTela = escalacoesTela
                    .Where(e => e.IsTimeCasa == casa && e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value).ToHashSet();
                return daApi.SetEquals(naTela);
            }

            vm.EscalacaoEditada = !MesmosTitulares(true) || !MesmosTitulares(false);
        }

        // Preenche os dados do tooltip de info do jogador, separados por escopo:
        // linha "Competição" (gols/assists/titular na competição deste jogo) e
        // linha "Temporada" (mesmos números na temporada, todas as competições),
        // além das médias por jogo. Usado pelos dois caminhos do Analisar.
        //
        // temporadaFiltro = rótulo da temporada (o mesmo de Jogo.Temporada) que
        // define o recorte de TODAS as seções; 0 = carreira inteira ("Todas").
        // Null usa a temporada do próprio jogo — sem isso o tooltip do primeiro
        // jogo de uma temporada nova mostrava os números da temporada anterior.
        private async Task PreencherDadosTooltipAsync(
            AnalisarViewModel vm, Jogo jogo, IEnumerable<Escalacao> escalacoes, string usuarioId,
            int? temporadaFiltro = null)
        {
            // Universo de jogadores que a tela realmente mostra: quem está escalado
            // (campo/banco) mais os elencos das listas laterais — o mesmo conjunto
            // que a view usa para montar o mapa DADOS_JOGADORES do tooltip.
            //
            // Sem esse recorte as agregações varriam a competição/temporada inteira e
            // os dicionários iam serializados por completo no HTML (milhares de
            // jogadores que a tela nunca desenha). Usar UM conjunto para todas as
            // consultas também alinha as linhas do tooltip: antes gols/assists vinham
            // de todo mundo e médias/titular só dos escalados, então o jogador
            // arrastado da lista lateral aparecia sem médias.
            var idsTooltip = escalacoes
                .Where(e => e.JogadorId != null)
                .Select(e => e.JogadorId!.Value)
                .Concat(vm.JogadoresCasa.Select(j => j.Id))
                .Concat(vm.JogadoresVisitante.Select(j => j.Id))
                .Distinct()
                .ToList();

            if (idsTooltip.Count == 0) return;

            var temporadaSel = temporadaFiltro ?? jogo.Temporada;
            var dados = await CalcularTooltipAsync(idsTooltip, jogo, usuarioId, temporadaSel);

            vm.GolsPorJogador = dados.Gols;
            vm.AssistsPorJogador = dados.Assists;
            vm.GolsTemporadaPorJogador = dados.GolsTemporada;
            vm.AssistsTemporadaPorJogador = dados.AssistsTemporada;
            vm.MediasPorJogador = dados.Medias;
            vm.TitularPorJogador = dados.TitularCompeticao;
            vm.TitularTemporadaPorJogador = dados.TitularTemporada;
            vm.TimeAnteriorPorJogador = dados.TimeAnterior;
            vm.TemporadaTooltip = temporadaSel;
            vm.TemporadasTooltip = await TemporadasDoTooltipAsync(idsTooltip, jogo.Temporada);
        }

        // Temporadas que o seletor do tooltip oferece: todas em que os jogadores da
        // tela têm estatística importada, mais a do próprio jogo (que pode ainda não
        // ter estatística nenhuma — caso do primeiro jogo da temporada).
        private async Task<List<int>> TemporadasDoTooltipAsync(
            IReadOnlyCollection<int> ids, int temporadaJogo)
        {
            var temporadas = await _context.EstatisticasJogador
                .Where(e => ids.Contains(e.JogadorId) && e.Jogo.Temporada > 0)
                .Select(e => e.Jogo.Temporada)
                .Distinct()
                .ToListAsync();

            if (temporadaJogo > 0 && !temporadas.Contains(temporadaJogo))
                temporadas.Add(temporadaJogo);

            return temporadas.OrderByDescending(t => t).ToList();
        }

        // Calcula as três seções do tooltip (Competição, Temporada e médias por jogo)
        // com o mesmo recorte de temporada. temporada = rótulo (Jogo.Temporada);
        // 0 = sem recorte (carreira inteira).
        private async Task<TooltipJogadorDados> CalcularTooltipAsync(
            IReadOnlyCollection<int> ids, Jogo jogo, string usuarioId, int temporada)
        {
            var dados = new TooltipJogadorDados { Temporada = temporada };

            // O rótulo Temporada segue a api-football: ligas européias usam o ano de
            // INÍCIO (2025 = 2025/26) e competições de ano civil (Brasileirão, Copa do
            // Mundo) o próprio ano. Comparar o int direto deixava de fora os jogos de
            // clube do jogador (Bayern Temporada 2025 vs Copa Temporada 2026) e a linha
            // "Temporada" ficava igual à "Competição". Normaliza pelo ANO DE TÉRMINO:
            // uma (competição, temporada) "cruza o ano" quando tem jogos em ano civil
            // maior que o rótulo — nesse caso termina em Temporada+1. Dois jogos são da
            // mesma temporada quando terminam no mesmo ano.
            int? anoTermino = null;
            var compsCruzadasAnterior = new List<int>();
            var compsCruzadasAtual = new List<int>();
            var cruzaAno = false;

            if (temporada > 0)
            {
                cruzaAno = await _context.Jogos.AnyAsync(j =>
                    j.CompeticaoId == jogo.CompeticaoId && j.Temporada == temporada &&
                    j.Data != null && j.Data.Value.Year > j.Temporada);
                anoTermino = temporada + (cruzaAno ? 1 : 0);

                // Competições cujo rótulo (anoTermino-1) cruza o ano → terminam em anoTermino (entram)
                compsCruzadasAnterior = await _context.Jogos
                    .Where(j => j.Temporada == anoTermino - 1 && j.Data != null && j.Data.Value.Year > j.Temporada)
                    .Select(j => j.CompeticaoId).Distinct().ToListAsync();

                // Competições cujo rótulo anoTermino cruza o ano → terminam em anoTermino+1 (saem)
                compsCruzadasAtual = await _context.Jogos
                    .Where(j => j.Temporada == anoTermino && j.Data != null && j.Data.Value.Year > j.Temporada)
                    .Select(j => j.CompeticaoId).Distinct().ToListAsync();
            }

            // Recorte de temporada repetido nas consultas abaixo — inline porque o
            // EF não traduz chamada de função local dentro da árvore de expressão.
            // semFiltro = "Todas as temporadas".
            var semFiltro = anoTermino == null;
            var ano = anoTermino ?? 0;

            // ── Linha "Competição": só esta competição, dentro da temporada ────
            dados.Gols = await _context.Gols
                .Where(g => g.Jogo.CompeticaoId == jogo.CompeticaoId && !g.Contra
                         && ids.Contains(g.JogadorId)
                         && (semFiltro
                             || (g.Jogo.Temporada == ano && !compsCruzadasAtual.Contains(g.Jogo.CompeticaoId))
                             || (g.Jogo.Temporada == ano - 1 && compsCruzadasAnterior.Contains(g.Jogo.CompeticaoId))))
                .GroupBy(g => g.JogadorId)
                .Select(g => new { JogadorId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

            dados.Assists = await _context.Assistencias
                .Where(a => a.Jogo.CompeticaoId == jogo.CompeticaoId
                         && ids.Contains(a.JogadorId)
                         && (semFiltro
                             || (a.Jogo.Temporada == ano && !compsCruzadasAtual.Contains(a.Jogo.CompeticaoId))
                             || (a.Jogo.Temporada == ano - 1 && compsCruzadasAnterior.Contains(a.Jogo.CompeticaoId))))
                .GroupBy(a => a.JogadorId)
                .Select(a => new { JogadorId = a.Key, Total = a.Count() })
                .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

            dados.TitularCompeticao = await CalcularTitularesPorJogadorAsync(
                ids, usuarioId, competicaoId: jogo.CompeticaoId,
                temporadaAnoTermino: anoTermino,
                compsCruzadasAnterior: compsCruzadasAnterior,
                compsCruzadasAtual: compsCruzadasAtual);

            // ── Linha "Temporada": todas as competições do mesmo ano ──────────
            // Temporada 0 (jogo sem temporada, ou "Todas") esconde a linha na view.
            if (anoTermino != null)
            {
                dados.GolsTemporada = await _context.Gols
                    .Where(g => !g.Contra && ids.Contains(g.JogadorId)
                             && ((g.Jogo.Temporada == ano && !compsCruzadasAtual.Contains(g.Jogo.CompeticaoId))
                              || (g.Jogo.Temporada == ano - 1 && compsCruzadasAnterior.Contains(g.Jogo.CompeticaoId))))
                    .GroupBy(g => g.JogadorId)
                    .Select(g => new { JogadorId = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

                dados.AssistsTemporada = await _context.Assistencias
                    .Where(a => ids.Contains(a.JogadorId)
                             && ((a.Jogo.Temporada == ano && !compsCruzadasAtual.Contains(a.Jogo.CompeticaoId))
                              || (a.Jogo.Temporada == ano - 1 && compsCruzadasAnterior.Contains(a.Jogo.CompeticaoId))))
                    .GroupBy(a => a.JogadorId)
                    .Select(a => new { JogadorId = a.Key, Total = a.Count() })
                    .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

                dados.TitularTemporada = await CalcularTitularesPorJogadorAsync(
                    ids, usuarioId,
                    temporadaAnoTermino: anoTermino,
                    compsCruzadasAnterior: compsCruzadasAnterior,
                    compsCruzadasAtual: compsCruzadasAtual);
            }

            dados.Medias = await CalcularMediasPorJogadorAsync(
                ids, anoTermino, compsCruzadasAnterior, compsCruzadasAtual);

            // "Vinha do": só faz sentido com uma temporada escolhida (a anterior é
            // sempre o rótulo - 1). Em "Todas as temporadas" não há referência.
            if (temporada > 0)
                dados.TimeAnterior = await CalcularTimeAnteriorAsync(ids, usuarioId, temporada - 1, cruzaAno);

            return dados;
        }

        // Clube pelo qual cada jogador atuou na temporada anterior, quando ele NÃO
        // atuou pelo clube atual naquela temporada — ou seja, chegou depois. É o que
        // o tooltip mostra como "Vinha do", para o analista reconhecer os reforços.
        //
        // Duas fontes, nesta ordem:
        //  1. Escalação dos jogos daquela temporada (o lado do jogo diz o clube) —
        //     é a mais confiável e ainda dá quantos jogos ele fez lá;
        //  2. Janela de Transferências (tabela Transferencia, alimentada por
        //     /Times → Transferências da API e pelas trocas manuais), para o reforço
        //     de liga que não tem jogo importado: aí só o clube de origem é conhecido,
        //     sem contagem de jogos.
        // Jogos de seleção ficam de fora — a comparação é entre clubes.
        private async Task<Dictionary<int, TimeAnteriorJogador>> CalcularTimeAnteriorAsync(
            IReadOnlyCollection<int> ids, string usuarioId, int temporadaAnterior, bool cruzaAno)
        {
            if (ids.Count == 0 || temporadaAnterior <= 0) return new();

            var timeAtual = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .Select(j => new { j.Id, j.TimeId })
                .ToDictionaryAsync(j => j.Id, j => j.TimeId);

            // Distinct por (jogador, jogo, time): o mesmo jogo tem escalação INICIAL e
            // FINAL, e ainda a do import junto com a do usuário — sem isso um jogo
            // valeria três na contagem.
            var participacoes = await _context.Escalacoes
                .Where(e => e.JogadorId != null && ids.Contains(e.JogadorId!.Value)
                         && e.Jogo.Temporada == temporadaAnterior
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null))
                .Select(e => new
                {
                    JogadorId = e.JogadorId!.Value,
                    e.JogoId,
                    TimeId = e.IsTimeCasa ? e.Jogo.TimeCasaId : e.Jogo.TimeVisitanteId,
                    EhSelecao = e.IsTimeCasa ? e.Jogo.TimeCasa.EhSelecao : e.Jogo.TimeVisitante.EhSelecao,
                })
                .Distinct()
                .ToListAsync();

            var anteriores = new Dictionary<int, (int TimeId, int Jogos)>();
            foreach (var grupo in participacoes.Where(p => !p.EhSelecao).GroupBy(p => p.JogadorId))
            {
                if (!timeAtual.TryGetValue(grupo.Key, out var atual)) continue;

                var porTime = grupo.GroupBy(p => p.TimeId)
                    .Select(g => new { TimeId = g.Key, Jogos = g.Count() })
                    .ToList();

                // Jogou pelo clube de hoje na temporada passada → não é reforço.
                if (porTime.Any(t => t.TimeId == atual)) continue;

                var principal = porTime.OrderByDescending(t => t.Jogos).FirstOrDefault();
                if (principal != null) anteriores[grupo.Key] = (principal.TimeId, principal.Jogos);
            }

            // ── Fonte 2: janela de transferências ─────────────────────────────
            // Só para quem a escalação não resolveu: sem jogo pelo clube atual e sem
            // jogo por clube nenhum na temporada passada. Vale a chegada MAIS RECENTE
            // ao clube de hoje, e só a partir do início da temporada atual — uma
            // transferência de anos atrás não diz nada sobre a temporada passada.
            var jogouNaTemporada = participacoes.Where(p => !p.EhSelecao)
                .Select(p => p.JogadorId).ToHashSet();
            var semJogos = ids.Where(id => !anteriores.ContainsKey(id) && !jogouNaTemporada.Contains(id))
                .ToList();

            if (semJogos.Count > 0)
            {
                // Início da temporada atual: julho quando a competição cruza o ano
                // civil (Europa), janeiro quando é de ano civil (Brasil, MLS…).
                var inicioTemporada = new DateTime(
                    temporadaAnterior + 1, cruzaAno ? 7 : 1, 1, 0, 0, 0, DateTimeKind.Utc);

                var chegadas = await _context.Transferencias
                    .Where(t => semJogos.Contains(t.JogadorId)
                             && t.TimeOrigemId != null && t.TimeDestinoId != null
                             && t.Data >= inicioTemporada)
                    .Select(t => new { t.JogadorId, TimeOrigemId = t.TimeOrigemId!.Value, TimeDestinoId = t.TimeDestinoId!.Value, t.Data, t.Id })
                    .ToListAsync();

                foreach (var grupo in chegadas.GroupBy(t => t.JogadorId))
                {
                    if (!timeAtual.TryGetValue(grupo.Key, out var atual)) continue;

                    var chegada = grupo
                        .Where(t => t.TimeDestinoId == atual && t.TimeOrigemId != atual)
                        .OrderByDescending(t => t.Data).ThenByDescending(t => t.Id)
                        .FirstOrDefault();

                    // Jogos = 0: a transferência não conta partidas, e o tooltip
                    // omite o "· N jogos" nesse caso.
                    if (chegada != null) anteriores[grupo.Key] = (chegada.TimeOrigemId, 0);
                }
            }

            if (anteriores.Count == 0) return new();

            var idsTimes = anteriores.Values.Select(a => a.TimeId).Distinct().ToList();
            var times = await _context.Times
                .Where(t => idsTimes.Contains(t.Id))
                .Select(t => new { t.Id, t.Nome, t.EscudoUrl })
                .ToDictionaryAsync(t => t.Id, t => t);

            // Rótulo igual ao da tela: "2024/25" quando a competição cruza o ano civil.
            var rotulo = cruzaAno
                ? $"{temporadaAnterior}/{(temporadaAnterior + 1) % 100:00}"
                : temporadaAnterior.ToString();

            return anteriores
                .Where(a => times.ContainsKey(a.Value.TimeId))
                .ToDictionary(a => a.Key, a =>
                {
                    var t = times[a.Value.TimeId];
                    return new TimeAnteriorJogador
                    {
                        Nome = t.Nome,
                        Escudo = string.IsNullOrEmpty(t.EscudoUrl)
                            ? ""
                            : Url.Action("Imagem", "MediaProxy", new { url = t.EscudoUrl }) ?? "",
                        Temporada = rotulo,
                        Jogos = a.Value.Jogos,
                    };
                });
        }

        // GET: Jogos/TooltipTemporada/5?temporada=2025 — recalcula os dados do tooltip
        // para outra temporada (0 = todas) sem recarregar a tela de análise, que tem
        // escalação em edição e não pode ser recarregada só para trocar um filtro.
        [HttpGet]
        public async Task<IActionResult> TooltipTemporada(int id, int temporada)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var jogo = await _context.Jogos.FirstOrDefaultAsync(j => j.Id == id);
            if (jogo == null) return NotFound();

            // Mesmo universo de jogadores da tela: escalados + elencos dos dois times.
            var idsEscalados = await _context.Escalacoes
                .Where(e => e.JogoId == id && e.JogadorId != null
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null))
                .Select(e => e.JogadorId!.Value)
                .ToListAsync();

            var idsElenco = await _context.Jogadores
                .Where(j => j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId
                         || j.TimeId == jogo.TimeVisitanteId || j.SelecaoId == jogo.TimeVisitanteId)
                .Select(j => j.Id)
                .ToListAsync();

            var ids = idsEscalados.Concat(idsElenco).Distinct().ToList();
            if (ids.Count == 0) return Json(new TooltipJogadorDados { Temporada = temporada });

            return Json(await CalcularTooltipAsync(ids, jogo, usuarioId, temporada));
        }

        // Médias por jogo das estatísticas importadas, em lote, para os jogadores
        // exibidos na tela — mesmas fórmulas de /Jogadores/Estatisticas (inclusive
        // o filtro Minutos > 0, que exclui reservas não utilizados). Alimenta o
        // tooltip de info do jogador em /Jogos/Analisar.
        // anoTermino recorta as médias na temporada escolhida no tooltip (null =
        // carreira inteira, que era o comportamento antigo e fixo).
        private async Task<Dictionary<int, MediasPorJogo>> CalcularMediasPorJogadorAsync(
            IReadOnlyCollection<int> ids, int? anoTermino = null,
            List<int>? compsCruzadasAnterior = null, List<int>? compsCruzadasAtual = null)
        {
            if (ids.Count == 0) return new();

            var semFiltro = anoTermino == null;
            var ano = anoTermino ?? 0;
            var cruzAnt = compsCruzadasAnterior ?? new List<int>();
            var cruzAtu = compsCruzadasAtual ?? new List<int>();

            var agregados = await _context.EstatisticasJogador
                .Where(e => ids.Contains(e.JogadorId) && e.Minutos != null && e.Minutos > 0
                         && (semFiltro
                             || (e.Jogo.Temporada == ano && !cruzAtu.Contains(e.Jogo.CompeticaoId))
                             || (e.Jogo.Temporada == ano - 1 && cruzAnt.Contains(e.Jogo.CompeticaoId))))
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

        // Total de jogos como titular por jogador — mesmo critério de dedupe usado
        // em /Jogadores/Estatisticas: por jogo, prefere a escalação do próprio
        // usuário sobre a compartilhada (importada, UsuarioId null), e conta só a
        // fase INICIAL (a FINAL é a mesma partida, não um jogo a mais).
        // competicaoId/temporada limitam o escopo (linhas Competição/Temporada do
        // tooltip); sem filtro, conta a carreira toda.
        private async Task<Dictionary<int, int>> CalcularTitularesPorJogadorAsync(
            IReadOnlyCollection<int> ids, string usuarioId, int? competicaoId = null,
            int? temporadaAnoTermino = null,
            List<int>? compsCruzadasAnterior = null, List<int>? compsCruzadasAtual = null)
        {
            if (ids.Count == 0) return new();

            var query = _context.Escalacoes
                .Where(e => e.JogadorId != null && ids.Contains(e.JogadorId!.Value)
                         && e.Titular && e.Posicao != null && e.Posicao != "RES"
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null)
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null));
            if (competicaoId != null) query = query.Where(e => e.Jogo.CompeticaoId == competicaoId);
            if (temporadaAnoTermino != null)
            {
                // Mesma normalização por ano de término usada em PreencherDadosTooltipAsync.
                var cruzAnt = compsCruzadasAnterior ?? new List<int>();
                var cruzAtu = compsCruzadasAtual ?? new List<int>();
                query = query.Where(e =>
                    (e.Jogo.Temporada == temporadaAnoTermino && !cruzAtu.Contains(e.Jogo.CompeticaoId)) ||
                    (e.Jogo.Temporada == temporadaAnoTermino - 1 && cruzAnt.Contains(e.Jogo.CompeticaoId)));
            }

            var candidatas = await query
                .Select(e => new { e.JogadorId, e.JogoId, e.UsuarioId })
                .ToListAsync();

            return candidatas
                .GroupBy(e => e.JogadorId!.Value)
                .ToDictionary(g => g.Key, g => g
                    .GroupBy(e => e.JogoId)
                    .Select(gj => gj.OrderBy(e => e.UsuarioId == usuarioId ? 0 : 1).First())
                    .Count());
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

            // Titulares da INICIAL ANTES da edição — usados adiante para saber se a FINAL
            // ainda é só uma réplica da INICIAL (nenhuma substituição montada à mão) e,
            // sendo, pode ser regenerada a partir da nova INICIAL.
            var titularesAntesCasa = new HashSet<int>();
            var titularesAntesVis = new HashSet<int>();
            if (faseAtual == "INICIAL")
            {
                titularesAntesCasa = escalacoes
                    .Where(e => e.IsTimeCasa && e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value).ToHashSet();
                titularesAntesVis = escalacoes
                    .Where(e => !e.IsTimeCasa && e.Titular && e.JogadorId != null)
                    .Select(e => e.JogadorId!.Value).ToHashSet();
            }

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

            await _context.SaveChangesAsync();

            // Mantém a FINAL coerente com a INICIAL recém-salva. Editar a INICIAL é só
            // ajustar a prévia (importada da última escalação ou montada à mão) antes do
            // jogo — trocar um titular aqui NÃO é uma substituição. Enquanto a FINAL for
            // apenas a réplica da INICIAL (+ substituições importadas), ela é regenerada a
            // partir da nova INICIAL, para a troca não aparecer como "substituição" na aba
            // Final. Se o usuário já tiver montado substituições à mão na Final (a FINAL
            // divergiu do esperado), preservamos o trabalho dele e não mexemos.
            if (faseAtual == "INICIAL")
            {
                var finais = await _context.Escalacoes
                    .Where(e => e.JogoId == id && e.FaseEscalacao == "FINAL" && e.UsuarioId == usuarioId)
                    .ToListAsync();

                bool regenerarFinal;
                if (finais.Count == 0)
                {
                    regenerarFinal = true; // ainda não há Final: cria do zero
                }
                else
                {
                    var subs = await _context.Substituicoes
                        .Where(s => s.JogoId == id)
                        .ToListAsync();

                    // Titular esperado na Final = titular ANTES da edição + substituições
                    // importadas aplicadas. Se a Final atual bate com isso, ela é só a
                    // réplica (sem edições manuais) e pode ser regenerada.
                    HashSet<int> AplicarSubs(HashSet<int> baseTitular, bool isCasa)
                    {
                        var set = new HashSet<int>(baseTitular);
                        foreach (var s in subs.Where(x => x.IsTimeCasa == isCasa).OrderBy(x => x.Minuto))
                        {
                            if (s.JogadorSaiuId == null || s.JogadorEntrouId == null
                                || s.JogadorEntrouId == s.JogadorSaiuId) continue;
                            if (set.Contains(s.JogadorSaiuId.Value) && !set.Contains(s.JogadorEntrouId.Value))
                            {
                                set.Remove(s.JogadorSaiuId.Value);
                                set.Add(s.JogadorEntrouId.Value);
                            }
                        }
                        return set;
                    }

                    var esperadoCasa = AplicarSubs(titularesAntesCasa, true);
                    var esperadoVis = AplicarSubs(titularesAntesVis, false);
                    var atualCasa = finais.Where(e => e.IsTimeCasa && e.Titular && e.JogadorId != null)
                        .Select(e => e.JogadorId!.Value).ToHashSet();
                    var atualVis = finais.Where(e => !e.IsTimeCasa && e.Titular && e.JogadorId != null)
                        .Select(e => e.JogadorId!.Value).ToHashSet();

                    regenerarFinal = esperadoCasa.SetEquals(atualCasa) && esperadoVis.SetEquals(atualVis);
                }

                if (regenerarFinal)
                {
                    _context.Escalacoes.RemoveRange(finais);
                    var novaFinal = await ConstruirFinalDaInicialAsync(_context, id, usuarioId);
                    _context.Escalacoes.AddRange(novaFinal);
                    await _context.SaveChangesAsync();
                }
            }

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

            var usuarioId = _userManager.GetUserId(User)!;

            // Remove a escalação desta fase POR COMPLETO: tanto a cópia do usuário
            // quanto a importada/compartilhada (UsuarioId == null). Apagar só a do
            // usuário não bastava — a tela recopiava da compartilhada no próximo
            // carregamento e a escalação "voltava". O usuário pediu para limpar
            // independentemente da origem (importada ou manual), então a importada
            // também sai (é recuperável pelo botão "Reimportar dados").
            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id && (e.FaseEscalacao == faseAtual || (faseAtual == "INICIAL" && e.FaseEscalacao == null)))
                .ToListAsync();
            _context.Escalacoes.RemoveRange(escalacoes);
            await _context.SaveChangesAsync();

            // Recria o campo com os SLOTS VAZIOS da formação atual (posições sem
            // jogador), para o usuário remontar arrastando. Ter os slots presentes
            // também impede o preenchimento automático (última escalação/padrão) de
            // repovoar justamente o que ele acabou de limpar — sem eles, a tela veria
            // o campo vazio e puxaria tudo de novo.
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo != null)
            {
                var vaziosCasa = await EscalacaoBaseHelper.MontarDaFormacaoAsync(
                    _context, jogo, true, usuarioId, faseAtual, jogo.FormacaoCasaId ?? 0);
                var vaziosVis = await EscalacaoBaseHelper.MontarDaFormacaoAsync(
                    _context, jogo, false, usuarioId, faseAtual, jogo.FormacaoVisitanteId ?? 0);

                _context.Escalacoes.AddRange(vaziosCasa.Escalacoes);
                _context.Escalacoes.AddRange(vaziosVis.Escalacoes);

                if (vaziosCasa.FormacaoId > 0) jogo.FormacaoCasaId = vaziosCasa.FormacaoId;
                if (vaziosVis.FormacaoId > 0) jogo.FormacaoVisitanteId = vaziosVis.FormacaoId;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Analisar", new { id, faseEscalacao = faseAtual });
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

                    // ── Fallback automático: ESPN e, depois, FotMob ──────────────────
                    // A api-football devolve a partida sem lineup com frequência (jogo
                    // de mata-mata sul-americano, sobretudo) e o usuário não tem por que
                    // descobrir isso e clicar num segundo botão: se depois da
                    // reimportação ainda falta lado, a ESPN é tentada aqui mesmo, dentro
                    // do mesmo background.
                    //
                    // Roda DEPOIS de RecriarEscalacoesPessoaisAsync de propósito: a
                    // recriação copia das linhas compartilhadas e sobrescreveria o que a
                    // ESPN tivesse acabado de gravar nas linhas pessoais.
                    if (!string.IsNullOrEmpty(uid))
                        await TentarEspnParaLadosFaltandoAsync(scope, ctx, id, uid);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ReimportarEscalacao] Erro no jogo {Id}", id);
                }
            });

            TempData["Mensagem"] = "⏳ Re-importação iniciada em background. Se a API não tiver a escalação, "
                                 + "o sistema tenta a ESPN sozinho. Aguarde ~1 minuto e recarregue a página.";
            return RedirectToAction("Analisar", new { id });
        }

        /// <summary>
        /// Que lados do jogo NÃO têm escalação importada (linha compartilhada com
        /// jogador). É o mesmo critério do selo de origem da tela de análise.
        /// </summary>
        private static async Task<(bool Casa, bool Visitante)> LadosSemEscalacaoImportadaAsync(
            FutebolContext ctx, int jogoId)
        {
            var lados = await ctx.Escalacoes
                .Where(e => e.JogoId == jogoId && e.UsuarioId == null && e.JogadorId != null
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                .Select(e => e.IsTimeCasa)
                .Distinct()
                .ToListAsync();

            return (!lados.Contains(true), !lados.Contains(false));
        }

        /// <summary>
        /// Falta estatística no jogo? Mesmo critério de Serviços › Jogos sem
        /// estatísticas (ServicosController.JogosComEstatisticaFaltando): ou o bloco de
        /// time está vazio, ou não há nenhuma linha por jogador.
        /// </summary>
        private static Task<bool> EstatisticasFaltandoAsync(FutebolContext ctx, int jogoId) =>
            ctx.Jogos.AnyAsync(j => j.Id == jogoId
                && (j.EstatisticasJson == null || j.EstatisticasJson == ""
                    || !ctx.EstatisticasJogador.Any(e => e.JogoId == j.Id)));

        /// <summary>
        /// Falta lance no jogo? Nenhum gol, cartão nem substituição gravados. Um 0 a 0
        /// sem cartão cai aqui de verdade — e reimportar ele da ESPN é inofensivo, só
        /// devolve o mesmo nada.
        /// </summary>
        private static async Task<bool> EventosFaltandoAsync(FutebolContext ctx, int jogoId) =>
            !await ctx.Gols.AnyAsync(g => g.JogoId == jogoId)
            && !await ctx.Cartoes.AnyAsync(c => c.JogoId == jogoId)
            && !await ctx.Substituicoes.AnyAsync(s => s.JogoId == jogoId);

        /// <summary>
        /// Completa pela ESPN os lados que a importação da api-football não trouxe.
        /// Não faz nada quando os dois lados vieram da API, quando a competição não tem
        /// slug da ESPN mapeado ou quando a ESPN também não publicou o XI — o silêncio é
        /// o esperado, já que isto roda sem ninguém olhando.
        /// </summary>
        /// <summary>
        /// A FINAL pessoal foi clonada da INICIAL errada e continuaria mostrando quem
        /// não jogou. Apagá-la faz a tela remontá-la da INICIAL nova; as fases táticas
        /// do cronômetro ficam, porque não são recriáveis.
        /// </summary>
        private static async Task LimparEscalacaoFinalPessoalAsync(
            FutebolContext ctx, int jogoId, string usuarioId)
        {
            var finais = await ctx.Escalacoes
                .Where(e => e.JogoId == jogoId && e.UsuarioId == usuarioId
                         && e.FaseEscalacao == "FINAL")
                .ToListAsync();

            if (finais.Count == 0) return;

            ctx.Escalacoes.RemoveRange(finais);
            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// O jogo com competição e times carregados, só para a linha de log ficar
        /// legível ("Al Sadd x Al Ahli Doha") em vez de mostrar um id solto.
        /// </summary>
        private static Task<Jogo?> JogoParaLogAsync(FutebolContext ctx, int jogoId) =>
            ctx.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == jogoId);

        private async Task TentarEspnParaLadosFaltandoAsync(
            IServiceScope scope, FutebolContext ctx, int jogoId, string usuarioId)
        {
            var (faltaCasa, faltaVis) = await LadosSemEscalacaoImportadaAsync(ctx, jogoId);
            if (!faltaCasa && !faltaVis) return;

            var espn = scope.ServiceProvider.GetRequiredService<EspnEstatisticasService>();
            var fotmob = scope.ServiceProvider.GetRequiredService<FotMobService>();
            var idApiLiga = await ctx.Jogos
                .Where(j => j.Id == jogoId)
                .Select(j => j.Competicao!.IdApi)
                .FirstOrDefaultAsync();

            bool Falta(bool ehCasa) => ehCasa ? faltaCasa : faltaVis;

            // 1º degrau: ESPN. Sem slug a chamada só devolveria erro; evita bater lá à toa.
            var ok = false;
            if (espn.SlugDaLiga(idApiLiga) != null)
            {
                var espnEscalacao = scope.ServiceProvider.GetRequiredService<EspnEscalacaoService>();
                var r = await espnEscalacao.AplicarAsync(ctx, jogoId, usuarioId, filtroLado: Falta);

                _logger.LogInformation("[ReimportarEscalacao] Jogo {Id} › ESPN: {Ok} — {Msg}",
                    jogoId, r.Ok, r.Mensagem);

                await LogFonteExterna.RegistrarAsync(ctx, LogFonteExterna.TipoEspn,
                    "Escalação (reimportação)", r.Ok, await JogoParaLogAsync(ctx, jogoId), r.Mensagem);

                ok = r.Ok;
            }
            else
            {
                _logger.LogInformation(
                    "[ReimportarEscalacao] Jogo {Id}: faltou escalação e a competição não tem ESPN mapeada.",
                    jogoId);

                await LogFonteExterna.RegistrarAsync(ctx, LogFonteExterna.TipoEspn,
                    "Escalação (reimportação)", false, await JogoParaLogAsync(ctx, jogoId),
                    "Competição sem correspondente na ESPN (ver wwwroot/data/ligas-espn.json).");
            }

            // 2º degrau: FotMob. É o que cobre as ligas fora do catálogo da ESPN (a do
            // Catar é a que motivou) e também salva o jogo que a ESPN tem na liga mas
            // não publicou. Só entra quando a ESPN não resolveu — não é para as duas
            // escreverem o mesmo XI.
            if (!ok && fotmob.TemCobertura(idApiLiga))
            {
                var fotmobEscalacao = scope.ServiceProvider.GetRequiredService<FotMobEscalacaoService>();
                var r = await fotmobEscalacao.AplicarAsync(ctx, jogoId, usuarioId, filtroLado: Falta);

                _logger.LogInformation("[ReimportarEscalacao] Jogo {Id} › FotMob: {Ok} — {Msg}",
                    jogoId, r.Ok, r.Mensagem);

                await LogFonteExterna.RegistrarAsync(ctx, LogFonteExterna.TipoFotMob,
                    "Escalação (reimportação)", r.Ok, await JogoParaLogAsync(ctx, jogoId), r.Mensagem);

                if (r.Ok)
                {
                    await LimparEscalacaoFinalPessoalAsync(ctx, jogoId, usuarioId);

                    // A escalação acabou de ser gravada, então agora as estatísticas por
                    // jogador têm com quem casar — elas são casadas contra os escalados.
                    // Rodar isto antes da escalação só gravaria as de time.
                    if (await EstatisticasFaltandoAsync(ctx, jogoId))
                    {
                        var rEst = await fotmob.ImportarAsync(ctx, jogoId);
                        _logger.LogInformation(
                            "[ReimportarEscalacao] Jogo {Id} › FotMob estatísticas: {Ok} — {Msg}",
                            jogoId, rEst.Ok, rEst.Mensagem);
                    }
                }

                // Os lances (gols, cartões, substituições) NÃO saem do FotMob ainda:
                // existe EspnEventosService, mas não o equivalente daqui. Um jogo que só
                // o FotMob tem fica com escalação e estatística certas e placar zerado
                // até alguém preencher — melhor do que ficar sem nada, que era o estado
                // anterior, mas está incompleto de propósito e não por descuido.
                return;
            }

            if (!ok) return;

            await LimparEscalacaoFinalPessoalAsync(ctx, jogoId, usuarioId);

            // Quem não tem a partida na api-football não tem nem escalação NEM
            // estatística: importar só a escalação deixaria o jogo pela metade e
            // mandaria o usuário para a tela de Serviços terminar na mão. Como a ESPN
            // acabou de confirmar que tem esta partida, a estatística sai da mesma
            // visita.
            if (await EstatisticasFaltandoAsync(ctx, jogoId))
            {
                var rEst = await espn.ImportarAsync(ctx, jogoId);
                _logger.LogInformation("[ReimportarEscalacao] Jogo {Id} › ESPN estatísticas: {Ok} — {Msg}",
                    jogoId, rEst.Ok, rEst.Mensagem);
            }

            // Gols, cartões e substituições pela mesma razão: a api-football não trouxe
            // a partida, então ela também não trouxe os lances. Sem isto o jogo ficava
            // com escalação e números certos e placar zerado.
            if (await EventosFaltandoAsync(ctx, jogoId))
            {
                var eventos = scope.ServiceProvider.GetRequiredService<EspnEventosService>();
                var rEv = await eventos.ImportarAsync(ctx, jogoId);
                _logger.LogInformation("[ReimportarEscalacao] Jogo {Id} › ESPN lances: {Ok} — {Msg}",
                    jogoId, rEv.Ok, rEv.Mensagem);
            }
        }

        // POST: Jogos/BuscarEscalacaoEspn/12964
        //
        // Plano B do "Reimportar dados" quando a api-football não publicou a partida:
        // tenta a ESPN, que costuma ter o que falta (Libertadores/Sul-Americana,
        // sobretudo). Faz os dois blocos numa tacada — escalação e estatística — porque
        // quem não tem a partida na API não tem nenhum dos dois, e parar no meio
        // mandaria o usuário terminar o serviço em outra tela.
        //
        // Ao contrário da tela de administração (Serviços › Conferir escalação), aqui
        // conferir e aplicar são um passo só: a escalação só é tocada nos lados que
        // NENHUMA importação preencheu, ou seja, onde o que está em campo é o chute do
        // EscalacaoBaseHelper e não há trabalho de verdade para preservar. Quando a ESPN
        // também não tem, nada é gravado e a mensagem diz isso — é a "verificação".
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarEscalacaoEspn(int id, CancellationToken ct)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var partes = new List<string>();
            var houveFalha = false;

            // ── Escalação ────────────────────────────────────────────────────────
            var (faltaCasa, faltaVis) = await LadosSemEscalacaoImportadaAsync(_context, id);

            if (faltaCasa || faltaVis)
            {
                var r = await _espnEscalacao.AplicarAsync(
                    _context, id, usuarioId,
                    filtroLado: ehCasa => ehCasa ? faltaCasa : faltaVis, ct: ct);

                partes.Add(r.Mensagem);
                houveFalha |= !r.Ok;

                if (r.Ok)
                {
                    // A FINAL pessoal foi clonada da INICIAL errada e continuaria
                    // mostrando quem não jogou. Apagá-la faz a tela remontá-la da
                    // INICIAL nova; as fases táticas do cronômetro ficam, porque não
                    // são recriáveis.
                    var finais = await _context.Escalacoes
                        .Where(e => e.JogoId == id && e.UsuarioId == usuarioId
                                 && e.FaseEscalacao == "FINAL")
                        .ToListAsync(ct);

                    if (finais.Count > 0)
                    {
                        _context.Escalacoes.RemoveRange(finais);
                        await _context.SaveChangesAsync(ct);
                    }
                }
            }

            // ── Estatísticas ─────────────────────────────────────────────────────
            // Roda mesmo quando a escalação já estava completa: é o caso do jogo cuja
            // escalação veio da ESPN numa passada anterior e ficou sem estatística,
            // que hoje só se resolvia em Serviços › Jogos sem estatísticas.
            if (await EstatisticasFaltandoAsync(_context, id))
            {
                var r = await _espn.ImportarAsync(_context, id, ct);
                partes.Add(r.Mensagem);
                houveFalha |= !r.Ok;
            }

            // ── Lances ───────────────────────────────────────────────────────────
            // Gols, assistências, cartões, substituições e pênaltis perdidos. Mesma
            // regra: só entra quando o jogo não tem nenhum, ou seja, quando a
            // api-football não trouxe — nunca por cima de evento já registrado.
            if (await EventosFaltandoAsync(_context, id))
            {
                var r = await _espnEventos.ImportarAsync(_context, id, ct);
                partes.Add(r.Mensagem);
                houveFalha |= !r.Ok;
            }

            if (partes.Count == 0)
                partes.Add("Este jogo já tem escalação e estatísticas importadas.");

            TempData["Mensagem"] = (houveFalha ? "⚠️ " : "✅ ") + string.Join(" ", partes);
            if (houveFalha) TempData["MensagemTipo"] = "erro";

            _logger.LogInformation("[BuscarEscalacaoEspn] Jogo {Id}: falha={Falha} — {Msg}",
                id, houveFalha, string.Join(" | ", partes));

            return RedirectToAction("Analisar", new { id, faseEscalacao = "INICIAL" });
        }

        // POST: Jogos/AplicarUltimaEscalacao/12964
        // Botão "Última escalação", ao lado de "Reimportar dados": quando a API ainda não
        // tem a escalação do jogo, monta a escalação dos dois times a partir do último jogo
        // de cada um (mesma lógica do Match-up do Pré-jogo); sem jogo anterior, cai na
        // escalação padrão do time e, na falta dela, nos slots vazios de uma formação —
        // sempre editável na tela.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AplicarUltimaEscalacao(int id, string? faseEscalacao)
        {
            var usuarioId = _userManager.GetUserId(User)!;

            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            // Fase alvo: a que o usuário está vendo (INICIAL, FINAL ou uma fase tática).
            var fase = string.IsNullOrWhiteSpace(faseEscalacao) ? "INICIAL" : faseEscalacao;

            var baseCasa = await EscalacaoBaseHelper.MontarAsync(
                _context, jogo, true, usuarioId, fase, jogo.FormacaoCasaId ?? 0);
            var baseVisitante = await EscalacaoBaseHelper.MontarAsync(
                _context, jogo, false, usuarioId, fase, jogo.FormacaoVisitanteId ?? 0);

            // Substitui a escalação do usuário nesta fase (as linhas compartilhadas da
            // importação, UsuarioId == null, não são tocadas).
            var atuais = await _context.Escalacoes
                .Where(e => e.JogoId == id && e.UsuarioId == usuarioId
                         && (e.FaseEscalacao == fase || (fase == "INICIAL" && e.FaseEscalacao == null)))
                .ToListAsync();
            _context.Escalacoes.RemoveRange(atuais);

            _context.Escalacoes.AddRange(baseCasa.Escalacoes);
            _context.Escalacoes.AddRange(baseVisitante.Escalacoes);

            if (baseCasa.FormacaoId > 0) jogo.FormacaoCasaId = baseCasa.FormacaoId;
            if (baseVisitante.FormacaoId > 0) jogo.FormacaoVisitanteId = baseVisitante.FormacaoId;

            await _context.SaveChangesAsync();

            // Aplicando na Inicial, a Final passa a espelhar a nova Inicial (aplicando as
            // substituições importadas, se houver). Carregar a última escalação é um reset
            // da prévia — a troca não deve aparecer como substituição na aba Final.
            if (fase == "INICIAL")
            {
                var finais = await _context.Escalacoes
                    .Where(e => e.JogoId == id && e.UsuarioId == usuarioId && e.FaseEscalacao == "FINAL")
                    .ToListAsync();
                _context.Escalacoes.RemoveRange(finais);
                var novaFinal = await ConstruirFinalDaInicialAsync(_context, id, usuarioId);
                _context.Escalacoes.AddRange(novaFinal);
                await _context.SaveChangesAsync();
            }

            TempData["Mensagem"] =
                "👥 " + baseCasa.Descrever(jogo.TimeCasa?.Nome ?? "Mandante")
                + " " + baseVisitante.Descrever(jogo.TimeVisitante?.Nome ?? "Visitante")
                + " Ajuste o que precisar arrastando os jogadores e salve.";

            return RedirectToAction("Analisar", new { id, faseEscalacao = fase });
        }

        // POST: Jogos/LimparEstatisticas/15874
        // Apaga as estatísticas importadas do jogo (as de time e as de cada jogador),
        // devolvendo o jogo à lista de /Servicos/JogosSemEstatisticas.
        //
        // Serve para refazer uma importação ruim: aquela lista só mostra jogo SEM
        // estatística nenhuma, então bastava a api-football gravar um bloco vazio para
        // o jogo sumir de lá e não haver mais como reimportar da ESPN.
        //
        // Não encosta em nota, observação, escalação, gol, cartão ou substituição —
        // só no que veio de importação e é reimportável.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimparEstatisticas(int id)
        {
            var jogo = await _context.Jogos.FirstOrDefaultAsync(j => j.Id == id);
            if (jogo == null) return NotFound();

            var estatisticas = await _context.EstatisticasJogador
                .Where(e => e.JogoId == id)
                .ToListAsync();

            _context.EstatisticasJogador.RemoveRange(estatisticas);
            jogo.EstatisticasJson = null;
            // Sem estatística não há nota automática, e o craque eleito por ela deixa de
            // valer para todos os analistas. Ver CraqueDaPartidaService.InvalidarJogoAsync.
            _context.CraquesDaPartida.RemoveRange(
                await _context.CraquesDaPartida.Where(c => c.JogoId == id).ToListAsync());
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[LimparEstatisticas] Jogo {JogoId}: {Linhas} linha(s) de jogador e as estatísticas de time removidas.",
                id, estatisticas.Count);

            TempData["Sucesso"] = estatisticas.Count > 0
                ? $"Estatísticas do jogo removidas ({estatisticas.Count} linha(s) de jogador). O jogo volta à lista de Serviços › Jogos sem estatísticas."
                : "O jogo já não tinha estatísticas de jogador; as de time foram removidas.";

            return RedirectToAction(nameof(Analisar), new { id });
        }

        // POST: Jogos/CriarJogadorNoTime — cadastra na hora um jogador que a API ainda
        // não trouxe (garoto da base, reforço recém-anunciado, elenco não importado),
        // direto no elenco do time, para poder ser escalado sem sair da análise.
        //
        // Fica sem IdApi: quando a importação rodar, ela reaproveita este cadastro pelo
        // nome e completa os dados em vez de criar um duplicado (ApiFootballService.ResolverJogador).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarJogadorNoTime([FromBody] CriarJogadorNoTimeRequest req)
        {
            var nome = (req.Nome ?? string.Empty).Trim();
            if (nome.Length < 2)
                return BadRequest(new { erro = "Informe o nome do jogador." });

            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == req.JogoId);

            if (jogo == null) return NotFound(new { erro = "Jogo não encontrado." });

            var time = req.IsTimeCasa ? jogo.TimeCasa : jogo.TimeVisitante;
            if (time == null) return NotFound(new { erro = "Time não encontrado." });

            // Já existe alguém com este nome no elenco? Devolve o cadastro atual — o
            // objetivo é escalar o jogador, não criar um segundo registro dele.
            var doTime = await _context.Jogadores
                .Where(j => j.TimeId == time.Id || j.SelecaoId == time.Id)
                .ToListAsync();

            var existente = doTime.FirstOrDefault(j => NomeJogadorHelper.Corresponde(j.Nome, nome));

            if (existente != null)
                return Ok(new
                {
                    id = existente.Id,
                    nome = existente.Nome,
                    numero = existente.NumeroCamisa?.ToString() ?? "",
                    posicao = existente.Posicao ?? "",
                    jaExistia = true
                });

            var jogador = new Jogador
            {
                Nome = nome,
                Posicao = (req.Posicao ?? string.Empty).Trim(),
                NumeroCamisa = req.Numero,
                TimeId = time.Id,
                SelecaoId = time.EhSelecao ? time.Id : null,
                DtInc = DateTime.UtcNow
            };

            _context.Jogadores.Add(jogador);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[CriarJogadorNoTime] {Nome} cadastrado no time {Time} pelo jogo {JogoId}",
                jogador.Nome, time.Nome, req.JogoId);

            return Ok(new
            {
                id = jogador.Id,
                nome = jogador.Nome,
                numero = jogador.NumeroCamisa?.ToString() ?? "",
                posicao = jogador.Posicao,
                jaExistia = false
            });
        }

        public class CriarJogadorNoTimeRequest
        {
            public int JogoId { get; set; }
            public bool IsTimeCasa { get; set; }
            public string? Nome { get; set; }
            public int? Numero { get; set; }
            public string? Posicao { get; set; }
        }

        // Monta a escalação FINAL a partir da INICIAL do usuário aplicando SOMENTE as
        // substituições importadas (as registradas na tabela Substituicoes): quem entrou
        // assume a posição em campo de quem saiu, e quem saiu vai para o banco. Sem
        // substituições importadas a FINAL é uma réplica exata da INICIAL. Retorna os
        // clones (fase FINAL) sem adicioná-los ao contexto — o chamador decide persistir.
        private static async Task<List<Escalacao>> ConstruirFinalDaInicialAsync(
            FutebolContext ctx, int jogoId, string usuarioId)
        {
            var iniciais = await ctx.Escalacoes
                .Where(e => e.JogoId == jogoId
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null)
                         && e.UsuarioId == usuarioId)
                .ToListAsync();
            if (!iniciais.Any()) return new List<Escalacao>();

            var clones = iniciais.Select(e => new Escalacao
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

            var substituicoes = await ctx.Substituicoes
                .Where(s => s.JogoId == jogoId)
                .OrderBy(s => s.Minuto)
                .ToListAsync();

            foreach (var sub in substituicoes)
            {
                if (sub.JogadorSaiuId == null || sub.JogadorEntrouId == null
                    || sub.JogadorEntrouId == sub.JogadorSaiuId) // linhas antigas corrompidas (entrou=saiu)
                    continue;

                // Slot em campo de quem saiu (titular). Sem ele, não há o que substituir.
                var slotSaiu = clones.FirstOrDefault(c =>
                    c.JogadorId == sub.JogadorSaiuId && c.IsTimeCasa == sub.IsTimeCasa && c.Titular);
                if (slotSaiu == null) continue;

                // Se quem entrou já está em campo, nada a fazer (evita reprocessar).
                bool jaEmCampo = clones.Any(c =>
                    c.JogadorId == sub.JogadorEntrouId && c.IsTimeCasa == sub.IsTimeCasa && c.Titular);
                if (jaEmCampo) continue;

                // Slot de quem entrou (no banco). Pode não existir se a importação trouxe
                // o banco incompleto ou o jogador não estava entre os reservas.
                var slotEntrou = clones.FirstOrDefault(c =>
                    c.JogadorId == sub.JogadorEntrouId && c.IsTimeCasa == sub.IsTimeCasa && !c.Titular);

                // Quem saiu vai para o banco (se houver slot de reserva); senão, apenas deixa o campo.
                if (slotEntrou != null)
                    slotEntrou.JogadorId = slotSaiu.JogadorId;

                // Quem entrou assume a posição em campo de quem saiu — mesmo que não
                // estivesse no banco importado (garante a seta verde no jogador certo).
                slotSaiu.JogadorId = sub.JogadorEntrouId;
            }

            return clones;
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


    }

}
