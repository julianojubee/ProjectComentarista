using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    [Authorize(Policy = "Admin")]
    public class ServicosController : Controller
    {
        private readonly ServicoMonitor _monitor;
        private readonly AtualizarJogadoresSemDataService _atualizarJogadores;
        private readonly FutebolContext _context;
        private readonly EspnEstatisticasService _espn;
        private readonly EspnEscalacaoService _espnEscalacao;
        private readonly FotMobService _fotmob;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CraqueDaPartidaService _craques;
        private readonly ApiFootballService _apiFootball;
        private readonly FifaEstatisticasService _fifaEstatisticas;

        public ServicosController(
            ServicoMonitor monitor,
            AtualizarJogadoresSemDataService atualizarJogadores,
            FutebolContext context,
            EspnEstatisticasService espn,
            EspnEscalacaoService espnEscalacao,
            FotMobService fotmob,
            UserManager<ApplicationUser> userManager,
            CraqueDaPartidaService craques,
            ApiFootballService apiFootball,
            FifaEstatisticasService fifaEstatisticas)
        {
            _fifaEstatisticas = fifaEstatisticas;
            _monitor = monitor;
            _atualizarJogadores = atualizarJogadores;
            _context = context;
            _espn = espn;
            _espnEscalacao = espnEscalacao;
            _fotmob = fotmob;
            _userManager = userManager;
            _craques = craques;
            _apiFootball = apiFootball;
        }

        public IActionResult Index()
        {
            return View(_monitor.ObterTodos());
        }

        // Retorna JSON para polling da página (atualiza status sem recarregar)
        [HttpGet]
        public IActionResult Status()
        {
            var lista = _monitor.ObterTodos().Select(s => new
            {
                s.Nome,
                Estado = s.Estado.ToString(),
                IniciadoEm = s.IniciadoEm?.ToString("dd/MM HH:mm"),
                UltimoCicloEm = s.UltimoCicloEm?.ToString("dd/MM HH:mm:ss"),
                ProximoCicloEm = s.ProximoCicloEm?.ToString("dd/MM HH:mm"),
                s.UltimaAtividade,
                s.CiclosCompletos,
                s.JogadoresAtualizados,
                s.Falhas
            });
            return Json(lista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Parar(string chave)
        {
            if (chave == AtualizarJogadoresSemDataService.Chave)
                _atualizarJogadores.Parar();

            TempData["Sucesso"] = "Serviço pausado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reiniciar(string chave)
        {
            if (chave == AtualizarJogadoresSemDataService.Chave)
                _atualizarJogadores.Reiniciar();

            TempData["Sucesso"] = "Serviço reiniciado.";
            return RedirectToAction(nameof(Index));
        }

        // ── Jogos sem estatísticas / fallback da ESPN ─────────────────────────

        /// <summary>
        /// Jogos encerrados a que falta estatística de time OU de jogador. O caso comum é
        /// a api-football devolver a partida só com o placar e os eventos (players,
        /// statistics e lineups vazios), mas ela também devolve partida com um bloco e não
        /// o outro — e esse buraco parcial é exatamente o que a importação da ESPN preenche,
        /// já que ela nunca sobrescreve o que a api-football gravou.
        ///
        /// Jogo da FIFA é exceção na parte de time: a FIFA não tem estatística de time
        /// que caiba em EstatisticasJson (indexado pelo IdApi da api-football), então só
        /// a de jogadora conta — sem isso o jogo nunca sairia da lista.
        /// </summary>
        private IQueryable<Jogo> JogosComEstatisticaFaltando() =>
            _context.Jogos
                .Where(j => j.Status == "Finalizado")
                .Where(j => ((j.EstatisticasJson == null || j.EstatisticasJson == "")
                             && (j.LinkDetalhes == null || !j.LinkDetalhes.StartsWith("fifa:")))
                            || !_context.EstatisticasJogador.Any(e => e.JogoId == j.Id));

        /// <summary>
        /// Tela de Serviços › Jogos sem estatísticas.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> JogosSemEstatisticas(
            int? competicaoId, int? temporada, CancellationToken ct)
        {
            var baseQuery = JogosComEstatisticaFaltando();

            var vm = new JogosSemEstatisticasViewModel
            {
                CompeticaoId = competicaoId,
                Temporada = temporada,
                LimiteLote = EspnEstatisticasService.LimitePorLote,
                LimiteLoteFotMob = FotMobService.LimitePorLote,
                Competicoes = await baseQuery
                    .GroupBy(j => new { j.CompeticaoId, j.Competicao!.Nome, j.Competicao.IdApi })
                    .Select(g => new CompeticaoSemEstatisticaItem
                    {
                        Id = g.Key.CompeticaoId,
                        Nome = g.Key.Nome,
                        Quantidade = g.Count(),
                    })
                    .OrderByDescending(c => c.Quantidade)
                    .ToListAsync(ct),
                Temporadas = await baseQuery.Select(j => j.Temporada).Distinct()
                    .OrderByDescending(t => t).ToListAsync(ct),
            };

            // O slug depende do IdApi da competição e o de-para vive em arquivo, então
            // é resolvido fora do banco.
            var idsApi = await _context.Competicoes
                .Where(c => vm.Competicoes.Select(x => x.Id).Contains(c.Id))
                .Select(c => new { c.Id, c.IdApi })
                .ToDictionaryAsync(c => c.Id, c => c.IdApi, ct);

            var competicoesFifa = await _context.Competicoes
                .Where(c => c.LinkTransfermarket != null && c.LinkTransfermarket.StartsWith("fifa:"))
                .Select(c => c.Id)
                .ToListAsync(ct);

            foreach (var c in vm.Competicoes)
            {
                c.TemEspn = _espn.SlugDaLiga(idsApi.GetValueOrDefault(c.Id)) != null;
                c.TemFotMob = _fotmob.TemCobertura(idsApi.GetValueOrDefault(c.Id));
                c.TemFifa = competicoesFifa.Contains(c.Id);
            }

            var filtrada = baseQuery;
            if (competicaoId.HasValue) filtrada = filtrada.Where(j => j.CompeticaoId == competicaoId.Value);
            if (temporada.HasValue) filtrada = filtrada.Where(j => j.Temporada == temporada.Value);

            vm.Total = await filtrada.CountAsync(ct);
            vm.LimiteLoteFifa = FifaEstatisticasService.LimitePorLote;
            vm.PendentesFifa = await JogosFifaSemEstatistica(competicaoId, temporada).CountAsync(ct);
            vm.Jogos = await filtrada
                .OrderByDescending(j => j.Data)
                .Take(200)
                .Select(j => new JogoSemEstatisticaItem
                {
                    Id = j.Id,
                    Data = j.Data,
                    CompeticaoId = j.CompeticaoId,
                    Competicao = j.Competicao!.Nome,
                    TimeCasa = j.TimeCasa.Nome,
                    TimeVisitante = j.TimeVisitante.Nome,
                    PlacarCasa = j.PlacarCasa,
                    PlacarVisitante = j.PlacarVisitante,
                    TemEstatisticasTime = j.EstatisticasJson != null && j.EstatisticasJson != "",
                    TemEstatisticasJogador = _context.EstatisticasJogador.Any(e => e.JogoId == j.Id),
                    EhFifa = j.LinkDetalhes != null && j.LinkDetalhes.StartsWith("fifa:"),
                })
                .ToListAsync(ct);

            foreach (var j in vm.Jogos)
            {
                j.SlugEspn = _espn.SlugDaLiga(idsApi.GetValueOrDefault(j.CompeticaoId));
                j.TemFotMob = _fotmob.TemCobertura(idsApi.GetValueOrDefault(j.CompeticaoId));
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarEstatisticasEspn(
            int jogoId, int? competicaoId, int? temporada, CancellationToken ct)
        {
            var resultado = await _espn.ImportarAsync(_context, jogoId, ct);

            if (resultado.Ok) TempData["Sucesso"] = $"Jogo {jogoId}: {resultado.Mensagem}";
            else TempData["Erro"] = $"Jogo {jogoId}: {resultado.Mensagem}";

            return RedirectToAction(nameof(JogosSemEstatisticas), new { competicaoId, temporada });
        }

        /// <summary>
        /// Importa em lote, do mais recente para o mais antigo. O teto de
        /// LimitePorLote existe para um clique não disparar centenas de chamadas
        /// seguidas na ESPN — repetir o botão continua de onde parou.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarEstatisticasEspnLote(
            int? competicaoId, int? temporada, CancellationToken ct)
        {
            var query = JogosComEstatisticaFaltando();

            if (competicaoId.HasValue) query = query.Where(j => j.CompeticaoId == competicaoId.Value);
            if (temporada.HasValue) query = query.Where(j => j.Temporada == temporada.Value);

            var ids = await query
                .OrderByDescending(j => j.Data)
                .Select(j => j.Id)
                .Take(EspnEstatisticasService.LimitePorLote)
                .ToListAsync(ct);

            int ok = 0, falhas = 0;
            string? ultimoErro = null;

            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;
                var r = await _espn.ImportarAsync(_context, id, ct);
                if (r.Ok) ok++;
                else { falhas++; ultimoErro = r.Mensagem; }
            }

            TempData["Sucesso"] = $"Lote da ESPN: {ok} jogo(s) preenchido(s), {falhas} sem dados.";
            if (falhas > 0 && ultimoErro != null) TempData["Erro"] = $"Último motivo: {ultimoErro}";

            return RedirectToAction(nameof(JogosSemEstatisticas), new { competicaoId, temporada });
        }

        // ── FotMob ────────────────────────────────────────────────────────────
        //
        // Terceira fonte, para o que nem a api-football nem a ESPN têm. Fica como botão
        // separado (e não como fallback automático do botão da ESPN) porque as duas não
        // cobrem as mesmas ligas nem os mesmos campos, e é melhor que a tela mostre qual
        // fonte foi usada em cada jogo do que esconder isso numa cascata.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarEstatisticasFotMob(
            int jogoId, int? competicaoId, int? temporada, CancellationToken ct)
        {
            var resultado = await _fotmob.ImportarAsync(_context, jogoId, ct);

            if (resultado.Ok) TempData["Sucesso"] = $"Jogo {jogoId}: {resultado.Mensagem}";
            else TempData["Erro"] = $"Jogo {jogoId}: {resultado.Mensagem}";

            return RedirectToAction(nameof(JogosSemEstatisticas), new { competicaoId, temporada });
        }

        /// <summary>
        /// Importa em lote, do mais recente para o mais antigo. O teto de
        /// LimitePorLote existe para um clique não disparar dezenas de chamadas
        /// seguidas no FotMob — repetir o botão continua de onde parou.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarEstatisticasFotMobLote(
            int? competicaoId, int? temporada, CancellationToken ct)
        {
            var query = JogosComEstatisticaFaltando();

            if (competicaoId.HasValue) query = query.Where(j => j.CompeticaoId == competicaoId.Value);
            if (temporada.HasValue) query = query.Where(j => j.Temporada == temporada.Value);

            var ids = await query
                .OrderByDescending(j => j.Data)
                .Select(j => j.Id)
                .Take(FotMobService.LimitePorLote)
                .ToListAsync(ct);

            int ok = 0, falhas = 0;
            string? ultimoErro = null;

            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;
                var r = await _fotmob.ImportarAsync(_context, id, ct);
                if (r.Ok) ok++;
                else { falhas++; ultimoErro = r.Mensagem; }
            }

            TempData["Sucesso"] = $"Lote do FotMob: {ok} jogo(s) preenchido(s), {falhas} sem dados.";
            if (falhas > 0 && ultimoErro != null) TempData["Erro"] = $"Último motivo: {ultimoErro}";

            return RedirectToAction(nameof(JogosSemEstatisticas), new { competicaoId, temporada });
        }

        // ── FIFA ──────────────────────────────────────────────────────────────
        //
        // Fonte das competições que só a FIFA publica (Mundial Sub-20 Feminino). Não é
        // fallback de nada: nesses jogos nem a ESPN nem o FotMob têm a partida.

        /// <summary>
        /// Jogos da FIFA já disputados sem nenhuma linha vinda da FIFA. Entram também os
        /// que têm marcação por vídeo — a importação preserva quem foi marcada e
        /// preenche o resto. "Já disputado" aceita o jogo cuja data passou há mais de
        /// três horas mesmo sem placar gravado: é o jogo que ninguém sincronizou, e a
        /// FIFA tem a estatística dele do mesmo jeito.
        /// </summary>
        private IQueryable<Jogo> JogosFifaSemEstatistica(int? competicaoId, int? temporada)
        {
            var limite = DateTime.UtcNow.AddHours(-3);

            var query = _context.Jogos
                .Where(j => j.LinkDetalhes != null && j.LinkDetalhes.StartsWith("fifa:"))
                .Where(j => j.Status == "Finalizado" || (j.Data != null && j.Data < limite))
                .Where(j => !_context.EstatisticasJogador.Any(e => e.JogoId == j.Id && e.Fonte == FonteEstatistica.Fifa));

            if (competicaoId.HasValue) query = query.Where(j => j.CompeticaoId == competicaoId.Value);
            if (temporada.HasValue) query = query.Where(j => j.Temporada == temporada.Value);
            return query;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarEstatisticasFifa(
            int jogoId, int? competicaoId, int? temporada, CancellationToken ct)
        {
            var resultado = await _fifaEstatisticas.ImportarAsync(_context, jogoId, ct);

            if (resultado.Ok) TempData["Sucesso"] = $"Jogo {jogoId}: {resultado.Mensagem}";
            else TempData["Erro"] = $"Jogo {jogoId}: {resultado.Mensagem}";

            return RedirectToAction(nameof(JogosSemEstatisticas), new { competicaoId, temporada });
        }

        /// <summary>
        /// Importa em lote os jogos da FIFA já disputados, do mais antigo para o mais
        /// recente — é a ordem da competição, e o jogo de ontem é o que mais pode ainda
        /// não ter sido publicado. Repetir o botão continua de onde parou.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarEstatisticasFifaLote(
            int? competicaoId, int? temporada, CancellationToken ct)
        {
            var ids = await JogosFifaSemEstatistica(competicaoId, temporada)
                .OrderBy(j => j.Data)
                .Select(j => j.Id)
                .Take(FifaEstatisticasService.LimitePorLote)
                .ToListAsync(ct);

            int ok = 0, falhas = 0;
            string? ultimoErro = null;

            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;
                var r = await _fifaEstatisticas.ImportarAsync(_context, id, ct);
                if (r.Ok) ok++;
                else { falhas++; ultimoErro = r.Mensagem; }
            }

            TempData["Sucesso"] = $"Lote da FIFA: {ok} jogo(s) preenchido(s), {falhas} sem dados.";
            if (falhas > 0 && ultimoErro != null) TempData["Erro"] = $"Último motivo: {ultimoErro}";

            return RedirectToAction(nameof(JogosSemEstatisticas), new { competicaoId, temporada });
        }

        // ── Placares que não fecham com os gols cadastrados ───────────────────
        //
        // Reimportar da api-football regrava lineup, eventos e placar do jogo — é o
        // conserto de quem tem os dados de origem incompletos ou trocados. O teto por
        // lote existe porque cada jogo é uma chamada à api-football, que é cotada.
        public const int LimiteLoteReimportacao = 20;

        /// <summary>
        /// Remonta o placar de cada jogo a partir dos gols cadastrados — com o lado do
        /// autor vindo da escalação daquela partida, a mesma regra da timeline — e devolve
        /// os que não batem com o placar gravado.
        /// </summary>
        private async Task<List<JogoPlacarDivergenteItem>> LevantarDivergentesAsync(
            int? competicaoId, int? temporada, CancellationToken ct)
        {
            var jogosQuery = _context.Jogos.AsNoTracking()
                .Where(j => j.PlacarCasa != null && j.PlacarVisitante != null);

            if (competicaoId.HasValue) jogosQuery = jogosQuery.Where(j => j.CompeticaoId == competicaoId.Value);
            if (temporada.HasValue) jogosQuery = jogosQuery.Where(j => j.Temporada == temporada.Value);

            var jogos = await jogosQuery
                .Select(j => new
                {
                    j.Id,
                    j.Data,
                    j.CompeticaoId,
                    j.Temporada,
                    Competicao = j.Competicao!.Nome,
                    Casa = j.TimeCasa.Nome,
                    Visitante = j.TimeVisitante.Nome,
                    j.TimeCasaId,
                    j.TimeVisitanteId,
                    PlacarCasa = j.PlacarCasa!.Value,
                    PlacarVisitante = j.PlacarVisitante!.Value,
                    j.LinkDetalhes,
                })
                .ToListAsync(ct);

            if (jogos.Count == 0) return new();

            // Filtro pela navegação, e não por uma lista de milhares de ids: sem filtro de
            // competição esta tela varre a base inteira.
            var golsQuery = _context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => g.Jogo.PlacarCasa != null && g.Jogo.PlacarVisitante != null);

            var escalacoesQuery = _context.Escalacoes.AsNoTracking()
                .Where(e => e.UsuarioId == null && e.JogadorId != null
                         && e.Jogo.PlacarCasa != null && e.Jogo.PlacarVisitante != null);

            if (competicaoId.HasValue)
            {
                golsQuery = golsQuery.Where(g => g.Jogo.CompeticaoId == competicaoId.Value);
                escalacoesQuery = escalacoesQuery.Where(e => e.Jogo.CompeticaoId == competicaoId.Value);
            }
            if (temporada.HasValue)
            {
                golsQuery = golsQuery.Where(g => g.Jogo.Temporada == temporada.Value);
                escalacoesQuery = escalacoesQuery.Where(e => e.Jogo.Temporada == temporada.Value);
            }

            var gols = await golsQuery.ToListAsync(ct);

            // Só a escalação de quem marcou: o resto do banco de reservas não muda placar.
            var escalacoes = await escalacoesQuery
                .Where(e => _context.Gols.Any(g => g.JogoId == e.JogoId && g.JogadorId == e.JogadorId))
                .Select(e => new Escalacao
                {
                    JogoId = e.JogoId,
                    JogadorId = e.JogadorId,
                    IsTimeCasa = e.IsTimeCasa,
                    FaseEscalacao = e.FaseEscalacao,
                })
                .ToListAsync(ct);

            var atuacoes = LadoJogadorHelper.Montar(escalacoes);
            var jogosComEscalacao = escalacoes.Select(e => e.JogoId).ToHashSet();
            var golsPorJogo = gols.GroupBy(g => g.JogoId).ToDictionary(g => g.Key, g => g.ToList());

            var divergentes = new List<JogoPlacarDivergenteItem>();

            foreach (var j in jogos)
            {
                if (!golsPorJogo.TryGetValue(j.Id, out var golsDoJogo)) continue;

                var jogo = new Jogo
                {
                    Id = j.Id,
                    TimeCasaId = j.TimeCasaId,
                    TimeVisitanteId = j.TimeVisitanteId,
                };

                int casa = 0, visitante = 0;
                foreach (var g in golsDoJogo)
                {
                    // Gol contra conta para o adversário de quem marcou.
                    if (LadoJogadorHelper.EhDoTimeDaCasa(g.Jogador, jogo, atuacoes) != g.Contra) casa++;
                    else visitante++;
                }

                if (casa == j.PlacarCasa && visitante == j.PlacarVisitante) continue;

                divergentes.Add(new JogoPlacarDivergenteItem
                {
                    Id = j.Id,
                    Data = j.Data,
                    CompeticaoId = j.CompeticaoId,
                    Temporada = j.Temporada,
                    Competicao = j.Competicao,
                    TimeCasa = j.Casa,
                    TimeVisitante = j.Visitante,
                    PlacarCasa = j.PlacarCasa,
                    PlacarVisitante = j.PlacarVisitante,
                    GolsCasa = casa,
                    GolsVisitante = visitante,
                    MotivoTotal = casa + visitante != j.PlacarCasa + j.PlacarVisitante,
                    TemProrrogacao = golsDoJogo.Any(g => g.Minuto > 90),
                    TemEscalacao = jogosComEscalacao.Contains(j.Id),
                    PodeReimportar = j.LinkDetalhes != null
                        && j.LinkDetalhes.StartsWith("apifoot:", StringComparison.OrdinalIgnoreCase),
                });
            }

            return divergentes.OrderByDescending(d => d.Data).ToList();
        }

        /// <summary>
        /// Tela de Serviços › Placares divergentes.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PlacaresDivergentes(
            int? competicaoId, int? temporada, CancellationToken ct)
        {
            // O levantamento sem filtro alimenta os selects; a tabela mostra só o recorte.
            var todos = await LevantarDivergentesAsync(null, null, ct);

            var filtrados = todos
                .Where(d => (!competicaoId.HasValue || d.CompeticaoId == competicaoId.Value)
                         && (!temporada.HasValue || d.Temporada == temporada.Value))
                .ToList();

            var vm = new PlacaresDivergentesViewModel
            {
                CompeticaoId = competicaoId,
                Temporada = temporada,
                LimiteLote = LimiteLoteReimportacao,
                Total = filtrados.Count,
                TotalReimportaveis = filtrados.Count(d => d.PodeReimportar),
                Jogos = filtrados.Take(200).ToList(),
                Competicoes = todos
                    .GroupBy(d => new { d.CompeticaoId, d.Competicao })
                    .Select(g => new CompeticaoDivergenteItem
                    {
                        Id = g.Key.CompeticaoId,
                        Nome = g.Key.Competicao,
                        Quantidade = g.Count(),
                    })
                    .OrderByDescending(c => c.Quantidade)
                    .ToList(),
                Temporadas = todos.Select(d => d.Temporada).Distinct().OrderByDescending(t => t).ToList(),
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReimportarJogo(
            int jogoId, int? competicaoId, int? temporada, CancellationToken ct)
        {
            var (ok, msg) = await _apiFootball.ForcarReimportarEscalacaoAsync(_context, jogoId, ct);

            if (ok) TempData["Sucesso"] = $"Jogo {jogoId}: {msg}";
            else TempData["Erro"] = $"Jogo {jogoId}: {msg}";

            return RedirectToAction(nameof(PlacaresDivergentes), new { competicaoId, temporada });
        }

        /// <summary>
        /// Reimporta em lote, do mais recente para o mais antigo, respeitando
        /// LimiteLoteReimportacao — repetir o botão continua de onde parou.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReimportarJogosDivergentesLote(
            int? competicaoId, int? temporada, CancellationToken ct)
        {
            var alvos = (await LevantarDivergentesAsync(competicaoId, temporada, ct))
                .Where(d => d.PodeReimportar)
                .Take(LimiteLoteReimportacao)
                .Select(d => d.Id)
                .ToList();

            int ok = 0, falhas = 0;
            string? ultimoErro = null;

            foreach (var id in alvos)
            {
                if (ct.IsCancellationRequested) break;
                var (sucesso, msg) = await _apiFootball.ForcarReimportarEscalacaoAsync(_context, id, ct);
                if (sucesso) ok++;
                else { falhas++; ultimoErro = msg; }
            }

            TempData["Sucesso"] = $"Reimportação: {ok} jogo(s) atualizado(s), {falhas} sem sucesso.";
            if (falhas > 0 && ultimoErro != null) TempData["Erro"] = $"Último motivo: {ultimoErro}";

            return RedirectToAction(nameof(PlacaresDivergentes), new { competicaoId, temporada });
        }

        // ── Conferência da escalação contra a ESPN ────────────────────────────

        /// <summary>
        /// Compara a escalação inicial salva com o XI publicado pela ESPN. Quando a
        /// api-football não manda lineup, a tela de Analisar preenche o campo com a
        /// escalação do último jogo do time — e nada avisa que aquilo é um chute.
        /// Só mostra: aplicar é outro botão.
        /// </summary>
        /// <param name="usuarioId">
        /// De quem é a escalação a conferir. A escalação é por usuário, e esta tela é
        /// de administração: dá para arrumar o jogo de outro analista. Sem valor, usa
        /// a de quem está logado.
        /// </param>
        [HttpGet]
        public async Task<IActionResult> ConferirEscalacao(int jogoId, string? usuarioId, CancellationToken ct)
        {
            var alvo = await ResolverUsuarioAlvoAsync(usuarioId);
            var vm = await _espnEscalacao.ConferirAsync(_context, jogoId, alvo, ct);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AplicarEscalacaoEspn(int jogoId, string? usuarioId, CancellationToken ct)
        {
            var alvo = await ResolverUsuarioAlvoAsync(usuarioId);
            var resultado = await _espnEscalacao.AplicarAsync(_context, jogoId, alvo, ct: ct);

            if (resultado.Ok) TempData["Sucesso"] = resultado.Mensagem;
            else TempData["Erro"] = resultado.Mensagem;

            return RedirectToAction(nameof(ConferirEscalacao), new { jogoId, usuarioId = alvo });
        }

        /// <summary>
        /// Remove as notas escolhidas na conferência — as que o analista deu a jogadores
        /// que não entraram em campo. Nunca é automático: a lista é marcada por ele.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverNotasSuspeitas(
            int jogoId, string? usuarioId, int[] notaIds, CancellationToken ct)
        {
            var alvo = await ResolverUsuarioAlvoAsync(usuarioId);

            if (notaIds is { Length: > 0 })
            {
                // Filtra por jogo e usuário também: o id vem do form e não pode virar
                // uma porta para apagar nota de outra partida ou de outro analista.
                var notas = await _context.Notas
                    .Include(n => n.Detalhes)
                    .Where(n => notaIds.Contains(n.Id) && n.JogoId == jogoId && n.UsuarioId == alvo)
                    .ToListAsync(ct);

                foreach (var n in notas) _context.NotaDetalhes.RemoveRange(n.Detalhes);
                _context.Notas.RemoveRange(notas);
                await _context.SaveChangesAsync(ct);

                // Apagar a nota de quem nem entrou em campo costuma justamente derrubar
                // um craque eleito por engano: refaz a eleição do jogo para esse analista.
                await _craques.RecalcularJogoAsync(jogoId, alvo, ct);

                TempData["Sucesso"] = $"{notas.Count} nota(s) removida(s).";
            }
            else
            {
                TempData["Erro"] = "Nenhuma nota selecionada.";
            }

            return RedirectToAction(nameof(ConferirEscalacao), new { jogoId, usuarioId = alvo });
        }

        // Só aceita id de usuário que existe — um id inventado na query string gravaria
        // escalação órfã, que nenhuma tela mostraria depois.
        private async Task<string> ResolverUsuarioAlvoAsync(string? usuarioId)
        {
            if (!string.IsNullOrWhiteSpace(usuarioId)
                && await _context.Users.AnyAsync(u => u.Id == usuarioId))
                return usuarioId;

            return _userManager.GetUserId(User)!;
        }
    }
}
