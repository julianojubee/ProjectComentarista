// ControleFutebolWeb/Controllers/SulAmericanaController.cs
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class SulAmericanaController : Controller
    {
        private readonly FutebolContext _context;

        // ID fixo da competição Sul-Americana no banco — ajuste se necessário
        private const int COMPETICAO_ID = 4;

        public SulAmericanaController(FutebolContext context)
        {
            _context = context;
        }
        // GET: /SulAmericana
        public IActionResult Index()
        {
            var grupos = MontarGrupos();
            var proximosJogos = BuscarProximosJogos();
            var rodadaAtual = proximosJogos.Any()
                ? proximosJogos.Min(j => j.Rodada)
                : (_context.Jogos.Any(j => j.CompeticaoId == COMPETICAO_ID)
                    ? _context.Jogos.Where(j => j.CompeticaoId == COMPETICAO_ID).Max(j => j.Rodada)
                    : 0);

            ViewBag.Grupos = grupos;
            ViewBag.ProximosJogos = proximosJogos;
            ViewBag.RodadaAtual = rodadaAtual;

            return View();
        }

        // ── GET: retorna o status de um jogo específico (AJAX) ───────────────
        // GET: /SulAmericana/StatusJogo/5
        [HttpGet]
        public async Task<IActionResult> StatusJogo(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Gols).ThenInclude(g => g.Jogador)
                .Include(j => j.Escalacoes).ThenInclude(e => e.Jogador)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            return Ok(new
            {
                id = jogo.Id,
                placarCasa = jogo.PlacarCasa,
                placarVisitante = jogo.PlacarVisitante,
                atualizado = jogo.Atualizado,
                gols = jogo.Gols?.Count ?? 0,
                escalacoes = jogo.Escalacoes?.Count ?? 0
            });
        }

        // ── POST: reinicia a flag Atualizado de um jogo ───────────────────────
        // POST: /SulAmericana/ResetarJogo/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetarJogo(int id)
        {
            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo == null) return NotFound();

            jogo.Atualizado = 0;

            // Remove escalações e gols para reimportar na próxima sincronização
            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id).ToListAsync();
            _context.Escalacoes.RemoveRange(escalacoes);

            var gols = await _context.Gols
                .Where(g => g.JogoId == id).ToListAsync();
            _context.Gols.RemoveRange(gols);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] =
                $"Jogo {id} resetado. Será reprocessado na próxima sincronização.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS PRIVADOS
        // ─────────────────────────────────────────────────────────────────────

        private List<GrupoViewModel> MontarGrupos()
        {
            var jogosRealizados = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID &&
                            j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue &&
                            !string.IsNullOrEmpty(j.Grupo))
                .OrderBy(j => j.Data)
                .ToList();

            return jogosRealizados
                .Select(j => j.Grupo!)
                .Distinct()
                .OrderBy(g => g)
                .Select(nomeGrupo =>
                {
                    var jogosGrupo = jogosRealizados
                        .Where(j => j.Grupo == nomeGrupo).ToList();
                    return new GrupoViewModel
                    {
                        Nome = nomeGrupo,
                        Times = CalcularClassificacaoGrupo(jogosGrupo)
                    };
                })
                .ToList();
        }

        private List<Jogo> BuscarProximosJogos()
        {
            // Jogos sem placar (agendados) ou com placar mas recentes
            var sem = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID &&
                            (!j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue))
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();

            if (sem.Any()) return sem;

            return _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID)
                .OrderByDescending(j => j.Data)
                .Take(10)
                .ToList();
        }

        private static List<Classificacao> CalcularClassificacaoGrupo(List<Jogo> jogos)
        {
            var tab = new Dictionary<int, Classificacao>();

            foreach (var j in jogos)
            {
                if (j.TimeCasa == null || j.TimeVisitante == null) continue;
                if (!j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue) continue;

                if (!tab.ContainsKey(j.TimeCasaId))
                    tab[j.TimeCasaId] = new Classificacao
                    { TimeId = j.TimeCasaId, Time = j.TimeCasa };
                if (!tab.ContainsKey(j.TimeVisitanteId))
                    tab[j.TimeVisitanteId] = new Classificacao
                    { TimeId = j.TimeVisitanteId, Time = j.TimeVisitante };

                var c = tab[j.TimeCasaId];
                var v = tab[j.TimeVisitanteId];

                c.Jogos++; v.Jogos++;
                c.GolsPro += j.PlacarCasa.Value;
                c.GolsContra += j.PlacarVisitante.Value;
                v.GolsPro += j.PlacarVisitante.Value;
                v.GolsContra += j.PlacarCasa.Value;

                if (j.PlacarCasa > j.PlacarVisitante)
                { c.Vitorias++; c.Pontos += 3; v.Derrotas++; }
                else if (j.PlacarCasa < j.PlacarVisitante)
                { v.Vitorias++; v.Pontos += 3; c.Derrotas++; }
                else
                { c.Empates++; v.Empates++; c.Pontos++; v.Pontos++; }
            }

            var lista = tab.Values
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.GolsPro - t.GolsContra)
                .ThenByDescending(t => t.GolsPro)
                .ToList();

            for (int i = 0; i < lista.Count; i++) lista[i].Posicao = i + 1;
            return lista;
        }
    }
}