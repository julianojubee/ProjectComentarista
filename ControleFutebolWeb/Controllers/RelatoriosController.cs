// ControleFutebolWeb/Controllers/RelatoriosController.cs
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // A montagem dos rankings/estatísticas vive em Services/RelatoriosService.cs,
    // compartilhada com a API mobile (RelatoriosApiController).
    public class RelatoriosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RelatoriosService _relatorios;

        public RelatoriosController(FutebolContext context, UserManager<ApplicationUser> userManager, RelatoriosService relatorios)
        {
            _context = context;
            _userManager = userManager;
            _relatorios = relatorios;
        }

        // GET: /Relatorios
        public async Task<IActionResult> Index(int[]? competicaoIds, int[]? timeIds, int? temporada, bool incluirNaoAnalisados = false, int? minJogos = null)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var vm = await _relatorios.MontarAsync(competicaoIds, timeIds, temporada, incluirNaoAnalisados, minJogos ?? 1, usuarioId);
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

                int passesChave = 0, desarmes = 0, bloqueios = 0, interceptacoes = 0, duelosVencidos = 0, finNoGol = 0, driles = 0;
                if (estatsPorJogador.TryGetValue(jId, out var estats))
                {
                    passesChave    = estats.Sum(e => e.PassesChave);
                    desarmes       = estats.Sum(e => e.Desarmes);
                    bloqueios      = estats.Sum(e => e.Bloqueios);
                    interceptacoes = estats.Sum(e => e.Interceptacoes);
                    duelosVencidos = estats.Sum(e => e.DuelosVencidos);
                    finNoGol       = estats.Sum(e => e.FinalizacoesNoGol);
                    driles         = estats.Sum(e => e.DriblesCertos);
                }

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
                                : null));
                    var estatsDict = (estatsPorJogador.GetValueOrDefault(jId) ?? new())
                        .GroupBy(e => e.JogoId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    double soma = 0; int comp = 0;
                    foreach (var jogoId in jogoIdSet)
                    {
                        double nj;
                        if (notasDict.TryGetValue(jogoId, out var ni))
                            nj = ni.manual.HasValue
                                ? Math.Round(Math.Max(0, Math.Min(10, ni.manual.Value)), 2)
                                : Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima, Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + ni.valor)), 2);
                        else if (estatsDict.TryGetValue(jogoId, out var es))
                            nj = Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima,
                                    Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + es.Sum(e => CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco)))), 2);
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
                });
            }

            vm.Resultados = resultados
                .OrderByDescending(r => r.NotaMedia ?? 0)
                .ThenByDescending(r => r.Jogos)
                .ToList();

            return View(vm);
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
