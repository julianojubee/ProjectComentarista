// ControleFutebolWeb/Services/RelatoriosService.cs
// Motor de agregação dos Relatórios (rankings de jogadores/times, totais, médias).
// Extraído do RelatoriosController para ser reaproveitado pela API mobile
// (api/v1/relatorios) sem duplicar regra de negócio.
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    public enum ResultadoJogo { Vitoria, Derrota, Empate }

    public class RelatoriosService
    {
        private readonly FutebolContext _context;

        public RelatoriosService(FutebolContext context)
        {
            _context = context;
        }

        // ── Monta o ViewModel completo ───────────────────────────────────────────
        // incluirEstatisticasCompeticoes/incluirMatchUp: a API mobile desliga os
        // blocos que não exibe (estatísticas por competição custam 1 rodada de
        // consultas POR competição; o Match Up custa 2 consultas extras).
        public async Task<RelatoriosViewModel> MontarAsync(
            int[]? competicaoIds = null, int[]? timeIds = null, int? temporada = null,
            bool incluirNaoAnalisados = false, int minJogos = 1, string? usuarioId = null,
            bool incluirEstatisticasCompeticoes = true, bool incluirMatchUp = true)
        {
            // Mínimo de jogos no ranking de estatísticas individuais: configurável pelo
            // usuário (antes era fixo em 10) — 1 ou menos = sem filtro (ex.: na Copa do
            // Mundo poucos jogadores chegam a 10 jogos).
            var minJogosEstat = minJogos < 1 ? 1 : minJogos;
            // Jogos com placar; se incluirNaoAnalisados=false, restringe aos analisados pelo usuário
            var jogosAnalisadosIds = usuarioId != null
                ? await _context.JogosAnalisadosUsuario
                    .Where(j => j.UsuarioId == usuarioId && j.Analisado)
                    .Select(j => j.JogoId)
                    .ToHashSetAsync()
                : new HashSet<int>();

            // Tudo aqui é somente leitura (alimenta a view): AsNoTrackingWithIdentityResolution
            // evita o custo do change tracker mas mantém a deduplicação das entidades
            // repetidas nos Includes (mesmo Jogador/Time em milhares de linhas).
            var jogosQuery = _context.Jogos
            .AsNoTrackingWithIdentityResolution()
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

            // Temporadas disponíveis (após filtro de competição/time). Padrão: a mais recente.
            var temporadasDisponiveis = await jogosQuery
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t).ToListAsync();

            int? temporadaSelecionada = temporada
                ?? (temporadasDisponiveis.Any() ? temporadasDisponiveis.First() : (int?)null);

            if (temporadaSelecionada.HasValue)
                jogosQuery = jogosQuery.Where(j => j.Temporada == temporadaSelecionada.Value);

            var jogos = await jogosQuery.ToListAsync();
            var jogoIds = jogos.Select(j => j.Id).ToHashSet();

            // Carregar dados relacionados filtrados pelos jogos
            var gols = await _context.Gols
                .AsNoTrackingWithIdentityResolution()
                .Include(g => g.Jogador).ThenInclude(j => j!.Time)
                .Include(g => g.Jogador).ThenInclude(j => j!.Selecao)
                .Where(g => jogoIds.Contains(g.JogoId))
                .ToListAsync();

            var estatisticasJogadores = await _context.EstatisticasJogador
                .AsNoTrackingWithIdentityResolution()
                .Include(e => e.Jogador).ThenInclude(j => j!.Time)
                .Include(e => e.Jogador).ThenInclude(j => j!.Selecao)
                // Jogo precisa vir junto: o bônus "não sofreu gol" (CriteriosNotaHelper)
                // lê e.Jogo (placar). Sem tracking não há fixup entre consultas, então
                // sem este Include a navegação fica null e o bônus sai errado.
                .Include(e => e.Jogo)
                // Exclui reservas não utilizados: a api-football cria uma linha de
                // estatística para todo mundo do elenco relacionado, mesmo quem não
                // entrou em campo (Minutos 0/null) — sem isso, o banco "jogaria" a
                // nota base (4.0) em cima de estatísticas zeradas de quem nem jogou.
                .Where(e => jogoIds.Contains(e.JogoId) && e.Minutos != null && e.Minutos > 0)
                .ToListAsync();

            var escalacoes = await _context.Escalacoes
                .AsNoTrackingWithIdentityResolution()
                .Include(e => e.Jogador).ThenInclude(j => j!.Time)
                .Include(e => e.Jogador).ThenInclude(j => j!.Selecao)
                .Where(e => jogoIds.Contains(e.JogoId) && e.JogadorId.HasValue && e.Titular
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null))
                .ToListAsync();

            var cartoes = await _context.Cartoes
                .AsNoTrackingWithIdentityResolution()
                .Include(c => c.Jogador).ThenInclude(j => j!.Time)
                .Include(c => c.Jogador).ThenInclude(j => j!.Selecao)
                .Where(c => jogoIds.Contains(c.JogoId))
                .ToListAsync();

            var assistencias = await _context.Assistencias
                .AsNoTrackingWithIdentityResolution()
                .Include(a => a.Jogador).ThenInclude(j => j!.Time)
                .Include(a => a.Jogador).ThenInclude(j => j!.Selecao)
                .Where(a => jogoIds.Contains(a.JogoId))
                .ToListAsync();

            var notas = await _context.Notas
                .AsNoTrackingWithIdentityResolution()
                .Include(n => n.Jogador).ThenInclude(j => j!.Time)
                .Include(n => n.Jogador).ThenInclude(j => j!.Selecao)
                .Where(n => jogoIds.Contains(n.JogoId)
                         && (usuarioId == null || n.UsuarioId == usuarioId))
                .ToListAsync();

            var topTierIdsRel = usuarioId != null
                ? await _context.CompeticoesTopTierUsuario.Where(t => t.UsuarioId == usuarioId).Select(t => t.CompeticaoId).ToHashSetAsync()
                : new HashSet<int>();
            var competicoes = (await _context.Competicoes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync())
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
                    .AsNoTracking()
                    .Where(t => idsTimesNasComps.Contains(t.Id))
                    .OrderBy(t => t.Nome).ToListAsync();
            }
            else
            {
                times = await _context.Times.AsNoTracking().OrderBy(t => t.Nome).ToListAsync();
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

            // Lado (casa/visitante) de cada jogador em cada jogo, tirado da escalação
            // da época — usar o time ATUAL do jogador inverteria V/D/E dos jogos do
            // clube antigo depois de uma transferência (o adversário viraria "seu time").
            var ladoPorJogadorJogo = escalacoes
                .Where(e => e.JogadorId.HasValue)
                .GroupBy(e => (e.JogadorId!.Value, e.JogoId))
                .ToDictionary(g => g.Key, g => g.First().IsTimeCasa);

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
                TemporadaFiltro = temporadaSelecionada,
                TemporadasDisponiveis = temporadasDisponiveis,
                TimeIdsFiltro = timeIdsFiltro,
                ExibirSelecao = exibirSelecao,
                IncluirNaoAnalisados = incluirNaoAnalisados,
                MinJogos = minJogosEstat,
                Competicoes = competicoes,
                Times = times,
                TotalJogos = jogos.Count,
                TotalGols = gols.Count(g => !g.Contra),
                TotalGolsContra = gols.Count(g => g.Contra),
                TotalCartaoAmarelo = cartoes.Count(c => c.Tipo == "Amarelo"),
                TotalCartaoVermelho = cartoes.Count(c => c.Tipo == "Vermelho"),

                // Jogadores
                RankingNotas = CalcularRankingMisto(notas, estatisticasJogadores, resultadoPorJogoTime, jogos, 20, usuarioId, ladoPorJogadorJogo, minPartidas: minJogosEstat),
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
                TimesExpectedGoals   = RankStatJogo(jogos, "expected_goals",    isPct: false, timesFiltro: timeIdsFiltro),
                TimesGolsEvitados    = RankStatJogo(jogos, "goals_prevented",   isPct: false, timesFiltro: timeIdsFiltro),

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

                RankImpedimentos        = RankEstatJogador(estatisticasJogadores, e => e.Offsides, minPartidas: minJogosEstat),
                RankFinalizacoesNoGol   = RankEstatJogador(estatisticasJogadores, e => e.FinalizacoesNoGol, minPartidas: minJogosEstat),
                RankPassesChave         = RankEstatJogador(estatisticasJogadores, e => e.PassesChave, minPartidas: minJogosEstat),
                RankDesarmes            = RankEstatJogador(estatisticasJogadores, e => e.Desarmes, minPartidas: minJogosEstat),
                RankBloqueios           = RankEstatJogador(estatisticasJogadores, e => e.Bloqueios, minPartidas: minJogosEstat),
                RankInterceptacoes      = RankEstatJogador(estatisticasJogadores, e => e.Interceptacoes, minPartidas: minJogosEstat),
                RankDrilesCertos        = RankEstatJogador(estatisticasJogadores, e => e.DriblesCertos, minPartidas: minJogosEstat),
                RankPenaltisDefendidos  = RankEstatJogador(estatisticasJogadores, e => e.PenaltiDefendido, minPartidas: minJogosEstat, ordenarPorTotal: true),
                RankVezesCapitao        = RankEstatJogador(estatisticasJogadores, e => e.Capitao ? 1 : 0, minPartidas: minJogosEstat, ordenarPorTotal: true),
            };

            // Rankings por posição (usa ranking completo, sem limite de 20)
            var rankingCompleto = CalcularRankingMisto(notas, estatisticasJogadores, resultadoPorJogoTime, jogos, int.MaxValue, usuarioId, ladoPorJogadorJogo, minPartidas: minJogosEstat);
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

            if (incluirEstatisticasCompeticoes)
                vm.EstatisticasCompeticoes = await CalcularEstatisticasCompeticoesAsync();

            // Match Up: só carrega (custa 2 consultas extras) quando o usuário filtrou
            // exatamente 2 times — nos demais casos a aba fica desabilitada na view.
            if (incluirMatchUp && timeIdsFiltro.Count == 2)
            {
                vm.MatchUpTime1 = await MatchUpHelper.MontarTimeAsync(_context, timeIdsFiltro[0], esquerda: true, usuarioId);
                vm.MatchUpTime2 = await MatchUpHelper.MontarTimeAsync(_context, timeIdsFiltro[1], esquerda: false, usuarioId);
            }

            vm.Selecao = await MontarSelecaoAsync(jogos, escalacoes, timeIdsFiltro, rankingCompleto);

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

                // Gols/cartões/assistências são contados por TODOS os jogos da competição,
                // não só os com placar definido: a reimportação de escalação/eventos
                // (ForcarReimportarEscalacaoAsync) grava gols de jogo ainda em andamento
                // sem preencher o placar (o placar só é gravado quando a API dá a partida
                // como encerrada), então um jogo pode ter gols registrados mesmo com
                // PlacarCasa/PlacarVisitante nulos — restringir ao placar sub-contava o total.
                var todosJogoIdsComp = await _context.Jogos
                    .Where(j => j.CompeticaoId == comp.Id)
                    .Select(j => j.Id)
                    .ToListAsync();
                var jogoIdsTodos = todosJogoIdsComp.ToHashSet();

                var gols = await _context.Gols
                    .Include(g => g.Jogador)
                        .ThenInclude(j => j.Time)
                    .Where(g => jogoIdsTodos.Contains(g.JogoId))
                    .ToListAsync();

                var cartoes = await _context.Cartoes
                    .Where(c => jogoIdsTodos.Contains(c.JogoId))
                    .ToListAsync();

                var assistencias = await _context.Assistencias
                    .Include(a => a.Jogador)
                        .ThenInclude(j => j.Time)
                    .Where(a => jogoIdsTodos.Contains(a.JogoId))
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

                var timeVencedor = timeMaisVitorias.Key > 0
                    ? jogos.SelectMany(j => new[] { j.TimeCasa, j.TimeVisitante })
                           .FirstOrDefault(t => t?.Id == timeMaisVitorias.Key)
                    : null;
                var timeNome = timeVencedor?.Nome;

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
                    ArtilheiroId         = artilheiro?.Key.Id ?? 0,
                    ArtilheiroEscudoUrl  = artilheiro?.Key.Time?.EscudoUrl,
                    ArtilheiroGols       = artilheiro?.Count() ?? 0,
                    AssistenteNome       = melhorAssistente?.Key.Nome,
                    AssistenteId         = melhorAssistente?.Key.Id ?? 0,
                    AssistenteEscudoUrl  = melhorAssistente?.Key.Time?.EscudoUrl,
                    AssistenciasTotal    = melhorAssistente?.Count() ?? 0,
                    TimeMaisVitorias     = timeNome,
                    TimeMaisVitoriasEscudoUrl = timeVencedor?.EscudoUrl,
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

        // ── Seleção (melhor XI) ──────────────────────────────────────────────────
        // Formação mais usada pelo filtro atual (por time filtrado, ou por qualquer
        // dos dois lados quando não há filtro de time) + melhor jogador do ranking
        // de notas para cada slot da formação.
        private async Task<SelecaoViewModel?> MontarSelecaoAsync(
            List<Jogo> jogos, List<Escalacao> escalacoes, List<int> timeIdsFiltro, List<RankingNotaItem> rankingCompleto)
        {
            var contagemFormacoes = new Dictionary<int, int>();
            void Conta(int? formacaoId)
            {
                if (!formacaoId.HasValue) return;
                contagemFormacoes[formacaoId.Value] = contagemFormacoes.GetValueOrDefault(formacaoId.Value) + 1;
            }

            foreach (var j in jogos)
            {
                if (timeIdsFiltro.Any())
                {
                    if (timeIdsFiltro.Contains(j.TimeCasaId)) Conta(j.FormacaoCasaId);
                    if (timeIdsFiltro.Contains(j.TimeVisitanteId)) Conta(j.FormacaoVisitanteId);
                }
                else
                {
                    Conta(j.FormacaoCasaId);
                    Conta(j.FormacaoVisitanteId);
                }
            }

            if (!contagemFormacoes.Any()) return null;
            int formacaoId = contagemFormacoes.OrderByDescending(kv => kv.Value).First().Key;

            var formacao = await _context.Formacoes
                .AsNoTracking()
                .Include(f => f.Posicoes)
                .FirstOrDefaultAsync(f => f.Id == formacaoId);
            if (formacao == null || !formacao.Posicoes.Any()) return null;

            // Posição granular jogada por cada jogador DENTRO do filtro atual (não a
            // agregada de toda a carreira em Jogador.Posicao) — um jogador pode ter
            // sido zagueiro em outra competição/temporada e lateral nesta; casar pelo
            // campo global colocaria gente na posição errada da Seleção deste filtro.
            var slotsPorFormacao = (await _context.PosicoesFormacao.AsNoTracking().ToListAsync())
                .GroupBy(p => p.FormacaoId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var jogoPorId = jogos.ToDictionary(j => j.Id);

            // Só considera a fase INICIAL de cada (jogador, jogo): quem só aparece na
            // FINAL entrou como substituto e herdou o slot de quem saiu (visual da
            // troca), não a posição real dele — mesmo critério do
            // PosicaoJogadorHelper.RecalcularAsync.
            var escalacoesTitulares = escalacoes
                .Where(e => e.JogadorId.HasValue)
                .GroupBy(e => (e.JogadorId!.Value, e.JogoId))
                .Where(g => g.Any(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                .Select(g => g.First(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null));

            var posicoesPorJogador = new Dictionary<int, List<string>>();
            foreach (var e in escalacoesTitulares)
            {
                if (!jogoPorId.TryGetValue(e.JogoId, out var jogo)) continue;
                var formacaoIdLado = e.IsTimeCasa ? jogo.FormacaoCasaId : jogo.FormacaoVisitanteId;
                if (formacaoIdLado == null || !slotsPorFormacao.TryGetValue(formacaoIdLado.Value, out var slotsLado)) continue;

                var nome = PosicaoJogadorHelper.PosicaoGranular(slotsLado, e.PosicaoX, e.PosicaoY);
                if (string.IsNullOrWhiteSpace(nome)) continue;

                if (!posicoesPorJogador.TryGetValue(e.JogadorId!.Value, out var lista))
                    posicoesPorJogador[e.JogadorId.Value] = lista = new List<string>();
                lista.Add(nome);
            }
            var posicoesJogadasSet = posicoesPorJogador.ToDictionary(kv => kv.Key, kv => kv.Value.ToHashSet());

            // Só cai pro Jogador.Posicao (agregado global) quando não há nenhuma
            // escalação com coordenada dentro do filtro atual (ex.: jogador cuja nota
            // veio de EstatisticaJogador sem escalação salva).
            bool JogouNaPosicao(Jogador jogador, string nomeSlotNormalizado) =>
                posicoesJogadasSet.TryGetValue(jogador.Id, out var posSet)
                    ? posSet.Contains(nomeSlotNormalizado)
                    : !string.IsNullOrEmpty(jogador.Posicao)
                        && jogador.Posicao.Split('/').Any(p => PosicaoJogadorHelper.NormalizarNomePosicao(p.Trim()) == nomeSlotNormalizado);

            string GrupoDoJogador(Jogador jogador) =>
                posicoesPorJogador.TryGetValue(jogador.Id, out var lista) && lista.Any()
                    ? NormalizarPosicao(lista.GroupBy(n => n).OrderByDescending(g => g.Count()).First().Key)
                    : NormalizarPosicao(jogador.Posicao ?? "");

            var disponiveis = rankingCompleto.OrderByDescending(r => r.NotaFinal).ToList();
            var usados = new HashSet<int>();
            var slots = formacao.Posicoes.OrderBy(p => p.Ordem).ToList();
            var resultado = slots.Select(s => new SelecaoSlotViewModel
            {
                NomePosicao = s.NomePosicao,
                PosicaoX = s.PosicaoX,
                PosicaoY = s.PosicaoY
            }).ToList();

            // 1ª passada: casamento exato pela posição granular (ex.: slot "Lateral
            // Direito" com jogador que atuou como Lateral Direito neste filtro).
            for (int i = 0; i < slots.Count; i++)
            {
                var nomeSlot = PosicaoJogadorHelper.NormalizarNomePosicao(slots[i].NomePosicao);
                var melhor = disponiveis.FirstOrDefault(r => !usados.Contains(r.Jogador.Id) && JogouNaPosicao(r.Jogador, nomeSlot));
                if (melhor != null)
                {
                    resultado[i].Jogador = melhor;
                    usados.Add(melhor.Jogador.Id);
                }
            }

            // 2ª passada: slots ainda vazios casam por grupo (Goleiro/Defensor/
            // Meio-campo/Atacante) — evita deixar posição sem jogador só porque o
            // texto granular não bateu.
            for (int i = 0; i < slots.Count; i++)
            {
                if (resultado[i].Jogador != null) continue;
                var grupoSlot = NormalizarPosicao(slots[i].NomePosicao);
                var melhor = disponiveis.FirstOrDefault(r => !usados.Contains(r.Jogador.Id) && GrupoDoJogador(r.Jogador) == grupoSlot);
                if (melhor != null)
                {
                    resultado[i].Jogador = melhor;
                    usados.Add(melhor.Jogador.Id);
                }
            }

            return new SelecaoViewModel { Formacao = formacao, Slots = resultado };
        }

        private static List<RankingNotaItem> FiltrarRankingPorPosicao(List<RankingNotaItem> ranking, string grupo)
        {
            bool Match(string? posicao) => grupo switch
            {
                "Goleiro"  => posicao != null && posicao.Contains("Goleiro", StringComparison.OrdinalIgnoreCase),
                "Defensor" => posicao != null && (posicao.Contains("Defensor", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Zagueiro", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Lateral", StringComparison.OrdinalIgnoreCase)),
                // Ala (esquerdo/direito) joga mais avançado que a defesa — entra
                // como Meia, não Defensor.
                "Meia"     => posicao != null && (posicao.Contains("Meia", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Volante", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Ala", StringComparison.OrdinalIgnoreCase)),
                "Atacante" => posicao != null && (posicao.Contains("Atacante", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Ponta", StringComparison.OrdinalIgnoreCase)
                                               || posicao.Contains("Centroavante", StringComparison.OrdinalIgnoreCase)),
                _ => false
            };

            // Jogador.Posicao pode ser composto ("Centroavante/Ala Esquerda" — as duas
            // posições mais frequentes dele, na ordem). Só considera a PRIMEIRA (a
            // dominante): senão um atacante que jogou algumas vezes de ala aparece
            // também em "Melhores Meias" só por casar com a posição secundária.
            string? Principal(string? posicao) => posicao?.Split('/', 2)[0];

            return ranking
                .Where(r => Match(Principal(r.Jogador.Posicao)))
                .Take(10)
                .ToList();
        }

        // ── Ranking misto: por jogo usa nota manual se existir, senão calcula das estatísticas ──
        private List<RankingNotaItem> CalcularRankingMisto(
            List<Nota> notas,
            List<EstatisticaJogador> estatisticas,
            Dictionary<(int jogoId, int timeId), ResultadoJogo> resultadoPorJogoTime,
            List<Jogo> jogos,
            int limite = 20,
            string? usuarioId = null,
            Dictionary<(int jogadorId, int jogoId), bool>? ladoPorJogadorJogo = null,
            int minPartidas = 1)
        {
            var criteriosBanco = CriteriosNotaHelper.MergeCriterios(
                _context.CriteriosNota.Where(c => c.UsuarioId == null).ToList(),
                _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToList());
            var compPorJogo = jogos.ToDictionary(j => j.Id, j => j.CompeticaoId);
            var jogoPorId = jogos.ToDictionary(j => j.Id);

            int JogosDoTimeNoCampeonato(int compId, int timeId) =>
                jogos.Count(j => j.CompeticaoId == compId
                              && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId));

            // índices para lookup rápido. Guarda o valor das ações (base + ações) e a nota
            // manual (override) quando informada.
            var notasPorJogadorJogo = notas
                .Where(n => n.Jogador != null)
                .GroupBy(n => (n.JogadorId, n.JogoId))
                .ToDictionary(
                    g => g.Key,
                    g => (
                        valor: g.Average(n => n.Valor),
                        notaManual: g.Any(n => n.NotaManual.HasValue)
                            ? (double?)g.Where(n => n.NotaManual.HasValue).Average(n => n.NotaManual!.Value)
                            : null));

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
                if (partidas == 0 || partidas < minPartidas) continue;

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
                    double notaFinalJogo;
                    if (notasPorJogadorJogo.TryGetValue((jogador.Id, jogoId), out var notaInfo))
                    {
                        if (notaInfo.notaManual.HasValue)
                        {
                            // Override: usa a nota manual como valor final absoluto (0–10), sem base.
                            notaFinalJogo = Math.Round(Math.Max(0, Math.Min(10, notaInfo.notaManual.Value)), 2);
                        }
                        else
                        {
                            notaFinalJogo = Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima,
                                Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + notaInfo.valor)), 2);
                        }
                    }
                    else if (estatsPorJogadorJogo.TryGetValue((jogador.Id, jogoId), out var estats))
                    {
                        var valorJogo = estats.Sum(e => Math.Round(CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco), 2));
                        notaFinalJogo = Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima,
                            Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + valorJogo)), 2);
                    }
                    else continue;

                    // Nota final do jogo arredondada a 2 casas — idêntico à página de Estatísticas
                    somaNotasFinais += notaFinalJogo;
                    jogosComputados++;

                    ResultadoJogo res = ResultadoJogo.Empate;
                    bool achouResultado;
                    // Prioriza o lado registrado na escalação daquele jogo (correto mesmo
                    // após transferência); só cai pro time atual sem escalação salva.
                    if (ladoPorJogadorJogo != null && ladoPorJogadorJogo.TryGetValue((jogador.Id, jogoId), out var ladoCasa)
                        && jogoPorId.TryGetValue(jogoId, out var jogoDoLado))
                        achouResultado = resultadoPorJogoTime.TryGetValue(
                            (jogoId, ladoCasa ? jogoDoLado.TimeCasaId : jogoDoLado.TimeVisitanteId), out res);
                    else
                        achouResultado = (jogador.TimeId > 0 && resultadoPorJogoTime.TryGetValue((jogoId, jogador.TimeId), out res))
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

                        // InvariantCulture: os valores vêm da api-football sempre com "."
                        // decimal (ex.: "0.65"). Sem isso, em servidor com cultura pt-BR o
                        // TryParse trata o "." como separador de milhar e gera valor errado
                        // (ex.: "0.65" virando 65) — só não dava pra notar antes porque as
                        // chaves usadas até então (finalizações, escanteios, posse) nunca
                        // tinham casas decimais.
                        double v;
                        if (isPct)
                        {
                            if (!double.TryParse(raw.Replace("%", "").Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
                        }
                        else
                        {
                            if (!double.TryParse(raw, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
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
            // Ala (esquerdo/direito) joga mais avançado que a defesa — conta como meio-campo.
            if (s.Contains("mei") || s.Contains("vol") || s.Contains("ala") || s == "mc") return "Meio-campo";
            if (s.Contains("ata") || s.Contains("ponta") || s.Contains("centro") || s == "at") return "Atacante";
            return "Outro";
        }
    }
}
