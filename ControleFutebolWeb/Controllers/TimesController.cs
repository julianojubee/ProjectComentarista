using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<ApplicationUser> _userManager;

        public TimesController(
            FutebolContext context,
            ILogger<TimesController> logger,
            IWebHostEnvironment env,
            ControleFutebolWeb.Services.ApiFootballService apiFootballService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _env = env;
            _apiFootballService = apiFootballService;
            _userManager = userManager;
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
            ViewBag.TitulosPorTime = await TitulosHelper.PorTimeAsync(_context);

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
            // (minutos, finalizações, passes, duelos, dribles, defesa, disciplina)
            // nos jogos já realizados do time, mais as titularidades de Escalacao.
            // A nota de cada jogador vem à parte, de CalcularNotaMediaElencoAsync.
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

            // Coluna "Rating" da tabela: é a nota do SISTEMA (a mesma régua de
            // /Relatorios e do perfil do jogador), não o rating da API-Football.
            var notasMedias = await CalcularNotaMediaElencoAsync(elencoIds, jogosRealizadosIds);

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
                        RatingMedio = notasMedias.TryGetValue(j.Id, out var notaMedia) ? notaMedia : null,
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
                Titulos = (await TitulosHelper.PorTimeAsync(_context))
                    .GetValueOrDefault(id, new List<TitulosHelper.Titulo>()),
                TemporadasElenco = temporadasDisponiveis,
                TemporadaElencoSelecionada = temporadaSelecionada
            };

            return View(viewModel);
        }

        // Nota média do sistema por jogador, nos jogos informados — mesma régua do
        // Scout/Relatórios: nota manual quando o usuário avaliou o jogo (com o override
        // de nota final, se houver) e nota automática (motor escolhido em /CriteriosNota)
        // quando só existe a estatística importada. Jogos sem nenhum dos dois ficam fora
        // da média em vez de virar zero.
        private async Task<Dictionary<int, double>> CalcularNotaMediaElencoAsync(
            List<int> jogadorIds, List<int> jogoIds)
        {
            var medias = new Dictionary<int, double>();
            if (jogadorIds.Count == 0 || jogoIds.Count == 0) return medias;

            var usuarioId = _userManager.GetUserId(User);

            var notas = await _context.Notas
                .AsNoTracking()
                .Include(n => n.Detalhes)
                .Where(n => jogoIds.Contains(n.JogoId) && jogadorIds.Contains(n.JogadorId)
                         && n.UsuarioId == usuarioId)
                .ToListAsync();

            // Reservas não utilizados (Minutos 0/null) ficam de fora: a API-Football
            // grava uma linha para todo o elenco relacionado, mesmo quem não entrou.
            var estatisticas = await _context.EstatisticasJogador
                .AsNoTracking()
                // Jogo e Jogador alimentam o bônus "não sofreu gol" do CriteriosNotaHelper.
                .Include(e => e.Jogo)
                .Include(e => e.Jogador)
                .Where(e => jogoIds.Contains(e.JogoId) && jogadorIds.Contains(e.JogadorId)
                         && e.Minutos != null && e.Minutos > 0)
                .ToListAsync();

            if (notas.Count == 0 && estatisticas.Count == 0) return medias;

            var criteriosBanco = CriteriosNotaHelper.MergeCriterios(
                await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync(),
                await _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToListAsync());

            var lados = await LadoJogadorHelper.CarregarAsync(_context, jogoIds, usuarioId);
            var contextos = await ContextoNotaHelper.CarregarAsync(_context, jogoIds, usuarioId, lados);
            var calculadora = await NotaAutomaticaHelper.CarregarAsync(
                _context, jogoIds, usuarioId, criteriosBanco, lados, contextos);

            var notasPorJogador = notas.GroupBy(n => n.JogadorId).ToDictionary(g => g.Key, g => g.ToList());
            var estatsPorJogador = estatisticas.GroupBy(e => e.JogadorId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var jogadorId in jogadorIds)
            {
                var notasDoJogador = notasPorJogador.GetValueOrDefault(jogadorId) ?? new List<Nota>();
                var estatsDoJogador = estatsPorJogador.GetValueOrDefault(jogadorId) ?? new List<EstatisticaJogador>();
                if (notasDoJogador.Count == 0 && estatsDoJogador.Count == 0) continue;

                // Uma nota por jogo: mais de uma linha do mesmo jogo (importação em
                // partes, avaliação regravada) não pode pesar duas vezes na média.
                var notasPorJogo = notasDoJogador
                    .GroupBy(n => n.JogoId)
                    .ToDictionary(g => g.Key, g => (
                        valor: g.Average(n => n.Valor),
                        manual: g.Any(n => n.NotaManual.HasValue)
                            ? (double?)g.Where(n => n.NotaManual.HasValue).Average(n => n.NotaManual!.Value)
                            : null,
                        detalhes: g.SelectMany(n => n.Detalhes).ToList()));
                var estatsPorJogo = estatsDoJogador
                    .GroupBy(e => e.JogoId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                double soma = 0;
                int computados = 0;
                foreach (var jogoId in notasPorJogo.Keys.Union(estatsPorJogo.Keys))
                {
                    double nota;
                    if (notasPorJogo.TryGetValue(jogoId, out var n))
                        nota = n.manual.HasValue
                            ? Math.Round(Math.Max(0, Math.Min(10, n.manual.Value)), 2)
                            : CriteriosNotaHelper.NotaFinal(n.valor, criteriosBanco,
                                ContextoNotaHelper.De(contextos, jogadorId, jogoId) with
                                {
                                    Acoes = CriteriosNotaHelper.ContarAcoes(n.detalhes)
                                });
                    else if (estatsPorJogo.TryGetValue(jogoId, out var es))
                        nota = calculadora.De(es, jogadorId, jogoId).Nota;
                    else continue;

                    soma += nota;
                    computados++;
                }

                if (computados > 0)
                    medias[jogadorId] = Math.Round(soma / computados, 2);
            }

            return medias;
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
                // Ponderada pelos minutos: a nota de quem joga sempre pesa mais.
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

        // Busca na api-football (/teams?id=X) o estádio do clube — nome, cidade,
        // capacidade, tipo de gramado e foto — e salva no perfil do time.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarEstadio(int id)
        {
            var time = await _context.Times.FindAsync(id);
            if (time == null) return NotFound();

            if (time.IdApi <= 0)
            {
                TempData["Mensagem"] = "Este time não está vinculado à api-football, então não há estádio para buscar.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                var entrada = await _apiFootballService.BuscarTimeApiAsync(time.IdApi);

                if (entrada?.Venue == null || string.IsNullOrWhiteSpace(entrada.Venue.Name))
                {
                    TempData["Mensagem"] = "A api-football não informou o estádio deste clube.";
                }
                else if (Services.ApiFootballService.AplicarEstadio(time, entrada.Venue))
                {
                    await _context.SaveChangesAsync();
                    TempData["Mensagem"] = $"Estádio de {time.Nome} atualizado: {time.EstadioNome}.";
                }
                else
                {
                    TempData["Mensagem"] = "O estádio do clube já estava cadastrado.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AtualizarEstadio] Falha ao buscar estádio do time {Id} (IdApi={IdApi})", time.Id, time.IdApi);
                TempData["Mensagem"] = "Não foi possível consultar o estádio na api-football.";
            }

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

        // Uma movimentação de mercado do clube já normalizada (ver MontarMovimentos).
        private sealed class MovimentoTransferencia
        {
            public bool Chegada { get; init; }
            public DateTime Data { get; init; }
            public string Tipo { get; init; } = "";
            public int Peso { get; init; }
            public long? JogadorIdApi { get; init; }
            public string Jogador { get; init; } = "";
            public int? ClubeIdApi { get; init; }
            public string Clube { get; init; } = "";
            public string? ClubeLogo { get; init; }

            // Preenchido ao aplicar no banco (ver AplicarMovimentoAsync):
            // "aplicada" (mudou o clube agora), "ok" (o cadastro já refletia),
            // "sem-cadastro" / "sem-clube" (não dava para aplicar).
            public string Situacao { get; set; } = "ok";
            public string? Motivo { get; set; }
        }

        // POST: Times/TransferenciasApi (timeId, season, janela)
        // Chegadas e saídas do clube na temporada escolhida, vindas de
        // /transfers?team=X da api-football. A API não filtra por data: devolve o
        // histórico inteiro do clube (centenas de jogadores, desde os anos 90),
        // então a janela é recortada aqui.
        //
        //   janela=civil    → 01/jan a 31/dez da temporada (Brasil, Argentina, MLS…)
        //   janela=europeia → 01/jul da temporada a 30/jun da seguinte (Europa)
        //
        // A consulta também SINCRONIZA o cadastro: quem chegou e ainda não está no
        // clube é transferido para cá, quem saiu e ainda consta aqui vai para o
        // clube de destino (criado se não existir), e cada troca vira registro na
        // Janela de Transferências (/Transferencias). Rodar de novo não duplica
        // nada — o que já bate com a API só aparece para conferência. Por mexer no
        // banco é POST com antiforgery, não GET.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferenciasApi(int timeId, int season, string? janela = null)
        {
            var time = await _context.Times.FirstOrDefaultAsync(t => t.Id == timeId);
            if (time == null || time.IdApi == 0)
                return Json(new { erro = "Time não encontrado ou sem IdApi configurado." });
            if (time.EhSelecao)
                return Json(new { erro = "Seleções não têm janela de transferências — só clubes." });

            var europeia = string.Equals(janela, "europeia", StringComparison.OrdinalIgnoreCase);
            var inicio = europeia ? new DateTime(season, 7, 1) : new DateTime(season, 1, 1);
            var fim = europeia ? new DateTime(season + 1, 6, 30) : new DateTime(season, 12, 31);

            List<ControleFutebolWeb.Services.AfTransfersEntry> entradas;
            try
            {
                entradas = await _apiFootballService.BuscarTransferenciasTimeAsync(time.IdApi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Transferências] Falha ao consultar a API para o time {Id}.", timeId);
                return Json(new { erro = "Não foi possível consultar as transferências na API-Football." });
            }

            var movimentos = MontarMovimentos(entradas, time.IdApi, inicio, fim);

            // Jogadores do cadastro local envolvidos, pelo id da API.
            var idsApi = movimentos.Where(m => m.JogadorIdApi.HasValue)
                                   .Select(m => m.JogadorIdApi!.Value).Distinct().ToList();
            var jogadores = await _context.Jogadores
                .Include(j => j.Time)
                .Where(j => j.IdApi != null && idsApi.Contains(j.IdApi.Value))
                .ToListAsync();
            var jogadorPorIdApi = jogadores
                .GroupBy(j => j.IdApi!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            // Registros que já existem no histórico da janela (desta sincronização
            // ou da detecção pela escalação) — evita gravar o mesmo negócio duas vezes.
            var jogadorIds = jogadores.Select(j => j.Id).ToList();
            var inicioUtc = DateTime.SpecifyKind(inicio, DateTimeKind.Utc);
            var fimUtc = DateTime.SpecifyKind(fim.AddDays(1), DateTimeKind.Utc);
            var jaRegistradas = (await _context.Transferencias
                    .AsNoTracking()
                    .Where(t => jogadorIds.Contains(t.JogadorId) && t.Data >= inicioUtc && t.Data < fimUtc)
                    .Select(t => new { t.JogadorId, t.TimeDestinoId, t.Data })
                    .ToListAsync())
                .Select(t => (t.JogadorId, t.TimeDestinoId, t.Data.Date))
                .ToHashSet();

            var usuarioId = _userManager.GetUserId(User);
            int aplicadas = 0, clubesCriados = 0, jogadoresCriados = 0;

            // O cadastro é reconciliado com o ESTADO FINAL da janela, não com o
            // histórico refeito passo a passo. Um jogador emprestado e devolvido
            // dentro da mesma janela termina onde a última movimentação diz; se a
            // sincronização repetisse cada passo, a segunda busca partiria do
            // estado final e ficaria movendo o jogador de um lado para o outro a
            // cada execução. Vale a última movimentação de cada jogador — as
            // anteriores continuam na lista, só para conferência.
            foreach (var grupo in movimentos
                         .Where(m => m.JogadorIdApi.HasValue)
                         .GroupBy(m => m.JogadorIdApi!.Value))
            {
                var ordenadas = grupo
                    .OrderBy(m => m.Data)
                    // Chegada e saída no mesmo dia: a chegada é o estado final.
                    .ThenBy(m => m.Chegada ? 1 : 0)
                    .ToList();

                foreach (var anterior in ordenadas.Take(ordenadas.Count - 1))
                {
                    anterior.Situacao = "anterior";
                    anterior.Motivo = "movimentação anterior da janela — vale a última";
                }

                var (mudou, criouClube, criouJogador) = await AplicarMovimentoAsync(
                    ordenadas[^1], time, jogadorPorIdApi, jaRegistradas, usuarioId);
                if (mudou) aplicadas++;
                if (criouClube) clubesCriados++;
                if (criouJogador) jogadoresCriados++;
            }

            // Movimentação sem id de jogador na API: não há como identificar quem é.
            foreach (var m in movimentos.Where(m => !m.JogadorIdApi.HasValue))
            {
                m.Situacao = "sem-cadastro";
                m.Motivo = "movimentação sem id de jogador na API";
            }

            if (aplicadas > 0 || clubesCriados > 0 || jogadoresCriados > 0)
                await _context.SaveChangesAsync();

            object Montar(IEnumerable<MovimentoTransferencia> lista) =>
                lista.OrderByDescending(m => m.Data).Select(m =>
                {
                    var local = m.JogadorIdApi.HasValue &&
                                jogadorPorIdApi.TryGetValue(m.JogadorIdApi.Value, out var j) ? j : null;
                    return new
                    {
                        jogador = local?.NomeExibicao ?? m.Jogador,
                        jogadorId = local?.Id,
                        fotoUrl = local?.FotoUrl,
                        posicao = local?.Posicao,
                        clube = m.Clube,
                        clubeLogo = m.ClubeLogo,
                        tipo = m.Tipo,
                        data = m.Data.ToString("dd/MM/yyyy"),
                        situacao = m.Situacao,
                        motivo = m.Motivo
                    };
                }).ToList();

            return Json(new
            {
                janela = europeia ? "europeia" : "civil",
                periodo = $"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}",
                aplicadas,
                clubesCriados,
                jogadoresCriados,
                chegadas = Montar(movimentos.Where(m => m.Chegada)),
                saidas = Montar(movimentos.Where(m => !m.Chegada))
            });
        }

        // Achata a resposta de /transfers em movimentações do clube dentro da
        // janela, com o vocabulário traduzido e sem os registros repetidos.
        private static List<MovimentoTransferencia> MontarMovimentos(
            List<ControleFutebolWeb.Services.AfTransfersEntry> entradas,
            int idApiClube, DateTime inicio, DateTime fim)
        {
            // Vocabulário da API traduzido; valores em dinheiro ("€ 30M") passam direto.
            static string TraduzirTipo(string? tipo)
            {
                var t = (tipo ?? "").Trim();
                if (t.Length == 0 || t == "-" || t.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                    return "—";
                if (t.Equals("Loan", StringComparison.OrdinalIgnoreCase)) return "Empréstimo";
                if (t.Contains("Return from loan", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("Back from Loan", StringComparison.OrdinalIgnoreCase)) return "Volta de empréstimo";
                if (t.Contains("Free", StringComparison.OrdinalIgnoreCase)) return "Livre";
                if (t.Equals("Transfer", StringComparison.OrdinalIgnoreCase)) return "Transferência";
                if (t.Equals("Raise", StringComparison.OrdinalIgnoreCase)) return "Promoção da base";
                return t;
            }

            // Quanto o "type" informa: dinheiro > rótulo > "—". Entre registros
            // duplicados do mesmo negócio, fica o mais informativo.
            static int PesoTipo(string? tipo)
            {
                var t = (tipo ?? "").Trim();
                if (t.Length == 0 || t == "-" || t.Equals("N/A", StringComparison.OrdinalIgnoreCase)) return 0;
                if (t.Any(char.IsDigit)) return 2;
                return 1;
            }

            var itens = new List<MovimentoTransferencia>();

            foreach (var e in entradas)
            {
                foreach (var t in e.Transfers)
                {
                    if (!DateTime.TryParse(t.Date, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var data))
                        continue;
                    if (data < inicio || data > fim) continue;

                    var entrou = t.Teams.In;
                    var saiu = t.Teams.Out;
                    bool chegada = entrou?.Id == idApiClube;
                    bool saida = saiu?.Id == idApiClube;

                    // Nem uma ponta é este clube (não deveria acontecer) ou as duas
                    // são ("Raise" = subida da base, movimento interno): fora.
                    if (chegada == saida) continue;

                    var outroLado = chegada ? saiu : entrou;
                    var clube = outroLado?.Name?.Trim();

                    itens.Add(new MovimentoTransferencia
                    {
                        Chegada = chegada,
                        Data = data,
                        Tipo = TraduzirTipo(t.Type),
                        Peso = PesoTipo(t.Type),
                        JogadorIdApi = e.Player.Id,
                        Jogador = e.Player.Name?.Trim() ?? "—",
                        ClubeIdApi = outroLado?.Id,
                        Clube = string.IsNullOrWhiteSpace(clube) ? "—" : clube,
                        ClubeLogo = outroLado?.Logo
                    });
                }
            }

            // Um negócio por jogador + direção + clube da ponta: o registro mais
            // informativo e, no empate, o mais antigo (data do anúncio).
            return itens
                .GroupBy(i => (i.Chegada, i.JogadorIdApi, i.Jogador, Clube: i.Clube.ToLowerInvariant()))
                .Select(g => g.OrderByDescending(i => i.Peso).ThenBy(i => i.Data).First())
                .ToList();
        }

        // Deixa o cadastro local igual ao que a API diz sobre esta movimentação.
        // Não chama SaveChanges: quem orquestra salva uma vez só no fim.
        // Retorna se algo mudou e se um clube novo precisou ser criado.
        private async Task<(bool mudou, bool clubeCriado, bool jogadorCriado)> AplicarMovimentoAsync(
            MovimentoTransferencia m, Time clube,
            Dictionary<long, Jogador> jogadorPorIdApi,
            HashSet<(int JogadorId, int? TimeDestinoId, DateTime Dia)> jaRegistradas,
            string? usuarioId)
        {
            if (!m.JogadorIdApi.HasValue)
            {
                m.Situacao = "sem-cadastro";
                m.Motivo = "movimentação sem id de jogador na API";
                return (false, false, false);
            }

            bool clubeCriado = false, jogadorCriado = false;

            if (!jogadorPorIdApi.TryGetValue(m.JogadorIdApi.Value, out var jogador))
            {
                // Só cria em chegada: o reforço vai mesmo entrar em campo por este
                // clube e seria criado na próxima importação de jogo. Em saída, o
                // jogador que nunca esteve no cadastro não tem o que ser movido —
                // ele será criado pelo clube de destino, se esse clube for seguido.
                if (!m.Chegada)
                {
                    m.Situacao = "sem-cadastro";
                    m.Motivo = "jogador não cadastrado no sistema";
                    return (false, false, false);
                }

                // Nasce no clube de origem para que a chegada vire um registro de
                // verdade na Janela de Transferências ("veio de X"). Sem clube de
                // origem identificável, entra direto neste clube.
                var (origem, origemCriada) = await ResolverOuCriarClubeTransferenciaAsync(m);
                clubeCriado = origemCriada;

                var criado = await _apiFootballService.CriarJogadorDeTransferenciaAsync(
                    _context, m.JogadorIdApi.Value, m.Jogador, origem ?? clube);

                if (criado == null)
                {
                    m.Situacao = "sem-cadastro";
                    m.Motivo = "não foi possível criar o jogador a partir da API";
                    return (false, clubeCriado, false);
                }

                jogador = criado;
                jogadorCriado = true;
                jogadorPorIdApi[m.JogadorIdApi.Value] = jogador;

                // Criado já neste clube (sem origem conhecida): nada a transferir.
                if (jogador.TimeId == clube.Id)
                {
                    m.Situacao = "criado";
                    m.Motivo = "jogador criado a partir da API, já neste clube";
                    return (false, clubeCriado, true);
                }
            }

            // Chegada → destino é este clube; saída → destino é o clube da outra ponta.
            Time destino;

            if (m.Chegada)
            {
                if (jogador.TimeId == clube.Id) return (false, clubeCriado, jogadorCriado);   // já está aqui
                destino = clube;
            }
            else
            {
                if (jogador.TimeId != clube.Id) return (false, clubeCriado, jogadorCriado);   // já saiu daqui
                var (resolvido, criadoClube) = await ResolverOuCriarClubeTransferenciaAsync(m);
                if (resolvido == null)
                {
                    m.Situacao = "sem-clube";
                    m.Motivo = "clube de destino não identificado na API";
                    return (false, clubeCriado, jogadorCriado);
                }
                destino = resolvido;
                clubeCriado = clubeCriado || criadoClube;
            }

            // Se este negócio já está no histórico (detecção pela escalação, por
            // exemplo), o clube ainda é corrigido, mas sem duplicar o registro.
            var registrar = jaRegistradas.Add((jogador.Id, (int?)destino.Id, m.Data.Date));

            var origemId = jogador.TimeId;
            // O nome precisa ser lido antes da troca: a navegação Time continua
            // apontando para o clube antigo, mas só até alguém recarregar a entidade.
            // Jogador recém-criado vem sem a navegação carregada — busca pelo id.
            var origemNome = jogador.Time?.Nome
                ?? await _context.Times.Where(t => t.Id == origemId).Select(t => t.Nome).FirstOrDefaultAsync()
                ?? "clube anterior";

            if (registrar)
            {
                _context.Transferencias.Add(new Transferencia
                {
                    JogadorId = jogador.Id,
                    TimeOrigemId = origemId,
                    TimeDestinoId = destino.Id,
                    JogoId = null,
                    // A coluna é timestamptz: a data do anúncio entra como UTC.
                    Data = DateTime.SpecifyKind(m.Data, DateTimeKind.Utc),
                    // Preenchido (e não null como na detecção por escalação) para que
                    // quem rodou a sincronização possa desfazer em /Transferencias.
                    UsuarioId = usuarioId
                });
            }

            jogador.TimeId = destino.Id;
            // Voltou a ter clube: se estava marcado como aposentado, deixa de estar.
            jogador.Aposentado = false;
            jogador.AposentadoEm = null;

            m.Situacao = "aplicada";
            m.Motivo = (m.Chegada ? $"veio de {origemNome}" : $"foi para {destino.Nome}")
                + (jogadorCriado ? " · jogador criado a partir da API" : "");

            _logger.LogInformation(
                "[Transferências] {Jogador}: {Origem} → {Destino} (api-football, {Data:dd/MM/yyyy})",
                jogador.Nome, origemNome, destino.Nome, m.Data);

            return (true, clubeCriado, jogadorCriado);
        }

        // Clube da outra ponta da transferência: procura pelo id da API, depois
        // pelo nome, e cria se ainda não existir — clube de liga que ninguém
        // cadastrou nunca chegaria aqui pela importação de jogos.
        private async Task<(Time? clube, bool criado)> ResolverOuCriarClubeTransferenciaAsync(
            MovimentoTransferencia m)
        {
            if (m.ClubeIdApi is > 0)
            {
                var porIdApi = await _context.Times.FirstOrDefaultAsync(t => t.IdApi == m.ClubeIdApi);
                if (porIdApi != null) return (porIdApi, false);
            }

            var nome = ControleFutebolWeb.Services.ApiFootballService.TraduzirNomeClube(m.Clube.Trim());
            if (string.IsNullOrWhiteSpace(nome) || nome == "—") return (null, false);

            var porNome = await _context.Times
                .FirstOrDefaultAsync(t => t.Nome.ToLower() == nome.ToLower() && !t.EhSelecao);
            if (porNome != null)
            {
                // Aproveita para completar o id da API do clube já cadastrado.
                if (porNome.IdApi == 0 && m.ClubeIdApi is > 0) porNome.IdApi = m.ClubeIdApi.Value;
                return (porNome, false);
            }

            // Sem id da API não se cria clube: nesses registros ("Free agent",
            // "Return from loan" incompletos) a API costuma repetir o NOME DO
            // JOGADOR no lugar do clube — criar viraria lixo no cadastro.
            if (m.ClubeIdApi is not > 0) return (null, false);

            var formacaoPadrao = await _context.Formacoes.FirstOrDefaultAsync();
            if (formacaoPadrao == null) return (null, false);

            var novo = new Time
            {
                Nome = nome,
                IdApi = m.ClubeIdApi.Value,
                EscudoUrl = m.ClubeLogo ?? "",
                Cidade = "Importado",
                CorPrincipal = "#000000",
                CorSecundaria = "#FFFFFF",
                FormacaoPadraoId = formacaoPadrao.Id,
                EhSelecao = false
            };
            _context.Times.Add(novo);
            // Precisa do Id agora: a Transferencia deste mesmo movimento aponta para ele.
            await _context.SaveChangesAsync();

            _logger.LogInformation("[Transferências] Clube criado a partir da API: {Nome} (idApi {IdApi}).",
                novo.Nome, novo.IdApi);
            return (novo, true);
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
