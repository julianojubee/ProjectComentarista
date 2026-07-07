using System.Security.Claims;
using ControleFutebolWeb.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Filters
{
    // Registrado globalmente em Program.cs. Atualiza ApplicationUser.UltimoAcesso
    // a cada requisição autenticada, com throttling de 1 min (ExecuteUpdateAsync,
    // sem carregar a entidade) para não gerar um UPDATE a cada clique.
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
