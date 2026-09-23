using System.Text;
using System.Text.Encodings.Web;
using ControleFutebolWeb.Filters;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Campeonatos;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AccountController> _logger;
        private readonly PixOptions _pixOptions;
        private readonly IMemoryCache _cache;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<AccountController> logger,
            Microsoft.Extensions.Options.IOptions<PixOptions> pixOptions,
            IMemoryCache cache)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
            _pixOptions = pixOptions.Value;
            _cache = cache;
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

        // Destino do AssinaturaFilter para quem abriu uma tela de um módulo que o
        // plano não inclui. Quem tem o módulo (ou é admin) volta pra Home.
        [Authorize]
        public async Task<IActionResult> ModuloIndisponivel(ModuloSistema modulo)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null || usuario.IsAdmin || ModuloAcesso.Tem(usuario.Modulos, modulo))
                return RedirectToAction("Index", "Home");

            ViewBag.Modulo = modulo;
            ViewBag.Modulos = usuario.Modulos;
            return View();
        }

        // Destino do AssinaturaFilter para usuários com pagamento vencido.
        // Quem não está bloqueado (em dia, sem cobrança ou admin) volta pra Home.
        [Authorize]
        public async Task<IActionResult> Bloqueado()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null || usuario.IsAdmin || !Filters.AssinaturaFilter.EstaInadimplente(usuario.AcessoPagoAte))
                return RedirectToAction("Index", "Home");

            ViewBag.VencidoEm = usuario.AcessoPagoAte!.Value;

            // QR de PIX estático para regularizar na hora, se a chave estiver
            // configurada (seção Pix do appsettings/env) e houver valor a cobrar.
            var valor = usuario.ValorMensalidade ?? _pixOptions.ValorPadrao;
            if (!string.IsNullOrWhiteSpace(_pixOptions.Chave) && valor > 0)
            {
                var payload = Helpers.PixHelper.GerarPayload(
                    _pixOptions.Chave, _pixOptions.NomeRecebedor, _pixOptions.Cidade, valor);

                using var gerador = new QRCoder.QRCodeGenerator();
                using var dados = gerador.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.M);
                var png = new QRCoder.PngByteQRCode(dados).GetGraphic(pixelsPerModule: 8);

                ViewBag.PixPayload = payload;
                ViewBag.PixQrBase64 = Convert.ToBase64String(png);
                ViewBag.PixValor = valor;
            }

            return View();
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
                // Campo vazio vira null (não string vazia) — e-mail é opcional aqui.
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
                Nome = model.Nome,
                IsAdmin = model.IsAdmin,
                EhAutorBlog = model.EhAutorBlog,
                Modulos = (model.ModuloAnalise ? ModuloSistema.Analise : ModuloSistema.Nenhum)
                        | (model.ModuloCampeonatos ? ModuloSistema.Campeonatos : ModuloSistema.Nenhum),
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
            if (usuario != null)
            {
                try
                {
                    await _userManager.DeleteAsync(usuario);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException)
                {
                    // FK Restrict em blogposts.autorid: posts públicos não podem
                    // sumir junto com o usuário. Exclua/reatribua os posts antes.
                    TempData["Erro"] = $"{usuario.Nome} tem posts no blog e não pode ser excluído. " +
                        "Exclua os posts dele em /blog/admin antes.";
                    return RedirectToAction("Usuarios");
                }
            }

            TempData["Sucesso"] = "Usuário excluído.";
            return RedirectToAction("Usuarios");
        }

        // POST: alterna a flag "Autor do blog" de um usuário (admin only).
        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarAutorBlog(string id)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null || !admin.IsAdmin) return Forbid();

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
            {
                TempData["Erro"] = "Usuário não encontrado.";
                return RedirectToAction("Usuarios");
            }

            usuario.EhAutorBlog = !usuario.EhAutorBlog;
            await _userManager.UpdateAsync(usuario);

            TempData["Sucesso"] = usuario.EhAutorBlog
                ? $"{usuario.Nome} agora é autor do blog."
                : $"{usuario.Nome} não é mais autor do blog.";
            return RedirectToAction("Usuarios");
        }

        // POST: liga/desliga um módulo do plano de um usuário (admin only). Vale na
        // próxima requisição dele: o AssinaturaFilter lê Modulos do banco a cada uma.
        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarModulo(string id, ModuloSistema modulo)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null || !admin.IsAdmin) return Forbid();
            if (modulo is not (ModuloSistema.Analise or ModuloSistema.Campeonatos)) return BadRequest();

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
            {
                TempData["Erro"] = "Usuário não encontrado.";
                return RedirectToAction("Usuarios");
            }

            usuario.Modulos ^= modulo;
            await _userManager.UpdateAsync(usuario);
            // O menu do layout fica em cache por 60s; sem isto ele mostraria o plano antigo.
            _cache.Remove($"layout-menu:{usuario.Id}");

            var ligado = ModuloAcesso.Tem(usuario.Modulos, modulo);
            TempData["Sucesso"] = $"{ModuloAcesso.Nome(modulo)}: {(ligado ? "liberado para" : "removido de")} {usuario.Nome}.";
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
