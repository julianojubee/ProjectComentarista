using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ControleFutebolWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Usuário ou senha inválidos.");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // ── Gerenciamento de usuários (admin only) ──────────────────────

        [Authorize]
        public async Task<IActionResult> Usuarios()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null || !usuario.IsAdmin)
                return Forbid();

            var usuarios = _userManager.Users.OrderBy(u => u.Nome).ToList();
            return View(usuarios);
        }

        [Authorize]
        public async Task<IActionResult> CriarUsuario()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null || !usuario.IsAdmin) return Forbid();
            return View();
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarUsuario(CriarUsuarioViewModel model)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null || !admin.IsAdmin) return Forbid();

            if (!ModelState.IsValid) return View(model);

            var novoUsuario = new ApplicationUser
            {
                UserName = model.UserName,
                Email = model.Email,
                Nome = model.Nome,
                IsAdmin = model.IsAdmin,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(novoUsuario, model.Password);
            if (result.Succeeded)
            {
                TempData["Sucesso"] = $"Usuário {model.Nome} criado com sucesso.";
                return RedirectToAction("Usuarios");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirUsuario(string id)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null || !admin.IsAdmin) return Forbid();
            if (id == admin.Id)
            {
                TempData["Erro"] = "Você não pode excluir sua própria conta.";
                return RedirectToAction("Usuarios");
            }

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario != null) await _userManager.DeleteAsync(usuario);

            TempData["Sucesso"] = "Usuário excluído.";
            return RedirectToAction("Usuarios");
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> RedefinirSenha(string id, string novaSenha)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null || !admin.IsAdmin) return Forbid();

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
            {
                TempData["Erro"] = "Usuário não encontrado.";
                return RedirectToAction("Usuarios");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
            var result = await _userManager.ResetPasswordAsync(usuario, token, novaSenha);

            TempData[result.Succeeded ? "Sucesso" : "Erro"] = result.Succeeded
                ? $"Senha de {usuario.Nome} redefinida."
                : string.Join(", ", result.Errors.Select(e => e.Description));

            return RedirectToAction("Usuarios");
        }
    }
}
