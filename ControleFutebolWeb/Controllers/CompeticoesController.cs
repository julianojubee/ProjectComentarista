using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ControleFutebolWeb.Controllers
{
    public class CompeticoesController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<CompeticoesController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;

        public CompeticoesController(
            FutebolContext context,
            ILogger<CompeticoesController> logger,
            IServiceScopeFactory scopeFactory,
            UserManager<ApplicationUser> userManager,
            IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _userManager = userManager;
            _cache = cache;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarJogos(int id)
        {
            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            if (string.IsNullOrWhiteSpace(competicao.LinkTransfermarket))
            {
                TempData["Erro"] = "Configure o link da competição antes de buscar jogos.";
                return RedirectToAction(nameof(Index));
            }

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var ctx    = scope.ServiceProvider.GetRequiredService<FutebolContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<CompeticoesController>>();

                try
                {
                    if (ApiFootballService.IsApiFootballLink(competicao.LinkTransfermarket))
                    {
                        var api = scope.ServiceProvider.GetRequiredService<ApiFootballService>();
                        var (jogos, times, erros, avisos) =
                            await api.SincronizarCompeticaoAsync(ctx, competicao);
                        logger.LogInformation(
                            "[BuscarJogos] {Nome}: {J} jogos, {T} times criados, {E} erros.",
                            competicao.Nome, jogos, times, erros);
                    }
                    else
                    {
                        logger.LogWarning(
                            "[BuscarJogos] {Nome}: link não é formato apifoot: — configure o link no formato apifoot:LEAGUE_ID:SEASON.",
                            competicao.Nome);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[BuscarJogos] Erro ao sincronizar {Nome}.", competicao.Nome);
                }
            });

            TempData["Sucesso"] = $"Busca de jogos de '{competicao.Nome}' iniciada em segundo plano.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User)!;
            var topTierIds = await _context.CompeticoesTopTierUsuario
                .Where(t => t.UsuarioId == uid)
                .Select(t => t.CompeticaoId)
                .ToHashSetAsync();

            var competicoes = await _context.Competicoes
                .OrderBy(c => c.Nome)
                .ToListAsync();

            // Contagem de jogos e times distintos por competição (para os cards da tela).
            var jogosPorCompeticao = await _context.Jogos
                .GroupBy(j => j.CompeticaoId)
                .Select(g => new { CompeticaoId = g.Key, Jogos = g.Count() })
                .ToDictionaryAsync(g => g.CompeticaoId, g => g.Jogos);

            var timesPorCompeticao = await _context.Jogos
                .Select(j => new { j.CompeticaoId, j.TimeCasaId, j.TimeVisitanteId })
                .ToListAsync();
            var timesDict = timesPorCompeticao
                .SelectMany(j => new[] { (j.CompeticaoId, TimeId: j.TimeCasaId), (j.CompeticaoId, TimeId: j.TimeVisitanteId) })
                .GroupBy(x => x.CompeticaoId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TimeId).Distinct().Count());

            // Injetar TopTier calculado por usuário via ViewBag
            ViewBag.TopTierIds = topTierIds;
            ViewBag.JogosPorCompeticao = jogosPorCompeticao;
            ViewBag.TimesPorCompeticao = timesDict;
            return View(competicoes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTopTier(int id)
        {
            var uid = _userManager.GetUserId(User)!;
            var registro = await _context.CompeticoesTopTierUsuario
                .FirstOrDefaultAsync(t => t.CompeticaoId == id && t.UsuarioId == uid);

            if (registro == null)
                _context.CompeticoesTopTierUsuario.Add(new CompeticaoTopTierUsuario { CompeticaoId = id, UsuarioId = uid });
            else
                _context.CompeticoesTopTierUsuario.Remove(registro);

            await _context.SaveChangesAsync();
            _cache.Remove($"layout-menu:{uid}"); // menu do layout muda → invalida o cache
            return RedirectToAction(nameof(Index));
        }

        // GET: Competicoes/EquipeDaRodada/5?temporada=2025&rodada=22
        // Os melhores por setor numa rodada, montados num 4-3-3. A nota é a mesma
        // que o resto do sistema usa: a manual do usuário quando existe, senão a
        // calculada pelos critérios dele em cima da estatística importada.
        public async Task<IActionResult> EquipeDaRodada(int id, int? temporada = null, int? rodada = null)
        {
            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            var usuarioId = _userManager.GetUserId(User);

            var temporadas = await _context.Jogos.AsNoTracking()
                .Where(j => j.CompeticaoId == id)
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t).ToListAsync();
            int? temporadaSel = temporada ?? (temporadas.Any() ? temporadas.First() : (int?)null);

            // Só rodadas com jogo terminado: oferecer uma rodada futura devolveria
            // uma seleção vazia sem explicar o motivo.
            var rodadas = await _context.Jogos.AsNoTracking()
                .Where(j => j.CompeticaoId == id && j.Rodada > 0
                         && (temporadaSel == null || j.Temporada == temporadaSel)
                         && j.PlacarCasa != null && j.PlacarVisitante != null)
                .Select(j => j.Rodada).Distinct()
                .OrderByDescending(r => r).ToListAsync();

            var vm = new EquipeDaRodadaViewModel
            {
                Competicao = competicao,
                Temporada = temporadaSel,
                TemporadasDisponiveis = temporadas,
                RodadasDisponiveis = rodadas,
                Rodada = rodada ?? (rodadas.Any() ? rodadas.First() : 0)
            };
            if (vm.Rodada == 0) return View(vm);

            var jogos = await _context.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa).Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == id && j.Rodada == vm.Rodada
                         && (temporadaSel == null || j.Temporada == temporadaSel)
                         && j.PlacarCasa != null && j.PlacarVisitante != null)
                .ToListAsync();
            vm.JogosNaRodada = jogos.Count;
            if (jogos.Count == 0) return View(vm);

            var jogoIds = jogos.Select(j => j.Id).ToHashSet();

            // Mesma régua de nota do resto do sistema (ver JogosPreJogo/Analisar):
            // peso inicial do usuário + ações, limitado entre esse peso e 10.
            var criterios = CriteriosNotaHelper.MergeCriterios(
                await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync(),
                await _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToListAsync());
            var notaBase = CriteriosNotaHelper.NotaBase(criterios);

            // Minutos e goleiro decisivo por (jogador, jogo) — os ajustes de
            // participação curta e de 100% de defesas dependem deles.
            var contextos = await ContextoNotaHelper.CarregarAsync(_context, jogoIds.ToList(), usuarioId);

            // Detalhes: o piso de merecimento conta os chips verdes x vermelhos.
            double NotaFinal(double valor, int jogadorId, int jogoId, IEnumerable<Notadetalhe>? detalhes) =>
                CriteriosNotaHelper.NotaFinalComBase(valor, notaBase,
                    ContextoNotaHelper.De(contextos, jogadorId, jogoId) with
                    {
                        Acoes = CriteriosNotaHelper.ContarAcoes(detalhes)
                    }, 1);

            var notasManuais = await _context.Notas.AsNoTracking()
                .Include(n => n.Detalhes)
                .Where(n => jogoIds.Contains(n.JogoId) && n.UsuarioId == usuarioId)
                .ToListAsync();

            // (jogador, jogo) -> (nota, veio de cálculo). A manual sempre vence.
            var notaPorChave = notasManuais.ToDictionary(
                n => (n.JogadorId, n.JogoId),
                n => (Nota: NotaFinal(n.Valor, n.JogadorId, n.JogoId, n.Detalhes), Automatica: false));

            // Minutos > 0 exclui o reserva que a api-football relaciona sem entrar:
            // ele receberia a nota base e disputaria vaga com quem jogou 90.
            var estatisticas = await _context.EstatisticasJogador.AsNoTracking()
                .Include(e => e.Jogo).Include(e => e.Jogador)
                .Where(e => jogoIds.Contains(e.JogoId) && e.Minutos > 0)
                .ToListAsync();

            if (estatisticas.Count > 0)
            {
                var lados = await LadoJogadorHelper.CarregarAsync(_context, jogoIds.ToList(), usuarioId);

                // Motor da nota automática escolhido em /CriteriosNota.
                var calculadora = await NotaAutomaticaHelper.CarregarAsync(
                    _context, jogoIds.ToList(), usuarioId, criterios, lados, contextos);

                foreach (var e in estatisticas)
                {
                    var chave = (e.JogadorId, e.JogoId);
                    if (notaPorChave.ContainsKey(chave)) continue;
                    // 1 casa: a régua desta tela sempre exibiu assim.
                    notaPorChave[chave] = (Math.Round(calculadora.De(e).Nota, 1), true);
                }
            }

            vm.JogadoresAvaliados = notaPorChave.Count;
            if (notaPorChave.Count == 0) return View(vm);

            var jogadorIds = notaPorChave.Keys.Select(k => k.Item1).Distinct().ToList();
            var jogadores = await _context.Jogadores.AsNoTracking()
                .Include(j => j.Time).Include(j => j.Selecao)
                .Where(j => jogadorIds.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id);

            var jogoPorId = jogos.ToDictionary(j => j.Id);
            var lado = await LadoJogadorHelper.CarregarAsync(_context, jogoIds.ToList(), usuarioId);

            var golsRodada = await _context.Gols.AsNoTracking()
                .Where(g => jogoIds.Contains(g.JogoId) && !g.Contra)
                .ToListAsync();
            var assistRodada = await _context.Assistencias.AsNoTracking()
                .Where(a => jogoIds.Contains(a.JogoId))
                .ToListAsync();

            var candidatos = new List<(string Setor, JogadorDaRodada Item)>();
            foreach (var ((jogadorId, jogoId), nota) in notaPorChave)
            {
                if (!jogadores.TryGetValue(jogadorId, out var jogador)) continue;
                if (!jogoPorId.TryGetValue(jogoId, out var jogo)) continue;

                var setor = PosicaoJogadorHelper.Setor(jogador.Posicao);
                // Sem posição reconhecível não dá para dizer em que vaga ele entra;
                // ficar de fora é melhor que ocupar a vaga errada na escalação.
                if (setor == null) continue;

                bool? emCasa = lado.TryGetValue((jogadorId, jogoId), out var atuacao) ? atuacao.IsTimeCasa : null;
                var adversario = emCasa switch
                {
                    true => jogo.TimeVisitante?.Nome,
                    false => jogo.TimeCasa?.Nome,
                    _ => $"{jogo.TimeCasa?.Nome} × {jogo.TimeVisitante?.Nome}"
                };

                candidatos.Add((setor, new JogadorDaRodada
                {
                    Jogador = jogador,
                    Time = emCasa == true ? jogo.TimeCasa : emCasa == false ? jogo.TimeVisitante : jogador.Time,
                    JogoId = jogoId,
                    Nota = nota.Nota,
                    NotaAutomatica = nota.Automatica,
                    Adversario = adversario ?? "",
                    Placar = $"{jogo.PlacarCasa}×{jogo.PlacarVisitante}",
                    Gols = golsRodada.Count(g => g.JogadorId == jogadorId && g.JogoId == jogoId),
                    Assistencias = assistRodada.Count(a => a.JogadorId == jogadorId && a.JogoId == jogoId)
                }));
            }

            // 4-3-3. Um jogador só ocupa uma vaga mesmo que tenha jogado duas vezes
            // na rodada (jogo adiado da rodada anterior, por exemplo): fica com a
            // melhor nota das duas.
            List<JogadorDaRodada> Melhores(string setor, int quantos) => candidatos
                .Where(c => c.Setor == setor)
                .GroupBy(c => c.Item.Jogador.Id)
                .Select(g => g.OrderByDescending(x => x.Item.Nota).First().Item)
                .OrderByDescending(i => i.Nota)
                .ThenByDescending(i => i.Gols + i.Assistencias)
                .Take(quantos)
                .ToList();

            vm.Goleiros = Melhores("GOL", 1);
            vm.Defensores = Melhores("DEF", 4);
            vm.MeioCampo = Melhores("MEI", 3);
            vm.Atacantes = Melhores("ATA", 3);

            return View(vm);
        }

        // GET: Competicoes/Estatisticas/5?temporada=2025
        // Painel de estatísticas da competição em tela cheia. A aba "Estatísticas"
        // de Detalhes mostra o mesmo painel; esta rota existe para abrir só ele,
        // com URL própria, sem o resto da tela em volta.
        public IActionResult Estatisticas(int id, int? temporada = null)
        {
            var competicao = _context.Competicoes
                .Include(c => c.Jogos).ThenInclude(j => j.TimeCasa)
                .Include(c => c.Jogos).ThenInclude(j => j.TimeVisitante)
                .FirstOrDefault(c => c.Id == id);

            if (competicao == null) return NotFound();

            var temporadasDisponiveis = competicao.Jogos
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t).ToList();
            int? temporadaSel = temporada
                ?? (temporadasDisponiveis.Any() ? temporadasDisponiveis.First() : (int?)null);

            var jogosRealizados = competicao.Jogos
                .Where(j => temporadaSel == null || j.Temporada == temporadaSel)
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            var jogoIds = jogosRealizados.Select(j => j.Id).ToHashSet();
            var gols = _context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => jogoIds.Contains(g.JogoId))
                .ToList();
            var cartoes = _context.Cartoes.AsNoTracking()
                .Include(c => c.Jogador)
                .Where(c => jogoIds.Contains(c.JogoId))
                .ToList();

            ViewBag.Competicao = competicao;
            ViewBag.Temporada = temporadaSel;
            ViewBag.TemporadasDisponiveis = temporadasDisponiveis;
            ViewBag.TotalJogos = jogosRealizados.Count;

            return View(EstatisticaTimeCalculator.Calcular(jogosRealizados, gols, cartoes));
        }

        public IActionResult Detalhes(int id, int? temporada = null)
        {
            var competicao = _context.Competicoes
                .Include(c => c.Jogos).ThenInclude(j => j.TimeCasa)
                .Include(c => c.Jogos).ThenInclude(j => j.TimeVisitante)
                .FirstOrDefault(c => c.Id == id);

            if (competicao == null) return NotFound();

            // Temporadas disponíveis; padrão = a mais recente
            var temporadasDisponiveis = competicao.Jogos
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t).ToList();
            int? temporadaSel = temporada
                ?? (temporadasDisponiveis.Any() ? temporadasDisponiveis.First() : (int?)null);
            ViewBag.Temporada = temporadaSel;
            ViewBag.TemporadasDisponiveis = temporadasDisponiveis;

            var jogosDaTemporada = competicao.Jogos
                .Where(j => temporadaSel == null || j.Temporada == temporadaSel)
                .ToList();

            var jogosRealizados = jogosDaTemporada
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .OrderByDescending(j => j.Data)
                .ToList();

            var proximosJogos = jogosDaTemporada
                .Where(j => !j.PlacarCasa.HasValue)
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();

            var vm = new CompeticaoDetalhesViewModel
            {
                Competicao = competicao,
                Tipo = competicao.Tipo,
                ProximosJogos = proximosJogos,
                JogosRealizados = jogosRealizados,
            };

            // ── Critérios de desempate da competição (Competicoes/Edit) ───────
            // Os cartões são carregados antes da tabela porque os critérios
            // "menos vermelhos/amarelos" e "fair play" dependem deles.
            var criterios = CriteriosDesempateHelper.Parse(competicao.CriteriosDesempate);
            var jogoIdsRealizados = jogosRealizados.Select(j => j.Id).ToHashSet();
            var cartoes = _context.Cartoes.AsNoTracking()
                .Include(c => c.Jogador)
                .Where(c => jogoIdsRealizados.Contains(c.JogoId))
                .ToList();
            ViewBag.CriteriosDesempate = criterios;

            var fasesDeclaradas = _context.CompeticaoFases
                .Where(f => f.CompeticaoId == id)
                .OrderBy(f => f.Ordem).ThenBy(f => f.Id)
                .ToList();

            if (fasesDeclaradas.Any())
            {
                // Fases declaradas pelo usuário (ex.: pontos corridos + playoffs):
                // distribui os jogos entre elas e monta a visualização do tipo de cada uma.
                var jogosPorFase = FaseJogoClassifier.DistribuirPorFases(fasesDeclaradas, jogosDaTemporada);

                vm.Fases = fasesDeclaradas.Select(fase =>
                {
                    var jogosFase = jogosPorFase[fase.Id];
                    var realizadosFase = jogosFase
                        .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                        .ToList();

                    return new FaseDetalheViewModel
                    {
                        Fase = fase,
                        Classificacao = fase.Tipo == "PONTOS_CORRIDOS" ? CalcularTabela(realizadosFase, criterios, cartoes) : new(),
                        Grupos = fase.Tipo == "GRUPOS" ? MontarGrupos(realizadosFase, criterios, cartoes) : new(),
                        FasesMataMata = fase.Tipo is "MATA_MATA" or "JOGO_UNICO"
                            ? MontarMataMata(jogosFase, fase.Tipo == "JOGO_UNICO")
                            : new(),
                    };
                }).ToList();
            }
            else if (competicao.Tipo == "MATA_MATA" || competicao.Tipo == "JOGO_UNICO")
            {
                // JOGO_UNICO: a competição é decidida numa partida só (Supercopa da UEFA,
                // Recopa...). O confronto é montado como um chaveamento de uma chave só e
                // o vencedor da partida é anunciado como campeão, não como "classificado".
                vm.FasesMataMata = MontarMataMata(jogosDaTemporada, competicao.Tipo == "JOGO_UNICO");
            }
            else if (competicao.Tipo == "GRUPOS")
            {
                // Competições "GRUPOS" às vezes têm, na mesma temporada, uma fase eliminatória
                // depois da fase de grupos (ex.: Sul-Americana → grupos + oitavas/quartas/semi/final).
                // A api-football rotula essas fases com nomes que não começam com "Group"/"Grupo"
                // (ex.: "Round of 16", "Quarterfinals", "Qualification Round 1"), então separamos
                // esses jogos para montar o chaveamento em vez de tratá-los como um "grupo" de pontos corridos.
                var jogosFaseGruposRealizados = jogosRealizados.Where(j => EhNomeDeGrupo(j.Grupo)).ToList();
                var jogosFaseEliminatoria = jogosDaTemporada
                    .Where(j => !string.IsNullOrEmpty(j.Grupo) && !EhNomeDeGrupo(j.Grupo))
                    .ToList();

                vm.Grupos = MontarGrupos(jogosFaseGruposRealizados, criterios, cartoes);
                vm.FasesMataMata = MontarMataMata(jogosFaseEliminatoria);
            }
            else
            {
                // Pontos corridos: jogos de playoff/eliminatória (round tipo "Quarterfinals")
                // não entram na tabela — viram um chaveamento à parte, como já ocorre em GRUPOS.
                var jogosEliminatorios = jogosDaTemporada
                    .Where(j => FaseJogoClassifier.Classificar(j.Grupo) == FaseCategoria.MataMata)
                    .ToList();

                vm.Classificacao = CalcularTabela(jogosRealizados
                    .Where(j => FaseJogoClassifier.Classificar(j.Grupo) != FaseCategoria.MataMata)
                    .ToList(), criterios, cartoes);
                vm.FasesMataMata = jogosEliminatorios.Any() ? MontarMataMata(jogosEliminatorios) : new();
            }

            // ── Stats do hero (times, jogos, gols) ────────────────────────────
            ViewBag.TotalTimes = jogosDaTemporada
                .SelectMany(j => new[] { j.TimeCasaId, j.TimeVisitanteId })
                .Distinct()
                .Count();
            ViewBag.TotalGols = jogosRealizados.Sum(j => (j.PlacarCasa ?? 0) + (j.PlacarVisitante ?? 0));

            // ── Artilheiros (top 5 goleadores da competição/temporada) ────────
            var topScorers = _context.Gols
                .Where(g => !g.Contra && g.Jogo.CompeticaoId == id && (temporadaSel == null || g.Jogo.Temporada == temporadaSel))
                .GroupBy(g => g.JogadorId)
                .Select(gr => new { JogadorId = gr.Key, Gols = gr.Count() })
                .OrderByDescending(x => x.Gols)
                .Take(5)
                .ToList();

            var jogadorIdsArtilheiros = topScorers.Select(t => t.JogadorId).ToList();
            var jogadoresArtilheiros = _context.Jogadores
                .Include(j => j.Time)
                .Where(j => jogadorIdsArtilheiros.Contains(j.Id))
                .ToDictionary(j => j.Id);

            ViewBag.Artilheiros = topScorers
                .Where(t => jogadoresArtilheiros.ContainsKey(t.JogadorId))
                .Select(t => new ArtilheiroViewModel { Jogador = jogadoresArtilheiros[t.JogadorId], Gols = t.Gols })
                .ToList();

            // ── Aba "Estatísticas" ─────────────────────────────────────────────
            var golsEstat = _context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => jogoIdsRealizados.Contains(g.JogoId))
                .ToList();
            ViewBag.EstatisticasTimes = EstatisticaTimeCalculator.Calcular(jogosRealizados, golsEstat, cartoes);

            return View(vm);
        }

        // GET: Competicoes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Competicoes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nome,Regiao,Tipo,EhSelecaoNacional,LinkTransfermarket")] Competicao competicao)
        {
            _logger.LogInformation("POST Create chamado: Nome={Nome}, Regiao={Regiao}, Tipo={Tipo}",
                competicao.Nome, competicao.Regiao, competicao.Tipo);

            if (!ModelState.IsValid)
            {
                foreach (var erro in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("Erro de validação: {Erro}", erro.ErrorMessage);
                }
                return View(competicao);
            }

            _context.Add(competicao);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Competição salva com sucesso no banco: Id={Id}", competicao.Id);

            return RedirectToAction(nameof(Index));
        }

        // GET: Competicoes/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            ViewBag.Fases = await _context.CompeticaoFases
                .Where(f => f.CompeticaoId == id)
                .OrderBy(f => f.Ordem).ThenBy(f => f.Id)
                .ToListAsync();

            ViewBag.CriteriosDesempate = CriteriosDesempateHelper.Parse(competicao.CriteriosDesempate);

            return View(competicao);
        }

        // POST: Competicoes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,Nome,Regiao,Tipo,EhSelecaoNacional,LinkTransfermarket,IdApi")] Competicao competicao,
            List<string>? criterios = null)
        {
            if (id != competicao.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.CriteriosDesempate = CriteriosDesempateHelper.Parse(string.Join(';', criterios ?? new()));
                return View(competicao);
            }

            // Atualiza só os campos do formulário para não apagar
            // TopTier, LogoUrl e demais colunas que não estão na tela.
            var existente = await _context.Competicoes.FindAsync(id);
            if (existente == null) return NotFound();

            existente.Nome = competicao.Nome;
            existente.Regiao = competicao.Regiao;
            existente.Tipo = competicao.Tipo;
            existente.EhSelecaoNacional = competicao.EhSelecaoNacional;
            existente.LinkTransfermarket = competicao.LinkTransfermarket;
            existente.IdApi = competicao.IdApi;
            existente.CriteriosDesempate = CriteriosDesempateHelper.Serializar(criterios);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Linha do formulário de fases (Edit) — não é entidade, só transporte do POST.
        public class CompeticaoFaseInput
        {
            public string? Nome { get; set; }
            public string? Tipo { get; set; }
            public string? RoundsPattern { get; set; }
        }

        // POST: Competicoes/SalvarFases/5 — substitui todas as fases declaradas da
        // competição pelas recebidas (replace-all: nada mais referencia a fase, os jogos
        // são associados em tempo de leitura pelo FaseJogoClassifier).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarFases(int id, List<CompeticaoFaseInput> fases)
        {
            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            var atuais = _context.CompeticaoFases.Where(f => f.CompeticaoId == id);
            _context.CompeticaoFases.RemoveRange(atuais);

            var validas = (fases ?? new())
                .Where(f => !string.IsNullOrWhiteSpace(f.Nome) && !string.IsNullOrWhiteSpace(f.Tipo))
                .ToList();

            for (int i = 0; i < validas.Count; i++)
            {
                _context.CompeticaoFases.Add(new CompeticaoFase
                {
                    CompeticaoId = id,
                    Nome = validas[i].Nome!.Trim(),
                    Tipo = validas[i].Tipo!.Trim(),
                    Ordem = i + 1,
                    RoundsPattern = string.IsNullOrWhiteSpace(validas[i].RoundsPattern)
                        ? null
                        : validas[i].RoundsPattern!.Trim(),
                });
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = validas.Count > 0
                ? $"Fases da competição salvas ({validas.Count})."
                : "Fases removidas — a competição volta a usar apenas o Tipo.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        // Ação Index e Detalhes...

        private List<Classificacao> CalcularTabela(
            ICollection<Jogo> jogos,
            IReadOnlyList<string>? criterios = null,
            IEnumerable<Cartao>? cartoes = null)
        {
            var tabela = new Dictionary<int, Classificacao>();

            foreach (var jogo in jogos)
            {
                // Garantir que os times existam na tabela
                if (!tabela.ContainsKey(jogo.TimeCasaId))
                    tabela[jogo.TimeCasaId] = new Classificacao { TimeId = jogo.TimeCasaId, Time = jogo.TimeCasa };

                if (!tabela.ContainsKey(jogo.TimeVisitanteId))
                    tabela[jogo.TimeVisitanteId] = new Classificacao { TimeId = jogo.TimeVisitanteId, Time = jogo.TimeVisitante };
                var casa = tabela[jogo.TimeCasaId];
                var visitante = tabela[jogo.TimeVisitanteId];

                casa.Jogos++;
                visitante.Jogos++;

                casa.GolsPro += (int)jogo.PlacarCasa;
                casa.GolsContra += (int)jogo.PlacarVisitante;
                visitante.GolsPro += (int)jogo.PlacarVisitante;
                visitante.GolsContra += (int)jogo.PlacarCasa;

                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                {
                    casa.Vitorias++;
                    casa.Pontos += 3;
                    visitante.Derrotas++;
                }
                else if (jogo.PlacarCasa < jogo.PlacarVisitante)
                {
                    visitante.Vitorias++;
                    visitante.Pontos += 3;
                    casa.Derrotas++;
                }
                else
                {
                    casa.Empates++;
                    visitante.Empates++;
                    casa.Pontos++;
                    visitante.Pontos++;
                }
            }

            foreach (var item in tabela.Values)
            {
                item.Saldo = item.GolsPro - item.GolsContra;
            }

            // Critérios de desempate cadastrados na competição (Competicoes/Edit).
            return CriteriosDesempateHelper.Ordenar(
                tabela.Values,
                criterios ?? CriteriosDesempateHelper.Padrao,
                DadosDesempate.Construir(jogos, cartoes));
        }

        // Distingue uma "fase de grupos" real (pontos corridos, rotulada "Group X"/"Grupo X")
        // de uma fase eliminatória (mata-mata) que pode existir na mesma competição/temporada
        // — a api-football nomeia fases eliminatórias como "Round of 16", "Quarterfinals",
        // "Qualification Round 1" etc., que não devem virar uma tabela de pontos corridos.
        private static bool EhNomeDeGrupo(string? nomeGrupo)
            => FaseJogoClassifier.Classificar(nomeGrupo) == FaseCategoria.Grupos;

        private List<GrupoViewModel> MontarGrupos(
            ICollection<Jogo> jogos,
            IReadOnlyList<string>? criterios = null,
            IEnumerable<Cartao>? cartoes = null)
        {
            var grupos = new List<GrupoViewModel>();

            // supondo que cada jogo tenha uma propriedade "Grupo" (string)
            var nomesGrupos = jogos
                .Where(j => !string.IsNullOrEmpty(j.Grupo)) // só pega jogos com grupo definido
                .Select(j => j.Grupo)
                .Distinct()
                .ToList();

            foreach (var nome in nomesGrupos)
            {
                var jogosDoGrupo = jogos.Where(j => j.Grupo == nome).ToList();
                var classificacao = CalcularTabela(jogosDoGrupo, criterios, cartoes);

                grupos.Add(new GrupoViewModel
                {
                    Nome = nome,
                    Times = classificacao
                });
            }

            return grupos;
        }

        // decideTitulo = a fase decide o título (competição/fase de JOGO_UNICO):
        // o vencedor é anunciado como campeão em vez de "classificado".
        private List<FaseMataMataViewModel> MontarMataMata(List<Jogo> jogos, bool decideTitulo = false)
        {
            // Ordena fases conhecidas; fases desconhecidas vão para o final
            var ordemFases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Qualification Round 1"] = -3, ["Preliminary Round"] = -3,
                ["Qualification Round 2"] = -2,
                ["Qualification Round 3"] = -1,
                ["32avos"]      = 1, ["Rodada 1"]    = 1, ["Round of 32"] = 1,
                ["16avos"]      = 2, ["Rodada 2"]    = 2, ["Round of 16"] = 2,
                ["Oitavas"]     = 3, ["Rodada 3"]    = 3,
                ["Quartas"]     = 4, ["Rodada 4"]    = 4, ["Quarterfinals"] = 4,
                ["Semifinal"]   = 5, ["Semi"]        = 5, ["Semifinals"]    = 5,
                ["Final"]       = 6,
                ["3rd Place Final"] = 7,
            };

            // Jogo sem round vindo da API: numa competição de jogo único a partida É a final.
            var nomePadrao = decideTitulo ? "Final" : "Fase Única";
            string NomeFaseDe(Jogo j) => string.IsNullOrWhiteSpace(j.Grupo) ? nomePadrao : j.Grupo;

            var faseNomes = jogos
                .Select(NomeFaseDe)
                .Distinct()
                .OrderBy(n => ordemFases.TryGetValue(n, out var o) ? o : 99)
                .ToList();

            var resultado = new List<FaseMataMataViewModel>();

            foreach (var fase in faseNomes)
            {
                var jogosFase = jogos.Where(j => NomeFaseDe(j) == fase).ToList();

                // Agrupa pares de times (ida e volta) pelo par de IDs ordenado
                var pares = jogosFase
                    .GroupBy(j => string.Join("-",
                        new[] { j.TimeCasaId, j.TimeVisitanteId }.OrderBy(x => x)))
                    .ToList();

                var confrontos = new List<ConfrontoViewModel>();
                foreach (var par in pares)
                {
                    var lista = par.OrderBy(j => j.Data).ToList();
                    var ida   = lista.FirstOrDefault();
                    var volta = lista.Count > 1 ? lista[1] : null;

                    confrontos.Add(new ConfrontoViewModel
                    {
                        JogoIda   = ida,
                        JogoVolta = volta,
                        TimeA     = ida?.TimeCasa,
                        TimeB     = ida?.TimeVisitante,
                    });
                }

                resultado.Add(new FaseMataMataViewModel
                {
                    Nome      = fase,
                    Ordem     = ordemFases.TryGetValue(fase, out var ord) ? ord : 99,
                    Confrontos = confrontos,
                    DecideTitulo = decideTitulo,
                });
            }

            return resultado;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarLinkCompeticao(int id, string linkCompeticao)
        {
            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            competicao.LinkTransfermarket = linkCompeticao;

            _context.Update(competicao);
            await _context.SaveChangesAsync();

            TempData["Mensagem"] = "Link da competição atualizado com sucesso!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarLogo(int id, string? logoUrl)
        {
            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            competicao.LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();

            _context.Update(competicao);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Escudo da competição atualizado.";
            return RedirectToAction(nameof(Index));
        }

        // Catálogo das ligas disponíveis na api-football para a temporada 2026
        // (dump estático em wwwroot/data/competicoes-api-2026.json), agrupadas
        // por país. Mostra nome + código (id da liga na API) para o usuário
        // registrar a competição (campo IdApi) e poder buscar os jogos.
        [HttpGet]
        public async Task<IActionResult> CompeticoesApi()
        {
            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var caminho = Path.Combine(env.WebRootPath, "data", "competicoes-api-2026.json");
            if (!System.IO.File.Exists(caminho))
                return NotFound("Arquivo de competições da API não encontrado.");

            var json = await System.IO.File.ReadAllTextAsync(caminho);
            var itens = System.Text.Json.JsonSerializer.Deserialize<List<CompeticaoApiLiga>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            // Marca as ligas que já têm competição cadastrada apontando pra elas
            var idsRegistrados = (await _context.Competicoes
                .Where(c => c.IdApi != null)
                .Select(c => c.IdApi!.Value)
                .ToListAsync()).ToHashSet();
            foreach (var item in itens)
                item.Registrada = idsRegistrados.Contains(item.Id);

            var vm = new CompeticoesApiViewModel
            {
                Total = itens.Count,
                TotalRegistradas = itens.Count(i => i.Registrada),
                Paises = itens
                    .GroupBy(i => i.Pais)
                    .Select(g => new CompeticoesApiPais
                    {
                        Nome = g.Key,
                        Bandeira = g.First().Bandeira,
                        Competicoes = g.OrderBy(i => i.Nome).ToList()
                    })
                    // "World" (internacionais) primeiro, depois países em ordem alfabética
                    .OrderBy(p => p.Nome == "World" ? 0 : 1)
                    .ThenBy(p => p.Nome)
                    .ToList()
            };

            return View(vm);
        }
    }
}
