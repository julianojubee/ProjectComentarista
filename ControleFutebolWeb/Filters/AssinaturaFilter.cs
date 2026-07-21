using System.Security.Claims;
using ControleFutebolWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Filters
{
    // Registrado globalmente em Program.cs. Bloqueia o acesso de usuários
    // inadimplentes: não-admin com AcessoPagoAte no passado é redirecionado
    // para /Account/Bloqueado (ou recebe 403 na API JWT do app Android).
    // AcessoPagoAte == null significa "sem cobrança" e nunca bloqueia.
    // O AccountController fica fora do bloqueio para o usuário conseguir
    // ver a tela de bloqueio, sair da conta e redefinir senha.
    public class AssinaturaFilter : IAsyncActionFilter
    {
        private readonly FutebolContext _context;

        public AssinaturaFilter(FutebolContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || EhControllerAccount(context))
            {
                await next();
                return;
            }

            var dados = await _context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsAdmin, u.AcessoPagoAte })
                .FirstOrDefaultAsync();

            if (dados == null || dados.IsAdmin || !EstaInadimplente(dados.AcessoPagoAte))
            {
                await next();
                return;
            }

            if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
            {
                context.Result = new ObjectResult(new
                {
                    erro = "Acesso suspenso por pendência de pagamento. Entre em contato com o administrador."
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }

            context.Result = new RedirectToActionResult("Bloqueado", "Account", null);
        }

        // Vencido quando a data (pura, ancorada ao meio-dia UTC) já passou no
        // calendário do Brasil (UTC-3) — o acesso vale até o fim do dia do vencimento.
        public static bool EstaInadimplente(DateTime? acessoPagoAte)
        {
            if (!acessoPagoAte.HasValue) return false;
            var hojeBrasil = DateTime.UtcNow.AddHours(-3).Date;
            return acessoPagoAte.Value.Date < hojeBrasil;
        }

        private static bool EhControllerAccount(ActionExecutingContext context)
        {
            return context.ActionDescriptor is ControllerActionDescriptor d &&
                   string.Equals(d.ControllerName, "Account", StringComparison.OrdinalIgnoreCase);
        }
    }
}
