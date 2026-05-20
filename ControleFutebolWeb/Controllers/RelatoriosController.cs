// ControleFutebolWeb/Controllers/RelatoriosController.cs
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class RelatoriosController : Controller
    {
        private readonly FutebolContext _context;

        public RelatoriosController(FutebolContext context)
        {
            _context = context;
        }

        // GET: /Relatorios
        public async Task<IActionResult> Index(int? competicaoId)
        {
            var vm = await MontarViewModel(competicaoId);
            return View(vm);
        }

        // ── Monta o ViewModel completo ───────────────────────────────────────────
        private async Task<RelatoriosViewModel> MontarViewModel(int? competicaoId = null)
        {
            // Jogos realizados e analisados (com placar) 
            var jogosQuery = _context.Jogos
            .Include(j => j.TimeCasa)
            .Include(j => j.TimeVisitante)
            .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue
             && j.Analisado == 1);

            if (competicaoId.HasValue)
                jogosQuery = jogosQuery.Where(j => j.CompeticaoId == competicaoId.Value);

            var jogos = await jogosQuery.ToListAsync();
            var jogoIds = jogos.Select(j => j.Id).ToHashSet();

            // Carregar dados relacionados filtrados pelos jogos
            var gols = await _context.Gols
                .Include(g => g.Jogador).ThenInclude(j => j!.Time)
                .Where(g => jogoIds.Contains(g.JogoId))
                .ToListAsync();

            var notas = await _context.Notas
                .Include(n => n.Jogador).ThenInclude(j => j!.Time)
                .Include(n => n.Detalhes)
                .Where(n => jogoIds.Contains(n.JogoId))
                .ToListAsync();

            var escalacoes = await _context.Escalacoes
                .Include(e => e.Jogador).ThenInclude(j => j!.Time)
                .Where(e => jogoIds.Contains(e.JogoId) && e.JogadorId.HasValue && e.Titular)
                .ToListAsync();

            var cartoes = await _context.Cartoes
                .Include(c => c.Jogador).ThenInclude(j => j!.Time)
                .Where(c => jogoIds.Contains(c.JogoId))
                .ToListAsync();

            var competicoes = await _context.Competicoes.OrderBy(c => c.Nome).ToListAsync();

            // ── Pré-computa resultado por (jogoId, timeId) ────────────────────────
            var resultadoPorJogoTime = new Dictionary<(int jogoId, int timeId), ResultadoJogo>();
            foreach (var j in jogos)
            {
                var pc = j.PlacarCasa ?? 0;
                var pv = j.PlacarVisitante ?? 0;
                ResultadoJogo resCasa, resVis;
                if (pc > pv) { resCasa = ResultadoJogo.Vitoria; resVis = ResultadoJogo.Derrota; }
                else if (pc < pv) { resCasa = ResultadoJogo.Derrota; resVis = ResultadoJogo.Vitoria; }
                else { resCasa = ResultadoJogo.Empate; resVis = ResultadoJogo.Empate; }
                resultadoPorJogoTime[(j.Id, j.TimeCasaId)] = resCasa;
                resultadoPorJogoTime[(j.Id, j.TimeVisitanteId)] = resVis;
            }

            // ── Estatísticas de times ─────────────────────────────────────────────
            var statsTimes = CalcularEstatisticasTimes(jogos);

            // ── Monta ViewModel ───────────────────────────────────────────────────
            var vm = new RelatoriosViewModel
            {
                CompeticaoIdFiltro = competicaoId,
                Competicoes = competicoes,
                TotalJogos = jogos.Count,
                TotalGols = gols.Count(g => !g.Contra),
                TotalGolsContra = gols.Count(g => g.Contra),
                TotalCartaoAmarelo = cartoes.Count(c => c.Tipo == "Amarelo"),
                TotalCartaoVermelho = cartoes.Count(c => c.Tipo == "Vermelho"),

                // Jogadores
                RankingNotas = CalcularRankingNotas(notas, jogos, resultadoPorJogoTime),
                Artilheiros = RankGols(gols, false, 15),
                GolsContraRanking = RankGols(gols, true, 10),
                MaisPartidas = RankPartidas(escalacoes, 15),
                MaisCartoesAmarelos = RankCartoes(cartoes, "Amarelo", 10),
                MaisCartoesVermelhos = RankCartoes(cartoes, "Vermelho", 10),

                // Times
                TimesGols = statsTimes.OrderByDescending(t => t.GolsPro).Take(10).ToList(),
                TimesVitorias = statsTimes.OrderByDescending(t => t.Vitorias).ThenByDescending(t => t.Pontos).Take(10).ToList(),
                TimesMenosGolsSofridos = statsTimes.Where(t => t.Jogos >= 2).OrderBy(t => t.GolsContra).Take(10).ToList(),
                TimesAproveitamento = statsTimes.Where(t => t.Jogos >= 2).OrderByDescending(t => t.Aproveitamento).Take(10).ToList(),
                TimesMaisPontos = statsTimes.OrderByDescending(t => t.Pontos).Take(10).ToList(),

                // Misc
                GolsPorRodada = jogos
                    .Where(j => j.Rodada > 0)
                    .GroupBy(j => j.Rodada)
                    .Select(g => new GolsPorRodada
                    {
                        Rodada = g.Key,
                        TotalGols = gols.Where(gl => g.Select(j => j.Id).Contains(gl.JogoId) && !gl.Contra).Count(),
                        Jogos = g.Count()
                    })
                    .OrderBy(r => r.Rodada)
                    .ToList(),

                MediasPorPosicao = new List<RelatoriosViewModel>()
                    .Select(x => new MediaPosicao()).ToList() // placeholder, preenchido abaixo
            };

            // Médias por posição (calculadas do ranking de notas)
            vm.MediasPorPosicao = vm.RankingNotas
                .Where(r => !string.IsNullOrEmpty(r.Jogador.Posicao))
                .GroupBy(r => NormalizarPosicao(r.Jogador.Posicao))
                .Select(g => new MediaPosicao
                {
                    Posicao = g.Key,
                    Media = Math.Round(g.Average(r => r.NotaFinal), 2),
                    TotalJogadores = g.Count()
                })
                .OrderByDescending(p => p.Media)
                .ToList();

            return vm;
        }

        // ── Cálculo da Nota Final com métrica base-5 + resultado ────────────────
        /// <summary>
        /// NotaFinal = 5  (base)
        ///           + média das notas de ação (Nota.Valor / partidas)
        ///           + bônus resultado: vitória = +1, derrota = -1, empate = 0 (por partida, calculado como média)
        /// Clamp 0..10
        /// </summary>
        private List<RankingNotaItem> CalcularRankingNotas(
            List<Nota> notas,
            List<Jogo> jogos,
            Dictionary<(int jogoId, int timeId), ResultadoJogo> resultadoPorJogoTime)
        {
            var ranking = new List<RankingNotaItem>();

            var notasPorJogador = notas
                .Where(n => n.Jogador != null)
                .GroupBy(n => n.Jogador!);

            foreach (var grupo in notasPorJogador)
            {
                var jogador = grupo.Key;
                var lista = grupo.ToList();
                int partidas = lista.Select(n => n.JogoId).Distinct().Count();
                if (partidas == 0) continue;

                double somaAcoes = lista.Sum(n => n.Valor);
                double mediaAcoes = somaAcoes / partidas;

                int vitorias = 0, derrotas = 0, empates = 0;
                foreach (var n in lista)
                {
                    if (jogador.TimeId <= 0) continue;
                    if (resultadoPorJogoTime.TryGetValue((n.JogoId, jogador.TimeId), out var res))
                    {
                        if (res == ResultadoJogo.Vitoria) vitorias++;
                        else if (res == ResultadoJogo.Derrota) derrotas++;
                        else empates++;
                    }
                }

                double bonusTotal = vitorias * 1.0 + derrotas * (-1.0);
                double mediaBonusResultado = bonusTotal / partidas;

                double notaFinal = 5.0 + mediaAcoes + mediaBonusResultado;
                notaFinal = Math.Max(0, Math.Min(10, notaFinal));

                ranking.Add(new RankingNotaItem
                {
                    Jogador = jogador,
                    NotaFinal = Math.Round(notaFinal, 2),
                    NotaBase = Math.Round(mediaAcoes, 2),
                    BonusResultado = Math.Round(mediaBonusResultado, 2),
                    Partidas = partidas,
                    Vitorias = vitorias,
                    Derrotas = derrotas,
                    Empates = empates,
                });
            }

            return ranking
                .OrderByDescending(r => r.NotaFinal)
                .ThenByDescending(r => r.Partidas)
                .Take(20)
                .ToList();
        }

        // ── Helpers de ranking ───────────────────────────────────────────────────
        private static List<JogadorEstatistica> RankGols(List<Gol> gols, bool contra, int top) =>
            gols
                .Where(g => g.Contra == contra && g.Jogador != null)
                .GroupBy(g => g.Jogador!)
                .Select(g => new JogadorEstatistica
                {
                    Jogador = g.Key,
                    Valor = g.Count(),
                    Detalhe = $"{g.Count()} {(contra ? "gol(s) contra" : "gol(s)")}"
                })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToList();

        private static List<JogadorEstatistica> RankPartidas(List<Escalacao> escalacoes, int top) =>
            escalacoes
                .Where(e => e.Jogador != null)
                .GroupBy(e => e.Jogador!)
                .Select(g => new JogadorEstatistica
                {
                    Jogador = g.Key,
                    Valor = g.Select(e => e.JogoId).Distinct().Count(),
                    Detalhe = $"{g.Select(e => e.JogoId).Distinct().Count()} partida(s)"
                })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToList();

        private static List<JogadorEstatistica> RankCartoes(List<Cartao> cartoes, string tipo, int top) =>
            cartoes
                .Where(c => c.Tipo == tipo && c.Jogador != null)
                .GroupBy(c => c.Jogador!)
                .Select(g => new JogadorEstatistica
                {
                    Jogador = g.Key,
                    Valor = g.Count(),
                    Detalhe = $"{g.Count()} cartão(ões)"
                })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToList();

        private static List<TimeEstatistica> CalcularEstatisticasTimes(List<Jogo> jogos)
        {
            var dict = new Dictionary<int, TimeEstatistica>();
            foreach (var jogo in jogos)
            {
                if (jogo.TimeCasa == null || jogo.TimeVisitante == null) continue;
                var pc = jogo.PlacarCasa ?? 0;
                var pv = jogo.PlacarVisitante ?? 0;

                EnsureTime(dict, jogo.TimeCasaId, jogo.TimeCasa);
                EnsureTime(dict, jogo.TimeVisitanteId, jogo.TimeVisitante);

                var c = dict[jogo.TimeCasaId];
                var v = dict[jogo.TimeVisitanteId];

                c.Jogos++; c.GolsPro += pc; c.GolsContra += pv;
                v.Jogos++; v.GolsPro += pv; v.GolsContra += pc;

                if (pc > pv) { c.Vitorias++; c.Pontos += 3; v.Derrotas++; }
                else if (pc < pv) { v.Vitorias++; v.Pontos += 3; c.Derrotas++; }
                else { c.Empates++; v.Empates++; c.Pontos++; v.Pontos++; }
            }
            return dict.Values.ToList();
        }

        private static void EnsureTime(Dictionary<int, TimeEstatistica> d, int id, Time t)
        {
            if (!d.ContainsKey(id)) d[id] = new TimeEstatistica { Time = t };
        }

        private static string NormalizarPosicao(string p)
        {
            var s = (p ?? "").ToLowerInvariant();
            if (s.Contains("gol") || s == "gl") return "Goleiro";
            if (s.Contains("zag") || s.Contains("def") || s.Contains("lat") || s == "zg") return "Defensor";
            if (s.Contains("mei") || s.Contains("vol") || s == "mc") return "Meio-campo";
            if (s.Contains("ata") || s.Contains("ponta") || s.Contains("centro") || s == "at") return "Atacante";
            return "Outro";
        }
    }

    public enum ResultadoJogo { Vitoria, Derrota, Empate }
}