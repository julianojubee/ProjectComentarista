// ControleFutebolWeb/Controllers/RelatoriosController.cs
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ControleFutebolWeb.Controllers
{
    // A montagem dos rankings/estatísticas vive em Services/RelatoriosService.cs,
    // compartilhada com a API mobile (RelatoriosApiController).
    public class RelatoriosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RelatoriosService _relatorios;
        private readonly PerfilJogadorService _perfilJogador;

        public RelatoriosController(FutebolContext context, UserManager<ApplicationUser> userManager, RelatoriosService relatorios, PerfilJogadorService perfilJogador)
        {
            _context = context;
            _userManager = userManager;
            _relatorios = relatorios;
            _perfilJogador = perfilJogador;
        }

        // GET: /Relatorios
        public async Task<IActionResult> Index(int[]? competicaoIds, int[]? timeIds, int? temporada,
            bool incluirNaoAnalisados = false, int? minJogos = null, string? baseEstat = null, string? mando = null)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            // "total" | "jogo" | "90" — valor inválido cai no padrão (por jogo).
            var baseCalculo = baseEstat switch
            {
                "total" => BaseEstatistica.Total,
                "90"    => BaseEstatistica.Por90,
                _       => BaseEstatistica.PorJogo
            };
            var mandoSel = mando switch
            {
                "casa" => MandoJogador.Casa,
                "fora" => MandoJogador.Fora,
                _      => MandoJogador.Todos
            };
            var vm = await _relatorios.MontarAsync(competicaoIds, timeIds, temporada, incluirNaoAnalisados,
                minJogos ?? 1, usuarioId, baseEstatistica: baseCalculo, mando: mandoSel);
            return View(vm);
        }

        // GET: /Relatorios/Scout
        [HttpGet]
        public async Task<IActionResult> Scout(
            string[]? posicoes, int? idadeMin, int? idadeMax,
            int? alturaMin, int? alturaMax, int? pesoMin, int? pesoMax, int[]? timeIds,
            int[]? competicaoIds, int[]? nacionalidadeIds, int? temporada,
            int? minJogos, int? minGols, int? minAssistencias,
            int? minPassesChave, int? minDesarmes, int? minBloqueios, int? minInterceptacoes, int? minDuelosVencidos, int? minFinalizacoesNoGol, int? minDrilesCertos,
            double? mediaPassesChave, double? mediaDesarmes, double? mediaBloqueios, double? mediaInterceptacoes, double? mediaDuelosVencidos, double? mediaFinalizacoesNoGol, double? mediaDrilesCertos,
            double? minNota,
            int? maxCartaoAmarelo, int? maxCartaoVermelho,
            bool pesquisou = false)
        {
            var usuarioId = _userManager.GetUserId(User)!;

            // Só a posição primária (primeiro trecho antes do "/") aparece no filtro —
            // "Ala Direito/Ala Esquerdo" vira só "Ala Direito" na lista, mas o jogador
            // continua aparecendo ao filtrar por qualquer posição em que já atuou.
            var posicoesList = (await _context.Jogadores
                    .Where(j => j.Posicao != null && j.Posicao != "")
                    .Select(j => j.Posicao!)
                    .Distinct()
                    .ToListAsync())
                .Select(p => p.Split('/')[0])
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            var topTierIds = await _context.CompeticoesTopTierUsuario
                .Where(t => t.UsuarioId == usuarioId)
                .Select(t => t.CompeticaoId)
                .ToHashSetAsync();

            var posicoesSelec   = (posicoes  ?? Array.Empty<string>()).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            var timeIdsSelec    = (timeIds   ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            var compIdsSelec    = (competicaoIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            var nacIdsSelec     = (nacionalidadeIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToList();

            var vm = new ScoutViewModel
            {
                Filtro = new ScoutFiltro
                {
                    Posicoes = posicoesSelec, IdadeMin = idadeMin, IdadeMax = idadeMax,
                    AlturaMin = alturaMin, AlturaMax = alturaMax, PesoMin = pesoMin, PesoMax = pesoMax,
                    TimeIds = timeIdsSelec, CompeticaoIds = compIdsSelec, NacionalidadeIds = nacIdsSelec, Temporada = temporada,
                    MinJogos = minJogos, MinGols = minGols, MinAssistencias = minAssistencias,
                    MinPassesChave = minPassesChave, MinDesarmes = minDesarmes,
                    MinBloqueios = minBloqueios, MinInterceptacoes = minInterceptacoes, MinDuelosVencidos = minDuelosVencidos,
                    MinFinalizacoesNoGol = minFinalizacoesNoGol, MinDrilesCertos = minDrilesCertos,
                    MediaPassesChave = mediaPassesChave, MediaDesarmes = mediaDesarmes,
                    MediaBloqueios = mediaBloqueios, MediaInterceptacoes = mediaInterceptacoes, MediaDuelosVencidos = mediaDuelosVencidos,
                    MediaFinalizacoesNoGol = mediaFinalizacoesNoGol, MediaDrilesCertos = mediaDrilesCertos,
                    MinNota = minNota, MaxCartaoAmarelo = maxCartaoAmarelo, MaxCartaoVermelho = maxCartaoVermelho,
                },
                Competicoes = (await _context.Competicoes.OrderBy(c => c.Nome).ToListAsync())
                    .OrderByDescending(c => topTierIds.Contains(c.Id))
                    .ThenBy(c => c.Nome)
                    .ToList(),
                Times = await _context.Times.OrderBy(t => t.Nome).ToListAsync(),
                Nacionalidades = await _context.Nacionalidades.OrderBy(n => n.Nome).ToListAsync(),
                Posicoes = posicoesList,
                Temporadas = await _context.Jogos.Select(j => j.Temporada).Distinct().OrderByDescending(t => t).ToListAsync(),
                Pesquisou = pesquisou,
            };

            if (!pesquisou) return View(vm);

            // Jogos filtrados por competição/temporada
            var jogosQuery = _context.Jogos.Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue);
            if (compIdsSelec.Any()) jogosQuery = jogosQuery.Where(j => compIdsSelec.Contains(j.CompeticaoId));
            if (temporada.HasValue) jogosQuery = jogosQuery.Where(j => j.Temporada == temporada.Value);
            var jogoIds = await jogosQuery.Select(j => j.Id).ToHashSetAsync();

            // Jogadores base (filtros de cadastro)
            var jogadoresQuery = _context.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .AsQueryable();
            if (posicoesSelec.Any())  jogadoresQuery = jogadoresQuery.Where(j => j.Posicao != null && posicoesSelec.Any(p => j.Posicao == p || j.Posicao!.StartsWith(p + "/") || j.Posicao!.EndsWith("/" + p)));
            if (timeIdsSelec.Any())   jogadoresQuery = jogadoresQuery.Where(j => timeIdsSelec.Contains(j.TimeId));
            if (nacIdsSelec.Any())    jogadoresQuery = jogadoresQuery.Where(j => j.NacionalidadeId != null && nacIdsSelec.Contains(j.NacionalidadeId.Value));
            // Altura/peso: quem não tem o dado cadastrado fica fora quando o filtro é usado
            if (alturaMin.HasValue)   jogadoresQuery = jogadoresQuery.Where(j => j.Altura != null && j.Altura >= alturaMin.Value);
            if (alturaMax.HasValue)   jogadoresQuery = jogadoresQuery.Where(j => j.Altura != null && j.Altura <= alturaMax.Value);
            if (pesoMin.HasValue)     jogadoresQuery = jogadoresQuery.Where(j => j.Peso != null && j.Peso >= pesoMin.Value);
            if (pesoMax.HasValue)     jogadoresQuery = jogadoresQuery.Where(j => j.Peso != null && j.Peso <= pesoMax.Value);
            var todosJogadores = await jogadoresQuery.ToListAsync();

            // Filtros de idade (calculados em memória)
            if (idadeMin.HasValue) todosJogadores = todosJogadores.Where(j => j.Idade >= idadeMin.Value).ToList();
            if (idadeMax.HasValue) todosJogadores = todosJogadores.Where(j => j.Idade > 0 && j.Idade <= idadeMax.Value).ToList();

            var jogadorIds = todosJogadores.Select(j => j.Id).ToHashSet();

            // Carregar dados de performance
            var gols = await _context.Gols
                .AsNoTracking()
                .Where(g => jogoIds.Contains(g.JogoId) && !g.Contra
                         && jogadorIds.Contains(g.JogadorId))
                .ToListAsync();

            var assistencias = await _context.Assistencias
                .AsNoTracking()
                .Where(a => jogoIds.Contains(a.JogoId)
                         && jogadorIds.Contains(a.JogadorId))
                .ToListAsync();

            var cartoes = await _context.Cartoes
                .AsNoTracking()
                .Where(c => jogoIds.Contains(c.JogoId)
                         && jogadorIds.Contains(c.JogadorId))
                .ToListAsync();

            var escalacoes = await _context.Escalacoes
                .AsNoTracking()
                .Where(e => jogoIds.Contains(e.JogoId) && e.JogadorId.HasValue
                         && jogadorIds.Contains(e.JogadorId!.Value)
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null))
                .ToListAsync();

            var estatisticas = await _context.EstatisticasJogador
                .AsNoTracking()
                // Jogo e Jogador: usados pelo bônus "não sofreu gol"
                // (CriteriosNotaHelper). Sem tracking não há fixup entre consultas.
                .Include(e => e.Jogo)
                .Include(e => e.Jogador)
                // Exclui reservas não utilizados (Minutos 0/null) — ver comentário
                // equivalente em MontarViewModel.
                .Where(e => jogoIds.Contains(e.JogoId) && jogadorIds.Contains(e.JogadorId)
                         && e.Minutos != null && e.Minutos > 0)
                .ToListAsync();

            var notas = await _context.Notas
                .AsNoTracking()
                .Include(n => n.Detalhes)
                .Where(n => jogoIds.Contains(n.JogoId) && jogadorIds.Contains(n.JogadorId)
                         && n.UsuarioId == usuarioId)
                .ToListAsync();

            var criteriosBanco = CriteriosNotaHelper.MergeCriterios(
                await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync(),
                await _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToListAsync());

            // Lado (casa/visitante) por jogador/jogo, da escalação da época — o bônus
            // "não sofreu gol" precisa dele para não errar os jogos pré-transferência.
            var lados = await LadoJogadorHelper.CarregarAsync(_context, jogoIds, usuarioId);

            // Minutos e goleiro decisivo por (jogador, jogo) — pisos de participação
            // curta e bônus de 100% de defesas.
            var contextos = await ContextoNotaHelper.CarregarAsync(_context, jogoIds, usuarioId, lados);

            // Motor da nota automática escolhido em /CriteriosNota. Só vale para os
            // jogos sem avaliação manual — os avaliados usam a nota do próprio usuário.
            var calculadora = await NotaAutomaticaHelper.CarregarAsync(
                _context, jogoIds, usuarioId, criteriosBanco, lados, contextos);

            // Dicionários de agregação
            var golsPorJogador      = gols.GroupBy(g => g.JogadorId).ToDictionary(g => g.Key, g => g.Count());
            var assisPorJogador     = assistencias.GroupBy(a => a.JogadorId).ToDictionary(g => g.Key, g => g.Count());
            var amareloPorJogador   = cartoes.Where(c => c.Tipo == "Amarelo").GroupBy(c => c.JogadorId).ToDictionary(g => g.Key, g => g.Count());
            var vermelhoPorJogador  = cartoes.Where(c => c.Tipo == "Vermelho").GroupBy(c => c.JogadorId).ToDictionary(g => g.Key, g => g.Count());
            var jogosPorJogador     = escalacoes.GroupBy(e => e.JogadorId!.Value).ToDictionary(g => g.Key, g => g.Select(e => e.JogoId).Distinct().Count());
            var estatsPorJogador    = estatisticas.GroupBy(e => e.JogadorId).ToDictionary(g => g.Key, g => g.ToList());
            var notasPorJogador     = notas.GroupBy(n => n.JogadorId).ToDictionary(g => g.Key, g => g.ToList());

            var resultados = new List<ScoutResultItem>();

            foreach (var jogador in todosJogadores)
            {
                var jId = jogador.Id;

                int jogosCount = jogosPorJogador.GetValueOrDefault(jId, 0);
                if (jogosCount == 0 && estatsPorJogador.TryGetValue(jId, out var esTemp))
                    jogosCount = esTemp.Select(e => e.JogoId).Distinct().Count();
                if (jogosCount == 0 && notasPorJogador.TryGetValue(jId, out var nTemp))
                    jogosCount = nTemp.Select(n => n.JogoId).Distinct().Count();

                int gol      = golsPorJogador.GetValueOrDefault(jId, 0);
                int ass      = assisPorJogador.GetValueOrDefault(jId, 0);
                int amarelo  = amareloPorJogador.GetValueOrDefault(jId, 0);
                int vermelho = vermelhoPorJogador.GetValueOrDefault(jId, 0);

                int passesChave = 0, desarmes = 0, bloqueios = 0, interceptacoes = 0, duelosVencidos = 0, finNoGol = 0, driles = 0, minutos = 0;
                if (estatsPorJogador.TryGetValue(jId, out var estats))
                {
                    minutos        = estats.Sum(e => e.Minutos ?? 0);
                    passesChave    = estats.Sum(e => e.PassesChave);
                    desarmes       = estats.Sum(e => e.Desarmes);
                    bloqueios      = estats.Sum(e => e.Bloqueios);
                    interceptacoes = estats.Sum(e => e.Interceptacoes);
                    duelosVencidos = estats.Sum(e => e.DuelosVencidos);
                    finNoGol       = estats.Sum(e => e.FinalizacoesNoGol);
                    driles         = estats.Sum(e => e.DriblesCertos);
                }

                // Somas que alimentam o seletor de colunas da tabela do Scout.
                var estatsDoJogador = estatsPorJogador.GetValueOrDefault(jId);
                int Soma(Func<EstatisticaJogador, int> f) => estatsDoJogador == null ? 0 : estatsDoJogador.Sum(f);

                // Nota média (mesma lógica do ranking misto)
                double? notaMedia = null;
                var jogoIdSet = new HashSet<int>();
                if (notasPorJogador.TryGetValue(jId, out var nLst))  foreach (var n in nLst) jogoIdSet.Add(n.JogoId);
                if (estatsPorJogador.TryGetValue(jId, out var eLst)) foreach (var e in eLst) jogoIdSet.Add(e.JogoId);

                if (jogoIdSet.Count > 0)
                {
                    var notasDict = (notasPorJogador.GetValueOrDefault(jId) ?? new())
                        .GroupBy(n => n.JogoId)
                        .ToDictionary(g => g.Key, g => (
                            valor: g.Average(n => n.Valor),
                            manual: g.Any(n => n.NotaManual.HasValue)
                                ? (double?)g.Where(n => n.NotaManual.HasValue).Average(n => n.NotaManual!.Value)
                                : null,
                            detalhes: g.SelectMany(n => n.Detalhes).ToList()));
                    var estatsDict = (estatsPorJogador.GetValueOrDefault(jId) ?? new())
                        .GroupBy(e => e.JogoId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    double soma = 0; int comp = 0;
                    foreach (var jogoId in jogoIdSet)
                    {
                        double nj;
                        var ctxJogo = ContextoNotaHelper.De(contextos, jId, jogoId);
                        if (notasDict.TryGetValue(jogoId, out var ni))
                            nj = ni.manual.HasValue
                                ? Math.Round(Math.Max(0, Math.Min(10, ni.manual.Value)), 2)
                                : CriteriosNotaHelper.NotaFinal(ni.valor, criteriosBanco,
                                    ctxJogo with { Acoes = CriteriosNotaHelper.ContarAcoes(ni.detalhes) });
                        else if (estatsDict.TryGetValue(jogoId, out var es))
                            nj = calculadora.De(es, jId, jogoId).Nota;
                        else continue;
                        soma += nj; comp++;
                    }
                    if (comp > 0) notaMedia = Math.Round(soma / comp, 2);
                }

                // Filtros de estatística
                if (minJogos.HasValue            && jogosCount < minJogos.Value) continue;
                if (minGols.HasValue             && gol < minGols.Value) continue;
                if (minAssistencias.HasValue     && ass < minAssistencias.Value) continue;
                if (maxCartaoAmarelo.HasValue    && amarelo > maxCartaoAmarelo.Value) continue;
                if (maxCartaoVermelho.HasValue   && vermelho > maxCartaoVermelho.Value) continue;
                if (minPassesChave.HasValue       && passesChave    < minPassesChave.Value) continue;
                if (minDesarmes.HasValue          && desarmes       < minDesarmes.Value) continue;
                if (minBloqueios.HasValue         && bloqueios      < minBloqueios.Value) continue;
                if (minInterceptacoes.HasValue    && interceptacoes < minInterceptacoes.Value) continue;
                if (minDuelosVencidos.HasValue    && duelosVencidos < minDuelosVencidos.Value) continue;
                if (minFinalizacoesNoGol.HasValue && finNoGol       < minFinalizacoesNoGol.Value) continue;
                if (minDrilesCertos.HasValue      && driles         < minDrilesCertos.Value) continue;

                // Médias por jogo: total / jogos disputados (mesmo JG exibido na tabela).
                // Sem jogos contabilizados não há média — o jogador sai do resultado.
                bool MediaAbaixo(double? minimo, int total) =>
                    minimo.HasValue && (jogosCount == 0 || (double)total / jogosCount < minimo.Value);
                if (MediaAbaixo(mediaPassesChave, passesChave)) continue;
                if (MediaAbaixo(mediaDesarmes, desarmes)) continue;
                if (MediaAbaixo(mediaBloqueios, bloqueios)) continue;
                if (MediaAbaixo(mediaInterceptacoes, interceptacoes)) continue;
                if (MediaAbaixo(mediaDuelosVencidos, duelosVencidos)) continue;
                if (MediaAbaixo(mediaFinalizacoesNoGol, finNoGol)) continue;
                if (MediaAbaixo(mediaDrilesCertos, driles)) continue;

                if (minNota.HasValue             && (!notaMedia.HasValue || notaMedia.Value < minNota.Value)) continue;

                // Jogadores sem nenhum dado nos jogos filtrados são omitidos
                if (jogosCount == 0 && gol == 0 && ass == 0) continue;

                resultados.Add(new ScoutResultItem
                {
                    Jogador          = jogador,
                    Jogos            = jogosCount,
                    Minutos          = minutos,
                    Gols             = gol,
                    Assistencias     = ass,
                    CartaoAmarelo    = amarelo,
                    CartaoVermelho   = vermelho,
                    NotaMedia        = notaMedia,
                    PassesChave       = passesChave,
                    Desarmes          = desarmes,
                    Bloqueios         = bloqueios,
                    Interceptacoes    = interceptacoes,
                    DuelosVencidos    = duelosVencidos,
                    FinalizacoesNoGol = finNoGol,
                    DrilesCertos      = driles,

                    FinalizacoesTotal = Soma(e => e.FinalizacoesTotal),
                    PassesTotal       = Soma(e => e.PassesTotal),
                    PassesCertos      = Soma(e => e.PassesCertos),
                    DriblesTentados   = Soma(e => e.DriblesTentados),
                    DriblesSofridos   = Soma(e => e.DriblesSofridos),
                    DuelosTotal       = Soma(e => e.DuelosTotal),
                    FaltasCometidas   = Soma(e => e.FaltasCometidas),
                    FaltasSofridas    = Soma(e => e.FaltasSofridas),
                    Impedimentos      = Soma(e => e.Offsides),
                    Defesas           = Soma(e => e.Defesas),
                    GolsSofridos      = Soma(e => e.GolsSofridos),
                    PenaltiSofrido    = Soma(e => e.PenaltiSofrido),
                    PenaltiCometido   = Soma(e => e.PenaltiCometido),
                    PenaltiDefendido  = Soma(e => e.PenaltiDefendido),
                    PenaltiPerdido    = Soma(e => e.PenaltiPerdido),
                    PenaltiConvertido = Soma(e => e.PenaltiConvertido),
                    VezesReserva      = Soma(e => e.EntrouDoBanco ? 1 : 0),
                });
            }

            vm.Resultados = resultados
                .OrderByDescending(r => r.NotaMedia ?? 0)
                .ThenByDescending(r => r.Jogos)
                .ToList();

            return View(vm);
        }

        // ─────────────────────────────────────────────────────────────────
        // Aba "Comparar" do Scout: até 3 jogadores lado a lado com radar,
        // percentil na liga, divisão de métricas prioritárias, grupos de
        // métricas detalhadas e (com exatamente 2) confrontos diretos.
        // Reaproveita PerfilJogadorService, extraído da mesma lógica usada
        // pela comparação de /Jogadores/Estatisticas.
        // ─────────────────────────────────────────────────────────────────

        // GET: /Relatorios/ScoutCompararDados?ids=1,2,3&temporada=2026
        [HttpGet]
        public async Task<IActionResult> ScoutCompararDados(int[] ids, int? temporada = null)
        {
            ids = (ids ?? Array.Empty<int>()).Distinct().Take(3).ToArray();
            if (ids.Length < 2)
                return Json(new { error = "Escolha ao menos 2 jogadores para comparar." });

            var uid = _userManager.GetUserId(User);
            var criterios = CriteriosNotaHelper.MergeCriterios(
                await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync(),
                await _context.CriteriosNota.Where(c => c.UsuarioId == uid).ToListAsync());

            var perfis = new List<PerfilJogador>();
            foreach (var id in ids)
            {
                var p = await _perfilJogador.MontarAsync(id, uid, criterios, temporada);
                if (p == null) return Json(new { error = "Jogador não encontrado." });
                perfis.Add(p);
            }

            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            string F(double v, string fmt = "0.##") => v.ToString(fmt, ptBr);

            var cores = new[] { "#7c3aed", "#0ea5e9", "#f59e0b" };

            // ── Cabeçalho de cada jogador ────────────────────────────────
            var jogadoresJson = perfis.Select((p, i) => new
            {
                id = p.J.Id,
                nome = p.J.NomeExibicao,
                fotoUrl = p.J.FotoUrl,
                posicao = p.J.Posicao,
                idade = p.J.Idade,
                clube = p.J.Time?.Nome,
                escudoUrl = p.J.Time?.EscudoUrl,
                pais = p.J.Nacionalidade?.Nome,
                jogos = p.JogosTotal,
                gols = p.Gols,
                assistencias = p.Assistencias,
                notaFmt = p.NotaMedia.HasValue ? F(p.NotaMedia.Value, "0.00") : "—",
                cor = cores[i],
            }).ToList();

            // ── Eixos (radar/percentil/divisão): defensivos ou ofensivos,
            // conforme as funções que o grupo selecionado cobre ──────────
            var rolesGrupo = PerfilJogadorService.OrdemRoles.Where(r => perfis.Any(p => p.Roles.Contains(r))).ToList();
            bool defensivo = rolesGrupo.Count > 0 && rolesGrupo.All(r => r is "GOL" or "ZAG" or "LAT" or "VOL");

            var eixos = defensivo
                ? new (string Label, Func<PerfilJogador, double> Get)[]
                  {
                      ("Desarmes /jogo", p => p.PJ(p.Desarmes)),
                      ("Interceptações /jogo", p => p.PJ(p.Interceptacoes)),
                      ("Bloqueios /jogo", p => p.PJ(p.Bloqueios)),
                      ("Duelos ganhos /jogo", p => p.PJ(p.DuelosVencidos)),
                      ("% duelos ganhos", p => p.Pct(p.DuelosVencidos, p.DuelosTotal)),
                      ("Passes /jogo", p => p.PJ(p.Passes)),
                  }
                : new (string Label, Func<PerfilJogador, double> Get)[]
                  {
                      ("Gols /jogo", p => p.PJTotal(p.Gols)),
                      ("Fin. no alvo /jogo", p => p.PJ(p.FinNoGol)),
                      ("% fin. no alvo", p => p.Pct(p.FinNoGol, p.Finalizacoes)),
                      ("Participações /jogo", p => p.PJTotal(p.Gols + p.Assistencias)),
                      ("Dribles certos /jogo", p => p.PJ(p.DriblesCertos)),
                      ("Passes-chave /jogo", p => p.PJ(p.PassesChave)),
                  };

            var valoresPorEixo = eixos.Select(e => perfis.Select(e.Get).ToArray()).ToArray();

            var radar = new
            {
                labels = eixos.Select(e => e.Label).ToArray(),
                datasets = perfis.Select((p, i) => new
                {
                    label = p.J.NomeExibicao,
                    cor = cores[i],
                    data = valoresPorEixo.Select(vals =>
                    {
                        var max = (vals.Max() * 1.12);
                        if (max <= 0) max = 1;
                        return (int)Math.Round(vals[i] / max * 100);
                    }).ToArray(),
                }).ToList(),
            };

            var percentil = new
            {
                labels = eixos.Select(e => e.Label).ToArray(),
                datasets = perfis.Select((p, i) => new
                {
                    label = p.J.NomeExibicao,
                    cor = cores[i],
                    data = valoresPorEixo.Select(vals =>
                    {
                        var refv = vals.Max() * 0.72;
                        if (refv <= 0) refv = 1;
                        return Math.Min(99, (int)Math.Round(vals[i] / refv * 50));
                    }).ToArray(),
                }).ToList(),
            };

            var divisao = eixos.Select((e, ei) =>
            {
                var vals = valoresPorEixo[ei];
                var soma = vals.Sum();
                if (soma <= 0) soma = 1;
                var max = vals.Max();
                return new
                {
                    label = e.Label,
                    resumo = string.Join(" · ", vals.Select(v => Math.Round(v / soma * 100) + "%")),
                    segmentos = perfis.Select((p, i) => new
                    {
                        cor = cores[i],
                        pct = (int)Math.Round(vals[i] / soma * 100),
                        opacidade = vals[i] == max ? 1.0 : 0.45,
                    }).ToList(),
                };
            }).ToList();

            // ── Grupos de métricas detalhadas (linhas com barra proporcional) ──
            var gruposJson = new List<object>();
            {
                var linhas = new List<object>();

                void Linha(string label, Func<PerfilJogador, double> get, string sufixo = "", bool menorMelhor = false, string fmt = "0.##")
                {
                    var vals = perfis.Select(get).ToArray();
                    if (vals.All(v => v == 0)) return;
                    var max = vals.Max();
                    if (max <= 0) max = 1;
                    var alvo = menorMelhor ? vals.Min() : vals.Max();
                    linhas.Add(new
                    {
                        label,
                        linhas = perfis.Select((p, i) => new
                        {
                            nome = p.J.NomeExibicao,
                            cor = cores[i],
                            corTexto = Math.Abs(vals[i] - alvo) < 0.005 ? cores[i] : "var(--text-muted)",
                            opacidade = Math.Abs(vals[i] - alvo) < 0.005 ? 1.0 : 0.45,
                            w = (int)Math.Round(vals[i] / max * 100),
                            txt = F(vals[i], fmt) + sufixo,
                        }).ToList(),
                    });
                }

                void FecharGrupo(string titulo)
                {
                    if (linhas.Count > 0) gruposJson.Add(new { titulo, metricas = linhas.ToArray() });
                    linhas.Clear();
                }

                Linha("Jogos", p => p.JogosTotal, fmt: "0");
                Linha("Nota média", p => p.NotaMedia ?? 0, fmt: "0.00");
                Linha("Rating médio (api)", p => p.Rating ?? 0, fmt: "0.00");
                Linha("Minutos por jogo", p => p.MinutosMedio, fmt: "0");
                FecharGrupo("Visão geral");

                Linha("Gols", p => p.Gols, fmt: "0");
                Linha("Gols por jogo", p => p.PJTotal(p.Gols));
                Linha("Finalizações por jogo", p => p.PJ(p.Finalizacoes));
                Linha("Finalizações no alvo por jogo", p => p.PJ(p.FinNoGol));
                Linha("% de finalizações no alvo", p => p.Pct(p.FinNoGol, p.Finalizacoes), "%", fmt: "0");
                FecharGrupo("Ataque");

                Linha("Assistências", p => p.Assistencias, fmt: "0");
                Linha("Assistências por jogo", p => p.PJTotal(p.Assistencias));
                Linha("Passes por jogo", p => p.PJ(p.Passes), fmt: "0.#");
                Linha("Passes-chave por jogo", p => p.PJ(p.PassesChave));
                FecharGrupo("Criação");

                Linha("Dribles certos por jogo", p => p.PJ(p.DriblesCertos));
                Linha("% de dribles certos", p => p.Pct(p.DriblesCertos, p.DriblesTentados), "%", fmt: "0");
                Linha("Duelos vencidos por jogo", p => p.PJ(p.DuelosVencidos));
                Linha("% de duelos vencidos", p => p.Pct(p.DuelosVencidos, p.DuelosTotal), "%", fmt: "0");
                FecharGrupo("Drible e duelos");

                Linha("Desarmes por jogo", p => p.PJ(p.Desarmes));
                Linha("Interceptações por jogo", p => p.PJ(p.Interceptacoes));
                Linha("Bloqueios por jogo", p => p.PJ(p.Bloqueios));
                FecharGrupo("Defesa");

                Linha("Faltas sofridas por jogo", p => p.PJ(p.FaltasSofridas));
                Linha("Faltas cometidas por jogo", p => p.PJ(p.FaltasCometidas), menorMelhor: true);
                Linha("Cartões", p => p.Cartoes, fmt: "0", menorMelhor: true);
                FecharGrupo("Disciplina");
            }

            // ── Prioridades da posição (texto + chips) ──────────────────
            var comuns = PerfilJogadorService.OrdemRoles.Where(r => perfis.All(p => p.Roles.Contains(r))).ToList();
            var todosRoles = PerfilJogadorService.OrdemRoles.Where(r => perfis.Any(p => p.Roles.Contains(r))).ToList();
            var textoFuncoes = string.Join("; ", perfis.Select(p => $"{p.J.NomeExibicao} atua como {p.J.Posicao}"))
                + (comuns.Count > 0
                    ? $". {(perfis.Count > 2 ? "Os jogadores" : "Os dois")} podem exercer a função de {string.Join(" e ", comuns.Select(r => PerfilJogadorService.RoleInfo(r).Nome))}."
                    : ". Funções diferentes — as métricas abaixo cobrem as prioridades de cada função do grupo.");

            var prioridadesRole = new Dictionary<string, string[]>
            {
                ["GOL"] = new[] { "Defesas por jogo", "Gols sofridos por jogo", "% jogos sem sofrer gols" },
                ["ZAG"] = new[] { "Ações defensivas por jogo", "% de duelos vencidos", "Bloqueios", "Jogos sem sofrer gols" },
                ["LAT"] = new[] { "Ações defensivas", "Passes-chave por jogo", "Dribles certos por jogo", "Assistências" },
                ["VOL"] = new[] { "Ações defensivas por jogo", "Passes por jogo", "% de duelos vencidos" },
                ["MEI"] = new[] { "Assistências por jogo", "Passes-chave por jogo", "Passes por jogo" },
                ["PON"] = new[] { "Dribles certos por jogo", "% de dribles certos", "Participações em gol", "Gols por jogo" },
                ["ATA"] = new[] { "Gols por jogo", "Finalizações no alvo", "% de finalizações no alvo", "Participações em gol" },
            };
            var prioridades = new List<string>();
            foreach (var r in todosRoles)
                if (prioridadesRole.TryGetValue(r, out var lista))
                    foreach (var x in lista)
                        if (!prioridades.Contains(x)) prioridades.Add(x);

            // ── Confrontos diretos (só com exatamente 2 jogadores) ──────
            object? confrontos = null;
            if (perfis.Count == 2)
            {
                var timeA = perfis[0].J.TimeId;
                var timeB = perfis[1].J.TimeId;
                var jogos = await _context.Jogos
                    .AsNoTracking()
                    .Include(j => j.TimeCasa)
                    .Include(j => j.TimeVisitante)
                    .Include(j => j.Competicao)
                    .Where(j => j.PlacarCasa != null && j.PlacarVisitante != null
                             && ((j.TimeCasaId == timeA && j.TimeVisitanteId == timeB)
                              || (j.TimeCasaId == timeB && j.TimeVisitanteId == timeA))
                             && (!temporada.HasValue || j.Temporada == temporada.Value))
                    .OrderByDescending(j => j.Data)
                    .Take(10)
                    .ToListAsync();

                var jogoIds = jogos.Select(j => j.Id).ToList();
                var idsJogadores = new[] { perfis[0].J.Id, perfis[1].J.Id };

                var golsConfronto = await _context.Gols.AsNoTracking()
                    .Where(g => jogoIds.Contains(g.JogoId) && !g.Contra && idsJogadores.Contains(g.JogadorId))
                    .ToListAsync();
                var assisConfronto = await _context.Assistencias.AsNoTracking()
                    .Where(a => jogoIds.Contains(a.JogoId) && idsJogadores.Contains(a.JogadorId))
                    .ToListAsync();
                var notasConfronto = await _context.Notas.AsNoTracking()
                    // Detalhes: o piso de merecimento conta os chips verdes x vermelhos.
                    .Include(n => n.Detalhes)
                    .Where(n => jogoIds.Contains(n.JogoId) && idsJogadores.Contains(n.JogadorId) && n.UsuarioId == uid)
                    .ToListAsync();
                var estatsConfronto = await _context.EstatisticasJogador.AsNoTracking()
                    // Jogo e Jogador: usados pelo bônus "não sofreu gol" (CriteriosNotaHelper).
                    .Include(e => e.Jogo)
                    .Include(e => e.Jogador)
                    .Where(e => jogoIds.Contains(e.JogoId) && idsJogadores.Contains(e.JogadorId) && e.Minutos != null && e.Minutos > 0)
                    .ToListAsync();
                var ladosConfronto = await LadoJogadorHelper.CarregarAsync(_context, jogoIds, uid, idsJogadores);
                // Lados próprios: ladosConfronto está filtrado nos dois jogadores do
                // comparativo e não fecharia as finalizações do elenco adversário.
                var contextosConfronto = await ContextoNotaHelper.CarregarAsync(_context, jogoIds, uid);
                var calculadoraConfronto = await NotaAutomaticaHelper.CarregarAsync(
                    _context, jogoIds, uid, criterios, ladosConfronto, contextosConfronto);

                double? NotaDoJogo(int jogadorId, int jogoId)
                {
                    var ctxJogo = ContextoNotaHelper.De(contextosConfronto, jogadorId, jogoId);
                    var n = notasConfronto.FirstOrDefault(x => x.JogadorId == jogadorId && x.JogoId == jogoId);
                    if (n != null)
                        return n.NotaManual.HasValue
                            ? Math.Round(Math.Max(0, Math.Min(10, n.NotaManual.Value)), 2)
                            : CriteriosNotaHelper.NotaFinal(n.Valor, criterios,
                                ctxJogo with { Acoes = CriteriosNotaHelper.ContarAcoes(n.Detalhes) });
                    var e = estatsConfronto.FirstOrDefault(x => x.JogadorId == jogadorId && x.JogoId == jogoId);
                    if (e != null) return calculadoraConfronto.De(e).Nota;
                    return null;
                }

                confrontos = jogos.Select(j =>
                {
                    var participacoes = new List<string>();
                    foreach (var p in perfis)
                    {
                        var g = golsConfronto.Count(x => x.JogoId == j.Id && x.JogadorId == p.J.Id);
                        var a = assisConfronto.Count(x => x.JogoId == j.Id && x.JogadorId == p.J.Id);
                        if (g > 0) participacoes.Add($"{g} gol{(g > 1 ? "s" : "")} ({p.J.NomeExibicao})");
                        if (a > 0) participacoes.Add($"{a} assistência{(a > 1 ? "s" : "")} ({p.J.NomeExibicao})");
                    }

                    var notas = perfis.Select(p => NotaDoJogo(p.J.Id, j.Id))
                        .Select(n => n.HasValue ? F(n.Value, "0.0") : "—");

                    return new
                    {
                        data = j.Data.HasValue ? j.Data.Value.ToString("dd/MM/yyyy") : "—",
                        competicao = j.Competicao?.Nome ?? "—",
                        partida = $"{j.TimeCasa.Nome} {j.PlacarCasa} x {j.PlacarVisitante} {j.TimeVisitante.Nome}",
                        participacoes = participacoes.Count > 0 ? string.Join(" · ", participacoes) : "—",
                        notas = string.Join(" · ", notas),
                    };
                }).ToList();
            }

            return Json(new
            {
                jogadores = jogadoresJson,
                textoFuncoes,
                prioridades,
                radar,
                percentil,
                divisao,
                grupos = gruposJson,
                confrontos,
            });
        }

        // POST: /Relatorios/RecalcularNotas
        // Reaplica os pesos atuais (Cadastros > Critérios de Nota) às notas manuais já salvas.
        // As notas automáticas (vindas das estatísticas) já usam os pesos atuais a cada acesso.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalcularNotas(int[]? competicaoIds, int[]? timeIds, int? temporada, bool incluirNaoAnalisados = false, int? minJogos = null, string? dummy = null)
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
            return RedirectToAction(nameof(Index), new { competicaoIds, timeIds, temporada, incluirNaoAnalisados, minJogos });
        }
    }
}
