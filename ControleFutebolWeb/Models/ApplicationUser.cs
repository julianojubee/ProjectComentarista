using Microsoft.AspNetCore.Identity;

namespace ControleFutebolWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Nome { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;

        // Atualizado a cada requisição autenticada (ver AtividadeUsuarioFilter),
        // com throttling de 1 min para não martelar o banco a cada clique.
        public DateTime? UltimoAcesso { get; set; }
    }
}
