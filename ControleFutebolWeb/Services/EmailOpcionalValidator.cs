using System.ComponentModel.DataAnnotations;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;

namespace ControleFutebolWeb.Services
{
    // Torna o e-mail opcional na criação/edição de usuários (ver Account/CriarUsuario).
    // O UserValidator padrão do Identity, quando IdentityOptions.User.RequireUniqueEmail
    // é true, também rejeita e-mail vazio/nulo com o erro "Email '' is invalid" — não dá
    // pra exigir unicidade só quando o e-mail é informado usando apenas essa flag. Por
    // isso RequireUniqueEmail fica false (Program.cs) e este validador assume sozinho a
    // checagem de e-mail: sem e-mail, passa direto; com e-mail, exige formato válido e
    // unicidade, igual ao comportamento nativo.
    public class EmailOpcionalValidator : IUserValidator<ApplicationUser>
    {
        private readonly IdentityErrorDescriber _describer = new();

        public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
        {
            var email = await manager.GetEmailAsync(user);
            if (string.IsNullOrWhiteSpace(email))
                return IdentityResult.Success;

            if (!new EmailAddressAttribute().IsValid(email))
                return IdentityResult.Failed(_describer.InvalidEmail(email));

            var dono = await manager.FindByEmailAsync(email);
            if (dono != null && !string.Equals(await manager.GetUserIdAsync(dono), await manager.GetUserIdAsync(user)))
                return IdentityResult.Failed(_describer.DuplicateEmail(email));

            return IdentityResult.Success;
        }
    }
}
