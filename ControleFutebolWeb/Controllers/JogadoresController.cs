using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System.Linq;
using System.Threading.Tasks;

namespace ControleFutebolWeb.Controllers
{
    public class JogadoresController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<JogadoresController> _logger;
        private readonly ApiFootballService _transfermarktService;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogadoresController(FutebolContext context, ILogger<JogadoresController> logger, ApiFootballService transfermarktService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _transfermarktService = transfermarktService;
            _userManager = userManager;
        }

        public IActionResult Index(string posicao, string nacionalidade, int? timeId, string sortOrder, string? nome, int? idadeMin, int? idadeMax, bool semIdade = false, int page = 1)
        {
            const int pageSize = 50;
            // Configura parâmetros de ordenação
            ViewBag.NomeSortParam = sortOrder == "Nome" ? "Nome_desc" : "Nome";
            ViewBag.PosicaoSortParam = sortOrder == "Posicao" ? "Posicao_desc" : "Posicao";
            ViewBag.IdadeSortParam = sortOrder == "Idade" ? "Idade_desc" : "Idade";
            ViewBag.NacionalidadeSortParam = sortOrder == "Nacionalidade" ? "Nacionalidade_desc" : "Nacionalidade";
            ViewBag.TimeSortParam = sortOrder == "Time" ? "Time_desc" : "Time";

            // Guarda filtros atuais
            ViewBag.CurrentSort = sortOrder;

            var jogadores = _context.Jogadores
                .Include(j => j.Nacionalidade)
                .Include(j => j.Time)
                .AsQueryable();

            // Aplica filtros
            if (!string.IsNullOrEmpty(nome))
                jogadores = jogadores.Where(j => j.Nome.ToLower().Contains(nome.ToLower()));

            if (!string.IsNullOrEmpty(posicao))
                jogadores = jogadores.Where(j => j.Posicao == posicao);

            if (!string.IsNullOrEmpty(nacionalidade))
                jogadores = jogadores.Where(j => j.Nacionalidade.Nome == nacionalidade);

            if (timeId.HasValue)
                jogadores = jogadores.Where(j => j.TimeId == timeId.Value);

            // Filtro "somente sem idade": jogadores sem data de nascimento cadastrada.
            if (semIdade)
            {
                jogadores = jogadores.Where(j => j.DataNascimento == null);
            }
            else
            {
                // Filtro por faixa de idade (convertido para faixa de data de nascimento).
                // idade >= min  ⇔ nascimento <= hoje - min anos
                // idade <= max  ⇔ nascimento >  hoje - (max+1) anos
                var hoje = DateTime.Today;
                if (idadeMin.HasValue)
                {
                    var limiteSuperior = hoje.AddYears(-idadeMin.Value);
                    jogadores = jogadores.Where(j => j.DataNascimento != null && j.DataNascimento <= limiteSuperior);
                }
                if (idadeMax.HasValue)
                {
                    var limiteInferior = hoje.AddYears(-(idadeMax.Value + 1));
                    jogadores = jogadores.Where(j => j.DataNascimento != null && j.DataNascimento > limiteInferior);
                }
            }

            // Aplica ordenação
            jogadores = sortOrder switch
            {
                "Nome" => jogadores.OrderBy(j => j.Nome),
                "Nome_desc" => jogadores.OrderByDescending(j => j.Nome),
                "Posicao" => jogadores.OrderBy(j => j.Posicao),
                "Posicao_desc" => jogadores.OrderByDescending(j => j.Posicao),
                "Idade" => jogadores.AsEnumerable().OrderBy(j => j.Idade).AsQueryable(),
                "Idade_desc" => jogadores.AsEnumerable().OrderByDescending(j => j.Idade).AsQueryable(),
                "Nacionalidade" => jogadores.OrderBy(j => j.Nacionalidade.Nome),
                "Nacionalidade_desc" => jogadores.OrderByDescending(j => j.Nacionalidade.Nome),
                "Time" => jogadores.OrderBy(j => j.Time.Nome),
                "Time_desc" => jogadores.OrderByDescending(j => j.Time.Nome),
                _ => jogadores.OrderBy(j => j.Nome)
            };

            ViewBag.Nome = nome;
            ViewBag.IdadeMin = idadeMin;
            ViewBag.IdadeMax = idadeMax;
            ViewBag.SemIdade = semIdade;

            // Preenche combos com SelectList
            var posicoes = new List<string> {
            "Goleiro","Zagueiro","Meio-campo","Volante","Atacante",
            "Ponta Esquerda","Ponta Direita","Meia Ofensivo",
            "Lateral Esquerdo","Lateral Direito","Centroavante"
         };
            ViewBag.Posicoes = new SelectList(posicoes, posicao);

            var nacionalidades = _context.Nacionalidades
                .Select(n => n.Nome)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            ViewBag.Nacionalidades = new SelectList(nacionalidades, nacionalidade);

            var times = _context.Times
                .OrderBy(t => t.Nome)
                .ToList();
            ViewBag.Times = new SelectList(times, "Id", "Nome", timeId);

            // Paginação (50 por página)
            var listaOrdenada = jogadores.ToList();
            var totalJogadores = listaOrdenada.Count;
            var totalPaginas = (int)Math.Ceiling(totalJogadores / (double)pageSize);
            if (totalPaginas < 1) totalPaginas = 1;
            if (page < 1) page = 1;
            if (page > totalPaginas) page = totalPaginas;

            var jogadoresPagina = listaOrdenada
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.PaginaAtual = page;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalJogadores = totalJogadores;
            ViewBag.PageSize = pageSize;

            // Filtros atuais, para preservar nos links de paginação
            ViewBag.FiltroPosicao = posicao;
            ViewBag.FiltroNacionalidade = nacionalidade;
            ViewBag.FiltroTimeId = timeId;
            ViewBag.FiltroSortOrder = sortOrder;

            return View(jogadoresPagina);
        }
        // GET: Jogadores/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jogador == null) return NotFound();

