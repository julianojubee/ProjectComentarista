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

        // Controle de cobrança manual (ver PagamentosController/AssinaturaFilter).
        // null = usuário sem cobrança (nunca é bloqueado). Com valor, o acesso é
        // bloqueado quando a data (ancorada ao meio-dia UTC) fica no passado.
        // Admins nunca são bloqueados, independente deste campo.
        public DateTime? AcessoPagoAte { get; set; }

        // Valor da mensalidade deste usuário (usado no QR PIX da tela de bloqueio
        // e como sugestão ao registrar pagamento). null = usa Pix:ValorPadrao.
        public decimal? ValorMensalidade { get; set; }
    }
}
