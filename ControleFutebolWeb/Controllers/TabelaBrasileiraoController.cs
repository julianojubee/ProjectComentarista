using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ControleFutebolWeb.Controllers
{
    public class TabelaController : Controller
    {
        private readonly FutebolContext _context;

        public TabelaController(FutebolContext context)
        {
            _context = context;
        }

        public int CalcularConfrontoDireto(int timeAId, int timeBId, List<Jogo> jogos)
        {
            int saldo = 0;

            // Filtra apenas jogos entre os dois times
            var confrontos = jogos.Where(j =>
                (j.TimeCasaId == timeAId && j.TimeVisitanteId == timeBId) ||
                (j.TimeCasaId == timeBId && j.TimeVisitanteId == timeAId)
            );

            foreach (var jogo in confrontos)
            {
                if (jogo.TimeCasaId == timeAId)
                {
                    saldo += ((jogo.PlacarCasa ?? 0) - (jogo.PlacarVisitante ?? 0));
                }
                else if (jogo.TimeVisitanteId == timeAId)
                {
                    saldo += ((jogo.PlacarVisitante ?? 0) - (jogo.PlacarCasa ?? 0));
                }
            }

            return saldo; // saldo positivo favorece timeA, negativo favorece timeB
        }

        public IActionResult Brasileirao()
        {
            var todosJogos = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == 1)
                .ToList();

            var jogos = todosJogos
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            var rodadaAlvo = todosJogos
                .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                .OrderBy(j => j.Rodada)
                .Select(j => (int?)j.Rodada)
                .FirstOrDefault();

            if (!rodadaAlvo.HasValue)
            {
                rodadaAlvo = todosJogos
                    .OrderByDescending(j => j.Data ?? DateTime.MinValue)
                    .ThenByDescending(j => j.Rodada)
                    .Select(j => (int?)j.Rodada)
                    .FirstOrDefault();
            }

            var proximaRodada = rodadaAlvo.HasValue
                ? todosJogos
                    .Where(j => j.Rodada == rodadaAlvo.Value)
                    .OrderBy(j => j.Data ?? DateTime.MaxValue)
                    .ThenBy(j => j.Id)
                    .ToList()
                : new List<Jogo>();

            ViewBag.ProximaRodada = proximaRodada;
            ViewBag.NumeroRodada = rodadaAlvo ?? 0;


            // Monta a classificação
            var tabela = jogos
                .SelectMany(j => new[]
                {
                    new { Time = j.TimeCasa, Pontos = j.PlacarCasa > j.PlacarVisitante ? 3 : j.PlacarCasa == j.PlacarVisitante ? 1 : 0, Vitorias = j.PlacarCasa > j.PlacarVisitante ? 1 : 0, Empates = j.PlacarCasa == j.PlacarVisitante ? 1 : 0, Derrotas = j.PlacarCasa < j.PlacarVisitante ? 1 : 0, GolsPro = j.PlacarCasa, GolsContra = j.PlacarVisitante },
                    new { Time = j.TimeVisitante, Pontos = j.PlacarVisitante > j.PlacarCasa ? 3 : j.PlacarVisitante == j.PlacarCasa ? 1 : 0, Vitorias = j.PlacarVisitante > j.PlacarCasa ? 1 : 0, Empates = j.PlacarVisitante == j.PlacarCasa ? 1 : 0, Derrotas = j.PlacarVisitante < j.PlacarCasa ? 1 : 0, GolsPro = j.PlacarVisitante, GolsContra = j.PlacarCasa }
                })
                .GroupBy(x => x.Time)
                .Select(g => new ClassificacaoViewModel
                {
                    CompeticaoId = 1, // Brasileirão
                    Time = g.Key,
                    Pontos = g.Sum(x => x.Pontos),
                    Vitorias = g.Sum(x => x.Vitorias),
                    Empates = g.Sum(x => x.Empates),
                    Derrotas = g.Sum(x => x.Derrotas),
                    GolsPro = g.Sum(x => x.GolsPro.GetValueOrDefault()),
                    GolsContra = g.Sum(x => x.GolsContra.GetValueOrDefault()),
                    SaldoGols = g.Sum(x => x.GolsPro.GetValueOrDefault()) - g.Sum(x => x.GolsContra.GetValueOrDefault())
                })

                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.Vitorias)
                .ThenByDescending(t => t.SaldoGols)
                .ThenByDescending(t => t.GolsPro)
                .ThenByDescending(t => jogos.Any(j => j.TimeCasaId == t.Time.Id || j.TimeVisitanteId == t.Time.Id))
                .ToList();


            return View(tabela);


        }
    }
}
