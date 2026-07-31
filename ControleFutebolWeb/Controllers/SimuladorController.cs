using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Simulador de tabela: o usuário escolhe uma competição de pontos corridos,
    /// preenche o placar dos jogos que faltam (rodada a rodada) e a classificação é
    /// recalculada na hora, sem tocar nos jogos reais.
    ///
    /// Só pontos corridos porque a simulação é uma soma de pontos — mata-mata
    /// dependeria de chaveamento (quem avança altera os confrontos seguintes).
    /// </summary>
    public class SimuladorController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SimuladorController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Simulador?competicaoId=82&temporada=2026&rodada=12
        public async Task<IActionResult> Index(int? competicaoId, int? temporada, int? rodada)
        {
            var vm = new SimuladorViewModel
            {
                CompeticoesDisponiveis = await CompeticoesElegiveisAsync()
            };

            if (competicaoId == null)
                return View(vm);

            var competicao = vm.CompeticoesDisponiveis.FirstOrDefault(c => c.Id == competicaoId);
            if (competicao == null)
            {
                TempData["Erro"] = "Só competições de pontos corridos podem ser simuladas.";
                return RedirectToAction(nameof(Index));
            }

            await PreencherSimulacaoAsync(vm, competicao, temporada, rodada);
            return View(vm);
        }

        // POST: /Simulador/Salvar — salvamento automático de um placar (AJAX).
        // Devolve a tabela recalculada já em HTML; os dois placares vazios apagam o palpite.
        [HttpPost]
        public async Task<IActionResult> Salvar(int jogoId, int? placarCasa, int? placarVisitante)
        {
            var uid = _userManager.GetUserId(User)!;

            var jogo = await _context.Jogos.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jogoId);
            if (jogo == null) return NotFound();

            // Jogo com resultado oficial não é simulável — o placar real sempre vence.
            if (jogo.PlacarCasa.HasValue && jogo.PlacarVisitante.HasValue)
                return BadRequest("Este jogo já tem resultado oficial.");

            var palpite = await _context.SimulacoesJogoUsuario
                .FirstOrDefaultAsync(s => s.UsuarioId == uid && s.JogoId == jogoId);

            var limpar = placarCasa == null || placarVisitante == null;

            if (limpar)
            {
                if (palpite != null) _context.SimulacoesJogoUsuario.Remove(palpite);
            }
            else
            {
                var casa = Math.Clamp(placarCasa.Value, 0, 99);
                var visitante = Math.Clamp(placarVisitante.Value, 0, 99);

                if (palpite == null)
                {
                    _context.SimulacoesJogoUsuario.Add(new SimulacaoJogoUsuario
                    {
                        JogoId = jogoId,
                        UsuarioId = uid,
                        CompeticaoId = jogo.CompeticaoId,
                        Temporada = jogo.Temporada,
                        PlacarCasa = casa,
                        PlacarVisitante = visitante,
                        AtualizadoEm = DateTime.UtcNow
                    });
                }
                else
                {
                    palpite.PlacarCasa = casa;
                    palpite.PlacarVisitante = visitante;
                    palpite.AtualizadoEm = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            var competicao = await CompeticaoElegivelAsync(jogo.CompeticaoId);
            if (competicao == null) return BadRequest("Competição não simulável.");

            var vm = new SimuladorViewModel();
            await PreencherSimulacaoAsync(vm, competicao, jogo.Temporada, rodada: null, montarRodada: false);

            return PartialView("_TabelaSimulada", vm);
        }

        // GET: /Simulador/Rodada — troca o painel de jogos ao clicar nas setas (AJAX).
        [HttpGet]
        public async Task<IActionResult> Rodada(int competicaoId, int? temporada, int rodada)
        {
            var competicao = await CompeticaoElegivelAsync(competicaoId);
            if (competicao == null) return NotFound();

            var vm = new SimuladorViewModel();
            await PreencherSimulacaoAsync(vm, competicao, temporada, rodada);

            if (vm.Rodada == null) return NotFound();
            return PartialView("_JogosRodada", vm.Rodada);
        }

        // POST: /Simulador/Limpar — apaga todos os palpites da competição/temporada.
        [HttpPost]
        public async Task<IActionResult> Limpar(int competicaoId, int? temporada)
        {
            var uid = _userManager.GetUserId(User)!;

            var palpites = await _context.SimulacoesJogoUsuario
                .Where(s => s.UsuarioId == uid
                            && s.CompeticaoId == competicaoId
                            && (temporada == null || s.Temporada == temporada))
                .ToListAsync();

            if (palpites.Count > 0)
            {
                _context.SimulacoesJogoUsuario.RemoveRange(palpites);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = $"Simulação limpa ({palpites.Count} {(palpites.Count == 1 ? "jogo" : "jogos")}).";
            }
            else
            {
                TempData["Mensagem"] = "Não havia nada simulado nesta competição.";
            }

            return RedirectToAction(nameof(Index), new { competicaoId, temporada });
        }

        // ── Montagem ─────────────────────────────────────────────────────────

        /// <summary>
        /// Competições simuláveis: pontos corridos puros ou com uma fase declarada de
        /// pontos corridos (liga + playoffs), e que já tenham jogos importados.
        /// </summary>
        private async Task<List<Competicao>> CompeticoesElegiveisAsync()
        {
            return await _context.Competicoes
                .Include(c => c.Fases)
                .Where(c => (c.Tipo == "PONTOS_CORRIDOS" || c.Fases.Any(f => f.Tipo == "PONTOS_CORRIDOS"))
                            && c.Jogos.Any())
                .OrderByDescending(c => c.TopTier)
                .ThenBy(c => c.Nome)
                .ToListAsync();
        }

        private async Task<Competicao?> CompeticaoElegivelAsync(int id)
        {
            var competicao = await _context.Competicoes
                .Include(c => c.Fases)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (competicao == null) return null;

            var ehLiga = competicao.Tipo == "PONTOS_CORRIDOS"
                         || competicao.Fases.Any(f => f.Tipo == "PONTOS_CORRIDOS");

            return ehLiga ? competicao : null;
        }

        private async Task PreencherSimulacaoAsync(
            SimuladorViewModel vm, Competicao competicao, int? temporada, int? rodada, bool montarRodada = true)
        {
            vm.Competicao = competicao;

            vm.TemporadasDisponiveis = await _context.Jogos
                .Where(j => j.CompeticaoId == competicao.Id)
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t)
                .ToListAsync();

            var temporadaSel = temporada
                ?? (vm.TemporadasDisponiveis.Count > 0 ? vm.TemporadasDisponiveis[0] : (int?)null);
            vm.Temporada = temporadaSel;

            // AsNoTracking porque os jogos pendentes recebem o placar do palpite só em
            // memória para alimentar o cálculo — nada disso pode voltar ao banco.
            var jogos = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == competicao.Id
                            && (temporadaSel == null || j.Temporada == temporadaSel))
                .ToListAsync();

            var jogosLiga = JogosDaFaseLiga(competicao, jogos);

            var realizados = jogosLiga
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();
            var pendentes = jogosLiga
                .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                .ToList();

            var uid = _userManager.GetUserId(User)!;
            var palpites = await _context.SimulacoesJogoUsuario
                .AsNoTracking()
                .Where(s => s.UsuarioId == uid
                            && s.CompeticaoId == competicao.Id
                            && (temporadaSel == null || s.Temporada == temporadaSel))
                .ToDictionaryAsync(s => s.JogoId);

            // Todos os times da competição entram na tabela, mesmo sem jogo realizado —
            // no começo da temporada a tabela apareceria vazia sem isso.
            var participantes = jogosLiga
                .SelectMany(j => new[] { j.TimeCasa, j.TimeVisitante })
                .Where(t => t != null)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            var tabelaReal = ClassificacaoCalculator.Calcular(realizados, participantes);

            // Jogos simulados: cópia rasa do jogo pendente com o placar do palpite.
            var jogosComSimulacao = new List<Jogo>(realizados);
            foreach (var jogo in pendentes)
            {
                if (!palpites.TryGetValue(jogo.Id, out var palpite)) continue;

                jogosComSimulacao.Add(new Jogo
                {
                    Id = jogo.Id,
                    TimeCasaId = jogo.TimeCasaId,
                    TimeCasa = jogo.TimeCasa,
                    TimeVisitanteId = jogo.TimeVisitanteId,
                    TimeVisitante = jogo.TimeVisitante,
                    PlacarCasa = palpite.PlacarCasa,
                    PlacarVisitante = palpite.PlacarVisitante,
                });
            }

            var tabelaSimulada = ClassificacaoCalculator.Calcular(jogosComSimulacao, participantes);

            var posicaoReal = tabelaReal.ToDictionary(c => c.TimeId, c => c.Posicao);
            var pontosReais = tabelaReal.ToDictionary(c => c.TimeId, c => c.Pontos);

            vm.Classificacao = tabelaSimulada
                .Select(c => new SimuladorLinhaViewModel
                {
                    Linha = c,
                    PosicaoReal = posicaoReal.TryGetValue(c.TimeId, out var p) ? p : (int?)null,
                    PontosSimulados = c.Pontos - (pontosReais.TryGetValue(c.TimeId, out var pts) ? pts : 0),
                })
                .ToList();

            vm.TotalPendentes = pendentes.Count;
            vm.TotalSimulados = pendentes.Count(j => palpites.ContainsKey(j.Id));

            // Rodadas navegáveis: as que ainda têm jogo por simular.
            vm.Rodadas = pendentes
                .Select(j => j.Rodada)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            if (!montarRodada || vm.Rodadas.Count == 0) return;

            var rodadaSel = rodada.HasValue && vm.Rodadas.Contains(rodada.Value)
                ? rodada.Value
                : vm.Rodadas[0];
            vm.RodadaAtual = rodadaSel;

            var indice = vm.Rodadas.IndexOf(rodadaSel);

            // A rodada mostra também os jogos já realizados dela (só leitura), para o
            // usuário ver a rodada inteira e não só as sobras.
            var jogosRodada = jogosLiga
                .Where(j => j.Rodada == rodadaSel)
                .OrderBy(j => j.Data ?? DateTime.MaxValue)
                .ThenBy(j => j.Id)
                .Select(j =>
                {
                    var realizado = j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue;
                    var temPalpite = !realizado && palpites.TryGetValue(j.Id, out _);

                    return new SimuladorJogoViewModel
                    {
                        Jogo = j,
                        Realizado = realizado,
                        Simulado = temPalpite,
                        PlacarCasa = realizado ? j.PlacarCasa : (temPalpite ? palpites[j.Id].PlacarCasa : null),
                        PlacarVisitante = realizado ? j.PlacarVisitante : (temPalpite ? palpites[j.Id].PlacarVisitante : null),
                    };
                })
                .ToList();

            vm.Rodada = new SimuladorRodadaViewModel
            {
                Numero = rodadaSel,
                RodadaAnterior = indice > 0 ? vm.Rodadas[indice - 1] : null,
                RodadaProxima = indice < vm.Rodadas.Count - 1 ? vm.Rodadas[indice + 1] : null,
                Jogos = jogosRodada,
            };

            ViewBag.CompeticaoIdRodada = competicao.Id;
            ViewBag.TemporadaRodada = temporadaSel;
        }

        /// <summary>
        /// Isola os jogos que valem pontos na tabela. Com fases declaradas, usa a fase
        /// de pontos corridos; sem elas, descarta só o que é claramente mata-mata
        /// (playoff de rebaixamento, final de série) — mesmo critério de Competicoes/Detalhes.
        /// </summary>
        private List<Jogo> JogosDaFaseLiga(Competicao competicao, List<Jogo> jogos)
        {
            var fases = competicao.Fases.OrderBy(f => f.Ordem).ThenBy(f => f.Id).ToList();
            var faseLiga = fases.FirstOrDefault(f => f.Tipo == "PONTOS_CORRIDOS");

            if (faseLiga != null)
                return FaseJogoClassifier.DistribuirPorFases(fases, jogos)[faseLiga.Id];

            return jogos
                .Where(j => FaseJogoClassifier.Classificar(j.Grupo) != FaseCategoria.MataMata)
                .ToList();
        }
    }
}