            return View(jogador);
        }

        // GET: Jogadores/Create
        public IActionResult Create()
        {
            ViewBag.TimeId = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.NacionalidadeId = new SelectList(_context.Nacionalidades, "Id", "Nome");
            return View();
        }

        // POST: Jogadores/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Jogador jogador)
        {
            if (ModelState.IsValid)
            {
                _context.Add(jogador);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            LogModelStateErrors("Create");

            ViewBag.TimeId = new SelectList(_context.Times, "Id", "Nome", jogador?.TimeId);
            ViewBag.NacionalidadeId = new SelectList(_context.Nacionalidades, "Id", "Nome", jogador?.NacionalidadeId);
            return View(jogador);
        }

        // GET: Jogadores/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Nacionalidade)
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogador == null)
            {
                return NotFound();
            }

            // Pega todas as posições distintas já salvas
            var posicoes = await _context.Jogadores
                .Select(j => j.Posicao)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            ViewBag.Posicoes = new SelectList(posicoes);

            ViewData["NacionalidadeId"] = new SelectList(_context.Nacionalidades, "Id", "Nome", jogador.NacionalidadeId);
            ViewData["TimeId"] = new SelectList(_context.Times, "Id", "Nome", jogador.TimeId);

            return View(jogador);
        }


        // POST: Jogadores/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Jogador jogador)
        {
            if (id != jogador.Id) return NotFound();

            _logger.LogInformation(
                "POST Edit recebido: Id={Id}, Nome={Nome}, Posicao={Posicao}, TimeId={TimeId}, NacionalidadeId={NacionalidadeId}, DataNascimento={DataNascimento}",
                jogador.Id,
                jogador.Nome,
                jogador.Posicao,
                jogador.TimeId,
                jogador.NacionalidadeId,
                jogador.DataNascimento
            );

            if (ModelState.IsValid)
            {
                try
                {
                    var jogadorExistente = await _context.Jogadores
                        .Include(j => j.Nacionalidade)
                        .Include(j => j.Time)
                        .FirstOrDefaultAsync(j => j.Id == id);

                    if (jogadorExistente == null) return NotFound();

                    // Validação contra api-football (somente se IdApi estiver preenchido)
                    var jogadorExistenteParaValidacao = await _context.Jogadores.FindAsync(id);
                    if (jogadorExistenteParaValidacao?.IdApi > 0)
                    {
                        var dadosApi = await _transfermarktService.BuscarInfoJogadorAsync(
                            jogadorExistenteParaValidacao.IdApi.Value);

                        if (dadosApi != null)
                        {
                            var divergencias = new List<string>();

                            if (dadosApi.DataNascimento.HasValue &&
                                dadosApi.DataNascimento.Value.Date != jogador.DataNascimento?.Date)
                                divergencias.Add($"Data de nascimento divergente. API: {dadosApi.DataNascimento.Value:dd/MM/yyyy}");

                            if (!string.IsNullOrEmpty(dadosApi.Nacionalidade) && jogador.NacionalidadeId.HasValue)
                            {
                                var nacSelecionada = await _context.Nacionalidades.FindAsync(jogador.NacionalidadeId.Value);
                                if (nacSelecionada?.Nome != dadosApi.Nacionalidade)
                                    divergencias.Add($"Nacionalidade divergente. API: {dadosApi.Nacionalidade}");
                            }

                            if (divergencias.Any())
                            {
                                TempData["Mensagem"] = string.Join(" | ", divergencias);
                                TempData["MensagemTipo"] = "erro";

                                ViewBag.TimeId = new SelectList(_context.Times, "Id", "Nome", jogador.TimeId);
                                ViewBag.NacionalidadeId = new SelectList(_context.Nacionalidades, "Id", "Nome", jogador.NacionalidadeId);
                                return View(jogador);
                            }
                        }
                    }

                    // Atualiza campos
                    jogadorExistente.Nome = jogador.Nome;
                    jogadorExistente.Posicao = jogador.Posicao;
                    jogadorExistente.DataNascimento = jogador.DataNascimento.HasValue
                        ? DateTime.SpecifyKind(jogador.DataNascimento.Value, DateTimeKind.Utc)
                        : null;
                    jogadorExistente.TimeId = jogador.TimeId;
                    jogadorExistente.NacionalidadeId = jogador.NacionalidadeId;
                    jogadorExistente.Observacoes = jogador.Observacoes;
                    jogadorExistente.DtAlt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    TempData["Mensagem"] = "Jogador atualizado com sucesso!";
                    TempData["MensagemTipo"] = "sucesso";

                    return RedirectToAction("Index", new { timeId = jogador.TimeId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Jogadores.Any(e => e.Id == jogador.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            LogModelStateErrors("Edit");

            ViewBag.TimeId = new SelectList(_context.Times, "Id", "Nome", jogador.TimeId);
            ViewBag.NacionalidadeId = new SelectList(_context.Nacionalidades, "Id", "Nome", jogador.NacionalidadeId);
            return View(jogador);
        }



        // GET: Jogadores/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jogador == null) return NotFound();

            return View(jogador);
        }

        // POST: Jogadores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador != null)
            {
                _context.Jogadores.Remove(jogador);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private void LogModelStateErrors(string contextAction)
        {
            var errors = ModelState
                .Where(kvp => kvp.Value.Errors.Count > 0)
                .Select(kvp => new { Key = kvp.Key, Errors = kvp.Value.Errors.Select(e => e.ErrorMessage ?? e.Exception?.Message).ToArray() })
                .ToList();

            if (errors.Any())
                _logger.LogWarning("ModelState inválido em Jogadores/{Action}. Erros: {@Errors}", contextAction, errors);
        }

        public async Task<IActionResult> Estatisticas(int id, int? competicaoId)
        {
            var uid = _userManager.GetUserId(User);

            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogador == null) return NotFound();

            // IDs de competições em que o jogador participou (para o dropdown)
            var competicaoIds = await _context.Notas
                .Where(n => n.JogadorId == id)
                .Select(n => n.Jogo.CompeticaoId)
                .Union(_context.EstatisticasJogador
                    .Where(e => e.JogadorId == id)
                    .Select(e => e.Jogo.CompeticaoId))
                .Union(_context.Escalacoes
                    .Where(e => e.JogadorId == id)
                    .Select(e => e.Jogo.CompeticaoId))
                .Distinct()
                .ToListAsync();

            var competicoesDoJogador = await _context.Competicoes
                .Where(c => competicaoIds.Contains(c.Id))
                .OrderBy(c => c.Nome)
                .ToListAsync();

            ViewBag.Competicoes = competicoesDoJogador;
            ViewBag.CompeticaoId = competicaoId;

            // ── Dados de eventos ──────────────────────────────────────────
            var notasQuery = _context.Notas
                .Include(n => n.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(n => n.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(n => n.Detalhes)
                .Where(n => n.JogadorId == id && n.UsuarioId == uid);

            if (competicaoId.HasValue)
                notasQuery = notasQuery.Where(n => n.Jogo.CompeticaoId == competicaoId);

            var notas = await notasQuery.ToListAsync();

            // Estatísticas importadas (api-football) — usadas quando não há nota manual,
            // para mostrar de onde vem a nota calculada automaticamente em /Relatorios.
            var estatisticasQuery = _context.EstatisticasJogador
                .Include(e => e.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(e => e.Jogo).ThenInclude(j => j.TimeVisitante)
                .Where(e => e.JogadorId == id);

            if (competicaoId.HasValue)
                estatisticasQuery = estatisticasQuery.Where(e => e.Jogo.CompeticaoId == competicaoId);

            var estatisticas = await estatisticasQuery.ToListAsync();

            var golsQuery = _context.Gols.Where(g => g.JogadorId == id && !g.Contra);
            if (competicaoId.HasValue)
                golsQuery = golsQuery.Where(g => g.Jogo.CompeticaoId == competicaoId);
            var gols = await golsQuery.ToListAsync();

            var assistenciasQuery = _context.Assistencias.Where(a => a.JogadorId == id);
            if (competicaoId.HasValue)
                assistenciasQuery = assistenciasQuery.Where(a => a.Jogo.CompeticaoId == competicaoId);
            var assistencias = await assistenciasQuery.ToListAsync();

            var cartoesQuery = _context.Cartoes.Where(c => c.JogadorId == id);
            if (competicaoId.HasValue)
                cartoesQuery = cartoesQuery.Where(c => c.Jogo.CompeticaoId == competicaoId);
            var cartoes = await cartoesQuery.ToListAsync();

            // ── Escalações (inclui jogos sem análise) ─────────────────────
            // Usa escalações do próprio usuário (se existir) ou as compartilhadas (UsuarioId == null)
            var escalacoesQuery = _context.Escalacoes
                .Include(e => e.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(e => e.Jogo).ThenInclude(j => j.TimeVisitante)
                .Where(e => e.JogadorId == id && (e.UsuarioId == uid || e.UsuarioId == null));

            if (competicaoId.HasValue)
                escalacoesQuery = escalacoesQuery.Where(e => e.Jogo.CompeticaoId == competicaoId);

            var escalacoes = await escalacoesQuery.ToListAsync();

            var jogosComNotaManualIds = notas.Select(n => n.JogoId).ToHashSet();
            var jogosComEstatisticaIds = estatisticas.Select(e => e.JogoId).ToHashSet();

            var criteriosCompartilhados = await _context.CriteriosNota
                .Where(c => c.UsuarioId == null).ToListAsync();
            var criteriosUsuario = await _context.CriteriosNota
                .Where(c => c.UsuarioId == uid).ToListAsync();
            var criteriosBanco = CriteriosNotaHelper.MergeCriterios(criteriosCompartilhados, criteriosUsuario);

            // ── Helper: monta item a partir de um jogo ────────────────────
            NotaJogoItem MontarItem(Jogo jogo, double notaValor, string? comentario,
                List<Notadetalhe> detalhes, bool analisado, bool origemManual = false, double? notaManual = null)
            {
                var pc = jogo.PlacarCasa ?? 0;
                var pv = jogo.PlacarVisitante ?? 0;
                bool isCasa = jogo.TimeCasaId == jogador.TimeId
                           || jogo.TimeCasaId == jogador.SelecaoId;

                int golsPro    = isCasa ? pc : pv;
                int golsContra = isCasa ? pv : pc;

                string resultado;
                if (!jogo.PlacarCasa.HasValue)                     resultado = "?";
                else if (pc == pv)                                  resultado = "E";
                else if ((isCasa && pc > pv) || (!isCasa && pv > pc)) resultado = "V";
                else                                                resultado = "D";

                double notaFinal;
                if (!analisado)
                    notaFinal = 0;
                else if (notaManual.HasValue)
                    // Override: nota final absoluta (0–10), sem somar base.
                    notaFinal = Math.Round(Math.Max(0, Math.Min(10, notaManual.Value)), 2);
                else
                    notaFinal = Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima, Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + notaValor)), 2);

                return new NotaJogoItem
                {
                    Jogo = jogo,
                    Analisado = analisado,
                    Nota = notaValor,
                    Comentario = comentario ?? "",
                    Gols = gols.Count(g => g.JogoId == jogo.Id),
                    Assistencias = assistencias.Count(a => a.JogoId == jogo.Id),
                    Cartoes = cartoes.Count(c => c.JogoId == jogo.Id),
                    Resultado = resultado,
                    BonusResultado = 0,
                    NotaFinal = notaFinal,
                    Detalhes = detalhes,
                    GolsPro = golsPro,
                    GolsContra = golsContra,
                    NotaBaseFixa = analisado && !notaManual.HasValue ? CriteriosNotaHelper.NotaBaseFixa : 0,
                    OrigemManual = origemManual,
                    NotaManual = notaManual,
                };
            }

            // ── Jogos com nota manual (avaliação dada por um analista) ────
            var itensManual = notas.Select(n =>
                MontarItem(n.Jogo, n.Valor, n.Comentario, n.Detalhes?.ToList() ?? new(), true, origemManual: true, notaManual: n.NotaManual));

            // ── Jogos sem nota manual, mas com estatísticas importadas ────
            var itensEstatisticas = estatisticas
                .Where(e => !jogosComNotaManualIds.Contains(e.JogoId))
                .Select(e => MontarItem(
                    e.Jogo,
                    Math.Round(CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco), 2),
                    null,
                    CriteriosNotaHelper.ConstruirDetalhes(e, criteriosBanco),
                    true));

            // ── Jogos sem nota manual nem estatística importada ───────────
            var itensNaoAnalisados = escalacoes
                .Where(e => !jogosComNotaManualIds.Contains(e.JogoId) && !jogosComEstatisticaIds.Contains(e.JogoId))
                .GroupBy(e => e.JogoId)
                .Select(g => g.First())
                .Select(e => MontarItem(e.Jogo, 0, null, new(), false));

            var notasPorJogo = itensManual
                .Concat(itensEstatisticas)
                .Concat(itensNaoAnalisados)
                .OrderByDescending(x => x.Jogo.Data)
                .ToList();

            double mediaFinal = notasPorJogo.Any(x => x.Analisado)
                ? Math.Round(notasPorJogo.Where(x => x.Analisado).Average(x => x.NotaFinal), 2)
                : 0;

            var vm = new JogadorEstatisticasViewModel
            {
                Jogador = jogador,
                MediaNotas = mediaFinal,
                TotalJogos = notasPorJogo.Count(x => x.Analisado),
                TotalJogosParticipados = notasPorJogo.Count,
                TotalGols = gols.Count,
                TotalAssistencias = assistencias.Count,
                NotasPorJogo = notasPorJogo
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> EstatisticasTemporada(int id, int season)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador?.IdApi == null)
                return Json(new { error = "Jogador sem IdApi" });

            try
            {
                var stats = await _transfermarktService.BuscarEstatisticasTemporadaAsync(jogador.IdApi.Value, season);
                var result = stats.Select(s => new
                {
                    league = new
                    {
                        id = s.League.Id,
                        name = s.League.Name,
                        country = s.League.Country,
                        logo = s.League.Logo,
                        flag = s.League.Flag,
                        season = s.League.Season
                    },
                    team = new { id = s.Team.Id, name = s.Team.Name, logo = s.Team.Logo },
                    games = new
                    {
                        appearences = s.Games.Appearences ?? 0,
                        lineups = s.Games.Lineups ?? 0,
                        minutes = s.Games.Minutes ?? 0,
                        position = s.Games.Position,
                        rating = s.Games.Rating
                    },
                    goals = new { total = s.Goals.Total ?? 0, assists = s.Goals.Assists ?? 0 },
                    passes = new { total = s.Passes.Total ?? 0, key = s.Passes.Key ?? 0, accuracy = s.Passes.Accuracy },
                    shots = new { total = s.Shots.Total ?? 0, on = s.Shots.On ?? 0 },
                    tackles = new { total = s.Tackles.Total ?? 0, blocks = s.Tackles.Blocks ?? 0, interceptions = s.Tackles.Interceptions ?? 0 },
                    dribbles = new { attempts = s.Dribbles.Attempts ?? 0, success = s.Dribbles.Success ?? 0 },
                    cards = new { yellow = s.Cards.Yellow ?? 0, yellowred = s.Cards.Yellowred ?? 0, red = s.Cards.Red ?? 0 },
                    substitutes = new { @in = s.Substitutes.In ?? 0, @out = s.Substitutes.Out ?? 0, bench = s.Substitutes.Bench ?? 0 },
                    duels = new { total = s.Duels.Total ?? 0, won = s.Duels.Won ?? 0 },
                    fouls = new { drawn = s.Fouls.Drawn ?? 0, committed = s.Fouls.Committed ?? 0 }
                });
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarFoto(int id)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogador == null) return NotFound();

            string? fotoUrl = null;
            if (jogador.IdApi > 0)
            {
                var info = await _transfermarktService.BuscarInfoJogadorAsync(jogador.IdApi!.Value);
                fotoUrl = info?.FotoUrl;
            }

            if (!string.IsNullOrEmpty(fotoUrl))
            {
                jogador.FotoUrl = fotoUrl;
                jogador.DtAlt = DateTime.UtcNow;

                _context.Update(jogador);
                await _context.SaveChangesAsync();

                TempData["Mensagem"] = "Foto atualizada com sucesso!";
            }
            else
            {
                TempData["Mensagem"] = "Não foi possível encontrar a foto (jogador sem IdApi ou sem foto na API).";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarInfo(int id)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador == null) return NotFound();

            if (!(jogador.IdApi > 0))
            {
                TempData["Erro"] = $"{jogador.Nome} não possui IdApi cadastrado — não é possível atualizar pela API.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var info = await _transfermarktService.BuscarPerfilJogadorAsync(jogador.IdApi!.Value);
                if (info == null)
                {
                    TempData["Erro"] = $"Não foi possível obter os dados de {jogador.Nome} na API.";
                    return RedirectToAction(nameof(Index));
                }

                var alteracoes = new List<string>();

                if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900)
                {
                    var novaData = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Unspecified);
                    if (jogador.DataNascimento?.Date != novaData.Date)
                    {
                        jogador.DataNascimento = novaData;
                        alteracoes.Add($"idade ({jogador.Idade} anos)");
                    }
                }

                if (!string.IsNullOrWhiteSpace(info.Nacionalidade))
                {
                    var nac = await ApiFootballService.ResolverOuCriarNacionalidadePublicAsync(_context, info.Nacionalidade);
                    if (nac != null && jogador.NacionalidadeId != nac.Id)
                    {
                        jogador.NacionalidadeId = nac.Id;
                        alteracoes.Add($"nacionalidade ({nac.Nome})");
                    }
                }

                if (!string.IsNullOrWhiteSpace(info.FotoUrl) && jogador.FotoUrl != info.FotoUrl)
                {
                    jogador.FotoUrl = info.FotoUrl;
                    alteracoes.Add("foto");
                }

                if (!string.IsNullOrWhiteSpace(info.PrimeiroNome) && jogador.PrimeiroNome != info.PrimeiroNome)
                {
                    jogador.PrimeiroNome = info.PrimeiroNome;
                    alteracoes.Add("primeiro nome");
                }

                if (!string.IsNullOrWhiteSpace(info.UltimoNome) && jogador.UltimoNome != info.UltimoNome)
                {
                    jogador.UltimoNome = info.UltimoNome;
                    alteracoes.Add("último nome");
                }

                jogador.Atualizado = true;
                jogador.DtAlt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                string listaAlteracoes = alteracoes.Count > 1
                    ? string.Join(", ", alteracoes.Take(alteracoes.Count - 1)) + " e " + alteracoes.Last()
                    : alteracoes.FirstOrDefault() ?? "";

                TempData["Sucesso"] = alteracoes.Any()
                    ? $"{jogador.Nome}: atualizado → {listaAlteracoes}."
                    : $"{jogador.Nome}: informações já estavam atualizadas.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar informações do jogador {Nome} (IdApi={Id})", jogador.Nome, jogador.IdApi);
                TempData["Erro"] = $"Erro ao atualizar {jogador.Nome}: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarInfoTodosSemIdade()
        {
            var jogadores = await _context.Jogadores
                .Where(j => j.DataNascimento == null && j.IdApi != null && j.IdApi > 0)
                .ToListAsync();

            int atualizados = 0, semData = 0, falhas = 0;

            foreach (var jogador in jogadores)
            {
                try
                {
                    var info = await _transfermarktService.BuscarPerfilJogadorAsync(jogador.IdApi!.Value);
                    if (info == null) { falhas++; continue; }

                    bool mudou = false;

                    if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900)
                    {
                        jogador.DataNascimento = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Unspecified);
                        mudou = true;
                    }

                    if (!string.IsNullOrWhiteSpace(info.Nacionalidade))
                    {
                        var nac = await ApiFootballService.ResolverOuCriarNacionalidadePublicAsync(_context, info.Nacionalidade);
                        if (nac != null && jogador.NacionalidadeId != nac.Id)
                        {
                            jogador.NacionalidadeId = nac.Id;
                            mudou = true;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(info.FotoUrl) && jogador.FotoUrl != info.FotoUrl)
                    {
                        jogador.FotoUrl = info.FotoUrl;
                        mudou = true;
                    }

                    if (!string.IsNullOrWhiteSpace(info.PrimeiroNome) && jogador.PrimeiroNome != info.PrimeiroNome)
                    {
                        jogador.PrimeiroNome = info.PrimeiroNome;
                        mudou = true;
                    }

                    if (!string.IsNullOrWhiteSpace(info.UltimoNome) && jogador.UltimoNome != info.UltimoNome)
                    {
                        jogador.UltimoNome = info.UltimoNome;
                        mudou = true;
                    }

                    if (jogador.DataNascimento.HasValue)
                    {
                        atualizados++;
                        jogador.Atualizado = true;
                        jogador.DtAlt = DateTime.UtcNow;
                    }
                    else
                    {
                        // perfil veio sem data e sem idade aproveitável
                        semData++;
                    }

                    if (mudou) await _context.SaveChangesAsync();
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao atualizar informações (lote) de {Nome} (IdApi={Id})", jogador.Nome, jogador.IdApi);
                    falhas++;
                }
            }

            TempData["Sucesso"] =
                $"Atualizados com idade: {atualizados} ✅  |  Ainda sem data na API: {semData} ⚠️  |  Falhas: {falhas} ❌  " +
                $"(total sem idade verificado: {jogadores.Count})";

            return RedirectToAction(nameof(Index), new { semIdade = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarLinkTransfermarkt(int id, string linktransfermarket)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador == null) return NotFound();

            jogador.LinkTransfermarket = linktransfermarket;
            jogador.DtAlt = DateTime.UtcNow;

            _context.Update(jogador);
            await _context.SaveChangesAsync();

            TempData["Mensagem"] = "Link Transfermarkt atualizado com sucesso!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarFotosTodos(int? timeId)
        {
            var query = _context.Jogadores
                .Include(j => j.Time)
                .Where(j => string.IsNullOrEmpty(j.FotoUrl));

            if (timeId.HasValue)
                query = query.Where(j => j.TimeId == timeId.Value);

            var jogadores = await query.ToListAsync();

            int atualizados = 0;
            int falhas = 0;

            foreach (var jogador in jogadores)
            {
                try
                {
                    string? fotoUrl = null;
                    if (jogador.IdApi > 0)
                    {
                        var info = await _transfermarktService.BuscarInfoJogadorAsync(jogador.IdApi!.Value);
                        fotoUrl = info?.FotoUrl;
                    }

                    if (!string.IsNullOrWhiteSpace(fotoUrl))
                    {
                        jogador.FotoUrl = fotoUrl;
                        jogador.DtAlt = DateTime.UtcNow;
                        atualizados++;
                    }
                    else
                    {
                        falhas++;
                    }

                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao buscar foto de {Nome}", jogador.Nome);
                    falhas++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] =
                $"Fotos atualizadas: {atualizados} ✅  |  Não encontradas: {falhas} ❌  " +
                $"(total verificado: {jogadores.Count})";

            return RedirectToAction(nameof(Index));
        }

    }
}