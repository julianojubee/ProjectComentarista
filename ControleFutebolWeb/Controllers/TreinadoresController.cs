using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class TreinadoresController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ApiFootballService _apiFootball;
        private readonly UserManager<ApplicationUser> _userManager;

        public TreinadoresController(
            FutebolContext context,
            ApiFootballService apiFootball,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _apiFootball = apiFootball;
            _userManager = userManager;
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
                query = query.Where(t => EF.Functions.ILike(t.Nome, $"%{nome}%"));

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

            // Prévia das anotações do usuário sobre esse treinador — a lista completa
            // e o formulário ficam em /AnotacoesTreinador.
            var uid = _userManager.GetUserId(User);
            ViewBag.TotalAnotacoes = await _context.AnotacoesTreinador
                .CountAsync(a => a.TreinadorId == id && a.UsuarioId == uid);
            ViewBag.UltimasAnotacoes = await _context.AnotacoesTreinador
                .AsNoTracking()
                .Where(a => a.TreinadorId == id && a.UsuarioId == uid)
                .OrderByDescending(a => a.DtInc)
                .Take(3)
                .ToListAsync();

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
                .Include(t => t.Nacionalidade)   // o cabeçalho mostra a bandeira
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
            List<int>? competicaoIds, List<int>? timeIds, List<string>? nacionalidades, int page = 1,
            bool voltarParaDetalhes = false)
        {
            // O botão existe no Index (volta pra listagem preservando filtros/página) e na
            // tela de detalhes (volta pra própria tela de detalhes).
            IActionResult Voltar() => voltarParaDetalhes
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectIndexComFiltros(competicaoIds, timeIds, nacionalidades, page);

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
                var (melhor, registros, ambiguo, _) = await _apiFootball.ResolverTreinadorApiAsync(
                    termoBusca, teamApiId, treinador.IdApi);

                if (melhor == null)
                {
                    TempData["Erro"] = $"❌ Nenhum treinador encontrado na API para '{termoBusca}'.";
                    return Voltar();
                }

                // registros.Count > 1 só acontece quando um stub foi resolvido para o
                // registro completo correspondente (ver ResolverTreinadorApiAsync).
                var resolveuStub = registros.Count > 1;

                var alteracoes = await AplicarDadosDaApiAsync(treinador, melhor, registros);
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
                        "abra \"Importar Histórico (API)\" para escolher qual é o certo.";
                    TempData["Info"] = TempData["Info"] != null ? $"{TempData["Info"]} {aviso}" : aviso;
                }
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"❌ Erro ao buscar dados: {ex.Message}";
            }

            return Voltar();
        }

        /// <summary>
        /// Copia para o treinador local os dados do registro escolhido na api-football: nome
        /// canônico, data de nascimento, nacionalidade, foto e o IdApi (que trava as buscas
        /// seguintes no técnico certo). O <b>time atual não é tocado</b> — o vínculo com o
        /// clube continua sendo o que está cadastrado aqui, mesmo quando a API aponta outro.
        /// Devolve a lista de campos alterados (vazia = já estava tudo atualizado).
        /// </summary>
        private async Task<List<string>> AplicarDadosDaApiAsync(
            Treinador treinador, AfCoachFull melhor, List<AfCoachFull> registros)
        {
            var alteracoes = new List<string>();

            // Grava/atualiza o IdApi para travar as próximas buscas no técnico certo —
            // sempre o id do registro completo quando a resolução encontrar um, o que
            // corrige automaticamente treinadores já travados no id de um stub.
            if (melhor.Id is int coachId && coachId > 0 && treinador.IdApi != coachId)
                treinador.IdApi = coachId;

            // Corrige o nome pelo canônico da API só quando o nome local é uma variação
            // dos mesmos tokens (ex.: "Ceni Rogerio", invertido, herdado de um stub →
            // "Rogério Ceni") ou veio literalmente do stub resolvido — nunca sobrescreve
            // um nome escolhido à mão pelo usuário.
            var stub = registros.Count > 1 ? registros.FirstOrDefault(r => r.Id != melhor.Id) : null;
            if (!string.IsNullOrWhiteSpace(melhor.Name) && treinador.Nome != melhor.Name &&
                (ApiFootballService.NomesEquivalentes(treinador.Nome, melhor.Name) ||
                 (stub != null && ApiFootballService.NomesEquivalentes(treinador.Nome, stub.Name))))
            {
                treinador.Nome = melhor.Name;
                alteracoes.Add($"nome ({melhor.Name})");
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
                // Data de nascimento é data pura: ancora ao meio-dia UTC para que nenhuma
                // conversão de fuso (converter do EF ou coerção do Postgres) mude o DIA —
                // meia-noite deslocada em -3h vira o dia anterior, o que fazia a data ser
                // regravada (e exibida errada) a cada clique em Buscar dados.
                var data = DateTime.SpecifyKind(novaData.Value.Date.AddHours(12), DateTimeKind.Utc);
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
            return alteracoes;
        }

        // ── Limpar histórico ──────────────────────────────────────────────────

        /// <summary>
        /// POST: Apaga todo o histórico salvo do treinador (sem reimportar nada).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimparHistorico(int id)
        {
            var treinador = await _context.Treinadores.FirstOrDefaultAsync(t => t.Id == id);
            if (treinador == null) return NotFound();

            var existentes = await _context.TreinadoresHistorico
                .Where(h => h.TreinadorId == id)
                .ToListAsync();

            if (existentes.Count == 0)
            {
                TempData["Info"] = "Não havia histórico salvo para limpar.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.TreinadoresHistorico.RemoveRange(existentes);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"🗑️ Histórico limpo — {existentes.Count} passagem(ns) removida(s).";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Importar histórico via api-football ──────────────────────────────

        /// <summary>
        /// GET: Resolve o técnico na api-football (mesma lógica de BuscarFoto) e monta a
        /// pré-visualização do histórico unindo o career de todos os registros do mesmo
        /// técnico — necessário porque o registro "stub" costuma ter a passagem atual que
        /// falta no career do registro completo.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PreVisualizarHistoricoApi(
            int id, int? coachApiId = null, bool escolher = false)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (treinador == null) return NotFound();

            // escolher=true ("trocar de técnico"): ignora o IdApi já gravado para a resolução
            // voltar a ficar ambígua e a tela oferecer os homônimos de novo.
            var (melhor, registros, ambiguo, candidatos) = await _apiFootball.ResolverTreinadorApiAsync(
                treinador.Nome, treinador.Time?.IdApi, escolher ? null : treinador.IdApi, coachApiId);

            if (melhor == null)
            {
                TempData["Erro"] = $"Nenhum treinador encontrado na API para '{treinador.Nome}'.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Ambíguo: a API tem vários técnicos homônimos (ex.: os dois "Luís Castro"
            // portugueses) e nenhum foi escolhido ainda. Em vez de importar só a passagem do
            // stub, a tela lista os candidatos para o usuário dizer qual é o certo.
            if (ambiguo && candidatos.Any())
            {
                return View("HistoricoPreVisualizacaoApi", new TreinadorHistoricoApiViewModel
                {
                    Treinador = treinador,
                    Ambiguo = true,
                    Candidatos = candidatos.Select(MapearCandidato).ToList()
                });
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
                .Where(c => c.Team?.Id != null)
                .Select(c => c.Team!.Id!.Value)
                .Distinct()
                .ToList();
            var timesLocais = await _context.Times
                .Where(t => timesApiIds.Contains(t.IdApi))
                .ToListAsync();

            // Histórico já salvo deste treinador, para prever na tela — com a mesma regra de
            // dedupe do SalvarHistoricoApi — quais passagens já existem no banco (evita que o
            // usuário veja uma coisa aqui e outra depois de salvar).
            var jaSalvos = await _context.TreinadoresHistorico
                .Where(h => h.TreinadorId == id)
                .Select(h => new { h.TimeId, h.DtInicio.Year, h.DtInicio.Month })
                .ToListAsync();

            var vm = new TreinadorHistoricoApiViewModel
            {
                Treinador = treinador,
                Ambiguo = ambiguo,
                RegistroCompletoEncontrado = registros.Count > 1,
                // Só há candidatos aqui quando a ambiguidade foi resolvida por escolha (agora
                // ou numa importação anterior) — a tela usa para mostrar quem foi escolhido e
                // permitir trocar.
                Candidatos = candidatos.Select(MapearCandidato).ToList(),
                EscolhidoId = candidatos.Any() ? melhor.Id : null,
                Escolhido = candidatos.Any() ? MapearCandidato(melhor) : null,
                Itens = carreira.Select(c =>
                {
                    var timeLocal = c.Team?.Id is int apiId
                        ? timesLocais.FirstOrDefault(t => t.IdApi == apiId)
                        : null;
                    var inicio = ParseDataCarreiraApi(c.Start);
                    var jaSalva = timeLocal != null && inicio != null && jaSalvos.Any(h =>
                        h.TimeId == timeLocal.Id && h.Year == inicio.Value.Year && h.Month == inicio.Value.Month);
                    return new HistoricoApiItemViewModel
                    {
                        TeamApiId = c.Team?.Id,
                        NomeTime = c.Team?.Name ?? "(desconhecido)",
                        LogoUrl = c.Team?.Logo,
                        DtInicio = inicio,
                        DtFim = ParseDataCarreiraApi(c.End),
                        TimeLocalId = timeLocal?.Id,
                        TimeLocalNome = timeLocal?.Nome,
                        JaSalva = jaSalva
                    };
                }).ToList()
            };

            // Mesma correção aplicada no salvamento — senão a pré-visualização prometeria
            // duas passagens "Atual" e o banco receberia outra coisa.
            AplicarFimInferido(vm.Itens);

            return View("HistoricoPreVisualizacaoApi", vm);
        }

        /// <summary>
        /// Resume um registro da api-football para a tela de escolha entre homônimos: dados
        /// pessoais + um recorte da carreira (período e primeiros clubes), que é o que
        /// realmente distingue um "Luís Castro" do outro na hora de escolher.
        /// </summary>
        private static TreinadorCandidatoViewModel MapearCandidato(AfCoachFull c)
        {
            var carreira = (c.Career ?? new List<AfCoachCareerItem>())
                .OrderByDescending(i => ParseDataCarreiraApi(i.Start) ?? DateTime.MinValue)
                .ToList();

            var anos = carreira
                .Select(i => ParseDataCarreiraApi(i.Start))
                .Where(d => d.HasValue)
                .Select(d => d!.Value.Year)
                .ToList();

            var nomeCompleto = string.Join(" ", new[] { c.Firstname, c.Lastname }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            return new TreinadorCandidatoViewModel
            {
                Id = c.Id ?? 0,
                Nome = c.Name ?? "(sem nome)",
                NomeCompleto = string.IsNullOrWhiteSpace(nomeCompleto) ? null : nomeCompleto,
                Idade = c.Age,
                Nacionalidade = c.Nationality,
                FotoUrl = c.Photo,
                TimeAtual = c.Team?.Name,
                TotalPassagens = carreira.Count,
                PeriodoCarreira = anos.Any() ? $"{anos.Min()} – {anos.Max()}" : null,
                ClubesResumo = carreira
                    .Select(i => i.Team?.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .Distinct()
                    .Take(5)
                    .ToList()
            };
        }

        /// <summary>
        /// POST: Confirma e salva o histórico importado via api-football. Refaz a resolução
        /// e a união de carreira no servidor (não confia em dados vindos do form além do id),
        /// para garantir que o que é salvo é exatamente o que a API tem agora.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarHistoricoApi(
            int treinadorId, bool criarClubes = false, int? coachApiId = null)
        {
            var treinador = await _context.Treinadores
                .Include(t => t.Time)
                .Include(t => t.Nacionalidade)
                .FirstOrDefaultAsync(t => t.Id == treinadorId);

            if (treinador == null) return NotFound();

            var (melhor, registros, _, _) = await _apiFootball.ResolverTreinadorApiAsync(
                treinador.Nome, treinador.Time?.IdApi, treinador.IdApi, coachApiId);

            if (melhor == null)
            {
                TempData["Erro"] = "Não foi possível localizar o treinador na API para salvar o histórico.";
                return RedirectToAction(nameof(Details), new { id = treinadorId });
            }

            var carreira = ApiFootballService.UnirCarreiras(registros);

            int salvos = 0, ignorados = 0, clubesCriados = 0;
            // Um item por passagem, no formato "STATUS|nome do time|motivo" — a
            // ConsultarHistorico lê isso para mostrar exatamente o que aconteceu com cada
            // passagem da pré-visualização, em vez de só um total agregado.
            var detalhes = new List<string>();

            // A API devolve end = null tanto para a passagem atual quanto, às vezes, para
            // passagens antigas que ela não fechou — o que fazia o técnico aparecer dirigindo
            // dois clubes ao mesmo tempo. Corrige antes de gravar.
            var finsCorrigidos = TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(
                carreira.Select(c => (ParseDataCarreiraApi(c.Start), ParseDataCarreiraApi(c.End))).ToList());

            for (var idx = 0; idx < carreira.Count; idx++)
            {
                var item = carreira[idx];
                var nomeExibicao = item.Team?.Name ?? "(desconhecido)";

                // Team.Id null = seleção/clube fora da base da API — sem como casar com um time local.
                if (item.Team?.Id is not int teamApiId)
                {
                    ignorados++;
                    detalhes.Add($"IGNORADO|{nomeExibicao}|clube fora da base da API");
                    continue;
                }

                // Clube não cadastrado no banco: por padrão só é exibido na pré-visualização,
                // nunca criado automaticamente. Se o usuário marcou "criar clubes" na tela de
                // pré-visualização, cria um cadastro genérico (mesma ideia do fluxo do
                // Transfermarkt) — com o IdApi já preenchido, então uma futura importação
                // reconhece esse time de cara.
                var timeLocal = await _context.Times.FirstOrDefaultAsync(t => t.IdApi == teamApiId);
                var clubeCriadoAgora = false;
                if (timeLocal == null)
                {
                    if (!criarClubes)
                    {
                        ignorados++;
                        detalhes.Add($"IGNORADO|{nomeExibicao}|clube não cadastrado no sistema");
                        continue;
                    }

                    timeLocal = new Time
                    {
                        Nome = nomeExibicao.Trim(),
                        Cidade = "Desconhecida",
                        EscudoUrl = item.Team?.Logo ?? "",
                        CorPrincipal = "#000000",
                        CorSecundaria = "#FFFFFF",
                        IdApi = teamApiId,
                        FormacaoPadraoId = await ObterFormacaoPadraoIdAsync()
                    };
                    _context.Times.Add(timeLocal);
                    await _context.SaveChangesAsync();
                    clubesCriados++;
                    clubeCriadoAgora = true;
                }

                var inicio = ParseDataCarreiraApi(item.Start);
                if (inicio == null)
                {
                    ignorados++;
                    detalhes.Add($"IGNORADO|{timeLocal.Nome}|data de início inválida");
                    continue;
                }

                var fim = finsCorrigidos[idx];

                // Dedupe contra histórico já salvo (mesmo time + mesmo mês/ano de início).
                var duplicado = await _context.TreinadoresHistorico.AnyAsync(h =>
                    h.TreinadorId == treinadorId && h.TimeId == timeLocal.Id &&
                    h.DtInicio.Year == inicio.Value.Year && h.DtInicio.Month == inicio.Value.Month);
                if (duplicado)
                {
                    detalhes.Add($"JASALVO|{timeLocal.Nome}|");
                    continue;
                }

                _context.TreinadoresHistorico.Add(new TreinadorHistorico
                {
                    TreinadorId = treinadorId,
                    TimeId = timeLocal.Id,
                    DtInicio = DateTime.SpecifyKind(inicio.Value, DateTimeKind.Utc),
                    DtFim = fim.HasValue ? DateTime.SpecifyKind(fim.Value, DateTimeKind.Utc) : null
                });
                salvos++;
                detalhes.Add(clubeCriadoAgora
                    ? $"SALVO|{timeLocal.Nome}|clube criado automaticamente"
                    : $"SALVO|{timeLocal.Nome}|");
            }

            // Aproveita a mesma resolução para atualizar o cadastro do treinador (idade,
            // nacionalidade, foto e o IdApi que trava as próximas buscas). O time atual fica
            // como está — quando o registro escolhido é o histórico completo, o "team" que a
            // API devolve nele é o clube anterior, e sobrescrever trocaria o time errado.
            var alteracoes = await AplicarDadosDaApiAsync(treinador, melhor, registros);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"✅ Histórico da API salvo! {salvos} passagem(ns) nova(s) salva(s)" +
                (clubesCriados > 0 ? $", {clubesCriados} clube(s) criado(s)" : "") +
                (ignorados > 0 ? $", {ignorados} ignorada(s)." : ".") +
                (alteracoes.Any() ? $" Cadastro atualizado: {string.Join(", ", alteracoes)}." : "");
            TempData["HistoricoDetalhes"] = string.Join("~~", detalhes);

            return RedirectToAction(nameof(ConsultarHistorico), new { id = treinadorId });
        }

        private async Task<int> ObterFormacaoPadraoIdAsync()
        {
            var f = await _context.Formacoes.FirstOrDefaultAsync();
            return f?.Id ?? 1;
        }

        // Uma única data "yyyy-MM-dd" (ou null) vinda da api-football (career.start/end).
        private static DateTime? ParseDataCarreiraApi(string? data) =>
            DateTime.TryParse(data, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt) ? dt : null;

        // Fecha as passagens que a API deixou sem data de fim mas que já terminaram
        // (ver TreinadorHistoricoNormalizador). A lista vem ordenada da mais recente
        // para a mais antiga, que é o que o normalizador espera.
        private static void AplicarFimInferido(List<HistoricoApiItemViewModel> itens)
        {
            var fins = TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(
                itens.Select(i => (i.DtInicio, i.DtFim)).ToList());

            for (var i = 0; i < itens.Count; i++)
                itens[i].DtFim = fins[i];
        }

    }
}