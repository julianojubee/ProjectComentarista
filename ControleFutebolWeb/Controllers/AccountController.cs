using System.Text;
using System.Text.Encodings.Web;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ControleFutebolWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
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

            var usuario = await _userManager.FindByNameAsync(model.UserName);
            if (usuario == null)
            {
                ModelState.AddModelError("", "Usuário ou senha inválidos.");
                return View(model);
            }

            var checagem = await _signInManager.CheckPasswordSignInAsync(usuario, model.Password, lockoutOnFailure: true);
            if (!checagem.Succeeded)
            {
                ModelState.AddModelError("", "Usuário ou senha inválidos.");
                return View(model);
            }

            await _signInManager.SignInAsync(usuario, isPersistent: model.RememberMe);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // ── Recuperação de senha ─────────────────────────────────────────

        [AllowAnonymous]
        public IActionResult ForgotPassword() => View();

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var usuario = await _userManager.FindByEmailAsync(model.Email);
            if (usuario != null && await _userManager.IsEmailConfirmedAsync(usuario))
            {
                try
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
                    var tokenCodificado = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

                    var link = Url.Action("ResetPassword", "Account",
                        new { userId = usuario.Id, code = tokenCodificado },
                        protocol: Request.Scheme);

                    var corpo = $"""
                        <p>Olá, {HtmlEncoder.Default.Encode(usuario.Nome)}.</p>
                        <p>Recebemos um pedido para redefinir a senha da sua conta no Comentarista.</p>
                        <p><a href="{link}">Clique aqui para criar uma nova senha</a></p>
                        <p>Se você não pediu essa redefinição, ignore este e-mail.</p>
                        """;

                    await _emailSender.EnviarAsync(usuario.Email!, "Redefinição de senha — Comentarista", corpo);
                }
                catch (Exception ex)
                {
                    // Nunca deixa falha de SMTP virar erro 500 pro usuário nem vazar
                    // se o e-mail existe na base — só registra e segue pra confirmação genérica.
                    _logger.LogError(ex, "Falha ao enviar e-mail de redefinição de senha para {Email}", model.Email);
                }
            }

            // Não revela se o e-mail existe ou não na base (evita enumeração de usuários).
            return View("ForgotPasswordConfirmation");
        }

        [AllowAnonymous]
        public IActionResult ResetPassword(string? userId, string? code)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
                return RedirectToAction("Login");

            string token;
            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return RedirectToAction("Login");
            }

            var model = new ResetPasswordViewModel { UserId = userId, Token = token };
            return View(model);
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var usuario = await _userManager.FindByIdAsync(model.UserId);
            if (usuario == null)
                return View("ResetPasswordConfirmation");

            var result = await _userManager.ResetPasswordAsync(usuario, model.Token, model.NovaSenha);
            if (result.Succeeded)
                return View("ResetPasswordConfirmation");

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ── Gerenciamento de usuários (admin only) ──────────────────────

        [Authorize]
        public async Task<IActionResult> Usuarios()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null || !usuario.IsAdmin)
                return Forbid();

            var usuarios = _userManager.Users
                .OrderByDescending(u => u.UltimoAcesso)
                .ThenBy(u => u.Nome)
                .ToList();
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
