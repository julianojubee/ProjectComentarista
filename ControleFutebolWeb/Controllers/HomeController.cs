using System.Diagnostics;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly FutebolContext _context;

        public HomeController(FutebolContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Buscar(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new object[0]);

            q = q.Trim();

            var times = await _context.Times
                .Where(t => t.Nome != null && EF.Functions.ILike(t.Nome, $"%{q}%"))
                .Select(t => new { tipo = "Time", nome = t.Nome, id = t.Id, extra = t.Cidade })
                .Take(5).ToListAsync();

            var jogadores = await _context.Jogadores
                .Where(j => j.Nome != null && EF.Functions.ILike(j.Nome, $"%{q}%"))
                .Select(j => new { tipo = "Jogador", nome = j.Nome, id = j.Id, extra = (string?)null })
                .Take(5).ToListAsync();

            var competicoes = await _context.Competicoes
                .Where(c => EF.Functions.ILike(c.Nome, $"%{q}%"))
                .Select(c => new { tipo = "Competição", nome = c.Nome, id = c.Id, extra = c.Regiao })
                .Take(5).ToListAsync();

            var resultados = times.Cast<object>()
                .Concat(jogadores.Cast<object>())
                .Concat(competicoes.Cast<object>())
                .ToList();

            return Json(resultados);
        }

        public async Task<IActionResult> Index()
        {
            // �ltimos 6 jogos finalizados (com placar)
            var jogosRecentes = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .OrderByDescending(j => j.Data)
                .Take(6)
                .ToListAsync();

            // Classificação calculada diretamente via SQL (sem carregar todos os jogos na memória)
            var jogosFinalizados = await _context.Jogos
                .AsNoTracking()
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .Select(j => new { j.TimeCasaId, j.TimeVisitanteId, j.PlacarCasa, j.PlacarVisitante })
                .ToListAsync();

            var times = await _context.Times.AsNoTracking().ToListAsync();

            var classificacao = times.Select(time =>
            {
                var comoMandante  = jogosFinalizados.Where(j => j.TimeCasaId == time.Id).ToList();
                var comoVisitante = jogosFinalizados.Where(j => j.TimeVisitanteId == time.Id).ToList();

                int vitorias = comoMandante.Count(j => j.PlacarCasa > j.PlacarVisitante)
                             + comoVisitante.Count(j => j.PlacarVisitante > j.PlacarCasa);
                int empates  = comoMandante.Count(j => j.PlacarCasa == j.PlacarVisitante)
                             + comoVisitante.Count(j => j.PlacarCasa == j.PlacarVisitante);
                int derrotas = comoMandante.Count(j => j.PlacarCasa < j.PlacarVisitante)
                             + comoVisitante.Count(j => j.PlacarVisitante < j.PlacarCasa);
                int golsPro    = comoMandante.Sum(j => j.PlacarCasa ?? 0) + comoVisitante.Sum(j => j.PlacarVisitante ?? 0);
                int golsContra = comoMandante.Sum(j => j.PlacarVisitante ?? 0) + comoVisitante.Sum(j => j.PlacarCasa ?? 0);

                return new ClassificacaoResumo
                {
                    Time = time,
                    Vitorias = vitorias,
                    Empates = empates,
                    Derrotas = derrotas,
                    GolsPro = golsPro,
                    GolsContra = golsContra,
                    Pontos = vitorias * 3 + empates,
                    Jogos = vitorias + empates + derrotas
                };
            })
            .Where(c => c.Jogos > 0)
            .OrderByDescending(c => c.Pontos)
            .ThenByDescending(c => c.SaldoGols)
            .ThenByDescending(c => c.GolsPro)
            .Take(8)
            .ToList();

            for (int i = 0; i < classificacao.Count; i++)
                classificacao[i].Posicao = i + 1;

            var vm = new HomeViewModel
            {
                JogosRecentes = jogosRecentes,
                Classificacao = classificacao,
                TotalTimes = await _context.Times.CountAsync(),
                TotalJogadores = await _context.Jogadores.CountAsync(),
                TotalJogos = await _context.Jogos.CountAsync(),
                TotalCompeticoes = await _context.Competicoes.CountAsync()
            };

            return View(vm);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
