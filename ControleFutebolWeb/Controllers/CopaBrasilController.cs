using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
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

        /// <summary>
        /// Quem avançou, considerando pênaltis. Em jogo único vale o placar (ou os
        /// pênaltis) do próprio jogo; em ida e volta, o agregado e, se empatado, os
        /// pênaltis da volta. Null enquanto não há como saber.
        /// </summary>
        public Time? Vencedor(bool jogoUnico)
        {
            var ida = JogoIda;
            if (ida == null || !ida.PlacarCasa.HasValue || !ida.PlacarVisitante.HasValue) return null;

            if (jogoUnico)
            {
                if (ida.PlacarCasa > ida.PlacarVisitante) return TimeA;
                if (ida.PlacarVisitante > ida.PlacarCasa) return TimeB;
                if (ida.PenaltisCasa.HasValue && ida.PenaltisVisitante.HasValue && ida.PenaltisCasa != ida.PenaltisVisitante)
                    return ida.PenaltisCasa > ida.PenaltisVisitante ? TimeA : TimeB;
                return null;
            }

            var direto = Classificado;
            if (direto != null) return direto;

            // Agregado empatado: pênaltis da volta, onde o TimeB é o mandante
            var v = JogoVolta;
            if (v?.PlacarCasa.HasValue == true && GolsTimeA == GolsTimeB &&
                v.PenaltisCasa.HasValue && v.PenaltisVisitante.HasValue && v.PenaltisCasa != v.PenaltisVisitante)
                return v.PenaltisCasa > v.PenaltisVisitante ? TimeB : TimeA;

            return null;
        }
    }

    // Uma fase da Copa do Brasil na navegação: a chave é o round gravado no jogo
    // ("Round of 16"...) e o rótulo, a tradução dele.
    public record FaseCopaBrasil(string Chave, string Rotulo);

    public class CopaBrasilViewModel
    {
        public string FaseAtual           { get; set; } = "";
        public string FaseChaveAtual      { get; set; } = "";
        // Fase em jogo único (nenhum confronto tem volta): o placar do jogo decide
        public bool   JogoUnico           { get; set; }
        public List<ConfrontoMataMata> Confrontos { get; set; } = new();
        // Fases disponíveis para navegação, na ordem em que aconteceram
        public List<FaseCopaBrasil> Fases { get; set; } = new();
    }

    // ─── Controller ─────────────────────────────────────────────────────────
    public class CopaBrasilController : Controller
    {
        private readonly FutebolContext _context;

        // A fase de um jogo é o round gravado no Grupo ("Round of 16"...). O número da
        // rodada não serve: a importação grava 128/64/32/16 em umas temporadas, 1/2/3 em
        // outras, e 0 para quartas, semi e final juntas.
        private static string ChaveFase(Jogo j) =>
            string.IsNullOrWhiteSpace(j.Grupo) ? $"rodada-{j.Rodada}" : j.Grupo.Trim();

        public CopaBrasilController(FutebolContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? fase, int? temporada = null)
        {
            var (temporadasDisponiveis, temporadaSel) =
                TemporadaHelper.Resolver(_context, 3, temporada);
            ViewBag.Temporada = temporadaSel;
            ViewBag.TemporadasDisponiveis = temporadasDisponiveis;

            // Todos os jogos da Copa do Brasil (CompeticaoId = 3)
            var todos = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == 3 && (temporadaSel == null || j.Temporada == temporadaSel))
                .OrderBy(j => j.Rodada)
                .ThenBy(j => j.Data)
                .ToList();

            // ── Aba "Estatísticas": todos os jogos já realizados da competição/temporada ──
            var jogosRealizadosEstat = todos.Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue).ToList();
            var jogoIdsEstat = jogosRealizadosEstat.Select(j => j.Id).ToHashSet();
            var golsEstat = _context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => jogoIdsEstat.Contains(g.JogoId))
                .ToList();
            var cartoesEstat = _context.Cartoes.AsNoTracking()
                .Include(c => c.Jogador)
                .Where(c => jogoIdsEstat.Contains(c.JogoId))
                .ToList();
            // Escalações dos gols e cartões: o dono do evento é o time pelo qual o
            // jogador entrou em campo naquele jogo, não o clube atual do cadastro.
            var escalacoesEstat = LadoJogadorHelper.EscalacoesDosEventos(
                _context, jogosRealizadosEstat.Select(j => j.Id),
                golsEstat.Select(g => g.JogadorId).Concat(cartoesEstat.Select(c => c.JogadorId))).ToList();

            ViewBag.EstatisticasTimes = EstatisticaTimeCalculator.Calcular(jogosRealizadosEstat, golsEstat, cartoesEstat, escalacoesEstat,
                DesarmesPorTimeHelper.Carregar(_context, jogosRealizadosEstat));

            if (!todos.Any())
            {
                ViewBag.Vm = new CopaBrasilViewModel { FaseAtual = "Sem dados" };
                return View();
            }

            // Fases na ordem em que começaram
            var porFase = todos
                .GroupBy(ChaveFase)
                .OrderBy(g => g.Min(j => j.Data ?? DateTime.MaxValue))
                .ToList();
            // Antes das oitavas a CBF numera as fases (1ª, 2ª, 3ª fase...); a api-football
            // chama as mesmas fases de "1/128-finals", "Round of 64" etc., que muda de um
            // ano para o outro. Daí em diante vale o nome traduzido.
            var nomeadas = new HashSet<string> { "Oitavas de final", "Quartas de final", "Semifinal", "Final" };
            var fases = porFase
                .Select((g, i) =>
                {
                    var rot = RodadaHelper.Rotulo(g.First().Grupo, g.First().Rodada);
                    return new FaseCopaBrasil(g.Key, nomeadas.Contains(rot) ? rot : $"{i + 1}ª fase");
                })
                .ToList();

            // Sem fase escolhida: a primeira que ainda tem jogo sem placar; tudo jogado,
            // a última.
            var alvo = porFase.FirstOrDefault(g => g.Key == fase)
                ?? porFase.FirstOrDefault(g => g.Any(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue))
                ?? porFase.Last();
            var rotulo = fases.First(f => f.Chave == alvo.Key).Rotulo;

            var confrontos = MontarConfrontos(alvo.ToList(), rotulo);

            var vm = new CopaBrasilViewModel
            {
                FaseAtual      = rotulo,
                FaseChaveAtual = alvo.Key,
                JogoUnico      = !confrontos.Any(c => c.JogoVolta != null),
                Confrontos     = confrontos,
                Fases          = fases,
            };

            ViewBag.Vm = vm;
            return View();
        }

        // ── Monta pares ida/volta ───────────────────────────────────────────
        private static List<ConfrontoMataMata> MontarConfrontos(List<Jogo> jogos, string faseName)
        {
            var confrontos = new List<ConfrontoMataMata>();
            var usados = new HashSet<int>();

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
