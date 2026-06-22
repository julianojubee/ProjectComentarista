using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // ─── ViewModels exclusivos da Copa do Brasil ────────────────────────────
    public class ConfrontoMataMata
    {
        public Time TimeA { get; set; } = null!;
        public Time TimeB { get; set; } = null!;

        // Jogo de ida: TimeA manda (pode ser null se ainda não cadastrado)
        public Jogo? JogoIda  { get; set; }
        // Jogo de volta: TimeB manda (pode ser null se ainda não realizado)
        public Jogo? JogoVolta { get; set; }

        // Saldo acumulado de gols nos dois jogos
        public int GolsTimeA => (JogoIda?.PlacarCasa   ?? 0) + (JogoVolta?.PlacarVisitante ?? 0);
        public int GolsTimeB => (JogoIda?.PlacarVisitante ?? 0) + (JogoVolta?.PlacarCasa   ?? 0);

        // Classificado: só definido quando ambos os jogos foram realizados
        public Time? Classificado
        {
            get
            {
                if (JogoIda == null || !JogoIda.PlacarCasa.HasValue) return null;
                if (JogoVolta == null || !JogoVolta.PlacarCasa.HasValue) return null;
                if (GolsTimeA > GolsTimeB) return TimeA;
                if (GolsTimeB > GolsTimeA) return TimeB;
                return null; // empate no agregado → pênaltis (não modelado aqui)
            }
        }

        public string FaseName { get; set; } = "";
    }

    public class CopaBrasilViewModel
    {
        public string FaseAtual           { get; set; } = "";
        public int    RodadaAtual         { get; set; }
        public List<ConfrontoMataMata> Confrontos { get; set; } = new();
        // Todas as rodadas disponíveis para navegação
        public List<int> RodasDisponiveis { get; set; } = new();
    }

    // ─── Controller ─────────────────────────────────────────────────────────
    public class CopaBrasilController : Controller
    {
        private readonly FutebolContext _context;

        // Mapa rodada → nome da fase
        private static readonly Dictionary<int, string> _fases = new()
        {
            { 1,  "1ª Fase" },
            { 2,  "2ª Fase" },
            { 3,  "3ª Fase" },
            { 4,  "Oitavas de Final" },
            { 5,  "Quartas de Final" },
            { 6,  "Semifinal" },
            { 7,  "Final" },
        };

        public CopaBrasilController(FutebolContext context)
        {
            _context = context;
        }

        public IActionResult Index(int? rodada)
        {
            // Todos os jogos da Copa do Brasil (CompeticaoId = 3)
            var todos = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == 3)
                .OrderBy(j => j.Rodada)
                .ThenBy(j => j.Data)
                .ToList();

            if (!todos.Any())
            {
                ViewBag.Vm = new CopaBrasilViewModel { FaseAtual = "Sem dados" };
                return View();
            }

            var rodadasDisponiveis = todos.Select(j => j.Rodada).Distinct().OrderBy(r => r).ToList();

            // Se não informada, detecta a rodada atual:
            // a rodada mais alta que ainda tem ao menos um jogo sem placar,
            // ou a mais alta de todas se todos já foram realizados.
            int rodadaAlvo;
            if (rodada.HasValue && rodadasDisponiveis.Contains(rodada.Value))
            {
                rodadaAlvo = rodada.Value;
            }
            else
            {
                var comPendente = todos
                    .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                    .Select(j => j.Rodada)
                    .Distinct()
                    .OrderBy(r => r)
                    .FirstOrDefault();

                rodadaAlvo = comPendente != 0 ? comPendente : rodadasDisponiveis.Last();
            }

            var jogosRodada = todos.Where(j => j.Rodada == rodadaAlvo).ToList();

            var confrontos = MontarConfrontos(jogosRodada, rodadaAlvo);

            var vm = new CopaBrasilViewModel
            {
                FaseAtual         = _fases.TryGetValue(rodadaAlvo, out var fn) ? fn : $"Rodada {rodadaAlvo}",
                RodadaAtual       = rodadaAlvo,
                Confrontos        = confrontos,
                RodasDisponiveis  = rodadasDisponiveis,
            };

            ViewBag.Vm = vm;
            return View();
        }

        // ── Monta pares ida/volta ───────────────────────────────────────────
        private static List<ConfrontoMataMata> MontarConfrontos(List<Jogo> jogos, int rodada)
        {
            var confrontos = new List<ConfrontoMataMata>();
            var usados = new HashSet<int>();
            var faseName = _fases.TryGetValue(rodada, out var fn) ? fn : $"Rodada {rodada}";

            // Ordena por data para que a ida venha antes da volta
            var ordenados = jogos.OrderBy(j => j.Data).ToList();

            foreach (var jogo in ordenados)
            {
                if (usados.Contains(jogo.Id)) continue;

                // Procura o jogo inverso (volta): times trocados, ainda não usado
                var volta = ordenados.FirstOrDefault(j =>
                    !usados.Contains(j.Id) &&
                    j.Id != jogo.Id &&
                    j.TimeCasaId == jogo.TimeVisitanteId &&
                    j.TimeVisitanteId == jogo.TimeCasaId);

                confrontos.Add(new ConfrontoMataMata
                {
                    TimeA    = jogo.TimeCasa!,
                    TimeB    = jogo.TimeVisitante!,
                    JogoIda  = jogo,
                    JogoVolta = volta,
                    FaseName = faseName,
                });

                usados.Add(jogo.Id);
                if (volta != null) usados.Add(volta.Id);
            }

            return confrontos;
        }
    }
}
