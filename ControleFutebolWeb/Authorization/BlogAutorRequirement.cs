using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace ControleFutebolWeb.Authorization
{
    // Política "BlogEscrever": admin OU usuário com a flag EhAutorBlog marcada
    // (checkbox na tela de usuários). Mesmo molde do AdminHandler — consulta o
    // banco a cada checagem de propósito: desmarcar a flag revoga o acesso na
    // hora, sem esperar o usuário relogar.
    public class BlogAutorRequirement : IAuthorizationRequirement { }

    public class BlogAutorHandler : AuthorizationHandler<BlogAutorRequirement>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public BlogAutorHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, BlogAutorRequirement requirement)
        {
            var user = await _userManager.GetUserAsync(context.User);
            if (user != null && (user.IsAdmin || user.EhAutorBlog))
                context.Succeed(requirement);
        }
    }
}
