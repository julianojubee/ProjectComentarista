using ControleFutebolWeb.Data;
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

        public ServicosController(
            ServicoMonitor monitor,
            AtualizarJogadoresSemDataService atualizarJogadores,
            FutebolContext context,
            EspnEstatisticasService espn,
            EspnEscalacaoService espnEscalacao,
            FotMobService fotmob,
            UserManager<ApplicationUser> userManager,
            CraqueDaPartidaService craques)
        {
            _monitor = monitor;
            _atualizarJogadores = atualizarJogadores;
            _context = context;
            _espn = espn;
            _espnEscalacao = espnEscalacao;
            _fotmob = fotmob;
            _userManager = userManager;
            _craques = craques;
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
        /// </summary>
        private IQueryable<Jogo> JogosComEstatisticaFaltando() =>
            _context.Jogos
                .Where(j => j.Status == "Finalizado")
                .Where(j => j.EstatisticasJson == null || j.EstatisticasJson == ""
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

            foreach (var c in vm.Competicoes)
            {
                c.TemEspn = _espn.SlugDaLiga(idsApi.GetValueOrDefault(c.Id)) != null;
                c.TemFotMob = _fotmob.TemCobertura(idsApi.GetValueOrDefault(c.Id));
            }

            var filtrada = baseQuery;
            if (competicaoId.HasValue) filtrada = filtrada.Where(j => j.CompeticaoId == competicaoId.Value);
            if (temporada.HasValue) filtrada = filtrada.Where(j => j.Temporada == temporada.Value);

            vm.Total = await filtrada.CountAsync(ct);
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
