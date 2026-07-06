using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
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

        public CriteriosNotaController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User);
            var compartilhados = await _context.CriteriosNota
                .Where(c => c.UsuarioId == null).OrderBy(c => c.Ordem).ToListAsync();
            var doUsuario = await _context.CriteriosNota
                .Where(c => c.UsuarioId == uid).ToListAsync();

            // Merged list: padrões com overrides do usuário aplicados
            var merged = CriteriosNotaHelper.MergeCriterios(compartilhados, doUsuario);

            // AcaoIds que o usuário personalizou
            ViewBag.AcoesPersonalizadas = doUsuario.Select(c => c.AcaoId).ToHashSet();
            // Map acaoid → id do registro do usuário (para editar/resetar)
            ViewBag.OverrideIds = doUsuario.ToDictionary(c => c.AcaoId, c => c.Id);
            // AcaoIds que existem como padrão compartilhado (para distinguir override de critério 100% próprio)
            ViewBag.AcaoIdsCompartilhados = compartilhados.Select(c => c.AcaoId).ToHashSet();

            return View(merged);
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
