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
        private readonly CatalogoLigasApi _catalogoLigas;
        private readonly IWebHostEnvironment _env;
        private readonly FifaService _fifa;

        public CompeticoesController(
            FutebolContext context,
            ILogger<CompeticoesController> logger,
            IServiceScopeFactory scopeFactory,
            UserManager<ApplicationUser> userManager,
            IMemoryCache cache,
            CatalogoLigasApi catalogoLigas,
            IWebHostEnvironment env,
            FifaService fifa)
        {
            _context = context;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _userManager = userManager;
            _cache = cache;
            _catalogoLigas = catalogoLigas;
            _env = env;
            _fifa = fifa;
        }

        /// <summary>
        /// Confere o link "apifoot:LEAGUE_ID:SEASON" contra o catálogo da api-football e,
        /// quando o código existe, grava IdApi e adota o escudo publicado pela API (evita
        /// subir um logo por competição). Devolve false com erro no ModelState quando o
        /// link está malformado ou o código não existe nem no dump local nem na API.
        ///
        /// Mesma regra usada em massa por <see cref="SincronizarEscudos"/>.
        ///
        /// Link vazio ou de outra fonte não é validado — o campo continua opcional.
        /// </summary>
        private async Task<bool> AplicarCatalogoApiAsync(Competicao competicao, string? logoAtual)
        {
            var link = competicao.LinkTransfermarket?.Trim();
            competicao.LinkTransfermarket = string.IsNullOrWhiteSpace(link) ? null : link;

            // LogoUrl não vem no formulário: parte sempre do que já está salvo, para que
            // salvar sem link (ou com link de outra fonte) não apague o escudo atual.
            competicao.LogoUrl = logoAtual;

            // Competição da FIFA: catálogo próprio, sem chave, e o escudo é a arte da
            // edição — quando ela já existe (ver FifaService.LogoDaSeasonAsync).
            if (FifaService.IsFifaLink(link))
                return await AplicarCatalogoFifaAsync(competicao, link!);

            if (!ApiFootballService.IsApiFootballLink(link)) return true;

            int leagueId, season;
            try
            {
                (leagueId, season) = ApiFootballService.ParseLink(link!);
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(nameof(Competicao.LinkTransfermarket),
                    "Formato inválido. Use apifoot:LEAGUE_ID:SEASON (ex.: apifoot:128:2026).");
                return false;
            }

            var liga = await _catalogoLigas.BuscarLigaAsync(leagueId, season);
            if (liga == null)
            {
                ModelState.AddModelError(nameof(Competicao.LinkTransfermarket),
                    $"A api-football não reconhece o código {leagueId}. " +
                    "Confira em Competições da API.");
                return false;
            }

            competicao.IdApi = leagueId;

            // O escudo é sempre o do catálogo: com código de API válido, a fonte do logo é
            // a própria API (um logo salvo à mão em SalvarLogo é substituído no próximo save).
            competicao.LogoUrl = liga.Logo;

            return true;
        }

        /// <summary>
        /// Confere o link "fifa:IDCOMPETITION:IDSEASON" contra o catálogo da FIFA.
        /// Mesmo contrato de <see cref="AplicarCatalogoApiAsync"/>: false com erro no
        /// ModelState quando o link está malformado ou a edição não existe.
        ///
        /// IdApi NÃO é preenchido — aquele campo é o id da liga na api-football, e é por
        /// ele que a ESPN e o FotMob decidem se cobrem a competição. Gravar um id da
        /// FIFA ali faria as duas procurarem a partida na liga errada.
        /// </summary>
        private async Task<bool> AplicarCatalogoFifaAsync(Competicao competicao, string link)
        {
            string idComp, idSeason;
            try
            {
                (idComp, idSeason) = FifaService.ParseLink(link);
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(nameof(Competicao.LinkTransfermarket),
                    "Formato inválido. Use fifa:IDCOMPETITION:IDSEASON (ex.: fifa:108:291518).");
                return false;
            }

            var season = await _fifa.BuscarSeasonAsync(idComp, idSeason);
            if (season == null)
            {
                ModelState.AddModelError(nameof(Competicao.LinkTransfermarket),
                    $"A FIFA não reconhece a edição {idSeason} da competição {idComp}. " +
                    "Confira em Competições da API › FIFA.");
                return false;
            }

            // Só sobrescreve o escudo quando a FIFA tem a arte: edição recém-anunciada
            // ainda não tem identidade visual publicada, e apagar o logo escolhido à mão
            // para deixar a competição sem escudo nenhum seria uma piora.
            if (!string.IsNullOrWhiteSpace(season.LogoUrl))
                competicao.LogoUrl = season.LogoUrl;

            return true;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarJogos(int id, string? returnUrl = null)
        {
            // returnUrl: as telas fixas (Brasileirão, Copa do Brasil, Libertadores,
            // Sul-Americana, Champions) chamam esta ação pelo partial
            // _BotaoSincronizarJogos e voltam para si mesmas, não para a lista.
            var voltarPara = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            if (string.IsNullOrWhiteSpace(competicao.LinkTransfermarket))
            {
                if (voltarPara != null)
                {
                    TempData["Mensagem"] = "Configure o link da competição antes de buscar jogos.";
                    TempData["MensagemTipo"] = "erro";
                    return Redirect(voltarPara);
                }

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
                    else if (FifaService.IsFifaLink(competicao.LinkTransfermarket))
                    {
                        var fifa = scope.ServiceProvider.GetRequiredService<FifaService>();
                        var (jogos, times, erros, avisos) =
                            await fifa.SincronizarCompeticaoAsync(ctx, competicao);
                        logger.LogInformation(
                            "[BuscarJogos] {Nome}: {J} jogos, {T} times criados, {E} erros (FIFA).",
                            competicao.Nome, jogos, times, erros);
                    }
                    else
                    {
                        logger.LogWarning(
                            "[BuscarJogos] {Nome}: link não reconhecido — use apifoot:LEAGUE_ID:SEASON ou fifa:IDCOMPETITION:IDSEASON.",
                            competicao.Nome);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[BuscarJogos] Erro ao sincronizar {Nome}.", competicao.Nome);
                }
            });

            var aviso = $"Busca de jogos de '{competicao.Nome}' iniciada em segundo plano.";

            if (voltarPara != null)
            {
                // Fora da tela de competições o feedback sai pelo toast do layout.
                TempData["Mensagem"] = aviso;
                return Redirect(voltarPara);
            }

            TempData["Sucesso"] = aviso;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User)!;
            var topTierIds = await _context.CompeticoesTopTierUsuario
                .Where(t => t.UsuarioId == uid)
                .Select(t => t.CompeticaoId)
                .ToHashSetAsync();

            // Competições que alimentam a tabela de classificação da home.
            var homeIds = await _context.CompeticoesHomeUsuario
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
            ViewBag.HomeIds = homeIds;
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

        // Marca/desmarca a competição como participante da tabela de classificação da home.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleHome(int id)
        {
            var uid = _userManager.GetUserId(User)!;
            var registro = await _context.CompeticoesHomeUsuario
                .FirstOrDefaultAsync(t => t.CompeticaoId == id && t.UsuarioId == uid);

            if (registro == null)
                _context.CompeticoesHomeUsuario.Add(new CompeticaoHomeUsuario { CompeticaoId = id, UsuarioId = uid });
            else
                _context.CompeticoesHomeUsuario.Remove(registro);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Capa (hero) da tela de detalhes. É preferência de usuário: cada um sobe a
        // sua arte e nenhuma delas muda o que os outros veem.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarCapa(int id, IFormFile? capaFile, int escurecimento = 55,
            int posX = 50, int posY = 50, int zoom = 100, string ajuste = "COBRIR", string? cor = null,
            int? temporada = null)
        {
            var uid = _userManager.GetUserId(User);
            if (uid == null) return Challenge();

            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            var registro = await _context.CompeticoesHeroUsuario
                .FirstOrDefaultAsync(h => h.CompeticaoId == id && h.UsuarioId == uid);

            // Fora da faixa o véu ou não protege o texto ou apaga a imagem toda.
            escurecimento = Math.Clamp(escurecimento, 0, 90);
            posX = Math.Clamp(posX, 0, 100);
            posY = Math.Clamp(posY, 0, 100);
            // Em COBRIR, abaixo de 100% a foto deixaria de cobrir a faixa; em CABER,
            // reduzir é justamente o ponto (arte quadrada que precisa aparecer inteira).
            ajuste = ajuste == "CABER" ? "CABER" : "COBRIR";
            zoom = Math.Clamp(zoom, ajuste == "CABER" ? 30 : 100, 300);

            // A cor entra em CSS: só #rrggbb passa. Vazio (campo desligado no
            // formulário) significa "voltar para a cor padrão do tipo".
            cor = string.IsNullOrWhiteSpace(cor) ? null : cor.Trim();
            if (cor != null && !System.Text.RegularExpressions.Regex.IsMatch(cor, "^#[0-9a-fA-F]{6}$"))
            {
                TempData["Erro"] = "Cor inválida.";
                return RedirectToAction(nameof(Detalhes), new { id, temporada });
            }

            if (capaFile != null && capaFile.Length > 0)
            {
                var r = await UploadHelper.SalvarImagemAsync(capaFile, _env.WebRootPath, "images/heroes", competicao.Nome);
                if (!r.Sucesso)
                {
                    TempData["Erro"] = $"Erro na capa: {r.Erro}";
                    return RedirectToAction(nameof(Detalhes), new { id, temporada });
                }

                var anterior = registro?.ImagemUrl;
                if (registro == null)
                {
                    registro = new CompeticaoHeroUsuario { CompeticaoId = id, UsuarioId = uid };
                    _context.CompeticoesHeroUsuario.Add(registro);
                }
                registro.ImagemUrl = r.UrlRelativa!;
                RemoverArquivoCapa(anterior);
            }
            else if (registro == null)
            {
                // Sem imagem: o registro nasce só com a cor e os ajustes escolhidos.
                registro = new CompeticaoHeroUsuario { CompeticaoId = id, UsuarioId = uid };
                _context.CompeticoesHeroUsuario.Add(registro);
            }

            registro.Escurecimento = escurecimento;
            registro.PosX = posX;
            registro.PosY = posY;
            registro.Zoom = zoom;
            registro.Ajuste = ajuste;
            registro.Cor = cor;

            // Nada personalizado sobrando (nem capa nem cor): apaga o registro em vez
            // de guardar uma linha que só repete o padrão.
            if (string.IsNullOrWhiteSpace(registro.ImagemUrl) && cor == null)
                _context.CompeticoesHeroUsuario.Remove(registro);

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Aparência da competição atualizada.";
            return RedirectToAction(nameof(Detalhes), new { id, temporada });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverCapa(int id, int? temporada = null)
        {
            var uid = _userManager.GetUserId(User);
            if (uid == null) return Challenge();

            var registro = await _context.CompeticoesHeroUsuario
                .FirstOrDefaultAsync(h => h.CompeticaoId == id && h.UsuarioId == uid);

            if (registro != null)
            {
                var imagem = registro.ImagemUrl;

                // A cor é uma escolha à parte: tirar a foto não deve desfazê-la.
                if (registro.Cor == null)
                    _context.CompeticoesHeroUsuario.Remove(registro);
                else
                    registro.ImagemUrl = "";

                await _context.SaveChangesAsync();
                RemoverArquivoCapa(imagem);
                TempData["Sucesso"] = "Capa padrão restaurada.";
            }

            return RedirectToAction(nameof(Detalhes), new { id, temporada });
        }

        // Apaga o arquivo da capa antiga: sem isso cada troca deixaria a imagem
        // anterior parada em wwwroot/images/heroes para sempre.
        private void RemoverArquivoCapa(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/images/heroes/", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var caminho = Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(caminho)) System.IO.File.Delete(caminho);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Não foi possível apagar a capa antiga {Url}", url);
            }
        }

        // GET: Competicoes/EquipeDaRodada/5?temporada=2025&rodada=22&formacao=3
        // Os melhores da rodada montados na formação mais usada pelos times naquela
        // rodada (o usuário pode escolher outra pelo seletor), cada um na posição em
        // que realmente jogou. A nota é a mesma que o resto do sistema usa: a manual
        // do usuário quando existe, senão a calculada pelos critérios dele em cima
        // da estatística importada.
        public async Task<IActionResult> EquipeDaRodada(int id, int? temporada = null, int? rodada = null, int? formacao = null)
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

            // Formação da equipe: a mais usada pelos times na rodada, salvo escolha
            // explícita do usuário. O 4-3-3 fixo de antes espalhava zagueiro pelas
            // laterais quando a rodada era jogada com três defensores.
            var usosFormacao = new Dictionary<int, int>();
            void ContarFormacao(int? formacaoId)
            {
                if (formacaoId.HasValue)
                    usosFormacao[formacaoId.Value] = usosFormacao.GetValueOrDefault(formacaoId.Value) + 1;
            }
            foreach (var j in jogos) { ContarFormacao(j.FormacaoCasaId); ContarFormacao(j.FormacaoVisitanteId); }

            // Sem posições cadastradas não há slot para preencher.
            var formacoes = await _context.Formacoes.AsNoTracking()
                .Include(f => f.Posicoes)
                .Where(f => f.Posicoes.Any())
                .ToListAsync();

            vm.FormacoesDisponiveis = formacoes
                .Select(f => new FormacaoDaRodada { Id = f.Id, Nome = f.Nome, Usos = usosFormacao.GetValueOrDefault(f.Id) })
                .OrderByDescending(o => o.Usos).ThenBy(o => o.Nome)
                .ToList();

            var formacaoEscolhida = formacoes.FirstOrDefault(f => f.Id == formacao);
            vm.FormacaoAutomatica = formacaoEscolhida == null;
            formacaoEscolhida ??= formacoes
                    .Where(f => usosFormacao.ContainsKey(f.Id))
                    .OrderByDescending(f => usosFormacao[f.Id]).ThenBy(f => f.Nome)
                    .FirstOrDefault()
                // Rodada sem nenhuma formação registrada: o 4-3-3 segue como desenho
                // padrão desta tela, como era antes do seletor existir.
                ?? formacoes.FirstOrDefault(f => f.Nome == "4-3-3")
                ?? formacoes.FirstOrDefault();
            if (formacaoEscolhida == null) return View(vm);

            vm.FormacaoId = formacaoEscolhida.Id;
            vm.FormacaoNome = formacaoEscolhida.Nome;

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

            var candidatos = new List<JogadorDaRodada>();
            foreach (var ((jogadorId, jogoId), nota) in notaPorChave)
            {
                if (!jogadores.TryGetValue(jogadorId, out var jogador)) continue;
                if (!jogoPorId.TryGetValue(jogoId, out var jogo)) continue;

                var temAtuacao = lado.TryGetValue((jogadorId, jogoId), out var atuacao);
                bool? emCasa = temAtuacao ? atuacao.IsTimeCasa : null;
                var adversario = emCasa switch
                {
                    true => jogo.TimeVisitante?.Nome,
                    false => jogo.TimeCasa?.Nome,
                    _ => $"{jogo.TimeCasa?.Nome} × {jogo.TimeVisitante?.Nome}"
                };

                // A posição é a daquele jogo (slot da escalação), não a agregada do
                // cadastro: é o que garante o lateral na lateral e o zagueiro no miolo.
                var posicaoNaRodada = temAtuacao && !string.IsNullOrWhiteSpace(atuacao.Posicao)
                    ? PosicaoJogadorHelper.NormalizarNomePosicao(atuacao.Posicao!)
                    : null;

                candidatos.Add(new JogadorDaRodada
                {
                    Jogador = jogador,
                    Time = emCasa == true ? jogo.TimeCasa : emCasa == false ? jogo.TimeVisitante : jogador.Time,
                    JogoId = jogoId,
                    Nota = nota.Nota,
                    NotaAutomatica = nota.Automatica,
                    Adversario = adversario ?? "",
                    Placar = $"{jogo.PlacarCasa}×{jogo.PlacarVisitante}",
                    Gols = golsRodada.Count(g => g.JogadorId == jogadorId && g.JogoId == jogoId),
                    Assistencias = assistRodada.Count(a => a.JogadorId == jogadorId && a.JogoId == jogoId),
                    PosicaoNaRodada = posicaoNaRodada
                });
            }

            // Um jogador só ocupa uma vaga mesmo que tenha jogado duas vezes na rodada
            // (jogo adiado da rodada anterior, por exemplo): fica com a melhor nota das
            // duas, mas segue elegível a qualquer posição em que atuou na rodada.
            var elegiveis = candidatos
                .GroupBy(c => c.Jogador.Id)
                .Select(g => new
                {
                    Item = g.OrderByDescending(x => x.Nota).ThenByDescending(x => x.Gols + x.Assistencias).First(),
                    // Da mais para a menos frequente na rodada. Sem escalação com
                    // coordenada, sobra o cadastro do jogador — que já é o agregado das
                    // posições em que ele mais joga.
                    Posicoes = g.Where(x => !string.IsNullOrWhiteSpace(x.PosicaoNaRodada))
                                .GroupBy(x => x.PosicaoNaRodada!)
                                .OrderByDescending(p => p.Count())
                                .Select(p => p.Key)
                                .ToList()
                })
                .Select(x => new
                {
                    x.Item,
                    Posicoes = x.Posicoes.Count > 0 ? x.Posicoes : PosicoesDoCadastro(x.Item.Jogador)
                })
                // Sem posição não dá para dizer em que vaga ele entra; ficar de fora é
                // melhor que ocupar a vaga errada na escalação.
                .Where(x => x.Posicoes.Count > 0)
                .OrderByDescending(x => x.Item.Nota)
                .ThenByDescending(x => x.Item.Gols + x.Item.Assistencias)
                .ToList();

            var slots = formacaoEscolhida.Posicoes.OrderBy(p => p.Ordem).ToList();
            vm.Slots = slots
                .Select(s => new SlotDaRodada
                {
                    NomePosicao = s.NomePosicao,
                    PosicaoX = s.PosicaoX,
                    PosicaoY = s.PosicaoY
                })
                .ToList();

            var usados = new HashSet<int>();

            // 1ª passada: casamento exato — o slot só aceita quem jogou naquela mesma
            // posição na rodada (slot "Lateral Direito" ← quem foi lateral direito).
            for (int i = 0; i < slots.Count; i++)
            {
                var nomeSlot = PosicaoJogadorHelper.NormalizarNomePosicao(slots[i].NomePosicao);
                var melhor = elegiveis.FirstOrDefault(x => !usados.Contains(x.Item.Jogador.Id)
                                                        && x.Posicoes.Contains(nomeSlot));
                if (melhor == null) continue;
                vm.Slots[i].Jogador = melhor.Item;
                usados.Add(melhor.Item.Jogador.Id);
            }

            // 2ª passada: ala e lateral são o mesmo jogador em desenhos diferentes
            // (linha de 5 x linha de 4), então o lateral direito é o candidato natural
            // do slot de ala direito — e vice-versa — antes de sair procurando por setor
            // (que jogaria um meia na vaga do ala).
            for (int i = 0; i < slots.Count; i++)
            {
                if (vm.Slots[i].Jogador != null) continue;

                var equivalente = PosicaoJogadorHelper.Sigla(slots[i].NomePosicao) switch
                {
                    "AE" => "LE",
                    "AD" => "LD",
                    "LE" => "AE",
                    "LD" => "AD",
                    _ => null
                };
                if (equivalente == null) continue;

                var lateral = elegiveis.FirstOrDefault(x => !usados.Contains(x.Item.Jogador.Id)
                    && x.Posicoes.Any(p => PosicaoJogadorHelper.Sigla(p) == equivalente));
                if (lateral == null) continue;
                vm.Slots[i].Jogador = lateral.Item;
                vm.Slots[i].Aproximado = true;
                usados.Add(lateral.Item.Jogador.Id);
            }

            // 3ª passada: o que sobrou vazio recebe o melhor do mesmo setor, marcado
            // como aproximado — com poucos jogos na rodada é comum não haver ninguém
            // avaliado numa posição específica, e a tela avisa quem entrou de tapa-buraco
            // em vez de deixar o desenho furado.
            for (int i = 0; i < slots.Count; i++)
            {
                if (vm.Slots[i].Jogador != null) continue;

                var setorSlot = PosicaoJogadorHelper.Setor(slots[i].NomePosicao);
                if (setorSlot == null) continue;

                var melhor = elegiveis.FirstOrDefault(x => !usados.Contains(x.Item.Jogador.Id)
                                                        && PosicaoJogadorHelper.Setor(x.Posicoes[0]) == setorSlot);
                if (melhor == null) continue;
                vm.Slots[i].Jogador = melhor.Item;
                vm.Slots[i].Aproximado = true;
                usados.Add(melhor.Item.Jogador.Id);
            }

            return View(vm);
        }

        // Posições do cadastro do jogador ("Lateral Direito/Zagueiro"), normalizadas —
        // fallback de quem tem nota na rodada mas nenhuma escalação com coordenada.
        private static List<string> PosicoesDoCadastro(Jogador jogador) =>
            (jogador.Posicao ?? "")
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => PosicaoJogadorHelper.NormalizarNomePosicao(p.Trim()))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

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

            // Escalações dos gols e cartões: o dono do evento é o time pelo qual o
            // jogador entrou em campo naquele jogo, não o clube atual do cadastro.
            var escalacoesEstat = LadoJogadorHelper.EscalacoesDosEventos(
                _context, jogoIds,
                gols.Select(g => g.JogadorId).Concat(cartoes.Select(c => c.JogadorId))).ToList();

            return View(EstatisticaTimeCalculator.Calcular(jogosRealizados, gols, cartoes, escalacoesEstat));
        }

        public async Task<IActionResult> Detalhes(int id, int? temporada = null)
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

            // Formato da competição (pontos corridos, grupos, mata-mata, fases
            // declaradas) — mesma leitura usada pela área pública /creators/tabelas.
            // Escalações dos cartões: o cartão pertence ao time pelo qual o jogador
            // entrou em campo naquele jogo, não ao clube atual do cadastro dele.
            var escalacoesCartoes = LadoJogadorHelper.EscalacoesDosEventos(
                _context, jogoIdsRealizados, cartoes.Select(c => c.JogadorId)).ToList();

            var painel = CompeticaoPainelBuilder.Montar(
                competicao, fasesDeclaradas, jogosDaTemporada, criterios, cartoes, escalacoesCartoes);

            vm.Fases = painel.Fases;
            vm.Classificacao = painel.Classificacao;
            vm.Grupos = painel.Grupos;
            vm.FasesMataMata = painel.FasesMataMata;

            // ── Stats do hero (times, jogos, gols) ────────────────────────────
            ViewBag.TotalTimes = jogosDaTemporada
                .SelectMany(j => new[] { j.TimeCasaId, j.TimeVisitanteId })
                .Distinct()
                .Count();
            ViewBag.TotalGols = jogosRealizados.Sum(j => (j.PlacarCasa ?? 0) + (j.PlacarVisitante ?? 0));

            // ── Capa personalizada do hero (por usuário) ──────────────────────
            var uidCapa = _userManager.GetUserId(User);
            ViewBag.HeroCapa = uidCapa == null ? null : _context.CompeticoesHeroUsuario.AsNoTracking()
                .FirstOrDefault(h => h.CompeticaoId == id && h.UsuarioId == uidCapa);

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

            // ── Assistências (top 5 garçons da competição/temporada) ──────────
            ViewBag.Assistencias = await RankingCompeticaoHelper.AssistentesAsync(_context, id, temporadaSel);

            // ── Aba "Estatísticas" ─────────────────────────────────────────────
            var golsEstat = _context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => jogoIdsRealizados.Contains(g.JogoId))
                .ToList();
            var escalacoesEstat = LadoJogadorHelper.EscalacoesDosEventos(
                _context, jogoIdsRealizados,
                golsEstat.Select(g => g.JogadorId).Concat(cartoes.Select(c => c.JogadorId))).ToList();
            ViewBag.EstatisticasTimes = EstatisticaTimeCalculator.Calcular(
                jogosRealizados, golsEstat, cartoes, escalacoesEstat);

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

            // Valida o código da API e já traz IdApi + escudo do catálogo.
            await AplicarCatalogoApiAsync(competicao, logoAtual: null);

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

            // Atualiza só os campos do formulário para não apagar
            // TopTier e demais colunas que não estão na tela.
            var existente = await _context.Competicoes.FindAsync(id);
            if (existente == null) return NotFound();

            // Valida o código da API e já traz IdApi + escudo do catálogo, preservando
            // um logo que tenha sido escolhido à mão.
            await AplicarCatalogoApiAsync(competicao, existente.LogoUrl);

            if (!ModelState.IsValid)
            {
                ViewBag.CriteriosDesempate = CriteriosDesempateHelper.Parse(string.Join(';', criterios ?? new()));
                ViewBag.Fases = await _context.CompeticaoFases
                    .Where(f => f.CompeticaoId == id)
                    .OrderBy(f => f.Ordem).ThenBy(f => f.Id)
                    .ToListAsync();
                return View(competicao);
            }

            existente.LogoUrl = competicao.LogoUrl;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarLinkCompeticao(int id, string linkCompeticao)
        {
            var competicao = await _context.Competicoes.FindAsync(id);
            if (competicao == null) return NotFound();

            competicao.LinkTransfermarket = linkCompeticao;

            // Mesma validação da tela de edição: código inexistente não é salvo, e o
            // código válido já traz IdApi e escudo.
            if (!await AplicarCatalogoApiAsync(competicao, competicao.LogoUrl))
            {
                TempData["Erro"] = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }

            _context.Update(competicao);
            await _context.SaveChangesAsync();

            TempData["Mensagem"] = "Link da competição atualizado com sucesso!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Competicoes/SincronizarEscudos — adota o escudo da api-football em todas as
        // competições que apontam para uma liga da API (pelo link apifoot: ou só pelo IdApi),
        // substituindo o logo atual (inclusive os informados à mão) para que a fonte do
        // escudo seja uma só.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SincronizarEscudos()
        {
            var competicoes = await _context.Competicoes
                .Where(c => c.IdApi != null ||
                            (c.LinkTransfermarket != null && c.LinkTransfermarket.StartsWith("apifoot:")))
                .ToListAsync();

            int atualizadas = 0;
            var semCodigo = new List<string>();

            foreach (var competicao in competicoes)
            {
                int leagueId;
                int? season = null;

                if (ApiFootballService.IsApiFootballLink(competicao.LinkTransfermarket))
                {
                    try
                    {
                        (leagueId, var s) = ApiFootballService.ParseLink(competicao.LinkTransfermarket!);
                        season = s;
                    }
                    catch (ArgumentException)
                    {
                        semCodigo.Add(competicao.Nome);
                        continue;
                    }
                }
                else
                {
                    // Sem link da API, o IdApi cadastrado à mão já identifica a liga.
                    leagueId = competicao.IdApi!.Value;
                }

                var liga = await _catalogoLigas.BuscarLigaAsync(leagueId, season);
                if (liga == null)
                {
                    semCodigo.Add(competicao.Nome);
                    continue;
                }

                competicao.IdApi = leagueId;

                if (competicao.LogoUrl != liga.Logo)
                {
                    competicao.LogoUrl = liga.Logo;
                    atualizadas++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{atualizadas} escudo(s) atualizado(s) a partir da api-football " +
                $"({competicoes.Count} competição(ões) ligadas à API).";

            if (semCodigo.Any())
                TempData["Erro"] = "Sem código válido no catálogo: " + string.Join(", ", semCodigo) + ".";

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
        /// <summary>
        /// Catálogo da FIFA: as competições que ela organiza e as edições de cada uma,
        /// com o link pronto para colar no cadastro. Existe porque o IdSeason não é
        /// deduzível — "Polônia 2026" é 291518 e não há como adivinhar isso.
        /// </summary>
        public async Task<IActionResult> CompeticoesFifa(string? comp = null)
        {
            var vm = new CompeticoesFifaViewModel
            {
                Competicoes = await _fifa.ListarCompeticoesAsync(),
                CompeticaoSelecionada = comp,
            };

            if (!string.IsNullOrWhiteSpace(comp))
            {
                vm.Edicoes = await _fifa.ListarSeasonsAsync(comp);
                vm.NomeSelecionada = vm.Competicoes.FirstOrDefault(c => c.Id == comp)?.Nome;
            }

            // Marca o que já está cadastrado, para não duplicar competição sem perceber.
            vm.LinksRegistrados = (await _context.Competicoes
                    .Where(c => c.LinkTransfermarket != null && c.LinkTransfermarket.StartsWith("fifa:"))
                    .Select(c => c.LinkTransfermarket!)
                    .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return View(vm);
        }

        public async Task<IActionResult> CompeticoesApi()
        {
            var itens = await _catalogoLigas.CarregarAsync();
            if (itens.Count == 0)
                return NotFound("Arquivo de competições da API não encontrado.");

            // Marca as ligas que já têm competição cadastrada apontando pra elas. A lista do
            // catálogo é cacheada e compartilhada entre requisições, então "Registrada" é
            // escrita em cópias — nunca nos itens do cache.
            var idsRegistrados = (await _context.Competicoes
                .Where(c => c.IdApi != null)
                .Select(c => c.IdApi!.Value)
                .ToListAsync()).ToHashSet();

            itens = itens
                .Select(l => new CompeticaoApiLiga
                {
                    Id = l.Id,
                    Nome = l.Nome,
                    Tipo = l.Tipo,
                    Logo = l.Logo,
                    Pais = l.Pais,
                    Bandeira = l.Bandeira,
                    Registrada = idsRegistrados.Contains(l.Id),
                })
                .ToList();

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
