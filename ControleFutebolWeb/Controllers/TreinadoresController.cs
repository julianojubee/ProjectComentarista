using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
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
        public async Task<IActionResult> Index(List<int>? competicaoIds, List<int>? timeIds, List<string>? nacionalidades, int page = 1)
        {
            const int pageSize = 50;
            competicaoIds ??= new List<int>();
            timeIds ??= new List<int>();
            nacionalidades ??= new List<string>();

            var query = _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .AsQueryable();

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

            // Listas completas para os tag selectors
            ViewBag.Competicoes = await _context.Competicoes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.Times = await _context.Times.OrderBy(t => t.Nome).ToListAsync();
            ViewBag.NacionalidadesLista = await _context.Nacionalidades.OrderBy(n => n.Nome).ToListAsync();
            ViewBag.CompeticaoIdsFiltro = competicaoIds;
            ViewBag.TimeIdsFiltro = timeIds;
            ViewBag.NacionalidadesFiltro = nacionalidades;

            ViewBag.PaginaAtual = page;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalTreinadores = totalTreinadores;
            ViewBag.PageSize = pageSize;

            return View(treinadoresPagina);
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
                try
                {
                    treinador.DtAlt = DateTime.UtcNow;
                    _context.Update(treinador);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Treinadores.Any(e => e.Id == treinador.Id))
                        return NotFound();
                    throw;
                }
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

                // 1ª tentativa: nome + id do time (desambigua, ex.: coachs?search=Riera&team=2325).
                var teamApiId = treinador.Time?.IdApi;
                var resultados = await _apiFootball.BuscarTreinadorApiAsync(termoBusca, teamApiId);

                // Fallback: se nada vier com o time, busca só pelo nome.
                if (!resultados.Any() && teamApiId is long)
                    resultados = await _apiFootball.BuscarTreinadorApiAsync(termoBusca);

                if (!resultados.Any())
                {
                    TempData["Erro"] = $"❌ Nenhum treinador encontrado na API para '{termoBusca}'.";
                    return RedirectIndexComFiltros(competicaoIds, timeIds, nacionalidades, page);
                }

                // Se o treinador já tem IdApi, trava no técnico com aquele id.
                // Senão, prefere o resultado cujo time atual bate com o time cadastrado,
                // depois o que tiver mais dados.
                var melhor = resultados
                    .OrderByDescending(r => (treinador.IdApi != null && r.Id == treinador.IdApi) ? 100 : 0)
                    .ThenByDescending(r => r.Team?.Id == treinador.Time?.IdApi ? 10 : 0)
                    .ThenByDescending(r => r.Age.HasValue ? 1 : 0)
                    .First();

                var alteracoes = new List<string>();

                // Grava/atualiza o IdApi para travar as próximas buscas no técnico certo
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
                    TempData["Sucesso"] = $"{treinador.Nome}: atualizado → {lista}.";
                }
                else
                {
                    TempData["Info"] = $"{treinador.Nome}: informações já estavam atualizadas.";
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