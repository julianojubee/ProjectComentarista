using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
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
        // temporadaElenco: recorte do painel "Estatísticas do Elenco". Sem valor,
        // usa a temporada mais recente com jogos realizados — misturar temporadas
        // distorce tudo (minutos possíveis, idade, dependência do XI).
        public async Task<IActionResult> Details(int id, int? temporadaElenco)
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

            var todosTreinadores = await _context.Treinadores
                .Include(t => t.Time)
                .OrderBy(t => t.Nome)
                .ToListAsync();

            var nacionalidades = await _context.Nacionalidades
                .OrderBy(n => n.Nome)
                .ToListAsync();

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

            // Painel "Estatísticas do Elenco": agrega tudo o que EstatisticaJogador guarda
            // (minutos, finalizações, passes, duelos, dribles, defesa, disciplina, rating)
            // nos jogos já realizados do time, mais as titularidades de Escalacao.
            var jogosRealizados = jogos
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            var temporadasDisponiveis = jogosRealizados
                .Select(j => j.Temporada)
                .Where(t => t > 0)
                .Distinct()
                .OrderByDescending(t => t)
                .ToList();

            // A temporada pedida só vale se o time tiver jogos nela.
            var temporadaSelecionada = temporadaElenco.HasValue && temporadasDisponiveis.Contains(temporadaElenco.Value)
                ? temporadaElenco.Value
                : temporadasDisponiveis.FirstOrDefault();

            var jogosRealizadosIds = jogosRealizados
                .Where(j => temporadaSelecionada == 0 || j.Temporada == temporadaSelecionada)
                .Select(j => j.Id)
                .ToList();
            var totalMinutosPossiveis = jogosRealizadosIds.Count * 90;

            var elencoIds = elenco.Select(j => j.Id).ToList();

            // Sem jogos realizados as duas consultas abaixo simplesmente não retornam linhas.
            var agregadoPorJogador = await _context.EstatisticasJogador
                    .Where(e => elencoIds.Contains(e.JogadorId) && jogosRealizadosIds.Contains(e.JogoId))
                    .GroupBy(e => e.JogadorId)
                    .Select(g => new
                    {
                        JogadorId = g.Key,
                        Minutos = g.Sum(e => e.Minutos ?? 0),
                        // Só conta como "jogo disputado" quem entrou em campo.
                        Jogos = g.Count(e => (e.Minutos ?? 0) > 0),
                        // Rating vem só de quem jogou; sem nenhum, fica null (não vira 0).
                        SomaRating = g.Sum(e => (e.Minutos ?? 0) > 0 ? (e.Rating ?? 0) : 0),
                        JogosComRating = g.Count(e => (e.Minutos ?? 0) > 0 && e.Rating != null),
                        Gols = g.Sum(e => e.Gols),
                        Assistencias = g.Sum(e => e.Assistencias),
                        FinalizacoesTotal = g.Sum(e => e.FinalizacoesTotal),
                        FinalizacoesNoGol = g.Sum(e => e.FinalizacoesNoGol),
                        PassesTotal = g.Sum(e => e.PassesTotal),
                        PassesChave = g.Sum(e => e.PassesChave),
                        Desarmes = g.Sum(e => e.Desarmes),
                        Interceptacoes = g.Sum(e => e.Interceptacoes),
                        Bloqueios = g.Sum(e => e.Bloqueios),
                        DuelosTotal = g.Sum(e => e.DuelosTotal),
                        DuelosVencidos = g.Sum(e => e.DuelosVencidos),
                        DriblesTentados = g.Sum(e => e.DriblesTentados),
                        DriblesCertos = g.Sum(e => e.DriblesCertos),
                        FaltasCometidas = g.Sum(e => e.FaltasCometidas),
                        FaltasSofridas = g.Sum(e => e.FaltasSofridas),
                        CartoesAmarelos = g.Sum(e => e.CartoesAmarelos),
                        CartoesVermelhos = g.Sum(e => e.CartoesVermelhos),
                        Defesas = g.Sum(e => e.Defesas),
                        GolsSofridos = g.Sum(e => e.GolsSofridos)
                    })
                    .ToDictionaryAsync(x => x.JogadorId);

            // Titularidades: Distinct porque cada usuário pode gravar a própria escalação
            // do mesmo jogo — sem isso um jogo viraria N titularidades.
            var titularidades = (await _context.Escalacoes
                    .Where(e => e.JogadorId != null && elencoIds.Contains(e.JogadorId.Value) &&
                                jogosRealizadosIds.Contains(e.JogoId) &&
                                e.Titular && e.FaseEscalacao == "INICIAL")
                    .Select(e => new { JogadorId = e.JogadorId!.Value, e.JogoId })
                    .Distinct()
                    .ToListAsync())
                .GroupBy(x => x.JogadorId)
                .ToDictionary(g => g.Key, g => g.Count());

            var estatisticasElenco = elenco
                .Where(j => agregadoPorJogador.ContainsKey(j.Id) && agregadoPorJogador[j.Id].Minutos > 0)
                .Select(j =>
                {
                    var a = agregadoPorJogador[j.Id];
                    return new JogadorElencoStatViewModel
                    {
                        JogadorId = j.Id,
                        Nome = string.IsNullOrWhiteSpace(j.Nome) ? j.NomeExibicao : j.Nome,
                        NomeCompleto = j.NomeExibicao,
                        Posicao = j.Posicao,
                        Grupo = ControleFutebolWeb.Services.PerfilJogadorService.GrupoPosicao(j.Posicao),
                        Idade = j.Idade,
                        Altura = j.Altura,
                        Nacionalidade = j.Nacionalidade?.Nome,
                        FotoUrl = j.FotoUrl,
                        MinutosJogados = a.Minutos,
                        PercMinutos = totalMinutosPossiveis > 0
                            ? Math.Round(Math.Min(100.0, a.Minutos / (double)totalMinutosPossiveis * 100), 1)
                            : 0,
                        Jogos = a.Jogos,
                        Titularidades = titularidades.GetValueOrDefault(j.Id),
                        RatingMedio = a.JogosComRating > 0 ? Math.Round(a.SomaRating / a.JogosComRating, 2) : null,
                        Gols = a.Gols,
                        Assistencias = a.Assistencias,
                        FinalizacoesTotal = a.FinalizacoesTotal,
                        FinalizacoesNoGol = a.FinalizacoesNoGol,
                        PassesTotal = a.PassesTotal,
                        PassesChave = a.PassesChave,
                        Desarmes = a.Desarmes,
                        Interceptacoes = a.Interceptacoes,
                        Bloqueios = a.Bloqueios,
                        DuelosTotal = a.DuelosTotal,
                        DuelosVencidos = a.DuelosVencidos,
                        DriblesTentados = a.DriblesTentados,
                        DriblesCertos = a.DriblesCertos,
                        FaltasCometidas = a.FaltasCometidas,
                        FaltasSofridas = a.FaltasSofridas,
                        CartoesAmarelos = a.CartoesAmarelos,
                        CartoesVermelhos = a.CartoesVermelhos,
                        Defesas = a.Defesas,
                        GolsSofridos = a.GolsSofridos
                    };
                })
                .OrderByDescending(e => e.MinutosJogados)
                .ToList();

            var elencoResumo = MontarResumoElenco(elenco, estatisticasElenco, jogosRealizadosIds.Count);

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
                CompeticoesApi = competicoesApi,
                TodosTreinadores = todosTreinadores,
                Nacionalidades = nacionalidades,
                EstatisticasElenco = estatisticasElenco,
                ElencoResumo = elencoResumo,
                TemporadasElenco = temporadasDisponiveis,
                TemporadaElencoSelecionada = temporadaSelecionada
            };

            return View(viewModel);
        }

        // Retrato do elenco em números únicos para o cabeçalho do painel de estatísticas.
        // Idade/altura médias saem do cadastro (todo o elenco); as médias ponderadas e a
        // concentração de minutos saem de quem realmente jogou.
        private static ElencoResumoViewModel MontarResumoElenco(
            List<Jogador> elenco,
            List<JogadorElencoStatViewModel> stats,
            int jogosRealizados)
        {
            var resumo = new ElencoResumoViewModel
            {
                JogosConsiderados = jogosRealizados,
                TamanhoElenco = elenco.Count,
                JogadoresUtilizados = stats.Count
            };

            var comIdade = elenco.Where(j => j.Idade > 0).ToList();
            if (comIdade.Count > 0)
                resumo.IdadeMedia = Math.Round(comIdade.Average(j => j.Idade), 1);

            var comAltura = elenco.Where(j => j.Altura > 0).ToList();
            if (comAltura.Count > 0)
                resumo.AlturaMedia = (int)Math.Round(comAltura.Average(j => j.Altura!.Value));

            resumo.Nacionalidades = elenco
                .Where(j => j.NacionalidadeId != null)
                .Select(j => j.NacionalidadeId!.Value)
                .Distinct()
                .Count();

            if (stats.Count == 0) return resumo;

            var comIdadeEMinutos = stats.Where(s => s.Idade > 0).ToList();
            var minutosComIdade = comIdadeEMinutos.Sum(s => (long)s.MinutosJogados);
            if (minutosComIdade > 0)
                resumo.IdadeMediaPonderada = Math.Round(
                    comIdadeEMinutos.Sum(s => (double)s.Idade * s.MinutosJogados) / minutosComIdade, 1);

            var minutosTotais = stats.Sum(s => (long)s.MinutosJogados);
            if (minutosTotais > 0)
            {
                var top11 = stats.OrderByDescending(s => s.MinutosJogados).Take(11).Sum(s => (long)s.MinutosJogados);
                resumo.PercMinutosTop11 = Math.Round(100.0 * top11 / minutosTotais, 1);
            }

            var comRating = stats.Where(s => s.RatingMedio.HasValue).ToList();
            if (comRating.Count > 0)
                // Ponderada pelos minutos: o rating de quem joga sempre pesa mais.
                resumo.RatingMedio = Math.Round(
                    comRating.Sum(s => s.RatingMedio!.Value * s.MinutosJogados) /
                    Math.Max(1, comRating.Sum(s => s.MinutosJogados)), 2);

            resumo.Gols = stats.Sum(s => s.Gols);
            resumo.Assistencias = stats.Sum(s => s.Assistencias);
            resumo.CartoesAmarelos = stats.Sum(s => s.CartoesAmarelos);
            resumo.CartoesVermelhos = stats.Sum(s => s.CartoesVermelhos);

            foreach (var grupo in new[] { "GOL", "DEF", "MEI", "ATA" })
            {
                var doGrupo = stats.Where(s => s.Grupo == grupo).ToList();
                resumo.MinutosPorGrupo[grupo] = doGrupo.Sum(s => s.MinutosJogados);

                var idades = doGrupo.Where(s => s.Idade > 0).ToList();
                var minutosGrupo = idades.Sum(s => (long)s.MinutosJogados);
                resumo.IdadeMediaPorGrupo[grupo] = minutosGrupo > 0
                    ? Math.Round(idades.Sum(s => (double)s.Idade * s.MinutosJogados) / minutosGrupo, 1)
                    : 0;
            }

            return resumo;
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nome,Cidade,LinkTransfermarket")] Time time, IFormFile? escudoFile)
        {
            if (id != time.Id) return NotFound();

            ModelState.Remove(nameof(Time.Jogadores));
            ModelState.Remove(nameof(Time.FormacaoPadrao));
            ModelState.Remove(nameof(Time.TimeEscalacaoPadrao));

            if (ModelState.IsValid)
            {
                var timeExistente = await _context.Times.FindAsync(id);
                if (timeExistente == null) return NotFound();

                try
                {
                    timeExistente.Nome = time.Nome;
                    timeExistente.Cidade = time.Cidade;
                    timeExistente.LinkTransfermarket = time.LinkTransfermarket;

                    if (escudoFile != null && escudoFile.Length > 0)
                    {
                        var r = await UploadHelper.SalvarImagemAsync(escudoFile, _env.WebRootPath, "images/escudos", time.Nome);
                        if (!r.Sucesso)
                        {
                            ModelState.AddModelError("", $"Escudo: {r.Erro}");
                            return View(timeExistente);
                        }
                        timeExistente.EscudoUrl = r.UrlRelativa;
                    }

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

        // POST: Times/VincularTreinador — vincula um treinador já cadastrado (de qualquer
        // time) como o treinador atual deste time, corrigindo TimeId dele.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VincularTreinador(int id, int treinadorId)
        {
            var time = await _context.Times.FindAsync(id);
            if (time == null) return NotFound();

            var treinador = await _context.Treinadores.FindAsync(treinadorId);
            if (treinador == null) return NotFound();

            treinador.TimeId = id;
            treinador.DtAlt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Mensagem"] = $"{treinador.Nome} vinculado como treinador de {time.Nome}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Times/CriarTreinador — cadastra um treinador novo já vinculado a este time.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarTreinador(int id, string nome, int? nacionalidadeId, DateTime? dataNascimento)
        {
            var time = await _context.Times.FindAsync(id);
            if (time == null) return NotFound();

            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Mensagem"] = "Informe o nome do treinador.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var treinador = new Treinador
            {
                Nome = nome.Trim(),
                NacionalidadeId = nacionalidadeId,
                // Data pura: ancora ao meio-dia UTC para o fuso -3 não deslocar o dia.
                DataNascimento = dataNascimento.HasValue
                    ? DateTime.SpecifyKind(dataNascimento.Value.Date.AddHours(12), DateTimeKind.Utc)
                    : null,
                TimeId = id,
                DtInc = DateTime.UtcNow
            };

            _context.Treinadores.Add(treinador);
            await _context.SaveChangesAsync();

            TempData["Mensagem"] = $"{treinador.Nome} cadastrado como treinador de {time.Nome}.";
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

        // GET: Times/EstatisticasLocaisApi?timeId=1&leagueId=71&season=2026
        // Complementa /Times/EstatisticasApi (API-Football) com o que só os jogos
        // cadastrados localmente sabem responder:
        //  • gols por intervalo com o gol contra atribuído ao time que se beneficiou
        //    dele — a API-Football soma o gol contra no intervalo do time do autor —
        //    e separados em casa × fora, recorte que a API não entrega;
        //  • cartões (amarelos e vermelhos) por intervalo, também casa × fora;
        //  • o resultado rodada a rodada, para pontos por rodada e saldo acumulado;
        //  • aproveitamento por formação — a API só informa quantos jogos com cada uma;
        //  • o perfil dos rivais da mesma liga/temporada, para o radar comparativo.
        [HttpGet]
        public async Task<IActionResult> EstatisticasLocaisApi(int timeId, int leagueId, int season)
        {
            string[] buckets = { "0-15", "16-30", "31-45", "46-60", "61-75", "76-90", "91-105" };
            int IndiceBucket(int minuto) => minuto switch
            {
                <= 15 => 0,
                <= 30 => 1,
                <= 45 => 2,
                <= 60 => 3,
                <= 75 => 4,
                <= 90 => 5,
                _ => 6
            };

            // O leagueId da API-Football fica guardado em Competicao.LinkTransfermarket
            // no formato "apifoot:{leagueId}:{season}" (ver TimesController.Details/CompeticoesApi).
            var prefixoLiga = $"apifoot:{leagueId}:";
            var jogosLiga = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.Temporada == season
                    && j.Competicao.LinkTransfermarket != null
                    && j.Competicao.LinkTransfermarket.StartsWith(prefixoLiga)
                    && j.PlacarCasa != null && j.PlacarVisitante != null)
                .ToListAsync();

            var jogosDoTime = jogosLiga
                .Where(j => j.TimeCasaId == timeId || j.TimeVisitanteId == timeId)
                .OrderBy(j => j.Rodada).ThenBy(j => j.Data)
                .ToList();

            if (jogosDoTime.Count == 0)
                return Json(new { disponivel = false });

            // Um evento (gol/cartão) pertence ao time do jogo em que o autor entrou —
            // resolve tanto elenco de clube (TimeId) quanto de seleção (SelecaoId).
            int? TimeDoAutor(Jogo j, Jogador? autor)
            {
                if (autor == null) return null;
                if (autor.TimeId == j.TimeCasaId || autor.SelecaoId == j.TimeCasaId) return j.TimeCasaId;
                if (autor.TimeId == j.TimeVisitanteId || autor.SelecaoId == j.TimeVisitanteId) return j.TimeVisitanteId;
                return null;
            }

            // ── Rodada a rodada + formação usada em cada jogo ────────────────────
            var partidas = jogosDoTime.Select(j =>
            {
                bool emCasa = j.TimeCasaId == timeId;
                int gp = emCasa ? j.PlacarCasa!.Value : j.PlacarVisitante!.Value;
                int gc = emCasa ? j.PlacarVisitante!.Value : j.PlacarCasa!.Value;
                string res = gp > gc ? "V" : gp == gc ? "E" : "D";
                return new
                {
                    Jogo = j,
                    EmCasa = emCasa,
                    Gp = gp,
                    Gc = gc,
                    Res = res,
                    Pts = res == "V" ? 3 : res == "E" ? 1 : 0,
                    FormacaoId = emCasa ? j.FormacaoCasaId : j.FormacaoVisitanteId,
                    Adversario = (emCasa ? j.TimeVisitante?.Nome : j.TimeCasa?.Nome) ?? "—"
                };
            }).ToList();

            var rodadas = partidas.Select(p => new
            {
                rodada = p.Jogo.Rodada,
                jogoId = p.Jogo.Id,
                data = DateHelper.FormatarData(p.Jogo.Data, "dd/MM"),
                casa = p.EmCasa,
                gp = p.Gp,
                gc = p.Gc,
                res = p.Res,
                pts = p.Pts,
                adversario = p.Adversario
            }).ToList();

            var nomesFormacao = await _context.Formacoes.AsNoTracking()
                .ToDictionaryAsync(f => f.Id, f => f.Nome);

            var formacoes = partidas
                .Where(p => p.FormacaoId.HasValue && nomesFormacao.ContainsKey(p.FormacaoId.Value))
                .GroupBy(p => p.FormacaoId!.Value)
                .Select(g => new
                {
                    formacao = nomesFormacao[g.Key],
                    jogos = g.Count(),
                    aproveitamento = Math.Round(g.Sum(p => p.Pts) / (g.Count() * 3.0) * 100, 1)
                })
                .OrderByDescending(f => f.jogos)
                .ToList();

            // ── Gols e cartões por intervalo, separados em casa × fora ───────────
            var mandante = partidas.ToDictionary(p => p.Jogo.Id, p => p.EmCasa);
            var idsJogosDoTime = mandante.Keys.ToList();

            int[] proCasa = new int[7], proFora = new int[7];
            int[] sofCasa = new int[7], sofFora = new int[7];
            int[] amCasa = new int[7], amFora = new int[7];
            int[] vmCasa = new int[7], vmFora = new int[7];

            var gols = await _context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => idsJogosDoTime.Contains(g.JogoId))
                .ToListAsync();

            foreach (var g in gols)
            {
                if (g.Jogador == null) continue;
                bool autorENosso = g.Jogador.TimeId == timeId || g.Jogador.SelecaoId == timeId;
                bool marcadoPorNos = g.Contra ? !autorENosso : autorENosso;
                int i = IndiceBucket(g.Minuto);
                bool emCasa = mandante[g.JogoId];

                if (marcadoPorNos) { if (emCasa) proCasa[i]++; else proFora[i]++; }
                else { if (emCasa) sofCasa[i]++; else sofFora[i]++; }
            }

            var jogoPorId = jogosLiga.ToDictionary(j => j.Id);
            var idsJogosLiga = jogoPorId.Keys.ToList();

            var cartoesLiga = await _context.Cartoes.AsNoTracking()
                .Include(c => c.Jogador)
                .Where(c => idsJogosLiga.Contains(c.JogoId))
                .ToListAsync();

            // Amarelos por time na liga inteira — alimenta o eixo "disciplina" do radar.
            var amarelosPorTime = new Dictionary<int, int>();

            foreach (var c in cartoesLiga)
            {
                if (!jogoPorId.TryGetValue(c.JogoId, out var jogo)) continue;
                var timeDono = TimeDoAutor(jogo, c.Jogador);
                if (timeDono == null) continue;

                bool vermelho = c.Tipo != null &&
                    c.Tipo.StartsWith("Verm", StringComparison.OrdinalIgnoreCase);

                if (!vermelho)
                    amarelosPorTime[timeDono.Value] = amarelosPorTime.GetValueOrDefault(timeDono.Value) + 1;

                if (timeDono != timeId || !mandante.TryGetValue(c.JogoId, out var emCasaCartao)) continue;

                int i = IndiceBucket(c.Minuto);
                if (vermelho) { if (emCasaCartao) vmCasa[i]++; else vmFora[i]++; }
                else { if (emCasaCartao) amCasa[i]++; else amFora[i]++; }
            }

            // ── Perfil do time e dos rivais, para o radar comparativo ────────────
            var classificacao = ClassificacaoCalculator.Calcular(jogosLiga);

            // Clean sheets por time: jogos da liga em que o time não sofreu gol.
            var cleanSheetPorTime = new Dictionary<int, int>();
            foreach (var j in jogosLiga)
            {
                if (j.PlacarVisitante == 0)
                    cleanSheetPorTime[j.TimeCasaId] = cleanSheetPorTime.GetValueOrDefault(j.TimeCasaId) + 1;
                if (j.PlacarCasa == 0)
                    cleanSheetPorTime[j.TimeVisitanteId] = cleanSheetPorTime.GetValueOrDefault(j.TimeVisitanteId) + 1;
            }

            object Perfil(string chave, string nome, int jogos, int vitorias, int pontos,
                          int golsPro, int golsContra, int cleanSheets, int amarelos)
            {
                double div = jogos == 0 ? 1 : jogos;
                return new
                {
                    chave,
                    nome,
                    jogos,
                    gpJogo = Math.Round(golsPro / div, 2),
                    gcJogo = Math.Round(golsContra / div, 2),
                    aproveitamento = Math.Round(pontos / (div * 3) * 100, 1),
                    csPct = Math.Round(cleanSheets / div * 100, 1),
                    vitPct = Math.Round(vitorias / div * 100, 1),
                    amJogo = Math.Round(amarelos / div, 2)
                };
            }

            var rivais = classificacao
                .Where(c => c.TimeId != timeId)
                .Select(c => Perfil(
                    "t" + c.TimeId,
                    $"{c.Time?.Nome ?? "Time " + c.TimeId} ({c.Posicao}º)",
                    c.Jogos, c.Vitorias, c.Pontos, c.GolsPro, c.GolsContra,
                    cleanSheetPorTime.GetValueOrDefault(c.TimeId),
                    amarelosPorTime.GetValueOrDefault(c.TimeId)))
                .ToList();

            var mediaLiga = Perfil("liga", "Média da liga",
                classificacao.Sum(c => c.Jogos),
                classificacao.Sum(c => c.Vitorias),
                classificacao.Sum(c => c.Pontos),
                classificacao.Sum(c => c.GolsPro),
                classificacao.Sum(c => c.GolsContra),
                cleanSheetPorTime.Values.Sum(),
                amarelosPorTime.Values.Sum());

            var meuTime = classificacao.FirstOrDefault(c => c.TimeId == timeId);
            var perfilDoTime = Perfil("time", meuTime?.Time?.Nome ?? "Este time",
                meuTime?.Jogos ?? partidas.Count,
                meuTime?.Vitorias ?? partidas.Count(p => p.Res == "V"),
                meuTime?.Pontos ?? partidas.Sum(p => p.Pts),
                meuTime?.GolsPro ?? partidas.Sum(p => p.Gp),
                meuTime?.GolsContra ?? partidas.Sum(p => p.Gc),
                cleanSheetPorTime.GetValueOrDefault(timeId),
                amarelosPorTime.GetValueOrDefault(timeId));

            return Json(new
            {
                disponivel = true,
                buckets,
                gols = new
                {
                    pro = new { casa = proCasa, fora = proFora },
                    sofridos = new { casa = sofCasa, fora = sofFora }
                },
                cartoes = new
                {
                    amarelo = new { casa = amCasa, fora = amFora },
                    vermelho = new { casa = vmCasa, fora = vmFora }
                },
                rodadas,
                formacoes,
                perfil = perfilDoTime,
                // "Média da liga" primeiro: é o comparativo padrão do radar.
                comparativos = new[] { mediaLiga }.Concat(rivais).ToList()
            });
        }
    }
}
