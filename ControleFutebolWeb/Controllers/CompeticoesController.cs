using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFutebolWeb.Controllers
{
    public class CompeticoesController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<CompeticoesController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public CompeticoesController(
            FutebolContext context,
            ILogger<CompeticoesController> logger,
            IServiceScopeFactory scopeFactory,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarJogos(int id)
        {
            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            if (string.IsNullOrWhiteSpace(competicao.linktransfermarket))
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
                    if (ApiFootballService.IsApiFootballLink(competicao.linktransfermarket))
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

            // Injetar TopTier calculado por usuário via ViewBag
            ViewBag.TopTierIds = topTierIds;
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
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Detalhes(int id)
        {
            var competicao = _context.Competicoes
                .Include(c => c.Jogos).ThenInclude(j => j.TimeCasa)
                .Include(c => c.Jogos).ThenInclude(j => j.TimeVisitante)
                .FirstOrDefault(c => c.Id == id);

            if (competicao == null) return NotFound();

            var jogosRealizados = competicao.Jogos
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .OrderByDescending(j => j.Data)
                .ToList();

            var proximosJogos = competicao.Jogos
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
                Classificacao = competicao.Tipo != "MATA_MATA"
                    ? CalcularTabela(jogosRealizados)
                    : new(),
                Grupos = competicao.Tipo == "GRUPOS"
                    ? MontarGrupos(jogosRealizados)
                    : new(),
                FasesMataMata = competicao.Tipo == "MATA_MATA"
                    ? MontarMataMata(competicao.Jogos.ToList())
                    : new(),
            };

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
        public async Task<IActionResult> Create([Bind("Nome,Regiao,Tipo,EhSelecaoNacional,linktransfermarket")] Competicao competicao)
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
            return View(competicao);
        }

        // POST: Competicoes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,Nome,Regiao,Tipo,EhSelecaoNacional,linktransfermarket")] Competicao competicao)
        {
            if (id != competicao.Id) return NotFound();

            if (!ModelState.IsValid) return View(competicao);

            _context.Update(competicao);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        // Ação Index e Detalhes...

        private List<Classificacao> CalcularTabela(ICollection<Jogo> jogos)
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

            var lista = tabela.Values
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.Saldo)
                .ThenByDescending(t => t.GolsPro)
                .ToList();

            for (int i = 0; i < lista.Count; i++)
            {
                lista[i].Posicao = i + 1;
            }

            return lista;
        }

        private List<GrupoViewModel> MontarGrupos(ICollection<Jogo> jogos)
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
                var classificacao = CalcularTabela(jogosDoGrupo);

                grupos.Add(new GrupoViewModel
                {
                    Nome = nome,
                    Times = classificacao
                });
            }

            return grupos;
        }

        private List<FaseMataMataViewModel> MontarMataMata(List<Jogo> jogos)
        {
            // Ordena fases conhecidas; fases desconhecidas vão para o final
            var ordemFases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["32avos"]    = 1, ["Rodada 1"] = 1,
                ["16avos"]    = 2, ["Rodada 2"] = 2,
                ["Oitavas"]   = 3, ["Rodada 3"] = 3,
                ["Quartas"]   = 4, ["Rodada 4"] = 4,
                ["Semifinal"] = 5, ["Semi"]     = 5,
                ["Final"]     = 6,
            };

            var faseNomes = jogos
                .Select(j => j.Grupo ?? "Fase Única")
                .Distinct()
                .OrderBy(n => ordemFases.TryGetValue(n, out var o) ? o : 99)
                .ToList();

            var resultado = new List<FaseMataMataViewModel>();

            foreach (var fase in faseNomes)
            {
                var jogosFase = jogos.Where(j => (j.Grupo ?? "Fase Única") == fase).ToList();

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

            competicao.linktransfermarket = linkCompeticao;

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
    }
}
