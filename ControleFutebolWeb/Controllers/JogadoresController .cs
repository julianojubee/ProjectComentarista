using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
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

        public JogadoresController(FutebolContext context,ILogger<JogadoresController> logger, ApiFootballService transfermarktService)
        {
            _context = context;
            _logger = logger;
            _transfermarktService = transfermarktService;
        }

        public IActionResult Index(string posicao, string nacionalidade, int? timeId, string sortOrder)
        {
            // Configura parâmetros de ordenação
            ViewBag.NomeSortParam = sortOrder == "Nome" ? "Nome_desc" : "Nome";
            ViewBag.PosicaoSortParam = sortOrder == "Posicao" ? "Posicao_desc" : "Posicao";
            ViewBag.IdadeSortParam = sortOrder == "Idade" ? "Idade_desc" : "Idade";
            ViewBag.NacionalidadeSortParam = sortOrder == "Nacionalidade" ? "Nacionalidade_desc" : "Nacionalidade";
            ViewBag.TimeSortParam = sortOrder == "Time" ? "Time_desc" : "Time";

            // Guarda filtros atuais
            ViewBag.CurrentSort = sortOrder;

            var jogadores = _context.Jogadores
                .Include(j => j.Nacionalidade)
                .Include(j => j.Time)
                .AsQueryable();

            // Aplica filtros
            if (!string.IsNullOrEmpty(posicao))
                jogadores = jogadores.Where(j => j.Posicao == posicao);

            if (!string.IsNullOrEmpty(nacionalidade))
                jogadores = jogadores.Where(j => j.Nacionalidade.Nome == nacionalidade);

            if (timeId.HasValue)
                jogadores = jogadores.Where(j => j.TimeId == timeId.Value);

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

            // Preenche combos com SelectList
            var posicoes = new List<string> {
            "Goleiro","Zagueiro","Meio-campo","Volante","Atacante",
            "Ponta Esquerda","Ponta Direita","Meia Ofensivo",
            "Lateral Esquerdo","Lateral Direito","Centroavante"
         };
            ViewBag.Posicoes = new SelectList(posicoes, posicao);

            var nacionalidades = _context.Nacionalidades
                .Select(n => n.Nome)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            ViewBag.Nacionalidades = new SelectList(nacionalidades, nacionalidade);

            var times = _context.Times
                .OrderBy(t => t.Nome)
                .ToList();
            ViewBag.Times = new SelectList(times, "Id", "Nome", timeId);

            return View(jogadores.ToList());
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

                            if (!string.IsNullOrEmpty(dadosApi.Nacionalidade) &&
                                jogador.Nacionalidade?.Nome != dadosApi.Nacionalidade)
                                divergencias.Add($"Nacionalidade divergente. API: {dadosApi.Nacionalidade}");

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
                    jogadorExistente.DataNascimento = jogador.DataNascimento.HasValue
                        ? DateTime.SpecifyKind(jogador.DataNascimento.Value, DateTimeKind.Utc)
                        : null;
                    jogadorExistente.TimeId = jogador.TimeId;
                    jogadorExistente.NacionalidadeId = jogador.NacionalidadeId;
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

        public async Task<IActionResult> Estatisticas(int id)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogador == null) return NotFound();

            // ── Dados de eventos ──────────────────────────────────────────
            var notas = await _context.Notas
                .Include(n => n.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(n => n.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(n => n.Detalhes)
                .Where(n => n.JogadorId == id)
                .ToListAsync();

            var gols = await _context.Gols
                .Where(g => g.JogadorId == id && !g.Contra)
                .ToListAsync();

            var assistencias = await _context.Assistencias
                .Where(a => a.JogadorId == id)
                .ToListAsync();

            var cartoes = await _context.Cartoes
                .Where(c => c.JogadorId == id)
                .ToListAsync();

            // ── Escalações (inclui jogos sem análise) ─────────────────────
            var escalacoes = await _context.Escalacoes
                .Include(e => e.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(e => e.Jogo).ThenInclude(j => j.TimeVisitante)
                .Where(e => e.JogadorId == id)
                .ToListAsync();

            var jogosAnalisadosIds = notas.Select(n => n.JogoId).ToHashSet();

            // ── Helper: monta item a partir de um jogo ────────────────────
            NotaJogoItem MontarItem(Jogo jogo, int notaValor, string? comentario,
                List<Notadetalhe> detalhes, bool analisado)
            {
                var pc = jogo.PlacarCasa ?? 0;
                var pv = jogo.PlacarVisitante ?? 0;
                bool isCasa = jogo.TimeCasaId == jogador.TimeId
                           || jogo.TimeCasaId == jogador.SelecaoId;

                int golsPro    = isCasa ? pc : pv;
                int golsContra = isCasa ? pv : pc;

                string resultado;
                double bonus;
                if (!jogo.PlacarCasa.HasValue)            { resultado = "?"; bonus = 0; }
                else if (pc == pv)                         { resultado = "E"; bonus = 0; }
                else if ((isCasa && pc > pv) ||
                         (!isCasa && pv > pc))             { resultado = "V"; bonus = +1; }
                else                                       { resultado = "D"; bonus = -1; }

                double notaFinal = analisado
                    ? Math.Round(Math.Max(0, Math.Min(10, 5.0 + notaValor + bonus)), 2)
                    : 0;

                return new NotaJogoItem
                {
                    Jogo = jogo,
                    Analisado = analisado,
                    Nota = notaValor,
                    Comentario = comentario ?? "",
                    Gols = gols.Count(g => g.JogoId == jogo.Id),
                    Assistencias = assistencias.Count(a => a.JogoId == jogo.Id),
                    Cartoes = cartoes.Count(c => c.JogoId == jogo.Id),
                    Resultado = resultado,
                    BonusResultado = bonus,
                    NotaFinal = notaFinal,
                    Detalhes = detalhes,
                    GolsPro = golsPro,
                    GolsContra = golsContra,
                };
            }

            // ── Jogos analisados ──────────────────────────────────────────
            var itensAnalisados = notas.Select(n =>
                MontarItem(n.Jogo, n.Valor, n.Comentario, n.Detalhes?.ToList() ?? new(), true));

            // ── Jogos não analisados (participou mas sem nota) ────────────
            var itensNaoAnalisados = escalacoes
                .Where(e => !jogosAnalisadosIds.Contains(e.JogoId))
                .GroupBy(e => e.JogoId)
                .Select(g => g.First())
                .Select(e => MontarItem(e.Jogo, 0, null, new(), false));

            var notasPorJogo = itensAnalisados
                .Concat(itensNaoAnalisados)
                .OrderByDescending(x => x.Jogo.Data)
                .ToList();

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
                NotasPorJogo = notasPorJogo
            };

            return View(vm);
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
        public async Task<IActionResult> SalvarLinkTransfermarkt(int id, string linktransfermarket)
        {
            var jogador = await _context.Jogadores.FindAsync(id);
            if (jogador == null) return NotFound();

            jogador.linktransfermarket = linktransfermarket;
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

    }
}