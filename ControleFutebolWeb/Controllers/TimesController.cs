using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
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
        private readonly IWebHostEnvironment _env;
        private readonly ControleFutebolWeb.Services.ApiFootballService _apiFootballService;

        public TimesController(
            FutebolContext context,
            ILogger<TimesController> logger,
            IWebHostEnvironment env,
            ControleFutebolWeb.Services.ApiFootballService apiFootballService)
        {
            _context = context;
            _logger = logger;
            _env = env;
            _apiFootballService = apiFootballService;
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

            var times = await query.Include(t => t.FormacaoPadrao).OrderBy(t => t.Nome).ToListAsync();
            var timeIdsPagina = times.Select(t => t.Id).ToList();

            // Jogadores por time (evita carregar a coleção inteira só pra contar)
            var jogadoresPorTime = await _context.Jogadores
                .Where(j => timeIdsPagina.Contains(j.TimeId))
                .GroupBy(j => j.TimeId)
                .Select(g => new { TimeId = g.Key, Qtd = g.Count() })
                .ToDictionaryAsync(x => x.TimeId, x => x.Qtd);

            // Forma recente (últimos 5 jogos finalizados de cada time exibido)
            var jogosRecentesPorTime = await _context.Jogos
                .AsNoTracking()
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue &&
                    (timeIdsPagina.Contains(j.TimeCasaId) || timeIdsPagina.Contains(j.TimeVisitanteId)))
                .OrderByDescending(j => j.Data)
                .Select(j => new { j.TimeCasaId, j.TimeVisitanteId, j.PlacarCasa, j.PlacarVisitante })
                .ToListAsync();

            var formaPorTime = new Dictionary<int, List<char>>();
            void AdicionarForma(int timeId, char resultado)
            {
                if (!timeIdsPagina.Contains(timeId)) return;
                if (!formaPorTime.TryGetValue(timeId, out var lista))
                    formaPorTime[timeId] = lista = new List<char>();
                if (lista.Count < 5) lista.Add(resultado);
            }
            foreach (var j in jogosRecentesPorTime)
            {
                int pc = j.PlacarCasa!.Value, pv = j.PlacarVisitante!.Value;
                AdicionarForma(j.TimeCasaId, pc > pv ? 'V' : pc < pv ? 'D' : 'E');
                AdicionarForma(j.TimeVisitanteId, pv > pc ? 'V' : pv < pc ? 'D' : 'E');
            }

            // Listas completas para os tag selectors
            ViewBag.Competicoes = await _context.Competicoes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.Times = await _context.Times.OrderBy(t => t.Nome).ToListAsync();
            ViewBag.CompeticaoIdsFiltro = competicaoIds;
            ViewBag.TimeIdsFiltro = timeIds;
            ViewBag.JogadoresPorTime = jogadoresPorTime;
            ViewBag.FormaPorTime = formaPorTime;

            // KPIs do topo da página (totais globais, independentes do filtro aplicado)
            ViewBag.TotalTimesGlobal = await _context.Times.CountAsync();
            ViewBag.TotalJogadoresGlobal = await _context.Jogadores.CountAsync();
            ViewBag.TotalCompeticoesGlobal = await _context.Competicoes.CountAsync();

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

        // Cadastro manual de time foi removido: os times passam a vir exclusivamente
        // da importação das competições (times/jogadores). A edição (incl. uploads de
        // escudo/camisa/background) permanece para customizar times já importados.

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
                        var r = await UploadHelper.SalvarImagemAsync(escudoFile, _env.WebRootPath, "images/escudos", time.Nome);
                        if (!r.Sucesso)
                        {
                            ModelState.AddModelError("", $"Escudo: {r.Erro}");
                            return View(time);
                        }
                        time.EscudoUrl = r.UrlRelativa;
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

        // Busca na api-football idade, altura e peso apenas dos jogadores do elenco
        // que ainda não têm esses dados — evita gastar requisições à toa.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarDadosJogadores(int id)
        {
            var time = await _context.Times.FindAsync(id);
            if (time == null) return NotFound();

            var pendentes = await _context.Jogadores
                .Where(j => (j.TimeId == id || j.SelecaoId == id) &&
                            j.IdApi != null && j.IdApi > 0 &&
                            (j.DataNascimento == null || j.Altura == null || j.Peso == null))
                .ToListAsync();

            if (pendentes.Count == 0)
            {
                TempData["Mensagem"] = "Todos os jogadores do elenco já têm idade, altura e peso cadastrados.";
                return RedirectToAction(nameof(Details), new { id });
            }

            int atualizados = 0, falhas = 0;

            foreach (var jogador in pendentes)
            {
                try
                {
                    var info = await _apiFootballService.BuscarPerfilJogadorAsync(jogador.IdApi!.Value);
                    if (info == null) { falhas++; continue; }

                    var alterado = false;

                    if (jogador.DataNascimento == null && info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900)
                    {
                        jogador.DataNascimento = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Unspecified);
                        alterado = true;
                    }

                    if (jogador.Altura == null && info.Altura.HasValue)
                    {
                        jogador.Altura = info.Altura;
                        alterado = true;
                    }

                    if (jogador.Peso == null && info.Peso.HasValue)
                    {
                        jogador.Peso = info.Peso;
                        alterado = true;
                    }

                    if (alterado)
                    {
                        jogador.DtAlt = DateTime.UtcNow;
                        atualizados++;
                    }
                }
                catch (Exception ex)
                {
                    falhas++;
                    _logger.LogWarning(ex, "[AtualizarDadosJogadores] Falha ao buscar dados de {Nome} (IdApi={Id})", jogador.Nome, jogador.IdApi);
                }
            }

            if (atualizados > 0) await _context.SaveChangesAsync();

            TempData["Mensagem"] = $"{atualizados} jogador(es) atualizado(s)" +
                (falhas > 0 ? $", {falhas} falha(s)." : ".");

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> ImportarUniforme(int id, IFormFile arquivo, string tipo = "casa")
        {
            var time = await _context.Times.FindAsync(id);
            if (time == null) return NotFound();

            if (arquivo != null && arquivo.Length > 0)
            {
                var sufixo = tipo == "visitante" ? "camisa-visitante" : "camisa";
                var r = await UploadHelper.SalvarImagemAsync(arquivo, _env.WebRootPath, "Images/kits", $"{time.Nome}-{sufixo}");
                if (!r.Sucesso)
                {
                    TempData["Mensagem"] = $"Erro na camisa: {r.Erro}";
                    return RedirectToAction(nameof(Index));
                }

                if (tipo == "visitante")
                    time.CamisaVisitanteUrl = r.UrlRelativa;
                else
                    time.CamisaUrl = r.UrlRelativa;

                _context.Update(time);
                await _context.SaveChangesAsync();
                TempData["Mensagem"] = $"Camisa {tipo} importada com sucesso!";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UploadBackground(int id, IFormFile backgroundFile)
        {
            var time = _context.Times.Find(id);
            if (time == null) return NotFound();

            if (backgroundFile != null && backgroundFile.Length > 0)
            {
                var r = await UploadHelper.SalvarImagemAsync(backgroundFile, _env.WebRootPath, "images/backgrounds", time.Nome);
                if (!r.Sucesso)
                {
                    TempData["Mensagem"] = $"Erro no background: {r.Erro}";
                    return RedirectToAction("Details", new { id });
                }

                time.BackgroundUrl = r.UrlRelativa;
                await _context.SaveChangesAsync();
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
                    // TimeEscalacaoPadrao guarda coordenadas como int (arredonda o input double)
                    posicao.PosicaoX = (int)Math.Round(e.PosicaoX);
                    posicao.PosicaoY = (int)Math.Round(e.PosicaoY);
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

        // GET: Times/GolsPorIntervaloApi?timeId=1&leagueId=71&season=2026
        // Recalcula "gols por intervalo" a partir dos gols cadastrados localmente,
        // atribuindo corretamente o gol contra ao time que se beneficiou dele —
        // diferente da API-Football, que soma o gol contra no intervalo do time do autor.
        [HttpGet]
        public async Task<IActionResult> GolsPorIntervaloApi(int timeId, int leagueId, int season)
        {
            string[] buckets = { "0-15", "16-30", "31-45", "46-60", "61-75", "76-90", "91-105" };
            string Bucket(int minuto) => minuto switch
            {
                <= 15 => "0-15",
                <= 30 => "16-30",
                <= 45 => "31-45",
                <= 60 => "46-60",
                <= 75 => "61-75",
                <= 90 => "76-90",
                _ => "91-105"
            };

            // O leagueId da API-Football fica guardado em Competicao.LinkTransfermarket
            // no formato "apifoot:{leagueId}:{season}" (ver TimesController.Details/CompeticoesApi).
            var prefixoLiga = $"apifoot:{leagueId}:";
            var jogos = await _context.Jogos
                .Where(j => (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId)
                    && j.Temporada == season
                    && j.Competicao.LinkTransfermarket != null
                    && j.Competicao.LinkTransfermarket.StartsWith(prefixoLiga))
                .Select(j => j.Id)
                .ToListAsync();

            if (jogos.Count == 0)
                return Json(new { disponivel = false });

            var gols = await _context.Gols
                .Include(g => g.Jogador)
                .Where(g => jogos.Contains(g.JogoId))
                .ToListAsync();

            var golsFor = buckets.ToDictionary(b => b, _ => 0);
            var golsAgainst = buckets.ToDictionary(b => b, _ => 0);

            foreach (var g in gols)
            {
                if (g.Jogador == null) continue;
                var autorENosso = g.Jogador.TimeId == timeId || g.Jogador.SelecaoId == timeId;
                var bucket = Bucket(g.Minuto);
                bool marcadoPorNos = g.Contra ? !autorENosso : autorENosso;
                if (marcadoPorNos)
                    golsFor[bucket]++;
                else
                    golsAgainst[bucket]++;
            }

            return Json(new
            {
                disponivel = true,
                @for = golsFor.ToDictionary(kv => kv.Key, kv => new { total = kv.Value }),
                against = golsAgainst.ToDictionary(kv => kv.Key, kv => new { total = kv.Value }),
            });
        }
    }
}
