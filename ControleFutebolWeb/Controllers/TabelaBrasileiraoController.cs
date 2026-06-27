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

            var confrontos = jogos.Where(j =>
                (j.TimeCasaId == timeAId && j.TimeVisitanteId == timeBId) ||
                (j.TimeCasaId == timeBId && j.TimeVisitanteId == timeAId)
            );

            foreach (var jogo in confrontos)
            {
                if (jogo.TimeCasaId == timeAId)
                    saldo += ((jogo.PlacarCasa ?? 0) - (jogo.PlacarVisitante ?? 0));
                else if (jogo.TimeVisitanteId == timeAId)
                    saldo += ((jogo.PlacarVisitante ?? 0) - (jogo.PlacarCasa ?? 0));
            }

            return saldo;
        }

        public IActionResult Brasileirao(int? temporada = null)
        {
            // Temporadas disponíveis para o Brasileirão; padrão = a mais recente
            var temporadasDisponiveis = _context.Jogos
                .Where(j => j.CompeticaoId == 1)
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t).ToList();

            int? temporadaSel = temporada
                ?? (temporadasDisponiveis.Any() ? temporadasDisponiveis.First() : (int?)null);

            var todosJogos = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == 1 && (temporadaSel == null || j.Temporada == temporadaSel))
                .ToList();

            ViewBag.Temporada = temporadaSel;
            ViewBag.TemporadasDisponiveis = temporadasDisponiveis;

            // Jogos realizados (com placar)
            var jogos = todosJogos
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            // ── Próximos jogos agendados ─────────────────────────────────────
            // Jogos sem placar com data futura ou nula, ordenados por data
            var agora = DateTime.UtcNow;

            var proximosJogosAgendados = todosJogos
                .Where(j => (!j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                            && (j.Data == null || j.Data >= agora))
                .OrderBy(j => j.Data ?? DateTime.MaxValue)
                .ThenBy(j => j.Rodada)
                .Take(30)
                .ToList();

            // Se não houver agendados com data futura, pega jogos sem placar independente de data
            if (!proximosJogosAgendados.Any())
            {
                proximosJogosAgendados = todosJogos
                    .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                    .OrderBy(j => j.Rodada)
                    .ThenBy(j => j.Data ?? DateTime.MaxValue)
                    .Take(30)
                    .ToList();
            }

            // Rodada atual = a menor rodada dos jogos agendados
            var rodadaAtual = proximosJogosAgendados.Any()
                ? proximosJogosAgendados.Min(j => j.Rodada)
                : (todosJogos.Any() ? todosJogos.Max(j => j.Rodada) : 0);

            // Próxima rodada completa (para o painel de destaque)
            var proximaRodada = proximosJogosAgendados
                .Where(j => j.Rodada == rodadaAtual)
                .OrderBy(j => j.Data ?? DateTime.MaxValue)
                .ToList();

            // ── Agrupamento por data para o calendário ───────────────────────
            // Agrupa os próximos jogos por data (dia), formatado como "Qui, 12 Jun"
            var calendarioPorData = proximosJogosAgendados
                .GroupBy(j => j.Data.HasValue
                    ? j.Data.Value.ToLocalTime().Date
                    : DateTime.MaxValue.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ProximosJogosDia
                {
                    Data = g.Key == DateTime.MaxValue.Date ? null : (DateTime?)g.Key,
                    Jogos = g.OrderBy(j => j.Data).ToList()
                })
                .ToList();

            ViewBag.ProximaRodada = proximaRodada;
            ViewBag.NumeroRodada = rodadaAtual;
            ViewBag.CalendarioPorData = calendarioPorData;
            ViewBag.TotalAgendados = proximosJogosAgendados.Count;

            // ── Classificação ────────────────────────────────────────────────
            var tabela = jogos
                .SelectMany(j => new[]
                {
                    new { Time = j.TimeCasa, Pontos = j.PlacarCasa > j.PlacarVisitante ? 3 : j.PlacarCasa == j.PlacarVisitante ? 1 : 0,
                          Vitorias = j.PlacarCasa > j.PlacarVisitante ? 1 : 0,
                          Empates = j.PlacarCasa == j.PlacarVisitante ? 1 : 0,
                          Derrotas = j.PlacarCasa < j.PlacarVisitante ? 1 : 0,
                          GolsPro = j.PlacarCasa, GolsContra = j.PlacarVisitante },
                    new { Time = j.TimeVisitante, Pontos = j.PlacarVisitante > j.PlacarCasa ? 3 : j.PlacarVisitante == j.PlacarCasa ? 1 : 0,
                          Vitorias = j.PlacarVisitante > j.PlacarCasa ? 1 : 0,
                          Empates = j.PlacarVisitante == j.PlacarCasa ? 1 : 0,
                          Derrotas = j.PlacarVisitante < j.PlacarCasa ? 1 : 0,
                          GolsPro = j.PlacarVisitante, GolsContra = j.PlacarCasa }
                })
                .GroupBy(x => x.Time)
                .Select(g => new ClassificacaoViewModel
                {
                    CompeticaoId = 1,
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
                .ToList();

            return View(tabela);
        }
    }

    // DTO para agrupamento por dia no calendário
    public class ProximosJogosDia
    {
        public DateTime? Data { get; set; }
        public List<Jogo> Jogos { get; set; } = new();

        public string DataLabel => Data.HasValue
            ? Data.Value.ToString("ddd, dd MMM", new System.Globalization.CultureInfo("pt-BR"))
            : "Sem data";

        public bool IsHoje => Data.HasValue && Data.Value.Date == DateTime.Today;
        public bool IsAmanha => Data.HasValue && Data.Value.Date == DateTime.Today.AddDays(1);

        public string DataLabelFormatado =>
            IsHoje ? "Hoje" :
            IsAmanha ? "Amanhã" :
            DataLabel;
    }
}
