using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
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

        public async Task<IActionResult> Index()
        {
            // Últimos 6 jogos finalizados (com placar)
            var jogosRecentes = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .OrderByDescending(j => j.Data)
                .Take(6)
                .ToListAsync();

            // Classificação: calcula pontos a partir dos jogos finalizados
            var todosJogos = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToListAsync();

            var times = await _context.Times.ToListAsync();

            var classificacao = times.Select(time =>
            {
                var comoMandante = todosJogos.Where(j => j.TimeCasaId == time.Id).ToList();
                var comoVisitante = todosJogos.Where(j => j.TimeVisitanteId == time.Id).ToList();

                int vitorias = comoMandante.Count(j => j.PlacarCasa > j.PlacarVisitante)
                             + comoVisitante.Count(j => j.PlacarVisitante > j.PlacarCasa);

                int empates = comoMandante.Count(j => j.PlacarCasa == j.PlacarVisitante)
                            + comoVisitante.Count(j => j.PlacarCasa == j.PlacarVisitante);

                int derrotas = comoMandante.Count(j => j.PlacarCasa < j.PlacarVisitante)
                             + comoVisitante.Count(j => j.PlacarVisitante < j.PlacarCasa);

                int golsPro = comoMandante.Sum(j => j.PlacarCasa ?? 0)
                            + comoVisitante.Sum(j => j.PlacarVisitante ?? 0);

                int golsContra = comoMandante.Sum(j => j.PlacarVisitante ?? 0)
                               + comoVisitante.Sum(j => j.PlacarCasa ?? 0);

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

            // Numera posições
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
    }
}
