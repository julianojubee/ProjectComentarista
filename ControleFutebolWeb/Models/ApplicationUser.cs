using Microsoft.AspNetCore.Identity;

namespace ControleFutebolWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Nome { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;
    }
}
