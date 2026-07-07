using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System.Linq;
using System.Threading.Tasks;

namespace ControleFutebolWeb.Controllers
{
    public class JogadoresController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<JogadoresController> _logger;
        private readonly ApiFootballService _transfermarktService;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogadoresController(FutebolContext context, ILogger<JogadoresController> logger, ApiFootballService transfermarktService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _transfermarktService = transfermarktService;
            _userManager = userManager;
        }

        public IActionResult Index(string posicao, string nacionalidade, int? timeId, string sortOrder, string? nome, int? idadeMin, int? idadeMax, bool semIdade = false, int page = 1)
        {
            const int pageSize = 50;
            var vm = new JogadoresIndexViewModel
            {
                // Configura parâmetros de ordenação
                NomeSortParam = sortOrder == "Nome" ? "Nome_desc" : "Nome",
                PosicaoSortParam = sortOrder == "Posicao" ? "Posicao_desc" : "Posicao",
                IdadeSortParam = sortOrder == "Idade" ? "Idade_desc" : "Idade",
                NacionalidadeSortParam = sortOrder == "Nacionalidade" ? "Nacionalidade_desc" : "Nacionalidade",
                TimeSortParam = sortOrder == "Time" ? "Time_desc" : "Time",

                // Guarda filtros atuais
                CurrentSort = sortOrder
            };

            var jogadores = _context.Jogadores
                .Include(j => j.Nacionalidade)
                .Include(j => j.Time)
                .AsQueryable();

            // Aplica filtros
            if (!string.IsNullOrEmpty(nome))
                jogadores = jogadores.Where(j => j.Nome.ToLower().Contains(nome.ToLower()));

            if (!string.IsNullOrEmpty(posicao))
                // Contains: a posição do jogador pode ser composta ("Lateral Direito/Zagueiro")
                // e filtros genéricos ("Meia") devem pegar as variantes ("Meia Ofensivo").
                jogadores = jogadores.Where(j => j.Posicao.Contains(posicao));

            if (!string.IsNullOrEmpty(nacionalidade))
                jogadores = jogadores.Where(j => j.Nacionalidade.Nome == nacionalidade);

            if (timeId.HasValue)
                jogadores = jogadores.Where(j => j.TimeId == timeId.Value);

            // Filtro "somente sem idade": jogadores sem data de nascimento cadastrada.
            if (semIdade)
            {
                jogadores = jogadores.Where(j => j.DataNascimento == null);
            }
            else
            {
                // Filtro por faixa de idade (convertido para faixa de data de nascimento).
                // idade >= min  ⇔ nascimento <= hoje - min anos
                // idade <= max  ⇔ nascimento >  hoje - (max+1) anos
                var hoje = DateTime.Today;
                if (idadeMin.HasValue)
                {
                    var limiteSuperior = hoje.AddYears(-idadeMin.Value);
                    jogadores = jogadores.Where(j => j.DataNascimento != null && j.DataNascimento <= limiteSuperior);
                }
                if (idadeMax.HasValue)
                {
                    var limiteInferior = hoje.AddYears(-(idadeMax.Value + 1));
                    jogadores = jogadores.Where(j => j.DataNascimento != null && j.DataNascimento > limiteInferior);
                }
            }

            // Aplica ordenação
            jogadores = sortOrder switch
            {
                "Nome" => jogadores.OrderBy(j => j.Nome),
                "Nome_desc" => jogadores.OrderByDescending(j => j.Nome),
                "Posicao" => jogadores.OrderBy(j => j.Posicao),
                "Posicao_desc" => jogadores.OrderByDescending(j => j.Posicao),
                "Idade" => jogadores.AsEnumerable().OrderBy(j => j.Idade).AsQueryable(),
                "Idade_desc" => jogadores.AsEnumerable().OrderByDescending(j => j.Idade).AsQueryable(),
                "Nacionalidade" => jogadores.OrderBy(j => j.Nacionalidade.Nome),
                "Nacionalidade_desc" => jogadores.OrderByDescending(j => j.Nacionalidade.Nome),
                "Time" => jogadores.OrderBy(j => j.Time.Nome),
                "Time_desc" => jogadores.OrderByDescending(j => j.Time.Nome),
                _ => jogadores.OrderBy(j => j.Nome)
            };

            vm.Nome = nome;
            vm.IdadeMin = idadeMin;
            vm.IdadeMax = idadeMax;
            vm.SemIdade = semIdade;

            // Preenche combos com SelectList — nomes alinhados com PosicaoFormacao.NomePosicao
            // (o filtro é Contains, então "Meia" também pega "Meia Ofensivo" etc.)
            var posicoes = new List<string> {
                "Goleiro","Defensor","Zagueiro","Lateral Direito","Lateral Esquerdo",
                "Ala","Volante","Meia","Meia Ofensivo",
                "Ponta Direita","Ponta Esquerda","Centroavante","Atacante"
            };
            vm.Posicoes = new SelectList(posicoes, posicao);

            var nacionalidades = _context.Nacionalidades
                .Select(n => n.Nome)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            vm.Nacionalidades = new SelectList(nacionalidades, nacionalidade);

            var times = _context.Times
                .OrderBy(t => t.Nome)
                .ToList();
            vm.Times = new SelectList(times, "Id", "Nome", timeId);

            // Paginação (50 por página)
            var listaOrdenada = jogadores.ToList();
            var totalJogadores = listaOrdenada.Count;
            var totalPaginas = (int)Math.Ceiling(totalJogadores / (double)pageSize);
            if (totalPaginas < 1) totalPaginas = 1;
            if (page < 1) page = 1;
            if (page > totalPaginas) page = totalPaginas;

            var jogadoresPagina = listaOrdenada
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            vm.PaginaAtual = page;
            vm.TotalPaginas = totalPaginas;
            vm.TotalJogadores = totalJogadores;
            vm.PageSize = pageSize;

            // KPIs do hero (totais globais, independentes do filtro aplicado)
            vm.TotalJogadoresGlobal = _context.Jogadores.Count();
            vm.TotalTimesComJogadores = _context.Jogadores.Select(j => j.TimeId).Distinct().Count();
            vm.TotalNacionalidadesGlobal = nacionalidades.Count;

            // Filtros atuais, para preservar nos links de paginação
            vm.FiltroPosicao = posicao;
            vm.FiltroNacionalidade = nacionalidade;
            vm.FiltroTimeId = timeId;
            vm.FiltroSortOrder = sortOrder;

            vm.Itens = jogadoresPagina;
            return View(vm);
        }
        // GET: Jogadores/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jogador == null) return NotFound();

