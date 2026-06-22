using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace ControleFutebolWeb.Authorization
{
    public class AdminRequirement : IAuthorizationRequirement { }

    public class AdminHandler : AuthorizationHandler<AdminRequirement>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, AdminRequirement requirement)
        {
            var user = await _userManager.GetUserAsync(context.User);
            if (user?.IsAdmin == true)
                context.Succeed(requirement);
        }
    }
}
