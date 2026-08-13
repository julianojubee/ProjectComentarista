using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    [Authorize]
    public class CriteriosNotaController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RatingAutomaticoService _rating;

        public CriteriosNotaController(FutebolContext context, UserManager<ApplicationUser> userManager,
            RatingAutomaticoService rating)
        {
            _context = context;
            _userManager = userManager;
            _rating = rating;
        }

        // Régua do rating automático: recalcula média e desvio de cada (posição,
        // métrica) sobre o histórico e devolve o quanto as notas resultantes batem
        // com o rating do provedor. JSON puro — é ferramenta de calibração, não tela.
        //
        // simular=true roda a conta sem gravar a régua nova, para comparar antes.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalibrarRating(bool simular = false, CancellationToken ct = default)
        {
            var relatorio = await _rating.CalibrarAsync(_userManager.GetUserId(User), persistir: !simular, ct);
            return Json(Resumir(relatorio, simular ? "simulacao" : "gravada"));
        }

        [HttpGet]
        public async Task<IActionResult> DiagnosticoRating(CancellationToken ct = default)
        {
            var relatorio = await _rating.DiagnosticarAsync(_userManager.GetUserId(User), ct);
            return Json(Resumir(relatorio, "atual"));
        }

        // A régua inteira é grande demais para a resposta (19 métricas × 8 posições):
        // o que interessa aqui é a aderência.
        private static object Resumir(RelatorioCalibracao r, string regua) => new
        {
            regua,
            r.JogosAnalisados,
            r.LinhasAnalisadas,
            r.GruposCalibrados,
            calibradoEm = r.Baseline.CalibradoEm,
            geral = r.Geral,
            porPosicao = r.PorPosicao,
        };

        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User);
            var compartilhados = await _context.CriteriosNota
                .Where(c => c.UsuarioId == null).OrderBy(c => c.Ordem).ToListAsync();
            var doUsuario = await _context.CriteriosNota
                .Where(c => c.UsuarioId == uid).ToListAsync();

            // Merged list: padrões com overrides do usuário aplicados
            var merged = CriteriosNotaHelper.MergeCriterios(compartilhados, doUsuario);

            // Peso inicial e bônus de não sofrer gol têm cards próprios na tela —
            // saem da lista de ações.
            ViewBag.PesoInicial = CriteriosNotaHelper.NotaBase(merged);
            ViewBag.PesoInicialPersonalizado = doUsuario.Any(c => c.AcaoId == CriteriosNotaHelper.AcaoPesoInicial);

            ViewBag.MotorNota = CriteriosNotaHelper.MotorDaNota(merged);
            ViewBag.MotorPersonalizado = doUsuario.Any(c => c.AcaoId == CriteriosNotaHelper.AcaoMotorNota);

            ViewBag.BonusSemSofrerGol = CriteriosNotaHelper.BonusSemSofrerGol(merged);
            ViewBag.PosicoesSemSofrerGol = CriteriosNotaHelper.PosicoesSemSofrerGol(merged);
            ViewBag.SemSofrerGolPersonalizado = doUsuario.Any(c => c.AcaoId == CriteriosNotaHelper.AcaoSemSofrerGol);

            merged = CriteriosNotaHelper.SomenteAcoes(merged);

            // AcaoIds que o usuário personalizou
            ViewBag.AcoesPersonalizadas = doUsuario.Select(c => c.AcaoId).ToHashSet();
            // Map acaoid → id do registro do usuário (para editar/resetar)
            ViewBag.OverrideIds = doUsuario.ToDictionary(c => c.AcaoId, c => c.Id);
            // AcaoIds que existem como padrão compartilhado (para distinguir override de critério 100% próprio)
            ViewBag.AcaoIdsCompartilhados = compartilhados.Select(c => c.AcaoId).ToHashSet();

            return View(merged);
        }

        // Peso inicial (nota base) do usuário: guardado como um CriterioNota especial
        // (AcaoId = peso_inicial). Não tem extrator, então nunca soma pontos por ação —
        // só define de onde a nota parte e qual é o piso dela.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarPesoInicial(double peso)
        {
            if (peso < 0 || peso > 10)
            {
                TempData["Erro"] = "O peso inicial deve estar entre 0 e 10.";
                return RedirectToAction(nameof(Index));
            }

            var uid = _userManager.GetUserId(User)!;
            var existente = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == CriteriosNotaHelper.AcaoPesoInicial && c.UsuarioId == uid);

            if (existente != null)
            {
                existente.Peso = peso;
                existente.Ativo = true;
            }
            else
            {
                _context.CriteriosNota.Add(new CriterioNota
                {
                    AcaoId = CriteriosNotaHelper.AcaoPesoInicial,
                    Label = CriteriosNotaHelper.LabelPesoInicial,
                    Peso = peso,
                    Ativo = true,
                    Ordem = 0,
                    UsuarioId = uid,
                });
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = $"Peso inicial atualizado para {peso.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture)}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetarPesoInicial()
        {
            var uid = _userManager.GetUserId(User);
            var existente = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == CriteriosNotaHelper.AcaoPesoInicial && c.UsuarioId == uid);

            if (existente != null)
            {
                _context.CriteriosNota.Remove(existente);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Peso inicial voltou ao padrão (4,0).";
            }
            return RedirectToAction(nameof(Index));
        }

        // Motor da nota automática. Guardado como CriterioNota especial, igual aos
        // outros dois: o modo fica no Config e o Peso não é usado. Quem aplica é o
        // NotaAutomaticaHelper — e só nos jogos que o usuário não avaliou à mão.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarMotorNota(string motor)
        {
            var escolhido = CriteriosNotaHelper.ParseMotor(motor);
            var uid = _userManager.GetUserId(User)!;

            var existente = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == CriteriosNotaHelper.AcaoMotorNota && c.UsuarioId == uid);

            if (existente != null)
            {
                existente.Config = escolhido.ToString();
                existente.Ativo = true;
            }
            else
            {
                _context.CriteriosNota.Add(new CriterioNota
                {
                    AcaoId = CriteriosNotaHelper.AcaoMotorNota,
                    Label = CriteriosNotaHelper.LabelMotorNota,
                    Config = escolhido.ToString(),
                    Peso = 0,
                    Ativo = true,
                    Ordem = 0,
                    UsuarioId = uid,
                });
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = escolhido == MotorNota.Automatico
                ? "Notas automáticas passam a ser calculadas por posição."
                : "Notas automáticas voltam a ser calculadas por pesos.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetarMotorNota()
        {
            var uid = _userManager.GetUserId(User);
            var existente = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == CriteriosNotaHelper.AcaoMotorNota && c.UsuarioId == uid);

            if (existente != null)
            {
                _context.CriteriosNota.Remove(existente);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Cálculo da nota voltou ao padrão (por pesos).";
            }
            return RedirectToAction(nameof(Index));
        }

        // Bônus de jogo sem sofrer gol: quanto vale (Peso) e quais posições recebem
        // (Config). Guardado como CriterioNota especial, igual ao peso inicial —
        // quem aplica é o CriteriosNotaHelper.TemJogoSemSofrerGol.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarSemSofrerGol(double peso, List<string>? posicoes = null)
        {
            if (peso < -10 || peso > 10)
            {
                TempData["Erro"] = "O bônus por não sofrer gol deve estar entre -10 e 10.";
                return RedirectToAction(nameof(Index));
            }

            var uid = _userManager.GetUserId(User)!;
            var config = CriteriosNotaHelper.SerializarPosicoes(posicoes);

            var existente = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == CriteriosNotaHelper.AcaoSemSofrerGol && c.UsuarioId == uid);

            if (existente != null)
            {
                existente.Peso = peso;
                existente.Config = config;
                existente.Ativo = true;
            }
            else
            {
                _context.CriteriosNota.Add(new CriterioNota
                {
                    AcaoId = CriteriosNotaHelper.AcaoSemSofrerGol,
                    Label = CriteriosNotaHelper.LabelSemSofrerGol,
                    Peso = peso,
                    Config = config,
                    Ativo = true,
                    Ordem = 0,
                    UsuarioId = uid,
                });
            }

            await _context.SaveChangesAsync();

            var quantas = CriteriosNotaHelper.ParsePosicoes(config).Count;
            TempData["Sucesso"] = quantas > 0
                ? $"Bônus por não sofrer gol salvo: {peso.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture)} ponto(s) para {quantas} posição(ões)."
                : "Bônus por não sofrer gol desligado — nenhuma posição marcada.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetarSemSofrerGol()
        {
            var uid = _userManager.GetUserId(User);
            var existente = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == CriteriosNotaHelper.AcaoSemSofrerGol && c.UsuarioId == uid);

            if (existente != null)
            {
                _context.CriteriosNota.Remove(existente);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Bônus por não sofrer gol voltou ao padrão (+2,0 para goleiro, zagueiro, lateral e ala).";
            }
            return RedirectToAction(nameof(Index));
        }

        // Edita/cria override do usuário para um critério (por AcaoId)
        [HttpGet]
        public async Task<IActionResult> Edit(string acaoId)
        {
            var uid = _userManager.GetUserId(User);

            // Busca override existente do usuário ou usa o compartilhado como base
            var override_ = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == acaoId && c.UsuarioId == uid);

            if (override_ != null)
                return View(override_);

            var compartilhado = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == acaoId && c.UsuarioId == null);

            if (compartilhado == null) return NotFound();

            // Exibe o formulário com valores do padrão (sem Id — será criado no POST)
            return View(new CriterioNota
            {
                AcaoId = compartilhado.AcaoId,
                Label  = compartilhado.Label,
                Peso   = compartilhado.Peso,
                Ativo  = compartilhado.Ativo,
                Ordem  = compartilhado.Ordem,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CriterioNota model)
        {
            var uid = _userManager.GetUserId(User)!;
            if (!ModelState.IsValid) return View(model);

            var existing = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == model.AcaoId && c.UsuarioId == uid);

            if (existing != null)
            {
                // Atualiza override existente
                existing.Peso  = model.Peso;
                existing.Ativo = model.Ativo;
                existing.Label = model.Label;
                existing.Ordem = model.Ordem;
            }
            else
            {
                // Cria novo override para o usuário
                model.UsuarioId = uid;
                model.Id = 0;
                _context.CriteriosNota.Add(model);
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Critério atualizado.";
            return RedirectToAction(nameof(Index));
        }

        // Reseta o override do usuário, voltando ao padrão compartilhado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resetar(string acaoId)
        {
            var uid = _userManager.GetUserId(User);
            var override_ = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == acaoId && c.UsuarioId == uid);

            if (override_ != null)
            {
                _context.CriteriosNota.Remove(override_);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Critério resetado para o padrão.";
            }
            return RedirectToAction(nameof(Index));
        }

        // Cria critério totalmente novo (acaoid que não existe nos compartilhados)
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CriterioNota { Ativo = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CriterioNota model)
        {
            var uid = _userManager.GetUserId(User)!;
            if (!ModelState.IsValid) return View(model);

            if (CriteriosNotaHelper.EhAcaoReservada(model.AcaoId))
            {
                ModelState.AddModelError("AcaoId", "Esse ID é reservado pelo sistema — ajuste-o pelos cards no topo da lista de critérios.");
                return View(model);
            }

            if (await _context.CriteriosNota.AnyAsync(c => c.AcaoId == model.AcaoId && (c.UsuarioId == uid || c.UsuarioId == null)))
            {
                ModelState.AddModelError("AcaoId", "Já existe um critério com esse ID de ação. Use Editar para ajustar o peso.");
                return View(model);
            }

            model.UsuarioId = uid;
            _context.CriteriosNota.Add(model);
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Critério criado com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        // Remove apenas critérios criados pelo próprio usuário (não shared)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = _userManager.GetUserId(User);
            var criterio = await _context.CriteriosNota.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == uid);
            if (criterio != null)
            {
                _context.CriteriosNota.Remove(criterio);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Critério removido.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
