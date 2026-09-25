using System.Security.Claims;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models.Campeonatos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Filters
{
    // Registrado globalmente em Program.cs. Bloqueia o acesso de usuários
    // inadimplentes: não-admin com AcessoPagoAte no passado é redirecionado
    // para /Account/Bloqueado (ou recebe 403 na API JWT do app Android).
    // Também aplica o plano por módulo (ApplicationUser.Modulos): tela de um
    // módulo não contratado leva a /Account/ModuloIndisponivel (403 na API).
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
            if (string.IsNullOrEmpty(userId) || EhControllerIsento(context))
            {
                await next();
                return;
            }

            var dados = await _context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsAdmin, u.AcessoPagoAte, u.Modulos })
                .FirstOrDefaultAsync();

            if (dados == null || dados.IsAdmin)
            {
                await next();
                return;
            }

            var ehApi = context.HttpContext.Request.Path.StartsWithSegments("/api");

            if (EstaInadimplente(dados.AcessoPagoAte))
            {
                if (ehApi)
                {
                    context.Result = new ObjectResult(new
                    {
                        erro = "Acesso suspenso por pendência de pagamento. Entre em contato com o administrador."
                    })
                    { StatusCode = StatusCodes.Status403Forbidden };
                    return;
                }

                context.Result = new RedirectToActionResult("Bloqueado", "Account", null);
                return;
            }

            // Plano por módulo: o controller diz qual módulo exige (ModuloRequerido;
            // sem atributo = análise) e o usuário precisa tê-lo contratado.
            var exigido = context.ActionDescriptor is ControllerActionDescriptor acao ? ModuloAcesso.Exigido(acao) : null;
            if (exigido == null || ModuloAcesso.Tem(dados.Modulos, exigido.Value))
            {
                await next();
                return;
            }

            if (ehApi)
            {
                context.Result = new ObjectResult(new
                {
                    erro = $"Seu plano não inclui {ModuloAcesso.Nome(exigido.Value)}. Entre em contato com o administrador."
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }

            // Quem só tem campeonatos cai na Home depois do login (e ao clicar na
            // marca): manda direto para a tela inicial do módulo dele, sem aviso.
            if (context.ActionDescriptor is ControllerActionDescriptor d &&
                d.ControllerName == "Home" && d.ActionName == "Index" &&
                ModuloAcesso.Tem(dados.Modulos, ModuloSistema.Campeonatos))
            {
                context.Result = new RedirectToActionResult("Index", "Campeonatos", null);
                return;
            }

            context.Result = new RedirectToActionResult("ModuloIndisponivel", "Account", new { modulo = exigido.Value });
        }

        // Vencido quando a data (pura, ancorada ao meio-dia UTC) já passou no
        // calendário do Brasil (UTC-3) — o acesso vale até o fim do dia do vencimento.
        public static bool EstaInadimplente(DateTime? acessoPagoAte)
        {
            if (!acessoPagoAte.HasValue) return false;
            var hojeBrasil = DateTime.UtcNow.AddHours(-3).Date;
            return acessoPagoAte.Value.Date < hojeBrasil;
        }

        // Account: usuário bloqueado precisa ver a tela de bloqueio/sair/redefinir senha.
        // Blog (leitura pública): é anônimo por design — um usuário logado e
        // inadimplente também pode ler o blog, como qualquer visitante.
        // AnalisePublica (/analise/{token}): idem — quem abre o link não tem
        // conta, e bloquear pela inadimplência de quem está logado no navegador
        // derrubaria um link que nada tem a ver com ele.
        // CampeonatoPublico (/c/{token}): mesmo caso, o link do campeonato próprio.
        private static bool EhControllerIsento(ActionExecutingContext context)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor d) return false;
            return string.Equals(d.ControllerName, "Account", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(d.ControllerName, "Blog", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(d.ControllerName, "AnalisePublica", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(d.ControllerName, "Creators", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(d.ControllerName, "CampeonatoPublico", StringComparison.OrdinalIgnoreCase);
        }
    }
}
