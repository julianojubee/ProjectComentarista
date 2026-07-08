using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class TreinadoresController : Controller
    {
        private readonly FutebolContext _context;
        private readonly TransfermarktTreinadorService _tmTreinadorService;
        private readonly ApiFootballService _apiFootball;

        public TreinadoresController(
            FutebolContext context,
            TransfermarktTreinadorService tmTreinadorService,
            ApiFootballService apiFootball)
        {
            _context = context;
            _tmTreinadorService = tmTreinadorService;
            _apiFootball = apiFootball;
        }

        // GET: Treinadores
        public async Task<IActionResult> Index(string? nome, List<int>? competicaoIds, List<int>? timeIds, List<string>? nacionalidades, int page = 1)
        {
            const int pageSize = 50;
            competicaoIds ??= new List<int>();
            timeIds ??= new List<int>();
            nacionalidades ??= new List<string>();

            var query = _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(nome))
                query = query.Where(t => t.Nome.Contains(nome));

            // Filtro por competições: treinadores cujos times jogaram nas competições selecionadas
            if (competicaoIds.Any())
            {
                var jogosComp = _context.Jogos.Where(j => competicaoIds.Contains(j.CompeticaoId));
                var timesComp = await jogosComp.Select(j => j.TimeCasaId)
                    .Union(jogosComp.Select(j => j.TimeVisitanteId))
                    .Distinct()
                    .ToListAsync();
                query = query.Where(t => timesComp.Contains(t.TimeId));
            }

            if (timeIds.Any())
                query = query.Where(t => timeIds.Contains(t.TimeId));

            if (nacionalidades.Any())
                query = query.Where(t => t.Nacionalidade != null && nacionalidades.Contains(t.Nacionalidade.Nome));

            query = query.OrderBy(t => t.Nome);

            // Paginação (50 por página)
            var totalTreinadores = await query.CountAsync();
            var totalPaginas = (int)Math.Ceiling(totalTreinadores / (double)pageSize);
            if (totalPaginas < 1) totalPaginas = 1;
            if (page < 1) page = 1;
            if (page > totalPaginas) page = totalPaginas;

            var treinadoresPagina = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var stats = await CalcularStatsCardAsync(treinadoresPagina);

            var vm = new TreinadoresIndexViewModel
            {
                Itens = treinadoresPagina,

                // Listas completas para os tag selectors
                Competicoes = await _context.Competicoes.OrderBy(c => c.Nome).ToListAsync(),
                Times = await _context.Times.OrderBy(t => t.Nome).ToListAsync(),
                NacionalidadesLista = await _context.Nacionalidades.OrderBy(n => n.Nome).ToListAsync(),
                NomeFiltro = nome,
                CompeticaoIdsFiltro = competicaoIds,
                TimeIdsFiltro = timeIds,
                NacionalidadesFiltro = nacionalidades,
                Stats = stats,

                PaginaAtual = page,
                TotalPaginas = totalPaginas,
                TotalTreinadores = totalTreinadores,
                PageSize = pageSize
            };

            return View(vm);
        }

        // Calcula V/E/D e "desde" de cada treinador da página, com base nos jogos do time
        // atual desde o início da passagem (histórico aberto) ou, na ausência de histórico
        // importado, a data de cadastro do treinador. Feito em lote (2 queries) para não gerar
        // N+1 na listagem paginada.
        private async Task<Dictionary<int, TreinadorCardStats>> CalcularStatsCardAsync(List<Treinador> treinadores)
        {
            var resultado = new Dictionary<int, TreinadorCardStats>();
            if (!treinadores.Any()) return resultado;

            var idsTreinadores = treinadores.Select(t => t.Id).ToList();
            var timeIds = treinadores.Select(t => t.TimeId).Distinct().ToList();

            var historicosAbertos = await _context.TreinadoresHistorico
                .AsNoTracking()
                .Where(h => idsTreinadores.Contains(h.TreinadorId) && h.DtFim == null)
                .ToListAsync();
            var inicioPorTreinador = historicosAbertos
                .GroupBy(h => h.TreinadorId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.DtInicio).First().DtInicio);

            var jogosTimes = await _context.Jogos
                .AsNoTracking()
                .Where(j => (timeIds.Contains(j.TimeCasaId) || timeIds.Contains(j.TimeVisitanteId))
                            && j.PlacarCasa != null && j.PlacarVisitante != null && j.Data != null)
                .Select(j => new { j.TimeCasaId, j.TimeVisitanteId, j.PlacarCasa, j.PlacarVisitante, j.Data })
                .ToListAsync();

            foreach (var t in treinadores)
            {
                var desde = inicioPorTreinador.TryGetValue(t.Id, out var dtHistorico) ? dtHistorico : t.DtInc;
                var stat = new TreinadorCardStats { Desde = desde };

                foreach (var jogo in jogosTimes)
                {
                    if (jogo.TimeCasaId != t.TimeId && jogo.TimeVisitanteId != t.TimeId) continue;
                    if (jogo.Data < desde) continue;

                    bool casa = jogo.TimeCasaId == t.TimeId;
                    int golsPro = casa ? jogo.PlacarCasa!.Value : jogo.PlacarVisitante!.Value;
                    int golsContra = casa ? jogo.PlacarVisitante!.Value : jogo.PlacarCasa!.Value;

                    if (golsPro > golsContra) stat.Vitorias++;
                    else if (golsPro == golsContra) stat.Empates++;
                    else stat.Derrotas++;
                }

                resultado[t.Id] = stat;
            }

            return resultado;
        }

        // GET: Treinadores/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .Include(t => t.Historicos)
                    .ThenInclude(h => h.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            return View(treinador);
        }

        // GET: Treinadores/Create
        public IActionResult Create()
        {
            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome");
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ConsultarHistorico(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            // Busca histórico já salvo no banco
            var historico = await _context.TreinadoresHistorico
            .Include(h => h.Time)
            .Where(h => h.TreinadorId == id)
            .OrderByDescending(h => h.DtInicio) // último trabalho primeiro
            .ToListAsync();

            ViewBag.Treinador = treinador;
            return View("HistoricoConsulta", historico);
        }


        // POST: Treinadores/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Treinador treinador)
        {
            if (ModelState.IsValid)
            {
                treinador.DtInc = DateTime.UtcNow;
                _context.Add(treinador);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", treinador.TimeId);
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome", treinador.NacionalidadeId);
            return View(treinador);
        }

        // GET: Treinadores/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var treinador = await _context.Treinadores.FindAsync(id);
            if (treinador == null) return NotFound();

            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", treinador.TimeId);
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome", treinador.NacionalidadeId);
            return View(treinador);
        }

        // POST: Treinadores/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Treinador treinador)
        {
            if (id != treinador.Id) return NotFound();

            if (ModelState.IsValid)
            {
                // Atualiza só os campos editáveis na entidade existente — assim campos que
                // não estão no formulário (IdApi, LinkOgol, DtInc, históricos) são preservados
                // em vez de serem zerados por um Update do objeto vindo só do form.
                var existente = await _context.Treinadores.FirstOrDefaultAsync(t => t.Id == id);
                if (existente == null) return NotFound();

                existente.Nome = treinador.Nome;
                existente.NacionalidadeId = treinador.NacionalidadeId;
                existente.DataNascimento = treinador.DataNascimento;
                existente.TimeId = treinador.TimeId;
                existente.FotoUrl = string.IsNullOrWhiteSpace(treinador.FotoUrl)
                    ? null : treinador.FotoUrl.Trim();
                existente.DtAlt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", treinador.TimeId);
            ViewBag.Nacionalidades = new SelectList(_context.Nacionalidades, "Id", "Nome", treinador.NacionalidadeId);
            return View(treinador);
        }

        // GET: Treinadores/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();
            return View(treinador);
        }

        // POST: Treinadores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var treinador = await _context.Treinadores.FindAsync(id);
            if (treinador != null)
            {
                _context.Treinadores.Remove(treinador);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ── Buscar dados (foto + idade + nacionalidade) via api-football ────────

        // Reconstrói a URL do Index preservando os filtros multi-seleção e a página atual
        private IActionResult RedirectIndexComFiltros(
            List<int>? competicaoIds, List<int>? timeIds, List<string>? nacionalidades, int page)
        {
            var qs = new System.Text.StringBuilder();
            qs.Append("?page=").Append(page < 1 ? 1 : page);
            foreach (var cid in competicaoIds ?? new()) qs.Append("&competicaoIds=").Append(cid);
            foreach (var tid in timeIds ?? new()) qs.Append("&timeIds=").Append(tid);
            foreach (var n in nacionalidades ?? new()) qs.Append("&nacionalidades=").Append(Uri.EscapeDataString(n));
            return Redirect(Url.Action(nameof(Index)) + qs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuscarFoto(int id,
            List<int>? competicaoIds, List<int>? timeIds, List<string>? nacionalidades, int page = 1)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            try
            {
                // Passa o nome completo: o serviço tenta o nome inteiro e, com o id do time,
                // cada parte do nome até a API encontrar. O sobrenome nem sempre é a última
                // palavra (ex.: "Francisco Zubeldia Luis" só acha buscando "Zubeldia").
                var termoBusca = treinador.Nome;
                var teamApiId = treinador.Time?.IdApi;

                // Resolve o registro certo (o mesmo tratamento de "stub" usado na importação
                // de histórico via API) — evita travar no cadastro parcial que a api-football
                // cria quando o técnico assume um time novo.
                var (melhor, registros, ambiguo) = await _apiFootball.ResolverTreinadorApiAsync(
                    termoBusca, teamApiId, treinador.IdApi);

                if (melhor == null)
                {
                    TempData["Erro"] = $"❌ Nenhum treinador encontrado na API para '{termoBusca}'.";
                    return RedirectIndexComFiltros(competicaoIds, timeIds, nacionalidades, page);
                }

                var alteracoes = new List<string>();

                // registros.Count > 1 só acontece quando um stub foi resolvido para o
                // registro completo correspondente (ver ResolverTreinadorApiAsync).
                var resolveuStub = registros.Count > 1;

                // Grava/atualiza o IdApi para travar as próximas buscas no técnico certo —
                // sempre o id do registro completo quando a resolução encontrar um, o que
                // corrige automaticamente treinadores já travados no id de um stub.
                if (melhor.Id is int coachId && coachId > 0 && treinador.IdApi != coachId)
                {
                    treinador.IdApi = coachId;
                }

                // Idade / data de nascimento
                DateTime? novaData = null;
                if (!string.IsNullOrEmpty(melhor.Birth?.Date) &&
                    DateTime.TryParse(melhor.Birth.Date,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dtNasc))
                {
                    novaData = dtNasc;
                }
                else if (melhor.Age is int idadeApi && idadeApi > 0 && idadeApi < 120)
                {
                    // Sem data na API: estima 01/01 do ano que resulta na idade informada.
                    novaData = new DateTime(DateTime.Today.Year - idadeApi, 1, 1);
                }

                if (novaData.HasValue && novaData.Value.Year > 1900)
                {
                    var data = DateTime.SpecifyKind(novaData.Value, DateTimeKind.Unspecified);
                    if (treinador.DataNascimento?.Date != data.Date)
                    {
                        treinador.DataNascimento = data;
                        alteracoes.Add($"idade ({treinador.Idade} anos)");
                    }
                }

                // Nacionalidade (resolve ou cria)
                if (!string.IsNullOrWhiteSpace(melhor.Nationality))
                {
                    var nac = await ApiFootballService.ResolverOuCriarNacionalidadePublicAsync(_context, melhor.Nationality);
                    if (nac != null && treinador.NacionalidadeId != nac.Id)
                    {
                        treinador.NacionalidadeId = nac.Id;
                        alteracoes.Add($"nacionalidade ({nac.Nome})");
                    }
                }

                // Foto
                if (!string.IsNullOrEmpty(melhor.Photo) && treinador.FotoUrl != melhor.Photo)
                {
                    treinador.FotoUrl = melhor.Photo;
                    alteracoes.Add("foto");
                }

                treinador.DtAlt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                if (alteracoes.Any())
                {
                    string lista = alteracoes.Count > 1
                        ? string.Join(", ", alteracoes.Take(alteracoes.Count - 1)) + " e " + alteracoes.Last()
                        : alteracoes.First();
                    var complemento = resolveuStub
                        ? " — registro completo do técnico localizado (a API tinha um cadastro parcial vinculado ao time novo)"
                        : "";
                    TempData["Sucesso"] = $"{treinador.Nome}: atualizado → {lista}{complemento}.";
                }
                else
                {
                    TempData["Info"] = $"{treinador.Nome}: informações já estavam atualizadas.";
                }

                if (ambiguo)
                {
                    var aviso = $"⚠️ A API tem mais de um técnico com nome parecido a '{treinador.Nome}' — " +
                        "só foi encontrado um cadastro parcial (sem idade/nacionalidade confirmadas) e não foi " +
                        "possível confirmar qual é o registro completo.";
                    TempData["Info"] = TempData["Info"] != null ? $"{TempData["Info"]} {aviso}" : aviso;
                }
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"❌ Erro ao buscar dados: {ex.Message}";
            }

            return RedirectIndexComFiltros(competicaoIds, timeIds, nacionalidades, page);
        }

        // ── Importar histórico pelo nome (busca automática) ──────────────────

        /// <summary>
        /// GET: Abre a tela de pré-visualização do histórico antes de salvar.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PreVisualizarHistorico(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            var info = await _tmTreinadorService.BuscarTreinadorAsync(
                treinador.Nome, treinador.Time?.Nome);

            if (info == null || !info.Historico.Any())
            {
                TempData["Erro"] =
                    $"Nenhum histórico encontrado para '{treinador.Nome}' no Transfermarkt. " +
                    "Tente usar a URL direta do perfil.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Treinador = treinador;
            ViewBag.Historico = info.Historico;
            ViewBag.ProfileUrl = info.ProfileUrl;
            ViewBag.FotoUrl = info.FotoUrl;

            return View("HistoricoPreVisualizacao", info);
        }

        /// <summary>
        /// GET: Busca histórico via URL direta do Transfermarkt.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PreVisualizarHistoricoUrl(int id, string url)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            if (string.IsNullOrWhiteSpace(url))
            {
                TempData["Erro"] = "URL inválida.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Converte URL de "estadisticas" ou "leistungsdaten" para "profil"
            url = NormalizarUrlPerfil(url);

            var info = await _tmTreinadorService.BuscarPerfilAsync(url);

            if (info == null || !info.Historico.Any())
            {
                TempData["Erro"] =
                    "Nenhum histórico encontrado na URL informada. " +
                    "Verifique se é uma URL de perfil de treinador válida.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Treinador = treinador;
            ViewBag.Historico = info.Historico;
            ViewBag.ProfileUrl = info.ProfileUrl;
            ViewBag.FotoUrl = info.FotoUrl;
            ViewBag.TreinadorId = id;

            return View("HistoricoPreVisualizacao", info);
        }

        /// <summary>
        /// POST: Confirma e salva o histórico no banco.
        /// Recebe os dados como JSON serializado nos hidden inputs.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarHistorico(
            int treinadorId,
            string profileUrl,
            bool atualizarFoto = false)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == treinadorId);

            if (treinador == null) return NotFound();

            if (string.IsNullOrWhiteSpace(profileUrl))
            {
                TempData["Erro"] = "URL do perfil não informada.";
                return RedirectToAction(nameof(Details), new { id = treinadorId });
            }

            profileUrl = NormalizarUrlPerfil(profileUrl);

            var info = await _tmTreinadorService.BuscarPerfilAsync(profileUrl);

            if (info == null)
            {
                TempData["Erro"] = "Não foi possível acessar o perfil para salvar o histórico.";
                return RedirectToAction(nameof(Details), new { id = treinadorId });
            }

            // Salva histórico
            var resultado = await _tmTreinadorService.SalvarHistoricoAsync(
                _context, treinadorId, info.Historico);

            // Atualiza foto se solicitado
            if (atualizarFoto && !string.IsNullOrWhiteSpace(info.FotoUrl))
            {
                treinador.FotoUrl = info.FotoUrl;
                treinador.DtAlt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] =
                $"✅ Histórico salvo! {resultado.RegistrosSalvos} registro(s), " +
                $"{resultado.TimesCreados} time(s) criado(s).";

            if (resultado.Avisos.Any())
                TempData["Avisos"] = string.Join(" | ", resultado.Avisos.Take(5));

            return RedirectToAction(nameof(Details), new { id = treinadorId });
        }

        // ── Importar histórico via api-football ──────────────────────────────

        /// <summary>
        /// GET: Resolve o técnico na api-football (mesma lógica de BuscarFoto) e monta a
        /// pré-visualização do histórico unindo o career de todos os registros do mesmo
        /// técnico — necessário porque o registro "stub" costuma ter a passagem atual que
        /// falta no career do registro completo.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PreVisualizarHistoricoApi(int id)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            var (melhor, registros, ambiguo) = await _apiFootball.ResolverTreinadorApiAsync(
                treinador.Nome, treinador.Time?.IdApi, treinador.IdApi);

            if (melhor == null)
            {
                TempData["Erro"] = $"Nenhum treinador encontrado na API para '{treinador.Nome}'.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var carreira = ApiFootballService.UnirCarreiras(registros);

            if (!carreira.Any())
            {
                TempData["Erro"] = $"Nenhum histórico de carreira encontrado na API para '{treinador.Nome}'.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Resolve os times locais em lote (por Time.IdApi) para marcar na tela quais
            // passagens têm clube cadastrado no banco.
            var timesApiIds = carreira
                .Where(c => c.Team != null)
                .Select(c => c.Team!.Id)
                .Distinct()
                .ToList();
            var timesLocais = await _context.Times
                .Where(t => timesApiIds.Contains(t.IdApi))
                .ToListAsync();

            var vm = new TreinadorHistoricoApiViewModel
            {
                Treinador = treinador,
                Ambiguo = ambiguo,
                RegistroCompletoEncontrado = registros.Count > 1,
                Itens = carreira.Select(c =>
                {
                    var timeLocal = c.Team == null
                        ? null
                        : timesLocais.FirstOrDefault(t => t.IdApi == c.Team.Id);
                    return new HistoricoApiItemViewModel
                    {
                        TeamApiId = c.Team?.Id,
                        NomeTime = c.Team?.Name ?? "(desconhecido)",
                        LogoUrl = c.Team?.Logo,
                        DtInicio = ParseDataCarreiraApi(c.Start),
                        DtFim = ParseDataCarreiraApi(c.End),
                        TimeLocalId = timeLocal?.Id,
                        TimeLocalNome = timeLocal?.Nome
                    };
                }).ToList()
            };

            return View("HistoricoPreVisualizacaoApi", vm);
        }

        /// <summary>
        /// POST: Confirma e salva o histórico importado via api-football. Refaz a resolução
        /// e a união de carreira no servidor (não confia em dados vindos do form além do id),
        /// para garantir que o que é salvo é exatamente o que a API tem agora.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarHistoricoApi(int treinadorId)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .FirstOrDefaultAsync(t => t.Id == treinadorId);

            if (treinador == null) return NotFound();

            var (melhor, registros, _) = await _apiFootball.ResolverTreinadorApiAsync(
                treinador.Nome, treinador.Time?.IdApi, treinador.IdApi);

            if (melhor == null)
            {
                TempData["Erro"] = "Não foi possível localizar o treinador na API para salvar o histórico.";
                return RedirectToAction(nameof(Details), new { id = treinadorId });
            }

            var carreira = ApiFootballService.UnirCarreiras(registros);

            int salvos = 0, ignorados = 0;

            foreach (var item in carreira)
            {
                if (item.Team == null) { ignorados++; continue; }

                // Clube não cadastrado no banco: só é exibido na pré-visualização, nunca
                // criado automaticamente (diferente do fluxo do Transfermarkt).
                var timeLocal = await _context.Times.FirstOrDefaultAsync(t => t.IdApi == item.Team.Id);
                if (timeLocal == null) { ignorados++; continue; }

                var inicio = ParseDataCarreiraApi(item.Start);
                if (inicio == null) { ignorados++; continue; }

                var fim = ParseDataCarreiraApi(item.End);

                // Dedupe contra histórico já salvo (mesmo time + mesmo mês/ano de início).
                var duplicado = await _context.TreinadoresHistorico.AnyAsync(h =>
                    h.TreinadorId == treinadorId && h.TimeId == timeLocal.Id &&
                    h.DtInicio.Year == inicio.Value.Year && h.DtInicio.Month == inicio.Value.Month);
                if (duplicado) continue;

                _context.TreinadoresHistorico.Add(new TreinadorHistorico
                {
                    TreinadorId = treinadorId,
                    TimeId = timeLocal.Id,
                    DtInicio = DateTime.SpecifyKind(inicio.Value, DateTimeKind.Utc),
                    DtFim = fim.HasValue ? DateTime.SpecifyKind(fim.Value, DateTimeKind.Utc) : null
                });
                salvos++;
            }

            // Aproveita a resolução para corrigir o IdApi se ele ainda estivesse travado
            // num stub (mesmo raciocínio do BuscarFoto).
            if (melhor.Id is int coachId && coachId > 0 && treinador.IdApi != coachId)
            {
                treinador.IdApi = coachId;
                treinador.DtAlt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"✅ Histórico da API salvo! {salvos} registro(s) salvo(s)" +
                (ignorados > 0
                    ? $", {ignorados} passagem(ns) ignorada(s) (clube não cadastrado, data inválida ou já existente)."
                    : ".");

            return RedirectToAction(nameof(Details), new { id = treinadorId });
        }

        // Uma única data "yyyy-MM-dd" (ou null) vinda da api-football (career.start/end).
        private static DateTime? ParseDataCarreiraApi(string? data) =>
            DateTime.TryParse(data, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt) ? dt : null;

        // ─── Helper ───────────────────────────────────────────────────────────

        private static string NormalizarUrlPerfil(string url)
        {
            // Garante que a URL aponta para /profil/trainer/
            if (!url.Contains("/profil/trainer/"))
            {
                // Tenta converter leistungsdaten → profil
                url = System.Text.RegularExpressions.Regex.Replace(
                    url,
                    @"/(leistungsdaten|statistik|transfers|steckbrief)/trainer/",
                    "/profil/trainer/");
            }
            return url;
        }
    }
}