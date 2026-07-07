using Microsoft.AspNetCore.Identity;

namespace ControleFutebolWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        public const string SessionClaimType = "SessionId";

        public string Nome { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;

        // Atualizado a cada requisição autenticada (ver AtividadeUsuarioFilter),
        // com throttling de 1 min para não martelar o banco a cada clique.
        public DateTime? UltimoAcesso { get; set; }

        // Identifica a sessão de login ativa (um valor por login). Comparado com a
        // claim SessionId do cookie a cada requisição (AtividadeUsuarioFilter): se
        // divergir, é porque outro login (ou um logoff forçado) invalidou esta sessão.
        public string? SessionId { get; set; }
    }
}
