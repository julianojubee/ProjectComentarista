using System.Security.Claims;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Filters
{
    // Registrado globalmente em Program.cs. Atualiza ApplicationUser.UltimoAcesso
    // a cada requisição autenticada, com throttling de 1 min (ExecuteUpdateAsync,
    // sem carregar a entidade) para não gerar um UPDATE a cada clique. Também
    // garante sessão única: se o SessionId salvo no banco não bater com a claim
    // do cookie, é porque houve login em outro local (ou logoff forçado por um
    // admin) — a sessão atual é encerrada na hora.
    public class AtividadeUsuarioFilter : IAsyncActionFilter
    {
        private static readonly TimeSpan Throttle = TimeSpan.FromMinutes(1);

        private readonly FutebolContext _context;

        public AtividadeUsuarioFilter(FutebolContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var sessionIdClaim = context.HttpContext.User.FindFirstValue(ApplicationUser.SessionClaimType);
                var sessionIdAtual = await _context.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.SessionId)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(sessionIdClaim) && sessionIdAtual != sessionIdClaim)
                {
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    context.Result = new RedirectToActionResult("Login", "Account", new { sessaoEncerrada = true });
                    return;
                }

                var agora = DateTime.UtcNow;
                var limite = agora - Throttle;

                await _context.Users
                    .Where(u => u.Id == userId && (u.UltimoAcesso == null || u.UltimoAcesso < limite))
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.UltimoAcesso, agora));
            }

            await next();
        }
    }
}