            return View(jogador);
        }

        // GET: Jogadores/Create
        public IActionResult Create()
        {
            ViewBag.TimeId = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.NacionalidadeId = new SelectList(_context.Nacionalidades, "Id", "Nome");
            return View();
        }

        // POST: Jogadores/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Jogador jogador)
        {
            if (ModelState.IsValid)
            {
                _context.Add(jogador);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            LogModelStateErrors("Create");

            ViewBag.TimeId = new SelectList(_context.Times, "Id", "Nome", jogador?.TimeId);
            ViewBag.NacionalidadeId = new SelectList(_context.Nacionalidades, "Id", "Nome", jogador?.NacionalidadeId);
            return View(jogador);
        }

        // GET: Jogadores/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Nacionalidade)
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogador == null)
            {
                return NotFound();
            }

            // Pega todas as posições distintas já salvas
            var posicoes = await _context.Jogadores
                .Select(j => j.Posicao)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            ViewBag.Posicoes = new SelectList(posicoes);

            ViewData["NacionalidadeId"] = new SelectList(_context.Nacionalidades, "Id", "Nome", jogador.NacionalidadeId);
            ViewData["TimeId"] = new SelectList(_context.Times, "Id", "Nome", jogador.TimeId);

            return View(jogador);
        }


        // POST: Jogadores/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Jogador jogador)
        {
            if (id != jogador.Id) return NotFound();

            _logger.LogInformation(
                "POST Edit recebido: Id={Id}, Nome={Nome}, Posicao={Posicao}, TimeId={TimeId}, NacionalidadeId={NacionalidadeId}, DataNascimento={DataNascimento}",
                jogador.Id,
                jogador.Nome,
                jogador.Posicao,
                jogador.TimeId,
                jogador.NacionalidadeId,
                jogador.DataNascimento
            );

            if (ModelState.IsValid)
            {
                try
                {
                    var jogadorExistente = await _context.Jogadores
                        .Include(j => j.Nacionalidade)
                        .Include(j => j.Time)
                        .FirstOrDefaultAsync(j => j.Id == id);

                    if (jogadorExistente == null) return NotFound();

                    // Validação contra api-football (somente se IdApi estiver preenchido)
                    var jogadorExistenteParaValidacao = await _context.Jogadores.FindAsync(id);
                    if (jogadorExistenteParaValidacao?.IdApi > 0)
                    {
                        var dadosApi = await _transfermarktService.BuscarInfoJogadorAsync(
                            jogadorExistenteParaValidacao.IdApi.Value);

                        if (dadosApi != null)
                        {
                            var divergencias = new List<string>();

                            if (dadosApi.DataNascimento.HasValue &&
                                dadosApi.DataNascimento.Value.Date != jogador.DataNascimento?.Date)
                                divergencias.Add($"Data de nascimento divergente. API: {dadosApi.DataNascimento.Value:dd/MM/yyyy}");

                            if (!string.IsNullOrEmpty(dadosApi.Nacionalidade) && jogador.NacionalidadeId.HasValue)
                            {
                                var nacSelecionada = await _context.Nacionalidades.FindAsync(jogador.NacionalidadeId.Value);
                                if (nacSelecionada?.Nome != dadosApi.Nacionalidade)
                                    divergencias.Add($"Nacionalidade divergente. API: {dadosApi.Nacionalidade}");
                            }

                            if (divergencias.Any())
                            {
                                TempData["Mensagem"] = string.Join(" | ", divergencias);
                                TempData["MensagemTipo"] = "erro";

                                ViewBag.TimeId = new SelectList(_context.Times, "Id", "Nome", jogador.TimeId);
                                ViewBag.NacionalidadeId = new SelectList(_context.Nacionalidades, "Id", "Nome", jogador.NacionalidadeId);
                                return View(jogador);
                            }
                        }
                    }

                    // Atualiza campos
                    jogadorExistente.Nome = jogador.Nome;
                    jogadorExistente.Posicao = jogador.Posicao;
                    // Coluna datanascimento é "timestamp without time zone": o Npgsql
                    // rejeita DateTime com Kind=Utc. Usa Unspecified (mesmo padrão do
                    // AtualizarInfo / AtualizarInfoTodosSemIdade).
                    jogadorExistente.DataNascimento = jogador.DataNascimento.HasValue
                        ? DateTime.SpecifyKind(jogador.DataNascimento.Value, DateTimeKind.Unspecified)
                        : null;
                    jogadorExistente.TimeId = jogador.TimeId;
                    jogadorExistente.NacionalidadeId = jogador.NacionalidadeId;
                    jogadorExistente.Observacoes = jogador.Observacoes;
                    jogadorExistente.FotoUrl = jogador.FotoUrl;
                    jogadorExistente.DtAlt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    TempData["Mensagem"] = "Jogador atualizado com sucesso!";
                    TempData["MensagemTipo"] = "sucesso";

                    return RedirectToAction("Index", new { timeId = jogador.TimeId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Jogadores.Any(e => e.Id == jogador.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            LogModelStateErrors("Edit");

            ViewBag.TimeId = new SelectList(_context.Times, "Id", "Nome", jogador.TimeId);
            ViewBag.NacionalidadeId = new SelectList(_context.Nacionalidades, "Id", "Nome", jogador.NacionalidadeId);
            return View(jogador);
        }



        // GET: Jogadores/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jogador == null) return NotFound();

            return View(jogador);
        }

        // POST: Jogadores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador != null)
            {
                _context.Jogadores.Remove(jogador);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private void LogModelStateErrors(string contextAction)
        {
            var errors = ModelState
                .Where(kvp => kvp.Value.Errors.Count > 0)
                .Select(kvp => new { Key = kvp.Key, Errors = kvp.Value.Errors.Select(e => e.ErrorMessage ?? e.Exception?.Message).ToArray() })
                .ToList();

            if (errors.Any())
                _logger.LogWarning("ModelState inválido em Jogadores/{Action}. Erros: {@Errors}", contextAction, errors);
        }

        public async Task<IActionResult> Estatisticas(int id, int? competicaoId)
        {
            var uid = _userManager.GetUserId(User);

            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogador == null) return NotFound();

            // IDs de competições em que o jogador participou (para o dropdown)
            var competicaoIds = await _context.Notas
                .Where(n => n.JogadorId == id)
                .Select(n => n.Jogo.CompeticaoId)
                .Union(_context.EstatisticasJogador
                    .Where(e => e.JogadorId == id)
                    .Select(e => e.Jogo.CompeticaoId))
                .Union(_context.Escalacoes
                    .Where(e => e.JogadorId == id)
                    .Select(e => e.Jogo.CompeticaoId))
                .Distinct()
                .ToListAsync();

            var competicoesDoJogador = await _context.Competicoes
                .Where(c => competicaoIds.Contains(c.Id))
                .OrderBy(c => c.Nome)
                .ToListAsync();

            ViewBag.Competicoes = competicoesDoJogador;
            ViewBag.CompeticaoId = competicaoId;

            // ── Dados de eventos ──────────────────────────────────────────
            var notasQuery = _context.Notas
                .Include(n => n.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(n => n.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(n => n.Detalhes)
                .Where(n => n.JogadorId == id && n.UsuarioId == uid);

            if (competicaoId.HasValue)
                notasQuery = notasQuery.Where(n => n.Jogo.CompeticaoId == competicaoId);

            var notas = await notasQuery.ToListAsync();

            // Estatísticas importadas (api-football) — usadas quando não há nota manual,
            // para mostrar de onde vem a nota calculada automaticamente em /Relatorios.
            // Exclui reservas não utilizados (Minutos 0/null): a api-football cria uma
            // linha de estatística pra todo o elenco relacionado, mesmo quem não jogou —
            // sem esse filtro o jogo aparecia com nota automática 4.0 sem ele ter atuado.
            var estatisticasQuery = _context.EstatisticasJogador
                .Include(e => e.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(e => e.Jogo).ThenInclude(j => j.TimeVisitante)
                .Where(e => e.JogadorId == id && e.Minutos != null && e.Minutos > 0);

            if (competicaoId.HasValue)
                estatisticasQuery = estatisticasQuery.Where(e => e.Jogo.CompeticaoId == competicaoId);

            var estatisticas = await estatisticasQuery.ToListAsync();

            var golsQuery = _context.Gols.Where(g => g.JogadorId == id && !g.Contra);
            if (competicaoId.HasValue)
                golsQuery = golsQuery.Where(g => g.Jogo.CompeticaoId == competicaoId);
            var gols = await golsQuery.ToListAsync();

            var assistenciasQuery = _context.Assistencias.Where(a => a.JogadorId == id);
            if (competicaoId.HasValue)
                assistenciasQuery = assistenciasQuery.Where(a => a.Jogo.CompeticaoId == competicaoId);
            var assistencias = await assistenciasQuery.ToListAsync();

            var cartoesQuery = _context.Cartoes.Where(c => c.JogadorId == id);
            if (competicaoId.HasValue)
                cartoesQuery = cartoesQuery.Where(c => c.Jogo.CompeticaoId == competicaoId);
            var cartoes = await cartoesQuery.ToListAsync();

            // ── Escalações (inclui jogos sem análise) ─────────────────────
            // Usa escalações do próprio usuário (se existir) ou as compartilhadas (UsuarioId == null)
            var escalacoesQuery = _context.Escalacoes
                .Include(e => e.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(e => e.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(e => e.Setas)
                .Where(e => e.JogadorId == id && (e.UsuarioId == uid || e.UsuarioId == null));

            if (competicaoId.HasValue)
                escalacoesQuery = escalacoesQuery.Where(e => e.Jogo.CompeticaoId == competicaoId);

            var escalacoes = await escalacoesQuery.ToListAsync();

            var jogosComNotaManualIds = notas.Select(n => n.JogoId).ToHashSet();
            var jogosComEstatisticaIds = estatisticas.Select(e => e.JogoId).ToHashSet();

            // Escalação titular usada em cada jogo — prefere a do próprio usuário
            // (com as setas/ajustes dele) sobre a compartilhada (importada, UsuarioId
            // null), e entre as do usuário prefere fase INICIAL (se ele só aparece na
            // FINAL — entrou no decorrer —, usa a FINAL). Reaproveitada tanto para a
            // posição por jogo (histórico) quanto para o agregado "Posições em campo".
            var escalacaoSelecionadaPorJogo = escalacoes
                .Where(e => e.Titular && !string.IsNullOrWhiteSpace(e.Posicao) && e.Posicao != "RES")
                .GroupBy(e => e.JogoId)
                .Select(g => g
                    .OrderBy(e => e.UsuarioId == uid ? 0 : 1)
                    .ThenBy(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null ? 0 : 1)
                    .First())
                .ToList();

            // Posição granular por jogo: casa as coordenadas do slot com a formação
            // usada naquele jogo (mesma lógica de PosicaoJogadorHelper.RecalcularAsync),
            // em vez de confiar no texto bruto salvo em Escalacao.Posicao — que pode
            // ter ficado genérico ("Defensor") dependendo de como a escalação daquele
            // jogo específico foi criada/importada.
            var slotsPorFormacao = (await _context.PosicoesFormacao.ToListAsync())
                .GroupBy(p => p.FormacaoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            string? PosicaoGranularDe(Escalacao e)
            {
                var formacaoId = e.IsTimeCasa ? e.Jogo.FormacaoCasaId : e.Jogo.FormacaoVisitanteId;
                if (formacaoId == null || !slotsPorFormacao.TryGetValue(formacaoId.Value, out var slots))
                    return null;
                return PosicaoJogadorHelper.PosicaoGranular(slots, e.PosicaoX, e.PosicaoY);
            }

            var granularPorEscalacao = escalacaoSelecionadaPorJogo
                .ToDictionary(e => e.Id, e => PosicaoGranularDe(e) ?? e.Posicao!);

            var posicaoPorJogoId = escalacaoSelecionadaPorJogo
                .ToDictionary(e => e.JogoId, e => granularPorEscalacao[e.Id]);

            // Lado (casa/visitante) do jogador em cada jogo, tirado da escalação da
            // época — inclui reservas. Usar o time ATUAL inverteria o histórico após
            // uma transferência (os jogos do clube antigo virariam "contra" ele).
            var ladoPorJogoId = escalacoes
                .GroupBy(e => e.JogoId)
                .ToDictionary(g => g.Key, g => g.First().IsTimeCasa);
            var minutosPorJogoId = estatisticas
                .Where(e => e.Minutos.HasValue)
                .ToDictionary(e => e.JogoId, e => e.Minutos!.Value);

            var criteriosCompartilhados = await _context.CriteriosNota
                .Where(c => c.UsuarioId == null).ToListAsync();
            var criteriosUsuario = await _context.CriteriosNota
                .Where(c => c.UsuarioId == uid).ToListAsync();
            var criteriosBanco = CriteriosNotaHelper.MergeCriterios(criteriosCompartilhados, criteriosUsuario);

            // ── Helper: monta item a partir de um jogo ────────────────────
            NotaJogoItem MontarItem(Jogo jogo, double notaValor, string? comentario,
                List<Notadetalhe> detalhes, bool analisado, bool origemManual = false, double? notaManual = null)
            {
                var pc = jogo.PlacarCasa ?? 0;
                var pv = jogo.PlacarVisitante ?? 0;
                // Prioriza o lado registrado na escalação daquele jogo (correto mesmo
                // após uma transferência); só cai pro time atual quando não há
                // escalação salva pra esse jogo (só nota/estatística importada).
                bool isCasa = ladoPorJogoId.TryGetValue(jogo.Id, out var ladoCasa)
                    ? ladoCasa
                    : (jogo.TimeCasaId == jogador.TimeId || jogo.TimeCasaId == jogador.SelecaoId);

                int golsPro    = isCasa ? pc : pv;
                int golsContra = isCasa ? pv : pc;

                string resultado;
                if (!jogo.PlacarCasa.HasValue)                     resultado = "?";
                else if (pc == pv)                                  resultado = "E";
                else if ((isCasa && pc > pv) || (!isCasa && pv > pc)) resultado = "V";
                else                                                resultado = "D";

                double notaFinal;
                if (!analisado)
                    notaFinal = 0;
                else if (notaManual.HasValue)
                    // Override: nota final absoluta (0–10), sem somar base.
                    notaFinal = Math.Round(Math.Max(0, Math.Min(10, notaManual.Value)), 2);
                else
                    notaFinal = Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima, Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + notaValor)), 2);

                return new NotaJogoItem
                {
                    Jogo = jogo,
                    Analisado = analisado,
                    Nota = notaValor,
                    Comentario = comentario ?? "",
                    Posicao = posicaoPorJogoId.TryGetValue(jogo.Id, out var posJogo) ? posJogo : null,
                    Minutos = minutosPorJogoId.TryGetValue(jogo.Id, out var minJogo) ? minJogo : null,
                    Gols = gols.Count(g => g.JogoId == jogo.Id),
                    Assistencias = assistencias.Count(a => a.JogoId == jogo.Id),
                    Cartoes = cartoes.Count(c => c.JogoId == jogo.Id),
                    Resultado = resultado,
                    IsCasa = isCasa,
                    BonusResultado = 0,
                    NotaFinal = notaFinal,
                    Detalhes = detalhes,
                    GolsPro = golsPro,
                    GolsContra = golsContra,
                    NotaBaseFixa = analisado && !notaManual.HasValue ? CriteriosNotaHelper.NotaBaseFixa : 0,
                    OrigemManual = origemManual,
                    NotaManual = notaManual,
                };
            }

            // ── Jogos com nota manual (avaliação dada por um analista) ────
            var itensManual = notas.Select(n =>
                MontarItem(n.Jogo, n.Valor, n.Comentario, n.Detalhes?.ToList() ?? new(), true, origemManual: true, notaManual: n.NotaManual));

            // ── Jogos sem nota manual, mas com estatísticas importadas ────
            var itensEstatisticas = estatisticas
                .Where(e => !jogosComNotaManualIds.Contains(e.JogoId))
                .Select(e => MontarItem(
                    e.Jogo,
                    Math.Round(CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco), 2),
                    null,
                    CriteriosNotaHelper.ConstruirDetalhes(e, criteriosBanco),
                    true));

            // ── Jogos sem nota manual nem estatística importada ───────────
            var itensNaoAnalisados = escalacoes
                .Where(e => !jogosComNotaManualIds.Contains(e.JogoId) && !jogosComEstatisticaIds.Contains(e.JogoId))
                .GroupBy(e => e.JogoId)
                .Select(g => g.First())
                .Select(e => MontarItem(e.Jogo, 0, null, new(), false));

            var notasPorJogo = itensManual
                .Concat(itensEstatisticas)
                .Concat(itensNaoAnalisados)
                .OrderByDescending(x => x.Jogo.Data)
                .ToList();

            // Observações marcadas com a tag "Jogador" (criadas em /Jogos/Analisar),
            // exibidas junto ao jogo correspondente no histórico.
            var observacoesJogadorPorJogo = await _context.ObservacoesJogoTag
                .Where(o => o.Tipo == "JOGADOR" && o.JogadorId == id && o.UsuarioId == uid)
                .GroupBy(o => o.JogoId)
                .ToDictionaryAsync(g => g.Key, g => g.OrderBy(o => o.Ordem).Select(o => o.Texto).ToList());

            foreach (var item in notasPorJogo)
                if (observacoesJogadorPorJogo.TryGetValue(item.Jogo.Id, out var obsJogador))
                    item.ObservacoesJogador = obsJogador;

            // ── Posições em campo (agregado das escalações tituladas) ─────
            var posicoesPorJogo = escalacaoSelecionadaPorJogo;

            var posicoesJogadas = posicoesPorJogo
                .GroupBy(e => granularPorEscalacao[e.Id])
                .Select(g =>
                {
                    // (0,0) não é uma posição real em nenhuma formação (todo slot cadastrado
                    // fica afastado dos cantos) — indica escalação sem coordenada salva, ex.:
                    // formação sem posições configuradas em PosicoesFormacao. Entra na contagem
                    // (a posição em si, herdada da API, ainda vale), mas não pode entrar na média
                    // de X/Y, senão puxa o ponto no mini-campo pro canto do ataque.
                    var comCoordenada = g.Where(e => e.PosicaoX != 0 || e.PosicaoY != 0).ToList();
                    double x, y;
                    if (comCoordenada.Count > 0)
                    {
                        x = Math.Round(comCoordenada.Average(e => e.PosicaoX), 1);
                        y = Math.Round(comCoordenada.Average(e => e.PosicaoY), 1);
                    }
                    else
                    {
                        // Nenhuma escalação do grupo tem coordenada real (ex.: só jogos com
                        // formação sem slots cadastrados) — usa uma âncora genérica pelo
                        // rótulo em vez de (0,0), que cairia no canto do ataque.
                        (x, y) = CoordenadaGenericaPorRotulo(g.Key);
                    }
                    return new PosicaoJogadaItem
                    {
                        Posicao = g.Key,
                        Jogos = g.Count(),
                        Pct = Math.Round(100.0 * g.Count() / posicoesPorJogo.Count),
                        X = x,
                        Y = y,
                    };
                })
                .OrderByDescending(p => p.Jogos)
                .ToList();

            AfastarPosicoesSobrepostas(posicoesJogadas);

            // Um ponto por jogo (titular, com coordenada real — mesmo critério usado
            // acima pra não puxar a média pro canto do ataque com (0,0) inexistente).
            var pontosHeatmap = posicoesPorJogo
                .Where(e => e.PosicaoX != 0 || e.PosicaoY != 0)
                .Select(e => new PontoHeatmap { X = e.PosicaoX, Y = e.PosicaoY, Peso = 1.0 })
                .ToList();

            // Destinos das setas de movimentação daquele jogo — mesmo peso reduzido
            // usado no mapa de calor de /Jogos/Analisar (indica lugar por onde
            // passou, não a posição principal).
            pontosHeatmap.AddRange(posicoesPorJogo
                .SelectMany(e => e.Setas)
                .Select(s => new PontoHeatmap { X = s.X, Y = s.Y, Peso = 0.45 }));

            // ── Médias por jogo (estatísticas importadas) ─────────────────
            MediasPorJogo? medias = null;
            if (estatisticas.Count > 0)
            {
                static int Pct(int certos, int total) =>
                    total > 0 ? (int)Math.Round(100.0 * certos / total) : 0;

                medias = new MediasPorJogo
                {
                    Jogos = estatisticas.Count,
                    Passes = Math.Round(estatisticas.Average(e => e.PassesTotal), 1),
                    PassesChave = Math.Round(estatisticas.Average(e => e.PassesChave), 1),
                    Finalizacoes = Math.Round(estatisticas.Average(e => e.FinalizacoesTotal), 1),
                    FinalizacoesPct = Pct(estatisticas.Sum(e => e.FinalizacoesNoGol), estatisticas.Sum(e => e.FinalizacoesTotal)),
                    Dribles = Math.Round(estatisticas.Average(e => e.DriblesTentados), 1),
                    DriblesPct = Pct(estatisticas.Sum(e => e.DriblesCertos), estatisticas.Sum(e => e.DriblesTentados)),
                    Duelos = Math.Round(estatisticas.Average(e => e.DuelosTotal), 1),
                    DuelosPct = Pct(estatisticas.Sum(e => e.DuelosVencidos), estatisticas.Sum(e => e.DuelosTotal)),
                    Desarmes = Math.Round(estatisticas.Average(e => e.Desarmes), 1),
                    Interceptacoes = Math.Round(estatisticas.Average(e => e.Interceptacoes), 1),
                    Bloqueios = Math.Round(estatisticas.Average(e => e.Bloqueios), 1),
                    Defesas = Math.Round(estatisticas.Average(e => e.Defesas), 1),
                    FaltasSofridas = Math.Round(estatisticas.Average(e => e.FaltasSofridas), 1),
                    FaltasCometidas = Math.Round(estatisticas.Average(e => e.FaltasCometidas), 1),
                };
            }

            double mediaFinal = notasPorJogo.Any(x => x.Analisado)
                ? Math.Round(notasPorJogo.Where(x => x.Analisado).Average(x => x.NotaFinal), 2)
                : 0;

            var vm = new JogadorEstatisticasViewModel
            {
                Jogador = jogador,
                MediaNotas = mediaFinal,
                TotalJogos = notasPorJogo.Count(x => x.Analisado),
                TotalJogosParticipados = notasPorJogo.Count,
                TotalGols = gols.Count,
                TotalAssistencias = assistencias.Count,
                NotasPorJogo = notasPorJogo,
                PosicoesJogadas = posicoesJogadas,
                PontosHeatmap = pontosHeatmap,
                Medias = medias
            };

            return View(vm);
        }

        // GET: /Jogadores/JogadoresSemelhantes?id=X
        // Retorna até 10 jogadores com perfil parecido ao jogador informado.
        // Critérios derivados do próprio jogador (posição, idade, jogos, gols, assistências);
        // um candidato precisa bater em pelo menos 4 deles para entrar no resultado.
        [HttpGet]
        public async Task<IActionResult> JogadoresSemelhantes(int id)
        {
            var uid = _userManager.GetUserId(User);

            var alvo = await _context.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (alvo == null) return NotFound();

            // ── Agregações por jogador (toda a base registrada) ───────────
            var golsPorJogador = await _context.Gols
                .Where(g => !g.Contra)
                .GroupBy(g => g.JogadorId)
                .Select(g => new { Id = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Total);

            var assisPorJogador = await _context.Assistencias
                .GroupBy(a => a.JogadorId)
                .Select(g => new { Id = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Total);

            // Jogos = nº de partidas distintas em que o jogador foi escalado
            // (escalações próprias do usuário ou compartilhadas).
            var jogosPorJogador = (await _context.Escalacoes
                    .Where(e => e.JogadorId.HasValue && (e.UsuarioId == uid || e.UsuarioId == null))
                    .Select(e => new { Jid = e.JogadorId!.Value, e.JogoId })
                    .Distinct()
                    .ToListAsync())
                .GroupBy(x => x.Jid)
                .ToDictionary(g => g.Key, g => g.Count());

            // Fallback de jogos via estatísticas importadas (quando não há escalação).
            // Exclui reservas não utilizados (Minutos 0/null).
            var jogosEstatPorJogador = (await _context.EstatisticasJogador
                    .Where(e => e.Minutos != null && e.Minutos > 0)
                    .Select(e => new { e.JogadorId, e.JogoId })
                    .Distinct()
                    .ToListAsync())
                .GroupBy(x => x.JogadorId)
                .ToDictionary(g => g.Key, g => g.Count());

            int Jogos(int jid) => Math.Max(
                jogosPorJogador.GetValueOrDefault(jid, 0),
                jogosEstatPorJogador.GetValueOrDefault(jid, 0));

            // Estatísticas avançadas (api-football) somadas por jogador — base
            // para as métricas defensivas / de criação usadas conforme a posição.
            // Exclui reservas não utilizados (Minutos 0/null).
            var estatAgg = await _context.EstatisticasJogador
                .Where(e => e.Minutos != null && e.Minutos > 0)
                .GroupBy(e => e.JogadorId)
                .Select(g => new
                {
                    Id                = g.Key,
                    Defesas           = g.Sum(e => e.Defesas),
                    GolsSofridos      = g.Sum(e => e.GolsSofridos),
                    Desarmes          = g.Sum(e => e.Desarmes),
                    Bloqueios         = g.Sum(e => e.Bloqueios),
                    Interceptacoes    = g.Sum(e => e.Interceptacoes),
                    DuelosVencidos    = g.Sum(e => e.DuelosVencidos),
                    PassesChave       = g.Sum(e => e.PassesChave),
                    FinalizacoesNoGol = g.Sum(e => e.FinalizacoesNoGol),
                    DriblesCertos     = g.Sum(e => e.DriblesCertos),
                    Rating            = g.Average(e => e.Rating),
                })
                .ToDictionaryAsync(x => x.Id, x => x);

            // Valor de uma métrica para um jogador (0 quando não há dado).
            // Rating é multiplicado por 10 para permitir limiar inteiro.
            int Metrica(string key, int jid)
            {
                switch (key)
                {
                    case "jogos":  return Jogos(jid);
                    case "gols":   return golsPorJogador.GetValueOrDefault(jid, 0);
                    case "assist": return assisPorJogador.GetValueOrDefault(jid, 0);
                    case "rating":
                        return estatAgg.TryGetValue(jid, out var er) && er.Rating.HasValue
                            ? (int)Math.Round(er.Rating.Value * 10) : 0;
                }
                if (!estatAgg.TryGetValue(jid, out var e)) return 0;
                return key switch
                {
                    "defesas"        => e.Defesas,
                    "golsSofridos"   => e.GolsSofridos,
                    "desarmes"       => e.Desarmes,
                    "bloqueios"      => e.Bloqueios,
                    "interceptacoes" => e.Interceptacoes,
                    "duelosVencidos" => e.DuelosVencidos,
                    "passesChave"    => e.PassesChave,
                    "finNoGol"       => e.FinalizacoesNoGol,
                    "driblesCertos"  => e.DriblesCertos,
                    _ => 0,
                };
            }

            // Grupo de posição → define quais métricas comparar.
            // Usa a mesma convenção dos rankings de /Relatorios (posições amplas
            // vindas da api-football: Goleiro/Defensor/Meia/Atacante), por Contains
            // para também cobrir variações detalhadas (Zagueiro, Lateral, Ponta...).
            static string GrupoPosicao(string? pos)
            {
                if (string.IsNullOrEmpty(pos)) return "ATA";
                bool C(string s) => pos.Contains(s, StringComparison.OrdinalIgnoreCase);
                if (C("Goleiro")) return "GOL";
                if (C("Defensor") || C("Zagueiro") || C("Lateral") || C("Ala")) return "DEF";
                if (C("Meia") || C("Meio") || C("Volante")) return "MEI";
                return "ATA"; // Atacante, Ponta, Centroavante e demais
            }

            string grupo = GrupoPosicao(alvo.Posicao);

            // Métricas usadas como critério (limiar = 70% do valor do alvo).
            (string key, string label, string emoji)[] criterioMetricas = grupo switch
            {
                "GOL" => new[] { ("defesas", "Defesas", "🧤"), ("rating", "Rating", "⭐") },
                "DEF" => new[] { ("desarmes", "Desarmes", "🛡️"), ("bloqueios", "Bloqueios", "🧱"), ("interceptacoes", "Interceptações", "✋"), ("duelosVencidos", "Duelos venc.", "💪") },
                "MEI" => new[] { ("desarmes", "Desarmes", "🛡️"), ("interceptacoes", "Interceptações", "✋"), ("passesChave", "Passes-chave", "🎯"), ("assist", "Assistências", "🅰️") },
                _     => new[] { ("gols", "Gols", "⚽"), ("assist", "Assistências", "🅰️"), ("finNoGol", "Fin. no alvo", "🎯"), ("driblesCertos", "Dribles certos", "🌀") },
            };

            // Colunas exibidas no card de cada jogador.
            (string key, string label)[] colunasDef = grupo switch
            {
                "GOL" => new[] { ("jogos", "Jogos"), ("defesas", "Defesas"), ("golsSofridos", "G.Sofr.") },
                "DEF" => new[] { ("jogos", "Jogos"), ("desarmes", "Desarm."), ("interceptacoes", "Interc."), ("bloqueios", "Bloq.") },
                "MEI" => new[] { ("jogos", "Jogos"), ("passesChave", "P.Chave"), ("desarmes", "Desarm."), ("assist", "Assist.") },
                _     => new[] { ("jogos", "Jogos"), ("gols", "Gols"), ("assist", "Assist."), ("finNoGol", "Fin.") },
            };

            // ── Perfil do jogador alvo / limiares ─────────────────────────
            int alvoIdade = alvo.Idade;
            int alvoJogos = Jogos(alvo.Id);

            int idadeMax = alvoIdade > 0 ? alvoIdade + 3 : 0;
            int idadeMin = alvoIdade > 3 ? alvoIdade - 3 : 0;
            int minJogos = (int)Math.Round(alvoJogos * 0.7);

            var limiares = criterioMetricas
                .ToDictionary(m => m.key, m => (int)Math.Round(Metrica(m.key, alvo.Id) * 0.7));

            var candidatos = await _context.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .Where(j => j.Id != alvo.Id)
                .ToListAsync();

            var resultados = new List<(Jogador j, int idade, int jogos, int batidos, double dist)>();

            foreach (var c in candidatos)
            {
                int cIdade = c.Idade;
                int cJogos = Jogos(c.Id);

                // Ignora jogadores sem nenhum dado registrado.
                if (cJogos == 0
                    && !golsPorJogador.ContainsKey(c.Id)
                    && !assisPorJogador.ContainsKey(c.Id)
                    && !estatAgg.ContainsKey(c.Id)) continue;

                bool mesmoGrupo = GrupoPosicao(c.Posicao) == grupo;

                int batidos = 0;
                if (mesmoGrupo) batidos++;
                if (cIdade > 0 && (alvoIdade == 0 || (cIdade >= idadeMin && cIdade <= idadeMax))) batidos++;
                if (cJogos >= minJogos) batidos++;
                foreach (var m in criterioMetricas)
                    if (Metrica(m.key, c.Id) >= limiares[m.key]) batidos++;

                if (batidos < 4) continue;

                double dist =
                    Math.Abs(cIdade - alvoIdade) / 5.0
                  + Math.Abs(cJogos - alvoJogos) / Math.Max(1.0, alvoJogos)
                  + (mesmoGrupo ? 0 : 1);
                foreach (var m in criterioMetricas)
                {
                    int av = Metrica(m.key, alvo.Id);
                    dist += Math.Abs(Metrica(m.key, c.Id) - av) / Math.Max(1.0, av);
                }

                resultados.Add((c, cIdade, cJogos, batidos, dist));
            }

            var top = resultados
                .OrderByDescending(r => r.batidos)
                .ThenBy(r => r.dist)
                .Take(10)
                .Select(r => new
                {
                    id        = r.j.Id,
                    nome      = r.j.NomeExibicao,
                    clube     = r.j.Time?.Nome,
                    escudoUrl = r.j.Time?.EscudoUrl,
                    fotoUrl   = r.j.FotoUrl,
                    idade     = r.idade,
                    pais      = r.j.Nacionalidade?.Nome,
                    posicao   = r.j.Posicao,
                    stats     = colunasDef.Select(col => Metrica(col.key, r.j.Id)).ToArray(),
                    batidos   = r.batidos,
                })
                .ToList();

            // Chips dos critérios usados (apenas os com limiar relevante).
            var chips = new List<string>();
            if (!string.IsNullOrEmpty(alvo.Posicao)) chips.Add($"🎽 {alvo.Posicao}");
            if (alvoIdade > 0) chips.Add($"📅 {idadeMin}–{idadeMax} anos");
            if (minJogos > 0) chips.Add($"🏟️ ≥ {minJogos} jogos");
            foreach (var m in criterioMetricas)
            {
                if (m.key == "rating") continue; // limiar de rating não é informativo
                if (limiares[m.key] > 0) chips.Add($"{m.emoji} ≥ {limiares[m.key]} {m.label}");
            }

            return Json(new
            {
                grupo,
                criterios = chips,
                colunas = colunasDef.Select(c => c.label).ToArray(),
                jogadores = top,
            });
        }

        [HttpGet]
        public async Task<IActionResult> EstatisticasTemporada(int id, int season)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador?.IdApi == null)
                return Json(new { error = "Jogador sem IdApi" });

            try
            {
                var stats = await _transfermarktService.BuscarEstatisticasTemporadaAsync(jogador.IdApi.Value, season);
                var result = stats.Select(s => new
                {
                    league = new
                    {
                        id = s.League.Id,
                        name = s.League.Name,
                        country = s.League.Country,
                        logo = s.League.Logo,
                        flag = s.League.Flag,
                        season = s.League.Season
                    },
                    team = new { id = s.Team.Id, name = s.Team.Name, logo = s.Team.Logo },
                    games = new
                    {
                        appearences = s.Games.Appearences ?? 0,
                        lineups = s.Games.Lineups ?? 0,
                        minutes = s.Games.Minutes ?? 0,
                        position = s.Games.Position,
                        rating = s.Games.Rating
                    },
                    goals = new { total = s.Goals.Total ?? 0, assists = s.Goals.Assists ?? 0 },
                    passes = new { total = s.Passes.Total ?? 0, key = s.Passes.Key ?? 0, accuracy = s.Passes.Accuracy },
                    shots = new { total = s.Shots.Total ?? 0, on = s.Shots.On ?? 0 },
                    tackles = new { total = s.Tackles.Total ?? 0, blocks = s.Tackles.Blocks ?? 0, interceptions = s.Tackles.Interceptions ?? 0 },
                    dribbles = new { attempts = s.Dribbles.Attempts ?? 0, success = s.Dribbles.Success ?? 0 },
                    cards = new { yellow = s.Cards.Yellow ?? 0, yellowred = s.Cards.Yellowred ?? 0, red = s.Cards.Red ?? 0 },
                    substitutes = new { @in = s.Substitutes.In ?? 0, @out = s.Substitutes.Out ?? 0, bench = s.Substitutes.Bench ?? 0 },
                    duels = new { total = s.Duels.Total ?? 0, won = s.Duels.Won ?? 0 },
                    fouls = new { drawn = s.Fouls.Drawn ?? 0, committed = s.Fouls.Committed ?? 0 }
                });
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarFoto(int id)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogador == null) return NotFound();

            string? fotoUrl = null;
            if (jogador.IdApi > 0)
            {
                var info = await _transfermarktService.BuscarInfoJogadorAsync(jogador.IdApi!.Value);
                fotoUrl = info?.FotoUrl;
            }

            if (!string.IsNullOrEmpty(fotoUrl))
            {
                jogador.FotoUrl = fotoUrl;
                jogador.DtAlt = DateTime.UtcNow;

                _context.Update(jogador);
                await _context.SaveChangesAsync();

                TempData["Mensagem"] = "Foto atualizada com sucesso!";
            }
            else
            {
                TempData["Mensagem"] = "Não foi possível encontrar a foto (jogador sem IdApi ou sem foto na API).";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarInfo(int id)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador == null) return NotFound();

            if (!(jogador.IdApi > 0))
            {
                TempData["Erro"] = $"{jogador.Nome} não possui IdApi cadastrado — não é possível atualizar pela API.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var info = await _transfermarktService.BuscarPerfilJogadorAsync(jogador.IdApi!.Value);
                if (info == null)
                {
                    TempData["Erro"] = $"Não foi possível obter os dados de {jogador.Nome} na API.";
                    return RedirectToAction(nameof(Index));
                }

                var alteracoes = new List<string>();

                if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900)
                {
                    var novaData = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Unspecified);
                    if (jogador.DataNascimento?.Date != novaData.Date)
                    {
                        jogador.DataNascimento = novaData;
                        alteracoes.Add($"idade ({jogador.Idade} anos)");
                    }
                }

                if (!string.IsNullOrWhiteSpace(info.Nacionalidade))
                {
                    var nac = await ApiFootballService.ResolverOuCriarNacionalidadePublicAsync(_context, info.Nacionalidade);
                    if (nac != null && jogador.NacionalidadeId != nac.Id)
                    {
                        jogador.NacionalidadeId = nac.Id;
                        alteracoes.Add($"nacionalidade ({nac.Nome})");
                    }
                }

                if (!string.IsNullOrWhiteSpace(info.FotoUrl) && jogador.FotoUrl != info.FotoUrl)
                {
                    jogador.FotoUrl = info.FotoUrl;
                    alteracoes.Add("foto");
                }

                if (!string.IsNullOrWhiteSpace(info.PrimeiroNome) && jogador.PrimeiroNome != info.PrimeiroNome)
                {
                    jogador.PrimeiroNome = info.PrimeiroNome;
                    alteracoes.Add("primeiro nome");
                }

                if (!string.IsNullOrWhiteSpace(info.UltimoNome) && jogador.UltimoNome != info.UltimoNome)
                {
                    jogador.UltimoNome = info.UltimoNome;
                    alteracoes.Add("último nome");
                }

                jogador.Atualizado = true;
                jogador.DtAlt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                string listaAlteracoes = alteracoes.Count > 1
                    ? string.Join(", ", alteracoes.Take(alteracoes.Count - 1)) + " e " + alteracoes.Last()
                    : alteracoes.FirstOrDefault() ?? "";

                TempData["Sucesso"] = alteracoes.Any()
                    ? $"{jogador.Nome}: atualizado → {listaAlteracoes}."
                    : $"{jogador.Nome}: informações já estavam atualizadas.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar informações do jogador {Nome} (IdApi={Id})", jogador.Nome, jogador.IdApi);
                TempData["Erro"] = $"Erro ao atualizar {jogador.Nome}: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarInfoTodosSemIdade()
        {
            var jogadores = await _context.Jogadores
                .Where(j => j.DataNascimento == null && j.IdApi != null && j.IdApi > 0)
                .ToListAsync();

            int atualizados = 0, semData = 0, falhas = 0;

            foreach (var jogador in jogadores)
            {
                try
                {
                    var info = await _transfermarktService.BuscarPerfilJogadorAsync(jogador.IdApi!.Value);
                    if (info == null) { falhas++; continue; }

                    bool mudou = false;

                    if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900)
                    {
                        jogador.DataNascimento = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Unspecified);
                        mudou = true;
                    }

                    if (!string.IsNullOrWhiteSpace(info.Nacionalidade))
                    {
                        var nac = await ApiFootballService.ResolverOuCriarNacionalidadePublicAsync(_context, info.Nacionalidade);
                        if (nac != null && jogador.NacionalidadeId != nac.Id)
                        {
                            jogador.NacionalidadeId = nac.Id;
                            mudou = true;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(info.FotoUrl) && jogador.FotoUrl != info.FotoUrl)
                    {
                        jogador.FotoUrl = info.FotoUrl;
                        mudou = true;
                    }

                    if (!string.IsNullOrWhiteSpace(info.PrimeiroNome) && jogador.PrimeiroNome != info.PrimeiroNome)
                    {
                        jogador.PrimeiroNome = info.PrimeiroNome;
                        mudou = true;
                    }

                    if (!string.IsNullOrWhiteSpace(info.UltimoNome) && jogador.UltimoNome != info.UltimoNome)
                    {
                        jogador.UltimoNome = info.UltimoNome;
                        mudou = true;
                    }

                    if (jogador.DataNascimento.HasValue)
                    {
                        atualizados++;
                        jogador.Atualizado = true;
                        jogador.DtAlt = DateTime.UtcNow;
                    }
                    else
                    {
                        // perfil veio sem data e sem idade aproveitável
                        semData++;
                    }

                    if (mudou) await _context.SaveChangesAsync();
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao atualizar informações (lote) de {Nome} (IdApi={Id})", jogador.Nome, jogador.IdApi);
                    falhas++;
                }
            }

            TempData["Sucesso"] =
                $"Atualizados com idade: {atualizados} ✅  |  Ainda sem data na API: {semData} ⚠️  |  Falhas: {falhas} ❌  " +
                $"(total sem idade verificado: {jogadores.Count})";

            return RedirectToAction(nameof(Index), new { semIdade = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarLinkTransfermarkt(int id, string linktransfermarket)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador == null) return NotFound();

            jogador.LinkTransfermarket = linktransfermarket;
            jogador.DtAlt = DateTime.UtcNow;

            _context.Update(jogador);
            await _context.SaveChangesAsync();

            TempData["Mensagem"] = "Link Transfermarkt atualizado com sucesso!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarFotosTodos(int? timeId)
        {
            var query = _context.Jogadores
                .Include(j => j.Time)
                .Where(j => string.IsNullOrEmpty(j.FotoUrl));

            if (timeId.HasValue)
                query = query.Where(j => j.TimeId == timeId.Value);

            var jogadores = await query.ToListAsync();

            int atualizados = 0;
            int falhas = 0;

            foreach (var jogador in jogadores)
            {
                try
                {
                    string? fotoUrl = null;
                    if (jogador.IdApi > 0)
                    {
                        var info = await _transfermarktService.BuscarInfoJogadorAsync(jogador.IdApi!.Value);
                        fotoUrl = info?.FotoUrl;
                    }

                    if (!string.IsNullOrWhiteSpace(fotoUrl))
                    {
                        jogador.FotoUrl = fotoUrl;
                        jogador.DtAlt = DateTime.UtcNow;
                        atualizados++;
                    }
                    else
                    {
                        falhas++;
                    }

                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao buscar foto de {Nome}", jogador.Nome);
                    falhas++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] =
                $"Fotos atualizadas: {atualizados} ✅  |  Não encontradas: {falhas} ❌  " +
                $"(total verificado: {jogadores.Count})";

            return RedirectToAction(nameof(Index));
        }

        // Âncora genérica no mini-campo para os rótulos crus vindos da API (usados
        // quando a formação do jogo não tem slots cadastrados em PosicoesFormacao,
        // então não há coordenada real pra calcular a média) — mantém o ponto na
        // zona correta do campo (defesa/meio/ataque) em vez do canto (0,0).
        private static (double X, double Y) CoordenadaGenericaPorRotulo(string rotulo) => rotulo switch
        {
            "Goleiro"  => (50, 90),
            "Defensor" => (50, 78),
            "Meia"     => (50, 48),
            "Atacante" => (50, 18),
            _          => (50, 50)
        };

        // Afasta os pontos do mini-campo "Posições em campo" (Estatisticas.cshtml)
        // quando duas posições têm médias muito próximas (ex.: Centroavante e
        // Atacante) — sem isso os círculos se sobrepõem e o texto vira ilegível.
        // Trabalha em pixels (dimensões fixas do .mini-campo: 160×240, dot 34px)
        // e depois converte de volta para % antes de devolver.
        private static void AfastarPosicoesSobrepostas(List<PosicaoJogadaItem> posicoes)
        {
            const double largura = 160, altura = 240, diametro = 34, distMinima = diametro + 6;
            const double margem = diametro / 2 + 4;

            if (posicoes.Count < 2) return;

            var pts = posicoes.Select(p => (x: p.X / 100.0 * largura, y: p.Y / 100.0 * altura)).ToList();

            // Clampa a CADA iteração (não só no final): senão pontos empurrados perto
            // da borda voltam a colidir quando o clamp final os "esmaga" de volta.
            void Clampar()
            {
                for (int k = 0; k < pts.Count; k++)
                    pts[k] = (Math.Clamp(pts[k].x, margem, largura - margem),
                              Math.Clamp(pts[k].y, margem, altura - margem));
            }

            Clampar();

            for (int iter = 0; iter < 20; iter++)
            {
                bool moveu = false;
                for (int i = 0; i < pts.Count; i++)
                {
                    for (int j = i + 1; j < pts.Count; j++)
                    {
                        double dx = pts[j].x - pts[i].x, dy = pts[j].y - pts[i].y;
                        double dist = Math.Sqrt(dx * dx + dy * dy);

                        if (dist <= 0.01)
                        {
                            // Exatamente sobrepostos: espalha em ângulos diferentes por par
                            // (não sempre a mesma direção, senão pontos em cadeia colidem de novo).
                            double ang = 2 * Math.PI * j / pts.Count;
                            pts[j] = (pts[i].x + Math.Cos(ang) * distMinima, pts[i].y + Math.Sin(ang) * distMinima);
                            moveu = true;
                        }
                        else if (dist < distMinima)
                        {
                            double falta = (distMinima - dist) / 2;
                            double ux = dx / dist, uy = dy / dist;
                            pts[i] = (pts[i].x - ux * falta, pts[i].y - uy * falta);
                            pts[j] = (pts[j].x + ux * falta, pts[j].y + uy * falta);
                            moveu = true;
                        }
                    }
                }
                Clampar();
                if (!moveu) break;
            }

            for (int i = 0; i < posicoes.Count; i++)
            {
                posicoes[i].X = Math.Round(pts[i].x / largura * 100, 1);
                posicoes[i].Y = Math.Round(pts[i].y / altura * 100, 1);
            }
        }
    }
}