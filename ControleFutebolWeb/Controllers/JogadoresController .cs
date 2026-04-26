using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
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

        public JogadoresController(FutebolContext context, ILogger<JogadoresController> logger)
        {
            _context = context;
            _logger = logger;
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
            // Log dos valores recebidos
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
                    var jogadorExistente = await _context.Jogadores.FindAsync(id);
                    if (jogadorExistente == null) return NotFound();

                    // Atualiza apenas os campos persistentes
                    jogadorExistente.Nome = jogador.Nome;
                    jogadorExistente.Posicao = jogador.Posicao;
                    jogadorExistente.DataNascimento = DateTime.SpecifyKind(jogador.DataNascimento, DateTimeKind.Unspecified);
                    jogadorExistente.TimeId = jogador.TimeId;
                    jogadorExistente.NacionalidadeId = jogador.NacionalidadeId;

                    await _context.SaveChangesAsync();
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
    }
}