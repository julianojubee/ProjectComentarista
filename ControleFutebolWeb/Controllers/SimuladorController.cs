using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
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
        private readonly SimuladorService _simulador;

        public SimuladorController(FutebolContext context, UserManager<ApplicationUser> userManager,
            SimuladorService simulador)
        {
            _context = context;
            _userManager = userManager;
            _simulador = simulador;
        }

        // Palpites salvos deste usuário na competição/temporada — é o que a tela
        // logada tem a mais que a pública, onde eles ficam só no navegador.
        private async Task<Dictionary<int, PalpiteSimulado>> PalpitesAsync(int competicaoId, int? temporada)
        {
            var uid = _userManager.GetUserId(User)!;
            return await _context.SimulacoesJogoUsuario
                .AsNoTracking()
                .Where(sim => sim.UsuarioId == uid
                            && sim.CompeticaoId == competicaoId
                            && (temporada == null || sim.Temporada == temporada))
                .ToDictionaryAsync(sim => sim.JogoId,
                                   sim => new PalpiteSimulado(sim.PlacarCasa, sim.PlacarVisitante));
        }

        // GET: /Simulador?competicaoId=82&temporada=2026&rodada=12
        public async Task<IActionResult> Index(int? competicaoId, int? temporada, int? rodada)
        {
            var vm = new SimuladorViewModel
            {
                CompeticoesDisponiveis = await _simulador.CompeticoesElegiveisAsync()
            };

            if (competicaoId == null)
                return View(vm);

            var competicao = vm.CompeticoesDisponiveis.FirstOrDefault(c => c.Id == competicaoId);
            if (competicao == null)
            {
                TempData["Erro"] = "Só competições de pontos corridos podem ser simuladas.";
                return RedirectToAction(nameof(Index));
            }

            await _simulador.PreencherSimulacaoAsync(vm, competicao, temporada, rodada,
                await PalpitesAsync(competicao.Id, temporada));
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

            var competicao = await _simulador.CompeticaoElegivelAsync(jogo.CompeticaoId);
            if (competicao == null) return BadRequest("Competição não simulável.");

            var vm = new SimuladorViewModel();
            await _simulador.PreencherSimulacaoAsync(vm, competicao, jogo.Temporada, rodada: null,
                await PalpitesAsync(competicao.Id, jogo.Temporada), montarRodada: false);

            return PartialView("_TabelaSimulada", vm);
        }

        // GET: /Simulador/Rodada — troca o painel de jogos ao clicar nas setas (AJAX).
        [HttpGet]
        public async Task<IActionResult> Rodada(int competicaoId, int? temporada, int rodada)
        {
            var competicao = await _simulador.CompeticaoElegivelAsync(competicaoId);
            if (competicao == null) return NotFound();

            var vm = new SimuladorViewModel();
            await _simulador.PreencherSimulacaoAsync(vm, competicao, temporada, rodada,
                await PalpitesAsync(competicao.Id, temporada));

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

    }
}
