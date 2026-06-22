// ControleFutebolWeb/Controllers/RelatoriosController.cs
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class RelatoriosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RelatoriosController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Relatorios
        public async Task<IActionResult> Index(int[]? competicaoIds, int[]? timeIds, bool incluirNaoAnalisados = false)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var vm = await MontarViewModel(competicaoIds, timeIds, incluirNaoAnalisados, usuarioId);
            return View(vm);
        }

        // POST: /Relatorios/RecalcularNotas
        // Reaplica os pesos atuais (Cadastros > Critérios de Nota) às notas manuais já salvas.
        // As notas automáticas (vindas das estatísticas) já usam os pesos atuais a cada acesso.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalcularNotas(int[]? competicaoIds, int[]? timeIds, bool incluirNaoAnalisados = false, string? dummy = null)
        {
            // Pesos atuais por AcaoId
            var pesosPorAcao = await _context.CriteriosNota
                .ToDictionaryAsync(c => c.AcaoId, c => c.Peso);

            var notas = await _context.Notas
                .Include(n => n.Detalhes)
                .ToListAsync();

            int notasAtualizadas = 0;

            foreach (var nota in notas)
            {
                if (nota.Detalhes == null || nota.Detalhes.Count == 0) continue;

                bool mudou = false;
                foreach (var d in nota.Detalhes)
                {
                    // Atualiza o peso apenas se o critério ainda existir
                    if (pesosPorAcao.TryGetValue(d.AcaoId, out var pesoAtual) && d.Peso != pesoAtual)
                    {
                        d.Peso = pesoAtual;
                        mudou = true;
                    }
                }

                var novoTotal = Math.Round(nota.Detalhes.Sum(d => d.Quantidade * d.Peso), 2);
                if (mudou || nota.Valor != novoTotal)
                {
                    nota.Valor = novoTotal;
                    notasAtualizadas++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Recálculo concluído: {notasAtualizadas} nota(s) manual(is) atualizada(s) com os pesos atuais.";
            return RedirectToAction(nameof(Index), new { competicaoIds, timeIds, incluirNaoAnalisados });
        }

        // ── Monta o ViewModel completo ───────────────────────────────────────────
        private async Task<RelatoriosViewModel> MontarViewModel(int[]? competicaoIds = null, int[]? timeIds = null, bool incluirNaoAnalisados = false, string? usuarioId = null)
        {
            // Jogos com placar; se incluirNaoAnalisados=false, restringe aos analisados pelo usuário
            var jogosAnalisadosIds = usuarioId != null
                ? await _context.JogosAnalisadosUsuario
                    .Where(j => j.UsuarioId == usuarioId)
                    .Select(j => j.JogoId)
                    .ToHashSetAsync()
                : new HashSet<int>();

            var jogosQuery = _context.Jogos
            .Include(j => j.TimeCasa)
            .Include(j => j.TimeVisitante)
            .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue
             && (incluirNaoAnalisados || jogosAnalisadosIds.Contains(j.Id)));

            var compIds = (competicaoIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            if (compIds.Any())
                jogosQuery = jogosQuery.Where(j => compIds.Contains(j.CompeticaoId));

            var timeIdsFiltro = (timeIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            if (timeIdsFiltro.Any())
                jogosQuery = jogosQuery.Where(j => timeIdsFiltro.Contains(j.TimeCasaId) || timeIdsFiltro.Contains(j.TimeVisitanteId));

            var jogos = await jogosQuery.ToListAsync();
            var jogoIds = jogos.Select(j => j.Id).ToHashSet();

            // Carregar dados relacionados filtrados pelos jogos
            var gols = await _context.Gols
                .Include(g => g.Jogador).ThenInclude(j => j!.Time)
                .Include(g => g.Jogador).ThenInclude(j => j!.Selecao)
                .Where(g => jogoIds.Contains(g.JogoId))
                .ToListAsync();

            var estatisticasJogadores = await _context.EstatisticasJogador
                .Include(e => e.Jogador).ThenInclude(j => j!.Time)
                .Include(e => e.Jogador).ThenInclude(j => j!.Selecao)
                .Where(e => jogoIds.Contains(e.JogoId))
                .ToListAsync();

            var escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador).ThenInclude(j => j!.Time)
                .Include(e => e.Jogador).ThenInclude(j => j!.Selecao)
                .Where(e => jogoIds.Contains(e.JogoId) && e.JogadorId.HasValue && e.Titular
                         && (usuarioId == null || e.UsuarioId == usuarioId))
                .ToListAsync();

            var cartoes = await _context.Cartoes
                .Include(c => c.Jogador).ThenInclude(j => j!.Time)
                .Include(c => c.Jogador).ThenInclude(j => j!.Selecao)
                .Where(c => jogoIds.Contains(c.JogoId))
                .ToListAsync();

            var assistencias = await _context.Assistencias
                .Include(a => a.Jogador).ThenInclude(j => j!.Time)
                .Include(a => a.Jogador).ThenInclude(j => j!.Selecao)
                .Where(a => jogoIds.Contains(a.JogoId))
                .ToListAsync();

            var notas = await _context.Notas
                .Include(n => n.Jogador).ThenInclude(j => j!.Time)
                .Include(n => n.Jogador).ThenInclude(j => j!.Selecao)
                .Where(n => jogoIds.Contains(n.JogoId)
                         && (usuarioId == null || n.UsuarioId == usuarioId))
                .ToListAsync();

            var topTierIdsRel = usuarioId != null
                ? await _context.CompeticoesTopTierUsuario.Where(t => t.UsuarioId == usuarioId).Select(t => t.CompeticaoId).ToHashSetAsync()
                : new HashSet<int>();
            var competicoes = (await _context.Competicoes.OrderBy(c => c.Nome).ToListAsync())
                .OrderByDescending(c => topTierIdsRel.Contains(c.Id)).ThenBy(c => c.Nome).ToList();

            // Lista de times do filtro: só os que participam das competições selecionadas (ou todos, se nenhuma)
            List<Time> times;
            if (compIds.Any())
            {
                var idsCasa = _context.Jogos
                    .Where(j => compIds.Contains(j.CompeticaoId))
                    .Select(j => j.TimeCasaId);
                var idsVisitante = _context.Jogos
                    .Where(j => compIds.Contains(j.CompeticaoId))
                    .Select(j => j.TimeVisitanteId);
                var idsTimesNasComps = await idsCasa.Union(idsVisitante).ToListAsync();
                times = await _context.Times
                    .Where(t => idsTimesNasComps.Contains(t.Id))
                    .OrderBy(t => t.Nome).ToListAsync();
            }
            else
            {
                times = await _context.Times.OrderBy(t => t.Nome).ToListAsync();
            }

            // Filtro por time: inclui jogadores vinculados ao clube OU à seleção (para competições de seleção)
            if (timeIdsFiltro.Any())
            {
                bool MatchTime(Jogador? j) => j != null && (timeIdsFiltro.Contains(j.TimeId) || (j.SelecaoId.HasValue && timeIdsFiltro.Contains(j.SelecaoId.Value)));
                gols = gols.Where(g => MatchTime(g.Jogador)).ToList();
                estatisticasJogadores = estatisticasJogadores.Where(e => MatchTime(e.Jogador)).ToList();
                escalacoes = escalacoes.Where(e => MatchTime(e.Jogador)).ToList();
                cartoes = cartoes.Where(c => MatchTime(c.Jogador)).ToList();
                assistencias = assistencias.Where(a => MatchTime(a.Jogador)).ToList();
                notas = notas.Where(n => MatchTime(n.Jogador)).ToList();
            }

            // ── Pré-computa resultado por (jogoId, timeId) ────────────────────────
            var resultadoPorJogoTime = new Dictionary<(int jogoId, int timeId), ResultadoJogo>();
            foreach (var j in jogos)
            {
                var pc = j.PlacarCasa ?? 0;
                var pv = j.PlacarVisitante ?? 0;
                ResultadoJogo resCasa, resVis;
                if (pc > pv) { resCasa = ResultadoJogo.Vitoria; resVis = ResultadoJogo.Derrota; }
                else if (pc < pv) { resCasa = ResultadoJogo.Derrota; resVis = ResultadoJogo.Vitoria; }
                else { resCasa = ResultadoJogo.Empate; resVis = ResultadoJogo.Empate; }
                resultadoPorJogoTime[(j.Id, j.TimeCasaId)] = resCasa;
                resultadoPorJogoTime[(j.Id, j.TimeVisitanteId)] = resVis;
            }

            // ── Estatísticas de times ─────────────────────────────────────────────
            var statsTimes = CalcularEstatisticasTimes(jogos);
            // Quando há filtro de times, mostra apenas os times selecionados (não os adversários)
            if (timeIdsFiltro.Any())
                statsTimes = statsTimes.Where(t => timeIdsFiltro.Contains(t.Time.Id)).ToList();

            // Exibir nome da seleção (em vez do clube) quando TODAS as competições filtradas são de seleções
            bool exibirSelecao = compIds.Any()
                && await _context.Competicoes
                    .Where(c => compIds.Contains(c.Id))
                    .AllAsync(c => c.EhSelecaoNacional);

            // ── Monta ViewModel ───────────────────────────────────────────────────
            var vm = new RelatoriosViewModel
            {
                CompeticaoIdsFiltro = compIds,
                TimeIdsFiltro = timeIdsFiltro,
                ExibirSelecao = exibirSelecao,
                IncluirNaoAnalisados = incluirNaoAnalisados,
                Competicoes = competicoes,
                Times = times,
                TotalJogos = jogos.Count,
                TotalGols = gols.Count(g => !g.Contra),
                TotalGolsContra = gols.Count(g => g.Contra),
                TotalCartaoAmarelo = cartoes.Count(c => c.Tipo == "Amarelo"),
                TotalCartaoVermelho = cartoes.Count(c => c.Tipo == "Vermelho"),

                // Jogadores
                RankingNotas = CalcularRankingMisto(notas, estatisticasJogadores, resultadoPorJogoTime, jogos, 20, usuarioId),
                Artilheiros = RankGols(gols, false, 15),
                GolsContraRanking = RankGols(gols, true, 10),
                Assistencias = RankAssistencias(assistencias, 15),
                MaisPartidas = RankPartidas(escalacoes, 15),
                MaisCartoesAmarelos = RankCartoes(cartoes, "Amarelo", 10),
                MaisCartoesVermelhos = RankCartoes(cartoes, "Vermelho", 10),

                // Times
                TimesGols = statsTimes.OrderByDescending(t => t.GolsPro).Take(10).ToList(),
                TimesVitorias = statsTimes.OrderByDescending(t => t.Vitorias).ThenByDescending(t => t.Pontos).Take(10).ToList(),
                TimesMenosGolsSofridos = statsTimes.Where(t => t.Jogos >= 2).OrderBy(t => t.GolsContra).Take(10).ToList(),
                TimesAproveitamento = statsTimes.Where(t => t.Jogos >= 2).OrderByDescending(t => t.Aproveitamento).Take(10).ToList(),
                TimesMaisPontos = statsTimes.OrderByDescending(t => t.Pontos).Take(10).ToList(),
                TimesVitoriasCasa = statsTimes.Where(t => t.VitoriasCasa > 0).OrderByDescending(t => t.VitoriasCasa).Take(10).ToList(),
                TimesVitoriasVisitante = statsTimes.Where(t => t.VitoriasVisitante > 0).OrderByDescending(t => t.VitoriasVisitante).Take(10).ToList(),

                TimesFinalizacoes    = RankStatJogo(jogos, "Total Shots",      isPct: false, timesFiltro: timeIdsFiltro),
                TimesFinalizacoesNoGol = RankStatJogo(jogos, "Shots on Goal",  isPct: false, timesFiltro: timeIdsFiltro),
                TimesEscanteios      = RankStatJogo(jogos, "Corner Kicks",      isPct: false, timesFiltro: timeIdsFiltro),
                TimesPassesCertos    = RankStatJogo(jogos, "Passes accurate",   isPct: false, timesFiltro: timeIdsFiltro),
                TimesPosseBola       = RankStatJogo(jogos, "Ball Possession",   isPct: true,  timesFiltro: timeIdsFiltro),

                // Misc
                GolsPorRodada = jogos
                    .Where(j => j.Rodada > 0)
                    .GroupBy(j => j.Rodada)
                    .Select(g => new GolsPorRodada
                    {
                        Rodada = g.Key,
                        TotalGols = gols.Where(gl => g.Select(j => j.Id).Contains(gl.JogoId) && !gl.Contra).Count(),
                        Jogos = g.Count()
                    })
                    .OrderBy(r => r.Rodada)
                    .ToList(),

                MediasPorPosicao = new List<RelatoriosViewModel>()
                    .Select(x => new MediaPosicao()).ToList(), // placeholder, preenchido abaixo

                RankImpedimentos        = RankEstatJogador(estatisticasJogadores, e => e.Offsides, minPartidas: 1),
                RankFinalizacoesNoGol   = RankEstatJogador(estatisticasJogadores, e => e.FinalizacoesNoGol, minPartidas: 1),
                RankPassesChave         = RankEstatJogador(estatisticasJogadores, e => e.PassesChave, minPartidas: 1),
                RankDesarmes            = RankEstatJogador(estatisticasJogadores, e => e.Desarmes, minPartidas: 1),
                RankBloqueios           = RankEstatJogador(estatisticasJogadores, e => e.Bloqueios, minPartidas: 1),
                RankInterceptacoes      = RankEstatJogador(estatisticasJogadores, e => e.Interceptacoes, minPartidas: 1),
                RankDrilesCertos        = RankEstatJogador(estatisticasJogadores, e => e.DriblesCertos, minPartidas: 1),
                RankPenaltisDefendidos  = RankEstatJogador(estatisticasJogadores, e => e.PenaltiDefendido, minPartidas: 1, ordenarPorTotal: true),
            };

            // Rankings por posição (usa ranking completo, sem limite de 20)
            var rankingCompleto = CalcularRankingMisto(notas, estatisticasJogadores, resultadoPorJogoTime, jogos, int.MaxValue, usuarioId);
            vm.RankingGoleiros   = FiltrarRankingPorPosicao(rankingCompleto, "Goleiro");
            vm.RankingDefensores = FiltrarRankingPorPosicao(rankingCompleto, "Defensor");
            vm.RankingMeias      = FiltrarRankingPorPosicao(rankingCompleto, "Meia");
            vm.RankingAtacantes  = FiltrarRankingPorPosicao(rankingCompleto, "Atacante");

            // Médias por posição (calculadas do ranking de notas)
            vm.MediasPorPosicao = vm.RankingNotas
                .Where(r => !string.IsNullOrEmpty(r.Jogador.Posicao))
                .GroupBy(r => NormalizarPosicao(r.Jogador.Posicao))
                .Select(g => new MediaPosicao
                {
                    Posicao = g.Key,
                    Media = Math.Round(g.Average(r => r.NotaFinal), 2),
                    TotalJogadores = g.Count()
                })
                .OrderByDescending(p => p.Media)
                .ToList();

            vm.EstatisticasCompeticoes = await CalcularEstatisticasCompeticoesAsync();

            return vm;
        }

        // ── Estatísticas por competição (ignora filtros — sempre mostra todas) ──
        private async Task<List<EstatisticaCompeticao>> CalcularEstatisticasCompeticoesAsync()
        {
            var competicoes = await _context.Competicoes.OrderBy(c => c.Nome).ToListAsync();
            var resultado = new List<EstatisticaCompeticao>();

            foreach (var comp in competicoes)
            {
                var jogos = await _context.Jogos
                    .Include(j => j.TimeCasa)
                    .Include(j => j.TimeVisitante)
                    .Where(j => j.CompeticaoId == comp.Id && j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                    .ToListAsync();

                if (!jogos.Any()) continue;

                var jogoIds = jogos.Select(j => j.Id).ToHashSet();

                var gols = await _context.Gols
                    .Include(g => g.Jogador)
                    .Where(g => jogoIds.Contains(g.JogoId))
                    .ToListAsync();

                var cartoes = await _context.Cartoes
                    .Where(c => jogoIds.Contains(c.JogoId))
                    .ToListAsync();

                var assistencias = await _context.Assistencias
                    .Include(a => a.Jogador)
                    .Where(a => jogoIds.Contains(a.JogoId))
                    .ToListAsync();

                int vitoriasMandante = 0, vitoriasVisitante = 0, empates = 0;
                var vitoriasPorTime = new Dictionary<int, int>();

                foreach (var j in jogos)
                {
                    var pc = j.PlacarCasa ?? 0;
                    var pv = j.PlacarVisitante ?? 0;
                    if (pc > pv)
                    {
                        vitoriasMandante++;
                        vitoriasPorTime[j.TimeCasaId] = vitoriasPorTime.GetValueOrDefault(j.TimeCasaId) + 1;
                    }
                    else if (pv > pc)
                    {
                        vitoriasVisitante++;
                        vitoriasPorTime[j.TimeVisitanteId] = vitoriasPorTime.GetValueOrDefault(j.TimeVisitanteId) + 1;
                    }
                    else empates++;
                }

                var artilheiro = gols.Where(g => !g.Contra && g.Jogador != null)
                    .GroupBy(g => g.Jogador!)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                var melhorAssistente = assistencias.Where(a => a.Jogador != null)
                    .GroupBy(a => a.Jogador!)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                KeyValuePair<int, int> timeMaisVitorias = vitoriasPorTime.Any()
                    ? vitoriasPorTime.OrderByDescending(kv => kv.Value).First()
                    : new KeyValuePair<int, int>(0, 0);

                var timeNome = timeMaisVitorias.Key > 0
                    ? jogos.SelectMany(j => new[] { j.TimeCasa, j.TimeVisitante })
                           .Where(t => t?.Id == timeMaisVitorias.Key)
                           .Select(t => t?.Nome).FirstOrDefault()
                    : null;

                var jogoMaisGols = jogos
                    .OrderByDescending(j => (j.PlacarCasa ?? 0) + (j.PlacarVisitante ?? 0))
                    .FirstOrDefault();

                resultado.Add(new EstatisticaCompeticao
                {
                    Competicao           = comp,
                    TotalJogos           = jogos.Count,
                    JogosAnalisados      = jogos.Count(j => j.Analisado == 1),
                    TotalGols            = gols.Count(g => !g.Contra),
                    TotalGolsContra      = gols.Count(g => g.Contra),
                    TotalCartaoAmarelo   = cartoes.Count(c => c.Tipo == "Amarelo"),
                    TotalCartaoVermelho  = cartoes.Count(c => c.Tipo == "Vermelho"),
                    VitoriasMandante     = vitoriasMandante,
                    VitoriasVisitante    = vitoriasVisitante,
                    Empates              = empates,
                    ArtilheiroNome       = artilheiro?.Key.Nome,
                    ArtilheiroGols       = artilheiro?.Count() ?? 0,
                    AssistenteNome       = melhorAssistente?.Key.Nome,
                    AssistenciasTotal    = melhorAssistente?.Count() ?? 0,
                    TimeMaisVitorias     = timeNome,
                    TimeMaisVitoriasQtd  = timeMaisVitorias.Value,
                    JogoMaisGols         = jogoMaisGols != null
                        ? $"{jogoMaisGols.TimeCasa?.Nome} {jogoMaisGols.PlacarCasa}x{jogoMaisGols.PlacarVisitante} {jogoMaisGols.TimeVisitante?.Nome}"
                        : null,
                    JogoMaisGolsTotal    = jogoMaisGols != null
                        ? (jogoMaisGols.PlacarCasa ?? 0) + (jogoMaisGols.PlacarVisitante ?? 0)
                        : 0,
                });
            }

            return resultado;
        }

        // ── Cálculo da Nota Final com métrica base-5 + resultado ────────────────
        /// <summary>
        /// NotaFinal = 5  (base)
        ///           + média das notas de ação (Nota.Valor / partidas)
        ///           + bônus resultado: vitória = +1, derrota = -1, empate = 0 (por partida, calculado como média)
        /// Clamp 0..10
        /// </summary>
        private static List<RankingNotaItem> FiltrarRankingPorPosicao(List<RankingNotaItem> ranking, string grupo)
        {
            bool Match(string? posicao) => grupo switch
            {
                "Goleiro"  => posicao != null && posicao.Contains("Goleiro", StringComparison.OrdinalIgnoreCase),
                "Defensor" => posicao != null && (posicao.Contains("Defensor", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Zagueiro", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Lateral", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Ala", StringComparison.OrdinalIgnoreCase)),
                "Meia"     => posicao != null && (posicao.Contains("Meia", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Volante", StringComparison.OrdinalIgnoreCase)),
                "Atacante" => posicao != null && (posicao.Contains("Atacante", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Ponta", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Centroavante", StringComparison.OrdinalIgnoreCase)),
                _ => false
            };

            return ranking
                .Where(r => Match(r.Jogador.Posicao))
                .Take(10)
                .ToList();
        }

        private List<RankingNotaItem> CalcularRankingEstatisticas(
            List<EstatisticaJogador> estatisticas,
            Dictionary<(int jogoId, int timeId), ResultadoJogo> resultadoPorJogoTime,
            List<Jogo> jogos,
            int limite = 20,
            string? usuarioId = null)
        {
            var ranking = new List<RankingNotaItem>();

            var criteriosBanco = CriteriosNotaHelper.MergeCriterios(
                _context.CriteriosNota.Where(c => c.UsuarioId == null).ToList(),
                _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToList());

            // jogoId → competição, para agrupar as partidas do jogador por campeonato
            var compPorJogo = jogos.ToDictionary(j => j.Id, j => j.CompeticaoId);

            // (competição, time) → nº de jogos que o time disputou no campeonato
            int JogosDoTimeNoCampeonato(int compId, int timeId) =>
                jogos.Count(j => j.CompeticaoId == compId
                              && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId));

            var estatisticasPorJogador = estatisticas
                .Where(e => e.Jogador != null)
                .GroupBy(e => e.Jogador!);

            foreach (var grupo in estatisticasPorJogador)
            {
                var jogador = grupo.Key;
                var lista = grupo.ToList();
                int partidas = lista.Select(e => e.JogoId).Distinct().Count();
                if (partidas == 0) continue;

                // ── Elegibilidade: o jogador precisa ter disputado pelo menos metade
                // dos jogos que seu time fez no campeonato em que mais atuou. ──────────
                var partidasPorComp = lista
                    .Select(e => e.JogoId).Distinct()
                    .Where(jid => compPorJogo.ContainsKey(jid))
                    .GroupBy(jid => compPorJogo[jid])
                    .Select(g => new { CompId = g.Key, Jogos = g.Count() })
                    .OrderByDescending(x => x.Jogos)
                    .FirstOrDefault();

                if (partidasPorComp != null)
                {
                    int timeId = jogador.TimeId > 0 ? jogador.TimeId : (jogador.SelecaoId ?? 0);
                    int totalTime = JogosDoTimeNoCampeonato(partidasPorComp.CompId, timeId);
                    if (totalTime > 0 && partidasPorComp.Jogos * 2 < totalTime)
                        continue; // jogou menos da metade → fora do ranking
                }

                // Nota final por jogo (clampada individualmente) e depois média — igual à página de Estatísticas
                double somaNotasFinais = lista
                    .GroupBy(e => e.JogoId)
                    .Sum(g => Math.Max(CriteriosNotaHelper.NotaMinima,
                        Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + g.Sum(e => CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco)))));

                int vitorias = 0, derrotas = 0, empates = 0;
                foreach (var e in lista)
                {
                    ResultadoJogo res = ResultadoJogo.Empate;
                    bool achou = (jogador.TimeId > 0 && resultadoPorJogoTime.TryGetValue((e.JogoId, jogador.TimeId), out res))
                              || (jogador.SelecaoId.HasValue && resultadoPorJogoTime.TryGetValue((e.JogoId, jogador.SelecaoId.Value), out res));
                    if (achou)
                    {
                        if (res == ResultadoJogo.Vitoria) vitorias++;
                        else if (res == ResultadoJogo.Derrota) derrotas++;
                        else empates++;
                    }
                }

                double notaFinal = Math.Round(somaNotasFinais / partidas, 2);

                ranking.Add(new RankingNotaItem
                {
                    Jogador = jogador,
                    NotaFinal = notaFinal,
                    NotaBase = notaFinal,
                    BonusResultado = 0,
                    Partidas = partidas,
                    Vitorias = vitorias,
                    Derrotas = derrotas,
                    Empates = empates,
                });
            }

            return ranking
                .OrderByDescending(r => r.NotaFinal)
                .ThenByDescending(r => r.Partidas)
                .Take(limite)
                .ToList();
        }

        // ── Ranking misto: por jogo usa nota manual se existir, senão calcula das estatísticas ──
        private List<RankingNotaItem> CalcularRankingMisto(
            List<Nota> notas,
            List<EstatisticaJogador> estatisticas,
            Dictionary<(int jogoId, int timeId), ResultadoJogo> resultadoPorJogoTime,
            List<Jogo> jogos,
            int limite = 20,
            string? usuarioId = null)
        {
            var criteriosBanco = CriteriosNotaHelper.MergeCriterios(
                _context.CriteriosNota.Where(c => c.UsuarioId == null).ToList(),
                _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToList());
            var compPorJogo = jogos.ToDictionary(j => j.Id, j => j.CompeticaoId);

            int JogosDoTimeNoCampeonato(int compId, int timeId) =>
                jogos.Count(j => j.CompeticaoId == compId
                              && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId));

            // índices para lookup rápido
            var notasPorJogadorJogo = notas
                .Where(n => n.Jogador != null)
                .GroupBy(n => (n.JogadorId, n.JogoId))
                .ToDictionary(g => g.Key, g => g.Average(n => n.Valor));

            var estatsPorJogadorJogo = estatisticas
                .Where(e => e.Jogador != null)
                .GroupBy(e => (e.JogadorId, e.JogoId))
                .ToDictionary(g => g.Key, g => g.ToList());

            // todos os jogadores que aparecem em qualquer fonte
            var todosJogadores = notas.Where(n => n.Jogador != null).Select(n => n.Jogador!)
                .Concat(estatisticas.Where(e => e.Jogador != null).Select(e => e.Jogador!))
                .GroupBy(j => j.Id)
                .Select(g => g.First())
                .ToList();

            // todos os jogos em que cada jogador aparece (de qualquer fonte)
            var jogosPorJogador = new Dictionary<int, HashSet<int>>();
            foreach (var n in notas.Where(x => x.Jogador != null))
            {
                if (!jogosPorJogador.ContainsKey(n.JogadorId)) jogosPorJogador[n.JogadorId] = new HashSet<int>();
                jogosPorJogador[n.JogadorId].Add(n.JogoId);
            }
            foreach (var e in estatisticas.Where(x => x.Jogador != null))
            {
                if (!jogosPorJogador.ContainsKey(e.JogadorId)) jogosPorJogador[e.JogadorId] = new HashSet<int>();
                jogosPorJogador[e.JogadorId].Add(e.JogoId);
            }

            var ranking = new List<RankingNotaItem>();

            foreach (var jogador in todosJogadores)
            {
                if (!jogosPorJogador.TryGetValue(jogador.Id, out var jogoIdSet)) continue;
                var jogoIdList = jogoIdSet.ToList();
                int partidas = jogoIdList.Count;
                if (partidas == 0) continue;

                // elegibilidade: ao menos metade dos jogos do campeonato principal
                var partidasPorComp = jogoIdList
                    .Where(jid => compPorJogo.ContainsKey(jid))
                    .GroupBy(jid => compPorJogo[jid])
                    .Select(g => new { CompId = g.Key, Jogos = g.Count() })
                    .OrderByDescending(x => x.Jogos)
                    .FirstOrDefault();

                if (partidasPorComp != null)
                {
                    int tId = jogador.TimeId > 0 ? jogador.TimeId : 0;
                    int totalTime = JogosDoTimeNoCampeonato(partidasPorComp.CompId, tId);
                    if (totalTime > 1 && partidasPorComp.Jogos * 2 < totalTime)
                        continue;
                }

                // por jogo: prioriza nota manual, cai para estatísticas.
                // A nota final de cada jogo é clampada individualmente (base + valor, entre mínima e 10)
                // e só então tiramos a média — igual à página de Estatísticas.
                double somaNotasFinais = 0;
                int jogosComputados = 0;
                int vitorias = 0, derrotas = 0, empates = 0;

                foreach (var jogoId in jogoIdList)
                {
                    double valorJogo;
                    if (notasPorJogadorJogo.TryGetValue((jogador.Id, jogoId), out var notaValor))
                    {
                        valorJogo = notaValor;
                    }
                    else if (estatsPorJogadorJogo.TryGetValue((jogador.Id, jogoId), out var estats))
                    {
                        valorJogo = estats.Sum(e => Math.Round(CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco), 2));
                    }
                    else continue;

                    // Nota final do jogo arredondada a 2 casas — idêntico à página de Estatísticas
                    somaNotasFinais += Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima,
                        Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + valorJogo)), 2);
                    jogosComputados++;

                    ResultadoJogo res = ResultadoJogo.Empate;
                    bool achouResultado = (jogador.TimeId > 0 && resultadoPorJogoTime.TryGetValue((jogoId, jogador.TimeId), out res))
                                      || (jogador.SelecaoId.HasValue && resultadoPorJogoTime.TryGetValue((jogoId, jogador.SelecaoId.Value), out res));
                    if (achouResultado)
                    {
                        if (res == ResultadoJogo.Vitoria) vitorias++;
                        else if (res == ResultadoJogo.Derrota) derrotas++;
                        else empates++;
                    }
                }

                if (jogosComputados == 0) continue;
                double notaFinal = Math.Round(somaNotasFinais / jogosComputados, 2);

                ranking.Add(new RankingNotaItem
                {
                    Jogador = jogador,
                    NotaFinal = notaFinal,
                    NotaBase = notaFinal,
                    BonusResultado = 0,
                    Partidas = partidas,
                    Vitorias = vitorias,
                    Derrotas = derrotas,
                    Empates = empates,
                });
            }

            return ranking
                .OrderByDescending(r => r.NotaFinal)
                .ThenByDescending(r => r.Partidas)
                .Take(limite)
                .ToList();
        }

        // ── Ranking baseado nas Notas manuais/automáticas (filtradas por competição) ──
        private static List<RankingNotaItem> CalcularRankingPorNotas(
            List<Nota> notas,
            Dictionary<(int jogoId, int timeId), ResultadoJogo> resultadoPorJogoTime,
            List<Jogo> jogos,
            int limite = 20)
        {
            var compPorJogo = jogos.ToDictionary(j => j.Id, j => j.CompeticaoId);

            int JogosDoTimeNoCampeonato(int compId, int timeId) =>
                jogos.Count(j => j.CompeticaoId == compId
                              && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId));

            var ranking = new List<RankingNotaItem>();

            var notasPorJogador = notas
                .Where(n => n.Jogador != null)
                .GroupBy(n => n.JogadorId);

            foreach (var grupo in notasPorJogador)
            {
                var lista = grupo.ToList();
                var jogador = lista.First().Jogador!;
                int partidas = lista.Select(n => n.JogoId).Distinct().Count();
                if (partidas == 0) continue;

                // Elegibilidade: jogou ao menos metade dos jogos do time no campeonato principal
                var partidasPorComp = lista
                    .Select(n => n.JogoId).Distinct()
                    .Where(jid => compPorJogo.ContainsKey(jid))
                    .GroupBy(jid => compPorJogo[jid])
                    .Select(g => new { CompId = g.Key, Jogos = g.Count() })
                    .OrderByDescending(x => x.Jogos)
                    .FirstOrDefault();

                if (partidasPorComp != null)
                {
                    int tId = jogador.TimeId > 0 ? jogador.TimeId : 0;
                    int totalTime = JogosDoTimeNoCampeonato(partidasPorComp.CompId, tId);
                    if (totalTime > 1 && partidasPorComp.Jogos * 2 < totalTime)
                        continue;
                }

                // Nota final por jogo (clampada individualmente) e depois média — igual à página de Estatísticas
                double somaNotasFinais = lista
                    .GroupBy(n => n.JogoId)
                    .Sum(g => Math.Max(CriteriosNotaHelper.NotaMinima,
                        Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + g.Average(n => n.Valor))));
                double notaFinal = Math.Round(somaNotasFinais / partidas, 2);

                int vitorias = 0, derrotas = 0, empates = 0;
                foreach (var n in lista)
                {
                    ResultadoJogo res = ResultadoJogo.Empate;
                    bool achou = (jogador.TimeId > 0 && resultadoPorJogoTime.TryGetValue((n.JogoId, jogador.TimeId), out res))
                              || (jogador.SelecaoId.HasValue && resultadoPorJogoTime.TryGetValue((n.JogoId, jogador.SelecaoId.Value), out res));
                    if (achou)
                    {
                        if (res == ResultadoJogo.Vitoria) vitorias++;
                        else if (res == ResultadoJogo.Derrota) derrotas++;
                        else empates++;
                    }
                }

                ranking.Add(new RankingNotaItem
                {
                    Jogador = jogador,
                    NotaFinal = notaFinal,
                    NotaBase = notaFinal,
                    BonusResultado = 0,
                    Partidas = partidas,
                    Vitorias = vitorias,
                    Derrotas = derrotas,
                    Empates = empates,
                });
            }

            return ranking
                .OrderByDescending(r => r.NotaFinal)
                .ThenByDescending(r => r.Partidas)
                .Take(limite)
                .ToList();
        }

        // ── Helpers de ranking ───────────────────────────────────────────────────
        private static List<JogadorEstatistica> RankGols(List<Gol> gols, bool contra, int top) =>
            gols
                .Where(g => g.Contra == contra && g.Jogador != null)
                .GroupBy(g => g.Jogador!)
                .Select(g => new JogadorEstatistica
                {
                    Jogador = g.Key,
                    Valor = g.Count(),
                    Detalhe = $"{g.Count()} {(contra ? "gol(s) contra" : "gol(s)")}"
                })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToList();

        private static List<JogadorEstatistica> RankPartidas(List<Escalacao> escalacoes, int top) =>
            escalacoes
                .Where(e => e.Jogador != null)
                .GroupBy(e => e.Jogador!)
                .Select(g => new JogadorEstatistica
                {
                    Jogador = g.Key,
                    Valor = g.Select(e => e.JogoId).Distinct().Count(),
                    Detalhe = $"{g.Select(e => e.JogoId).Distinct().Count()} partida(s)"
                })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToList();

        private static List<JogadorEstatistica> RankAssistencias(List<Assistencia> assistencias, int top) =>
            assistencias
                .Where(a => a.Jogador != null)
                .GroupBy(a => a.Jogador!)
                .Select(g => new JogadorEstatistica
                {
                    Jogador = g.Key,
                    Valor = g.Count(),
                    Detalhe = $"{g.Count()} assistência(s)"
                })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToList();

        private static List<JogadorEstatistica> RankCartoes(List<Cartao> cartoes, string tipo, int top) =>
            cartoes
                .Where(c => c.Tipo == tipo && c.Jogador != null)
                .GroupBy(c => c.Jogador!)
                .Select(g => new JogadorEstatistica
                {
                    Jogador = g.Key,
                    Valor = g.Count(),
                    Detalhe = $"{g.Count()} cartão(ões)"
                })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToList();

        private static List<TimeEstatistica> CalcularEstatisticasTimes(List<Jogo> jogos)
        {
            var dict = new Dictionary<int, TimeEstatistica>();
            foreach (var jogo in jogos)
            {
                if (jogo.TimeCasa == null || jogo.TimeVisitante == null) continue;
                var pc = jogo.PlacarCasa ?? 0;
                var pv = jogo.PlacarVisitante ?? 0;

                EnsureTime(dict, jogo.TimeCasaId, jogo.TimeCasa);
                EnsureTime(dict, jogo.TimeVisitanteId, jogo.TimeVisitante);

                var c = dict[jogo.TimeCasaId];
                var v = dict[jogo.TimeVisitanteId];

                c.Jogos++; c.GolsPro += pc; c.GolsContra += pv;
                v.Jogos++; v.GolsPro += pv; v.GolsContra += pc;

                if (pc > pv) { c.Vitorias++; c.VitoriasCasa++; c.Pontos += 3; v.Derrotas++; }
                else if (pc < pv) { v.Vitorias++; v.VitoriasVisitante++; v.Pontos += 3; c.Derrotas++; }
                else { c.Empates++; v.Empates++; c.Pontos++; v.Pontos++; }
            }
            return dict.Values.ToList();
        }

        private static void EnsureTime(Dictionary<int, TimeEstatistica> d, int id, Time t)
        {
            if (!d.ContainsKey(id)) d[id] = new TimeEstatistica { Time = t };
        }

        private static List<TimeStatJogo> RankStatJogo(
            List<Jogo> jogos, string chave, bool isPct, int top = 10, List<int>? timesFiltro = null)
        {
            var acc = new Dictionary<int, (Time time, double soma, int jogos)>();

            foreach (var jogo in jogos)
            {
                if (string.IsNullOrEmpty(jogo.EstatisticasJson)) continue;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jogo.EstatisticasJson);
                    foreach (var entry in doc.RootElement.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("TimeId", out var tidEl)) continue;
                        int apiId = tidEl.GetInt32();

                        Time? time = null;
                        int internalId = 0;
                        if (jogo.TimeCasa?.IdApi == apiId)  { time = jogo.TimeCasa;       internalId = jogo.TimeCasaId; }
                        else if (jogo.TimeVisitante?.IdApi == apiId) { time = jogo.TimeVisitante; internalId = jogo.TimeVisitanteId; }
                        if (time == null) continue;

                        if (!entry.TryGetProperty("Stats", out var stats)) continue;
                        if (!stats.TryGetProperty(chave, out var valEl)) continue;

                        string? raw = valEl.ValueKind == System.Text.Json.JsonValueKind.Null
                            ? null : valEl.GetString();
                        if (string.IsNullOrEmpty(raw)) continue;

                        double v;
                        if (isPct)
                        {
                            if (!double.TryParse(raw.Replace("%", "").Trim(), out v)) continue;
                        }
                        else
                        {
                            if (!double.TryParse(raw, out v)) continue;
                        }

                        if (!acc.ContainsKey(internalId))
                            acc[internalId] = (time, 0, 0);
                        var cur = acc[internalId];
                        acc[internalId] = (time, cur.soma + v, cur.jogos + 1);
                    }
                }
                catch { /* ignora JSON malformado */ }
            }

            return acc.Values
                .Where(x => x.jogos >= 1)
                .Where(x => timesFiltro == null || !timesFiltro.Any() || timesFiltro.Contains(x.time.Id))
                .Select(x => new TimeStatJogo
                {
                    Time = x.time,
                    Jogos = x.jogos,
                    Valor = Math.Round(x.soma / x.jogos, 1)
                })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToList();
        }

        private static List<RankingEstatJogador> RankEstatJogador(
            List<EstatisticaJogador> estatisticas,
            Func<EstatisticaJogador, int> seletor,
            int minPartidas = 1,
            bool ordenarPorTotal = false,
            int top = 10)
        {
            return estatisticas
                .Where(e => e.Jogador != null)
                .GroupBy(e => e.JogadorId)
                .Where(g => g.Count() >= minPartidas)
                .Select(g => new RankingEstatJogador
                {
                    Jogador  = g.First().Jogador,
                    Partidas = g.Count(),
                    Total    = g.Sum(seletor),
                    Media    = Math.Round(g.Average(e => (double)seletor(e)), 2)
                })
                .Where(r => r.Total > 0)
                .OrderByDescending(r => ordenarPorTotal ? r.Total : r.Media)
                .ThenByDescending(r => r.Partidas)
                .Take(top)
                .ToList();
        }

        private static string NormalizarPosicao(string p)
        {
            var s = (p ?? "").ToLowerInvariant();
            if (s.Contains("gol") || s == "gl") return "Goleiro";
            if (s.Contains("zag") || s.Contains("def") || s.Contains("lat") || s == "zg") return "Defensor";
            if (s.Contains("mei") || s.Contains("vol") || s == "mc") return "Meio-campo";
            if (s.Contains("ata") || s.Contains("ponta") || s.Contains("centro") || s == "at") return "Atacante";
            return "Outro";
        }
    }

    public enum ResultadoJogo { Vitoria, Derrota, Empate }
}